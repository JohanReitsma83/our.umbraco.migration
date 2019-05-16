using Umbraco.Core.Models;
using Umbraco.Web.PropertyEditors;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MediaPicker")]
    public class MediaPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewEditorAlias(IDataType dataType, object oldConfig) => "Umbraco.MediaPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataType dataType, object oldConfig) => ContentBaseType.Media;

        public override object GetNewConfiguration(IDataType dataType, object oldConfig)
        {
            var oldVal = oldConfig as MediaPickerConfiguration;
            var newConfig = new MediaPickerConfiguration
            {
                Multiple = false,
                OnlyImages = false,
                DisableFolderSelect = false,
                StartNodeId = oldVal.StartNodeId
            };

            return newConfig;
        }
    }
}
