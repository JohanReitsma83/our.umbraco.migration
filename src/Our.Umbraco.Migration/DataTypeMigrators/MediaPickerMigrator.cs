using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MediaPicker")]
    public class MediaPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => "Umbraco.MediaPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Media;

        public override IDictionary<string, PreValue> GetNewPreValues(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            var preValues = new Dictionary<string, PreValue>(4);

            if (oldPreValues != null && oldPreValues.TryGetValue("startNodeId", out var value)) preValues["startNodeId"] = value;
            preValues["multiPicker"] = new PreValue("0");
            preValues["onlyImages"] = new PreValue("0");
            preValues["disableFolderSelect"] = new PreValue("0");

            return preValues;
        }
    }
}
