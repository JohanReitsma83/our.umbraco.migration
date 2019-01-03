using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MultiNodeTreePicker")]
    public class MultiNodeTreePickerMigrator : IDataTypeMigrator
    {
        private static readonly Regex MediaTypePattern = new Regex("\"type\"\\s*:\\s*\"media\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public string GetNewPropertyEditorAlias(string oldPropertyEditorAlias) => "Umbraco.MultiNodeTreePicker2";

        public IDictionary<string, PreValue> GetNewPreValues(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => oldPreValues;

        public ContentBaseType GetContentBaseType(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => 
            oldPreValues != null && oldPreValues.TryGetValue("startNode", out var value) && value.Value != null && MediaTypePattern.IsMatch(value.Value)
                ? ContentBaseType.Media
                : ContentBaseType.Document;

        public DataTypeDatabaseType GetNewDatabaseType(string oldPropertyEditorAlias, DataTypeDatabaseType oldDatabaseType) => DataTypeDatabaseType.Ntext;

        public bool MigrateIdToUdi => true;
    }
}
