using System;
using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration
{
    public interface IDataTypeMigrator
    {
        bool NeedsMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
        string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
        DataTypeDatabaseType GetNewDatabaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
        IDictionary<string, PreValue> GetNewPreValues(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
        IPropertyMigration GetPropertyMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
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
