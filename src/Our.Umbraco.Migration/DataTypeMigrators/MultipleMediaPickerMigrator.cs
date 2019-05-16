using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MultipleMediaPicker")]
    public class MultipleMediaPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewEditorAlias(IDataType dataType, object oldConfig) => "Umbraco.MediaPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataType dataType, object oldConfig) => ContentBaseType.Media;
    }
}
