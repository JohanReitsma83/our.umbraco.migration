using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MemberPicker")]
    public class MemberPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewEditorAlias(IDataType dataType, object oldConfig) => "Umbraco.MemberPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataType dataType, object oldConfig) => ContentBaseType.Member;
    }
}
