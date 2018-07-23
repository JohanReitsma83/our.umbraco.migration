using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;

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
    public class MigrationStartupHandler : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ApplyNeededMigrations();
        }

        private static void ApplyNeededMigrations()
        {
            var resolvers = GetRegisteredResolvers();
            if (resolvers == null || !resolvers.Any()) return;

            var runners = new List<MigrationRunnerDetail>();
            foreach (var resolver in resolvers)
            {
                var applied = DetermineAppliedMigrations(resolver);
                if (applied.Count == 0) continue;

                AddMigrationRunners(resolver, applied, runners);
            }

            if (runners.Count > 0) ExecuteMigrationRunners(runners);
        }

        private static List<IMigrationResolver> GetRegisteredResolvers()
        {
            var resolvers = new List<IMigrationResolver>();
            MigrationResolverSection section = null;

            try
            {
                section = ConfigurationManager.GetSection("migrationResolvers") as MigrationResolverSection;
            }
            catch (Exception e)
            {
                LogHelper.Error<MigrationStartupHandler>("Could not read the resolvers section", e);
            }
            if (section?.Resolvers == null) return resolvers;

            var interfaceType = typeof(IMigrationResolver);
            foreach (ResolverElement resolver in section.Resolvers)
            {
                try
                {
                    var type = Type.GetType(resolver.Type, false);
                    if (type == null)
                        LogHelper.Warn<MigrationStartupHandler>($"The type '{resolver.Type}' for migration resolver '{resolver.Name}' could not be found");
                    else if (!interfaceType.IsAssignableFrom(type) || !(Activator.CreateInstance(type) is IMigrationResolver instance))
                        LogHelper.Warn<MigrationStartupHandler>($"The type '{type.FullName}' for migration resolver '{resolver.Name}' does not implement IMigrationResolver");
                    else AddResolver(resolvers, resolver, instance);
                }
                catch (Exception e)
                {
                    LogHelper.Error<MigrationStartupHandler>($"Could not instantiate the migration resolver '{resolver.Name}'", e);
                }
            }

            return resolvers;
        }

        private static void AddResolver(ICollection<IMigrationResolver> resolvers, ResolverElement resolver, IMigrationResolver instance)
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
                LogHelper.Error<MigrationStartupHandler>($"Could not initiate the migration resolver '{resolver.Name}' with settings from the config file", e);
            }
        }

        private static Dictionary<string, IEnumerable<IMigrationEntry>> DetermineAppliedMigrations(IMigrationResolver resolver)
        {
            var applied = new Dictionary<string, IEnumerable<IMigrationEntry>>();
            IEnumerable<string> names = null;

            try
            {
                names = resolver.GetProductNames();
            }
            catch (Exception e)
            {
                LogHelper.Error<MigrationStartupHandler>($"Could not determine the migration source names for the resolver '{resolver.GetType().FullName}'", e);
            }
            if (names == null) return applied;

            foreach (var name in names)
            {
                try
                {
                    applied[name] = ApplicationContext.Current.Services.MigrationEntryService.GetAll(name);
                }
                catch (Exception e)
                {
                    LogHelper.Error<MigrationStartupHandler>($"Could not determine the applied migrations for '{name}' from the resolver '{resolver.GetType().FullName}'", e);
                }
            }

            return applied;
        }

        private static void AddMigrationRunners(IMigrationResolver resolver, IReadOnlyDictionary<string, IEnumerable<IMigrationEntry>> applied, List<MigrationRunnerDetail> runners)
        {
            try
            {
                var rnrs = resolver.GetMigrationRunners(applied);
                runners.AddRange(rnrs);
            }
            catch (Exception e)
            {
                LogHelper.Error<MigrationStartupHandler>($"Could not determine the migration runners for the resolver '{resolver.GetType().FullName}'", e);
            }
        }

        private static void ExecuteMigrationRunners(IEnumerable<MigrationRunnerDetail> runners)
        {
            var es = ApplicationContext.Current.Services.MigrationEntryService;
            var logger = ApplicationContext.Current.ProfilingLogger.Logger;
            var db = UmbracoContext.Current.Application.DatabaseContext.Database;
            foreach (var detail in runners)
            {
                try
                {
                    var runner = detail?.CreateRunner(es, logger);
                    if (runner == null)
                    {
                        LogHelper.Warn<MigrationStartupHandler>($"No runner was returned for {detail?.ProductName} between version {detail?.CurrentVersion} and {detail?.TargetVersion}");
                    }
                    else
                    {
                        runner.Execute(db);
                    }
                }
                catch (Exception e)
                {
                    LogHelper.Error<MigrationStartupHandler>($"Could not execute the runner for {detail?.ProductName} between version {detail?.CurrentVersion} and {detail?.TargetVersion}", e);
                }
            }
        }
    }
}
