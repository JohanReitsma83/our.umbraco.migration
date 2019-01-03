using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MultipleMediaPicker")]
    public class MultipleMediaPickerMigrator : IDataTypeMigrator
    {
        public string GetNewPropertyEditorAlias(string oldPropertyEditorAlias) => "Umbraco.MediaPicker2";

        public IDictionary<string, PreValue> GetNewPreValues(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => oldPreValues;

        public ContentBaseType GetContentBaseType(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues) => ContentBaseType.Media;

        public DataTypeDatabaseType GetNewDatabaseType(string oldPropertyEditorAlias, DataTypeDatabaseType oldDatabaseType) => DataTypeDatabaseType.Ntext;

        public bool MigrateIdToUdi => true;
    }
}
