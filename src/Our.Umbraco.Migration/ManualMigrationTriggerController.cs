using System;
using System.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Web.Editors;

namespace Our.Umbraco.Migration
{
    public class ManualMigrationTriggerController : UmbracoAuthorizedJsonController
    {
        private const string TriggerKeyKey = "Our.Umbraco.Migration:ManualTriggerKey";
        private static readonly string TriggerKey = ConfigurationManager.AppSettings[TriggerKeyKey];

        public bool TriggerMigrations(string triggerKey)
        {
            if (TriggerKey == null)
            {
                Logger.Warn<ManualMigrationTriggerController>($"You must first set the {TriggerKeyKey} application setting");
                return false;
            }

            if (triggerKey != TriggerKey)
            {
                Logger.Warn<ManualMigrationTriggerController>($"An incorrect triggerKey value was passed: {triggerKey}");
                return false;
            }

            try
            {
                new MigrationApplier(ApplicationContext).ApplyNeededMigrations();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error<ManualMigrationTriggerController>("Could not apply migrations", ex);
                return false;
            }
        }
    }
}
