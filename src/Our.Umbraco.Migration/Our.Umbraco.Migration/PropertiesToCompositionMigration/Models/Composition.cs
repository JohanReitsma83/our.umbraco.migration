using System;
using System.Text.RegularExpressions;

namespace Our.Umbraco.Migration.PropertiesToCompositionMigration.Models
{
    public class Composition
    {
        public string ContainerName { get; set; }
        public string CompositionName { get; set; }

        public string CompositionAlias
        {
            get
            {
                var specialCharacterRegex = new Regex("[^a-zA-Z0-9]");
                var alphaNumericCompositionName = specialCharacterRegex.Replace(CompositionName, "");
                return (Char.ToLowerInvariant(alphaNumericCompositionName[0]) + alphaNumericCompositionName.Substring(1)).Replace(" ", "");
            }
        }
        public string Icon { get; set; }
    }
}