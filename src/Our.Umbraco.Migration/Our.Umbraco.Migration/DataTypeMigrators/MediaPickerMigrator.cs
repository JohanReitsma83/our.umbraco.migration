using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MediaPicker")]
    public class MediaPickerMigrator : IDataTypeMigrator
    {
        public string GetNewPropertyEditorAlias(string oldPropertyEditorAlias) => "Umbraco.MediaPicker2";

        public IDictionary<string, PreValue> GetNewPreValues(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues)
        {
            var preValues = new Dictionary<string, PreValue>(4);

            if (oldPreValues != null && oldPreValues.TryGetValue("startNodeId", out var value)) preValues["startNodeId"] = value;
            preValues["multiPicker"] = new PreValue("0");
            preValues["onlyImages"] = new PreValue("0");
            preValues["disableFolderSelect"] = new PreValue("0");

            return preValues;
        }

        public ContentBaseType GetContentBaseType(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Media;

        public DataTypeDatabaseType GetNewDatabaseType(string oldPropertyEditorAlias, DataTypeDatabaseType oldDatabaseType) => DataTypeDatabaseType.Ntext;

        public bool MigrateIdToUdi => true;
    }
}
