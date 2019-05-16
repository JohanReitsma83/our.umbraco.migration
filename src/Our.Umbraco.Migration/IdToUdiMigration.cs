using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a helper class that performs migrations of IDs to UDIs in content nodes, by content type and field name.  The field values can be single integer IDs, or a comma-separated list of integers.
    /// </summary>
    public abstract class IdToUdiMigration : FieldTransformMigration
    {
        private static ContentTransformMapper CreateIdToUdiMapper(ContentBaseType sourceType, string contentTypeAlias, IReadOnlyDictionary<string, ContentBaseType> fieldTypes, bool retainInvalidData)
        {
            return new ContentTransformMapper(
                new ContentsByTypeSource(sourceType, contentTypeAlias),
                fieldTypes.Select(p => new FieldMapper(p.Key, sourceType + " Picker", new[] { new PropertyMigration(new IdToUdiTransform(p.Value, retainInvalidData), new UdiToIdTransform()) })));
        }
        private readonly List<string> _includedDataTypes;
        private readonly List<string> _excludedDataTypes;
        private readonly ServiceContext _svc;

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance.  Use this overload if you want to explicitly specify the content types and field names to migrate.
        /// </summary>
        /// <param name="contentTypeFieldMappings">This dictionary has as its key the content type aliases to be mapped.  Each one is then mapped to a list of fields that should be migrated from an integer to a UDI for that content type.  The field values can be single integers, or a comma-separated list of integers</param>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        /// <param name="retainInvalidData">If data is found that isn't a valid UDI or a valid ID of the right content type, should we keep that data, or remove it</param>
        protected IdToUdiMigration(IDictionary<string, IReadOnlyDictionary<string, ContentBaseType>> contentTypeFieldMappings, IMigrationContext context, ServiceContext svc, bool retainInvalidData = false)
            : base(contentTypeFieldMappings.Select(p => CreateIdToUdiMapper(ContentBaseType.Document, p.Key, p.Value, retainInvalidData)), context)
        {
            _svc = svc;
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance.  Use this overload if you want the migration to determine which data types need to be migrated and automatically convert data types and migrate content.
        /// </summary>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        /// <param name="retainInvalidData">If data is found that isn't a valid UDI or a valid ID of the right content type, should we keep that data, or remove it</param>
        /// <param name="includedDataTypes">The list of data types to include in the migration.  If specified, only these data types, and their related content, will be migrated.  By default includes all data types</param>
        /// <param name="excludedDataTypes">The list of data types to exclude from the migration.  If specified, only these data types, and their related content, will be migrated.  By default no data types are excluded</param>
        protected IdToUdiMigration(IMigrationContext context, ServiceContext svc, bool retainInvalidData = false, IEnumerable<string> includedDataTypes = null, IEnumerable<string> excludedDataTypes = null)
            : base(context)
        {
            if (includedDataTypes != null) _includedDataTypes = new List<string>(includedDataTypes);
            if (excludedDataTypes != null) _excludedDataTypes = new List<string>(excludedDataTypes);
            RetainInvalidData = retainInvalidData;
            _svc = svc;
        }

        protected virtual bool RetainInvalidData { get; }

        /// <inheritdoc />
        /// <summary>
        /// Finds all data types which have IDataTypeMigrators registered, where the data types are not barred by the include/exclude lists passed to the constructor.
        /// It then updates those data types to their new type, and finds all content types, media types, and member types associated with those data types and returns mappers for them.
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<IContentTransformMapper> LoadMappings()
        {
            var dts = _svc.DataTypeService;

            var migrations = FindDataTypeMigrations(dts);
            var allDataTypes = new Dictionary<int, IPropertyMigration>(migrations.Sum(m => m.Value.DataTypes.Count));

            foreach (var migration in migrations.Values)
            {
                foreach (var dataType in migration.DataTypes)
                {
                    var propMigration = GetPropertyMigration(migration.Migrator, dataType);
                    if (propMigration == null) continue;

                    allDataTypes[dataType.type.Id] = propMigration;
                }
            }

            // Do this in two separate loops so that all the migrators can see the old data types before we convert any of them to the new data types
            foreach (var migration in migrations.Values)
            {
                foreach (var dataType in migration.DataTypes)
                {
                    // If we can't update the data type, we shouldn't try to migrate the content
                    if (!UpdateDataType(dts, migration.Migrator, dataType))
                        allDataTypes.Remove(dataType.type.Id);
                }
            }

            var documentMappings = FindMappings(_svc.ContentTypeService.GetAll(), ContentBaseType.Document, allDataTypes) ?? new IContentTransformMapper[0];
            var mediaMappings = FindMappings(_svc.ContentTypeService.GetAll(), ContentBaseType.Media, allDataTypes) ?? new IContentTransformMapper[0];
            var memberMappings = FindMappings(_svc.MemberTypeService.GetAll(), ContentBaseType.Member, allDataTypes) ?? new IContentTransformMapper[0];

            return documentMappings.Union(mediaMappings).Union(memberMappings);
        }

        protected virtual Dictionary<string, DataTypeMigrations> FindDataTypeMigrations(IDataTypeService dts)
        {
            var migrations = new Dictionary<string, DataTypeMigrations>(StringComparer.InvariantCultureIgnoreCase);
            var allDataTypes = dts.GetAll();

            foreach (var dataType in allDataTypes)
            {
                var name = dataType.Name;

                if ((_excludedDataTypes != null && _excludedDataTypes.Contains(name, StringComparer.InvariantCultureIgnoreCase)) ||
                    (_includedDataTypes != null && !_includedDataTypes.Contains(name, StringComparer.InvariantCultureIgnoreCase)))
                    continue;

                if (GetConfiguration(dts, dataType, out var oldConfig))
                    UpdateDataTypeMigrations((dataType, oldConfig), migrations);
            }

            migrations.RemoveAll(m => m.Value == null);
            return migrations;
        }

        protected virtual IDataTypeMigrator CreateMigrator((IDataType type, object config) dataType)
        {
            return DataTypeMigratorFactory.Instance.CreateDataTypeMigrator(dataType.type.EditorAlias);
        }

        protected virtual void UpdateDataTypeMigrations((IDataType type, object config) dataType, IDictionary<string, DataTypeMigrations> knownMigrations)
        {
            var alias = dataType.type.EditorAlias;

            if (!knownMigrations.TryGetValue(alias, out var migration))
            {
                var migrator = CreateMigrator(dataType);
                migration = knownMigrations[alias] = migrator == null ? null : new DataTypeMigrations { Migrator = migrator, DataTypes = new List<(IDataType type, object config)>() };
            }

            if (migration?.Migrator != null && migration.Migrator.NeedsMigration(dataType.type, dataType.config))
                migration.DataTypes.Add(dataType);
        }

        protected virtual bool GetConfiguration(IDataTypeService dts, IDataType dataType, out object oldConfig)
        {
            oldConfig = null;

            try
            {
                oldConfig = dataType.Configuration;
            }
            catch (Exception e)
            {
                Logger.Error<IdToUdiMigration>($"Could not retrieve the existing pre-values for the data type {dataType.Name} ({dataType.EditorAlias})", e);
                return false;
            }

            return true;
        }

        protected virtual IPropertyMigration GetPropertyMigration(IDataTypeMigrator migrator, (IDataType type, object config) dataType)
        {
            try
            {
                return migrator.GetPropertyMigration(dataType.type, dataType.config, RetainInvalidData);
            }
            catch (Exception e)
            {
                Logger.Error<IdToUdiMigration>($"Could not get the property migration for the data type {dataType.type.Name} ({dataType.type.EditorAlias})", e);
                return null;
            }
        }

        protected virtual bool UpdateDataType(IDataTypeService dts, IDataTypeMigrator migrator, (IDataType type, object config) dataType)
        {
            object newConfig;
            ValueStorageType dbType;
            string newAlias;

            try
            {
                dbType = migrator.GetNewDatabaseType(dataType.type, dataType.config);
            }
            catch (Exception e)
            {
                Logger.Error<IdToUdiMigration>($"Could not find the database type for the data type {dataType.type.Name} ({dataType.type.EditorAlias})", e);
                return false;
            }

            try
            {
                newAlias = migrator.GetNewEditorAlias(dataType.type, dataType.config);
            }
            catch (Exception e)
            {
                Logger.Error<IdToUdiMigration>($"Could not find the new property editor alias for the data type {dataType.type.Name} ({dataType.type.EditorAlias})", e);
                return false;
            }

            try
            {
                newConfig = migrator.GetNewConfiguration(dataType.type, dataType.config);
            }
            catch (Exception e)
            {
                Logger.Error<IdToUdiMigration>($"Could not get the new pre-values for the data type {dataType.type.Name}, when migrating from {dataType.type.EditorAlias} to {newAlias}", e);
                return false;
            }

            try
            {
                Logger.Info<IdToUdiMigration>($"Updating data type {dataType.type.Name}, from {dataType.type.EditorAlias} to {newAlias}");

                if (dataType.type.EditorAlias != newAlias || dataType.type.DatabaseType != dbType || !AreEquivalentConfig(dataType.config, newConfig))
                {
                    if (dataType.type.EditorAlias != newAlias && Current.PropertyEditors.TryGet(newAlias, out var editor)) dataType.type.Editor = editor;
                    dataType.type.DatabaseType = dbType;
                    dataType.type.Configuration = newConfig;
                    dts.Save(dataType.type);
                }
            }
            catch (Exception e)
            {
                Logger.Error<IdToUdiMigration>($"Could not update the data type {dataType.type.Name} from {dataType.type.EditorAlias} to {newAlias}", e);
                return false;
            }

            return true;
        }

        protected virtual bool AreEquivalentConfig(object oldConfig, object newConfig)
        {
            if (ReferenceEquals(oldConfig, newConfig) || (oldConfig == null && newConfig == null)) return true;
            if (oldConfig == null || newConfig == null) return false;

            var oldType = oldConfig.GetType();
            var newType = newConfig.GetType();
            if (oldType != newType) return false;

            var props = oldType.GetPublicProperties();
            foreach (var prop in props)
            {
                var oldVal = prop.GetValue(oldConfig);
                var newVal = prop.GetValue(newConfig);

                if (ReferenceEquals(oldVal, newVal) || (oldVal == null && newVal == null)) continue;
                if (oldVal == null || newVal == null || !oldVal.Equals(newVal)) return false;
            }

            return true;
        }

        protected virtual IEnumerable<IContentTransformMapper> FindMappings(IEnumerable<IContentTypeBase> cts, ContentBaseType sourceType, IDictionary<int, IPropertyMigration> dataTypes)
        {
            var headerShown = false;

            foreach (var ct in cts)
            {
                var mappings = new List<IFieldMapper>();

                foreach (var property in ct.PropertyTypes)
                {
                    if (!dataTypes.TryGetValue(property.DataTypeId, out var migrations)) continue;

                    mappings.Add(new FieldMapper(property.Alias, sourceType + " Picker", new[] {migrations}));
                }

                if (mappings.Count == 0) continue;

                if (!headerShown)
                {
                    Logger.Info<IdToUdiMigration>($"Updating properties for the following {sourceType} types");
                    headerShown = true;
                }

                var fields = string.Join(", ", mappings.Select(m => m.FieldName));
                Logger.Info<IdToUdiMigration>($"    {ct.Name} ({ct.Alias} - #{ct.Id}), in properties {fields}");
                yield return new ContentTransformMapper(new ContentsByTypeSource(sourceType, ct.Alias), mappings);
            }
        }

        protected class DataTypeMigrations
        {
            public IDataTypeMigrator Migrator { get; set; }
            public List<(IDataType type, object config)> DataTypes { get; set; }
        }
    }
}
