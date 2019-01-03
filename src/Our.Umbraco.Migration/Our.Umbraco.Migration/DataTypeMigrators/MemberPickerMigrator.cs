using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MemberPicker")]
    public class MemberPickerMigrator : IDataTypeMigrator
    {
        public string GetNewPropertyEditorAlias(string oldPropertyEditorAlias) => "Umbraco.MemberPicker2";

        public IDictionary<string, PreValue> GetNewPreValues(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => oldPreValues;

        public ContentBaseType GetContentBaseType(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Member;

        public DataTypeDatabaseType GetNewDatabaseType(string oldPropertyEditorAlias, DataTypeDatabaseType oldDatabaseType) => DataTypeDatabaseType.Nvarchar;

        public bool MigrateIdToUdi => true;
    }
}
