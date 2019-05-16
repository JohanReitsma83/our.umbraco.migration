using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Semver;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Scoping;

namespace Our.Umbraco.Migration
{
    public class VersionedUpgrader : Upgrader
    {
        public static VersionedUpgrader CreateUpgrader(string productName, string initialState, IEnumerable<Tuple<SemVersion, Type>> migrations, IUmbracoDatabase database)
        {
            var migs = migrations.OrderBy(m => m.Item1).ToList();
            var finalState = migs.Last().Item1;
            var plan = migs.Aggregate(new MigrationPlan(productName).From(initialState), (current1, migration) => current1.To(migration.Item1.ToString(), migration.Item2));

            return new VersionedUpgrader(productName, finalState, plan, database);
        }

        private string ProductName { get; }
        private SemVersion FinalState { get; }
        private IUmbracoDatabase Database { get; }

        private VersionedUpgrader(string productName, SemVersion finalState, MigrationPlan plan, IUmbracoDatabase database)
            : base(plan)
        {
            ProductName = productName;
            FinalState = finalState;
            Database = database;
        }

        public override void AfterMigrations(IScope scope, ILogger logger)
        {
            base.AfterMigrations(scope, logger);

            Database.Insert(new MigrationEntry(0, DateTime.Now, ProductName, FinalState));
        }
    }
}