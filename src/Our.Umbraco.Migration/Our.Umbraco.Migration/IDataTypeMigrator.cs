using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration
{
    public interface IDataTypeMigrator
    {
        string GetNewPropertyEditorAlias(string oldPropertyEditorAlias);
        DataTypeDatabaseType GetNewDatabaseType(string oldPropertyEditorAlias, DataTypeDatabaseType oldDatabaseType);
        IDictionary<string, PreValue> GetNewPreValues(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues);
        ContentBaseType GetContentBaseType(string oldPropertyEditorAlias, IDictionary<string, PreValue> oldPreValues);
        bool MigrateIdToUdi { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DataTypeMigratorAttribute : Attribute
    {
        public string[] PropertyEditorAliases { get; }

        public DataTypeMigratorAttribute(params string[] propertyEditorAliases)
        {
            PropertyEditorAliases = propertyEditorAliases;
        }
    }
}
