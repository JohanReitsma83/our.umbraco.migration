using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.ContentPickerAlias")]
    public class ContentPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewEditorAlias(IDataType dataType, object oldConfig) => "Umbraco.ContentPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataType dataType, object oldConfig) => ContentBaseType.Document;
    }
}
