using System.Collections.Generic;

namespace Our.Umbraco.Migration.PropertiesToCompositionMigration.Models
{
    public class CompositionSourceData
    {
        public string SourceContentTypeAlias { get; set; }
        public IEnumerable<string> PropertyAliases { get; set; }
    }
}