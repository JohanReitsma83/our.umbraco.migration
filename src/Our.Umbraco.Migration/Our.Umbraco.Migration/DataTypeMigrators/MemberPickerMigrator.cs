using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MemberPicker")]
    public class MemberPickerMigrator : IdToUdiMigrator
    {
        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => "Umbraco.MemberPicker2";
        public override ContentBaseType GetNewPropertyContentBaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Member;
    }
}
