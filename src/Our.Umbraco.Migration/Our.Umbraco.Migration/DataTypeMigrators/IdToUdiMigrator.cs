using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    public abstract class IdToUdiMigrator : IDataTypeMigrator
    {
        public abstract string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
        public abstract ContentBaseType GetNewPropertyContentBaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);

        public virtual bool NeedsMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => true;
        public virtual DataTypeDatabaseType GetNewDatabaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => DataTypeDatabaseType.Ntext;
        public virtual IDictionary<string, PreValue> GetNewPreValues(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => oldPreValues;
        public virtual IPropertyMigration GetPropertyMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) =>
            new PropertyMigration(new IdToUdiTransform(GetNewPropertyContentBaseType(dataType, oldPreValues)), new UdiToIdTransform());
    }
}
