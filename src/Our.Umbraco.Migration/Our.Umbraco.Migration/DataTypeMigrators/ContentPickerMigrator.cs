using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.ContentPickerAlias")]
    public class ContentPickerMigrator : IDataTypeMigrator
    {
        public string GetNewPropertyEditorAlias(string oldPropertyEditorAlias) => "Umbraco.ContentPicker2";

        public IDictionary<string, PreValue> GetNewPreValues(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => oldPreValues;

        public ContentBaseType GetContentBaseType(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Document;

        public DataTypeDatabaseType GetNewDatabaseType(string oldPropertyEditorAlias, DataTypeDatabaseType oldDatabaseType) => DataTypeDatabaseType.Nvarchar;

        public bool MigrateIdToUdi => true;
    }
}
