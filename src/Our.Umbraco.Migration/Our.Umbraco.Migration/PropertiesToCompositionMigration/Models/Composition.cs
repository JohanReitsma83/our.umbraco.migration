using System;

namespace Our.Umbraco.Migration.PropertiesToCompositionMigration.Models
{
    public class Composition
    {
        public string ContainerName { get; set; }
        public string CompositionName { get; set; }
        public string CompositionAlias => (Char.ToLowerInvariant(CompositionName[0]) + CompositionName.Substring(1)).Replace(" ", "");
        public string Icon { get; set; }
    }
}