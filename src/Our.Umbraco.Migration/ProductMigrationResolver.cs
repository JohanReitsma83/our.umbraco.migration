using System;
using System.Collections.Generic;
using System.Linq;
using Semver;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Persistence;

/* Copyright 2018 ProWorks, Inc.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
namespace Our.Umbraco.Migration
{
    /// <summary>
    /// The purpose of the ProductMigrationResolver is to simplify the migration developing experience.  While the new
    /// migration framework provides a lot of flexibility, it requires each developer to create a lot of boiler-plate
    /// code for common scenarios.  This provides one method to simplify the developer experience.
    ///
    /// To use the ProductMigrationResolver, add it to your web.config file in the migrationResolvers section (it is
    /// added by default when installing the NuGet package).  You then need to define which product names you want to
    /// monitor, again in the web.config file.  Finally, you add a MigrationAttribute to each class that will be a
    /// migration to be executed.
    ///
    /// The resolver will find all classes tagged with the Migration attribute, and group them by product names.  If
    /// the product name is a monitored one, it will then build dependencies trees based on the DependentMigrations
    /// field on the attribute.  Once the dependency tree is built, the resolver will collapse this into an upgrade
    /// plan that defines the order of migrations.  Dependencies are honored, but migrations without dependencies have
    /// no defined order.  The order of sibling branches in a tree is not defined either, unless there is a defined
    /// dependency which causes them to run in a certain order.  In this way, you can ensure that one migration will
    /// run after another, but if a given migration doesn't have any dependencies, it can run in any order.
    ///
    /// The resolver will store its final state as the class names of all the final nodes of all dependency trees.  It
    /// will then use this information the next time it runs to ensure each migration is only run once.
    /// </summary>
    public class ProductMigrationResolver : IMigrationResolver
    {
        private const string NamesKey = "MonitoredProductNames";
        private readonly object _lock = new object();

        private HashSet<string> ProductNames { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        private List<MigrationDetail> MigrationDetails { get; } = new List<MigrationDetail>();
        private bool Loaded { get; set; }
        private ILogger Logger { get; }
        private IUmbracoDatabase Database { get; }

        public ProductMigrationResolver(ILogger logger, IUmbracoDatabase database)
        {
            Logger = logger;
            Database = database;
        }

        private IEnumerable<MigrationDetail> GetMigrationDetails()
        {
            lock (_lock)
            {
                if (Loaded) return MigrationDetails;

                var classType = typeof(IMigration);
                var attributeType = typeof(MigrationAttribute);
                var migrations = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes()).Where(t => classType.IsAssignableFrom(t))
                    .Select(t => t.GetCustomAttributes(attributeType, false).OfType<MigrationAttribute>().Select(a => new {Type = t, Attribute = a}).ToArray())
                    .Where(c => c.Length > 0)
                    .SelectMany(c => c
                        .Where(a => a.Attribute.ProductName != null && ProductNames.Contains(a.Attribute.ProductName))
                        .Select(a => new MigrationDetail(a.Type, a.Attribute.ProductName, a.Attribute.DependentMigrations)));

                MigrationDetails.AddRange(migrations);

                Loaded = true;
            }

            return MigrationDetails;
        }

        public void Initialize(IReadOnlyDictionary<string, string> settings)
        {
            lock (_lock)
            {
                Loaded = false;
                MigrationDetails.Clear();
                ProductNames.Clear();

                if (settings.TryGetValue(NamesKey, out var names))
                {
                    ProductNames.UnionWith(names.Split(',').Select(n => n.Trim())
                        .Where(n => n.Length > 0 && !string.Equals(n, "umbraco", StringComparison.InvariantCultureIgnoreCase)));
                    var pn = string.Join(", ", ProductNames);
                    Logger.Info<ProductMigrationResolver>($"Looking for migrations in products: {pn}");
                }
                else
                {
                    Logger.Warn<ProductMigrationResolver>("No product names defined in the web.config file");
                }
            }
        }

        public IEnumerable<string> GetProductNames()
        {
            var details = GetMigrationDetails();

            return details.Select(d => d.ProductName).Distinct();
        }

        public IEnumerable<Upgrader> GetUpgraders(IReadOnlyDictionary<string, string> initialStates)
        {
            var details = GetMigrationDetails().ToList();
            var lk = details.ToLookup(d => d.ProductName);

            foreach (var productRunners in lk)
            {
                // TODO: Need to implement this method to find upgrade paths based on the types that need to run before and after each other, and the ones that are independent
                var target = productRunners.Select(r => r.TargetVersion).Max();
                var state = initialStates != null && initialStates.TryGetValue(productRunners.Key, out var stt) ? stt : null;
                var current = SemVersion.TryParse(state, out var c) ? c : new SemVersion(0);

                if (current < target)
                {
                    Logger.Info<ProductMigrationResolver>($"{productRunners.Key} has migrations from {current} to {target}");
                    var toApply = productRunners.Where(m => m.TargetVersion > current).Select(m => new Tuple<SemVersion, Type>(m.TargetVersion, m.Type));
                    yield return VersionedUpgrader.CreateUpgrader(productRunners.Key, state, toApply, Database);
                }
                else
                {
                    Logger.Info<ProductMigrationResolver>($"{productRunners.Key} is up to date");
                }
            }
        }

        private class MigrationDetail
        {
            public MigrationDetail(Type type, string productName, IEnumerable<Type> dependentMigrations)
            {
                ProductName = productName;
                Type = type;
                TypesThatMustRunBeforeMe = new HashSet<Type>((dependentMigrations ?? new Type[0])
                    .Where(t => t.GetCustomAttributes(typeof(MigrationAttribute), false).OfType<MigrationAttribute>().Any(a => a.ProductName == productName)));
                TypesThatMustRunAfterMe = new HashSet<Type>();
            }

            public string ProductName { get; }
            public ICollection<Type> TypesThatMustRunBeforeMe { get; }
            public ICollection<Type> TypesThatMustRunAfterMe { get; }
            public Type Type { get; }
        }
    }
}
