using System;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration
{
    public interface IDataTypeMigrator
    {
        bool NeedsMigration(IDataType dataType, object oldConfig);
        string GetNewEditorAlias(IDataType dataType, object oldConfig);
        ValueStorageType GetNewDatabaseType(IDataType dataType, object oldConfig);
        object GetNewConfiguration(IDataType dataType, object oldConfig);
        IPropertyMigration GetPropertyMigration(IDataType dataType, object oldConfig, bool retainInvalidData);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DataTypeMigratorAttribute : Attribute
    {
        public string[] EditorAliases { get; }

        public DataTypeMigratorAttribute(params string[] editorAliases)
        {
            EditorAliases = editorAliases;
        }
    }
}
