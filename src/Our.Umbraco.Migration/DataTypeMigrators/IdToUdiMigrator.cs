using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    public abstract class IdToUdiMigrator : IDataTypeMigrator
    {
        public abstract string GetNewEditorAlias(IDataType dataType, object oldConfig);
        public abstract ContentBaseType GetNewPropertyContentBaseType(IDataType dataType, object oldConfig);

        public virtual bool NeedsMigration(IDataType dataType, object oldConfig) => true;
        public virtual ValueStorageType GetNewDatabaseType(IDataType dataType, object oldConfig) => ValueStorageType.Ntext;
        public virtual object GetNewConfiguration(IDataType dataType, object oldConfig) => oldConfig;
        public virtual IPropertyMigration GetPropertyMigration(IDataType dataType, object oldConfig, bool retainInvalidData) =>
            new PropertyMigration(new IdToUdiTransform(GetNewPropertyContentBaseType(dataType, oldConfig), retainInvalidData), new UdiToIdTransform());
    }
}
