using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MultipleMediaPicker")]
    public class MultipleMediaPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => "Umbraco.MediaPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Media;
    }
}
