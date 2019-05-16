using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Umbraco.Core; 
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;

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
    [RuntimeLevel(MinLevel = RuntimeLevel.Upgrade)]
    // ReSharper disable once UnusedMember.Global
    public class MigrationStartupComposer : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.Components().Append<MigrationStartupComponent>(); 
        }
    }
    public class MigrationStartupComponent : IComponent
    {
        private IFactory Container { get; }
        private IScopeProvider ScopeProvider { get; }
        private IMigrationBuilder MigrationBuilder { get; }
        private IKeyValueService KeyValueService { get; }
        private ILogger Logger { get; }
        private IUmbracoDatabase Database { get; }

        public MigrationStartupComponent(IFactory container, IScopeProvider scopeProvider, IMigrationBuilder migrationBuilder, IKeyValueService keyValueService, ILogger logger, IUmbracoDatabase database)
        {
            Container = container;
            ScopeProvider = scopeProvider;
            MigrationBuilder = migrationBuilder;
            KeyValueService = keyValueService;
            Logger = logger;
            Database = database;
        }

        public void Terminate(){}
        public void Initialize()
        {
            var resolvers = GetRegisteredResolvers();
            if (resolvers == null || !resolvers.Any()) return;

            var upgraders = new List<Upgrader>();
            foreach (var resolver in resolvers)
            {
                var initialStates = GetInitialStates(resolver);
                if (initialStates.Count == 0) continue;

                AddUpgraders(resolver, initialStates, upgraders);
            }

            if (upgraders.Count <= 0) return;

            ExecuteMigrationRunners(upgraders);
        }

        private List<IMigrationResolver> GetRegisteredResolvers()
        {
            var resolvers = new List<IMigrationResolver>();
            MigrationResolverSection section = null;

            try
            {
                section = ConfigurationManager.GetSection("migrationResolvers") as MigrationResolverSection;
            }
            catch (Exception e)
            {
                Logger.Error<MigrationStartupComponent>("Could not read the resolvers section", e);
            }
            if (section?.Resolvers == null) return resolvers;

            var interfaceType = typeof(IMigrationResolver);
            foreach (ResolverElement resolver in section.Resolvers)
            {
                try
                {
                    var type = Type.GetType(resolver.Type, false);
                    if (type == null)
                        Logger.Warn<MigrationStartupComponent>($"The type '{resolver.Type}' for migration resolver '{resolver.Name}' could not be found");
                    else if (!interfaceType.IsAssignableFrom(type))
                        Logger.Warn<MigrationStartupComponent>($"The type '{type.FullName}' for migration resolver '{resolver.Name}' does not implement IMigrationResolver");
                    else 
                    {
                        var inst = Container.CreateInstance(type);
                        if (inst is IMigrationResolver instance) AddResolver(resolvers, resolver, instance);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error<MigrationStartupComponent>($"Could not instantiate the migration resolver '{resolver.Name}'", e);
                }
            }

            return resolvers;
        }

        private void AddResolver(ICollection<IMigrationResolver> resolvers, ResolverElement resolver, IMigrationResolver instance)
        {
            try
            {
                var settings = new Dictionary<string, string>();
                foreach (KeyValueConfigurationElement setting in resolver.Settings)
                {
                    settings[setting.Key] = setting.Value;
                }

                instance.Initialize(settings);
                resolvers.Add(instance);
            }
            catch (Exception e)
            {
                Logger.Error<MigrationStartupComponent>($"Could not initiate the migration resolver '{resolver.Name}' with settings from the config file", e);
            }
        }

        private Dictionary<string, string> GetInitialStates(IMigrationResolver resolver)
        {
            var initialStates = new Dictionary<string, string>();
            IEnumerable<string> names = null;

            try
            {
                names = resolver.GetProductNames();
            }
            catch (Exception e)
            {
                Logger.Error<MigrationStartupComponent>($"Could not determine the migration source names for the resolver '{resolver.GetType().FullName}'", e);
            }
            if (names == null) return initialStates; 

            var versions = Database.Fetch<MigrationEntry>().ToLookup(e => e.MigrationName);

            foreach (var name in names)
            {
                try
                {
                    var init = versions[name].OrderByDescending(e => e.Version).Select(e => e.Version.ToString()).FirstOrDefault() ?? "0";
                    initialStates[name] = init;
                }
                catch (Exception e)
                {
                    Logger.Error<MigrationStartupComponent>($"Could not determine the applied migrations for '{name}' from the resolver '{resolver.GetType().FullName}'", e);
                }
            }

            return initialStates;
        }

        private void AddUpgraders(IMigrationResolver resolver, IReadOnlyDictionary<string, string> initialStates, List<Upgrader> upgraders)
        {
            try
            {
                var upg = resolver.GetUpgraders(initialStates);
                upgraders.AddRange(upg);
            }
            catch (Exception e)
            {
                Logger.Error<MigrationStartupComponent>($"Could not determine the migration runners for the resolver '{resolver.GetType().FullName}'", e);
            }
        }

        private void ExecuteMigrationRunners(IEnumerable<Upgrader> upgraders)
        {
            foreach (var upgrader in upgraders)
            {
                try
                {
                    Logger.Info<MigrationStartupComponent>($"Executing migration for {upgrader.Plan.Name} from {upgrader.Plan.InitialState} to {upgrader.Plan.FinalState}");
                    upgrader.Execute(ScopeProvider, MigrationBuilder, KeyValueService, Logger);
                    Logger.Info<MigrationStartupComponent>($"Completed migration of {upgrader.Plan.Name} from {upgrader.Plan.InitialState} to {upgrader.Plan.FinalState}");
                }
                catch (Exception e)
                {
                    Logger.Error<MigrationStartupComponent>($"Could not execute the migration of {upgrader.Plan.Name} from {upgrader.Plan.InitialState} to {upgrader.Plan.FinalState}", e);
                }
            }
        }
    }
}
