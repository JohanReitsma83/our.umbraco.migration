﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Semver;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Migrations;

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
    public class ProductMigrationResolver : IMigrationResolver
    {
        private const string NamesKey = "MonitoredProductNames";
        private readonly object _lock = new object();

        private HashSet<string> ProductNames { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        private List<MigrationDetail> MigrationDetails { get; } = new List<MigrationDetail>();
        private bool Loaded { get; set; }

        private IEnumerable<MigrationDetail> GetMigrationDetails()
        {
            lock (_lock)
            {
                if (Loaded) return MigrationDetails;

                var classType = typeof(IMigration);
                var attributeType = typeof(MigrationAttribute);
                var migrations = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    { try { return a.GetTypes(); } catch { return new Type[0]; } }).Where(t => classType.IsAssignableFrom(t))
                    .Select(t => t.GetCustomAttributes(attributeType, false) as MigrationAttribute[])
                    .Where(c => c != null && c.Length > 0)
                    .SelectMany(c => c
                        .Where(a => a.ProductName != null && ProductNames.Contains(a.ProductName))
                        .Select(a => new MigrationDetail
                        {
                            ProductName = a.ProductName,
                            TargetVersion = new SemVersion(a.TargetVersion.Major, a.TargetVersion.Minor, a.TargetVersion.Build)
                        }));

                MigrationDetails.AddRange(migrations);

                Loaded = true;
            }

            return MigrationDetails;
        }

        private IEnumerable<Type> GetAssemblyTypes(Assembly a)
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                if (e.LoaderExceptions != null)
                {
                    foreach (var ex in e.LoaderExceptions)
                    {
                        if (ex != null) LogHelper.Debug<MigrationStartupHandler>($"Loader exception - " + ex);
                    }
                }
                return e.Types.Where(t => t != null);
            }
            catch
            {
                return new Type[0];
            }
        }

        public void Initialize(ILogger logger, IReadOnlyDictionary<string, string> settings)
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
                    logger.Info<ProductMigrationResolver>($"Looking for migrations in products: {pn}");
                }
                else
                {
                    logger.Warn<ProductMigrationResolver>("No product names defined in the web.config file");
                }
            }
        }

        public IEnumerable<string> GetProductNames(ILogger logger)
        {
            var details = GetMigrationDetails();

            return details.Select(d => d.ProductName).Distinct();
        }

        public IEnumerable<MigrationRunnerDetail> GetMigrationRunners(ILogger logger, IReadOnlyDictionary<string, IEnumerable<IMigrationEntry>> appliedMigrations)
        {
            var details = GetMigrationDetails();
            var lk = details.ToLookup(d => d.ProductName);

            foreach (var productRunners in lk)
            {
                var target = productRunners.Select(r => r.TargetVersion).Max();
                var applied = appliedMigrations != null && appliedMigrations.TryGetValue(productRunners.Key, out var appl) ? appl : null;
                var current = applied?.OrderByDescending(a => a.Version).Select(a => a.Version).FirstOrDefault() ?? new SemVersion(0);

                if (current < target)
                {
                    logger.Info<ProductMigrationResolver>($"{productRunners.Key} has migrations from {current} to {target}");
                    yield return new MigrationRunnerDetail(productRunners.Key, current, target);
                }
                else
                {
                    logger.Info<ProductMigrationResolver>($"{productRunners.Key} is up to date");
                }
            }
        }

        private class MigrationDetail
        {
            public string ProductName { get; set; }
            public SemVersion TargetVersion { get; set; }
        }
    }
}
