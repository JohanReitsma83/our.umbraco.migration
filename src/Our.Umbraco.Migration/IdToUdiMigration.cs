using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a helper class that performs migrations of IDs to UDIs in content nodes, by content type and field name.  The field values can be single integer IDs, or a comma-separated list of integers.
    /// </summary>
    public abstract class IdToUdiMigration : FieldTransformMigration
    {
        private static ContentTransformMapper CreateIdToUdiMapper(ContentBaseType sourceType, string contentTypeAlias, IReadOnlyDictionary<string, ContentBaseType> fieldTypes, bool retainInvalidData, bool raiseSaveAndPublishEvents)
        {
            return new ContentTransformMapper(
                new ContentsByTypeSource(sourceType, contentTypeAlias),
                fieldTypes.Select(p => new FieldMapper(p.Key, sourceType + " Picker", new[] { new PropertyMigration(new IdToUdiTransform(p.Value, retainInvalidData), new UdiToIdTransform()) })),
                raiseSaveAndPublishEvents);
        }
        private readonly List<string> _includedDataTypes;
        private readonly List<string> _excludedDataTypes;

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance.  Use this overload if you want to explicitly specify the content types and field names to migrate.
        /// </summary>
        /// <param name="contentTypeFieldMappings">This dictionary has as its key the content type aliases to be mapped.  Each one is then mapped to a list of fields that should be migrated from an integer to a UDI for that content type.  The field values can be single integers, or a comma-separated list of integers</param>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        /// <param name="retainInvalidData">If data is found that isn't a valid UDI or a valid ID of the right content type, should we keep that data, or remove it</param>
        protected IdToUdiMigration(IDictionary<string, IReadOnlyDictionary<string, ContentBaseType>> contentTypeFieldMappings, ISqlSyntaxProvider sqlSyntax, ILogger logger, bool retainInvalidData = false, bool raiseSaveAndPublishEvents = true)
            : base(contentTypeFieldMappings.Select(p => CreateIdToUdiMapper(ContentBaseType.Document, p.Key, p.Value, retainInvalidData, raiseSaveAndPublishEvents)), sqlSyntax, logger)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance.  Use this overload if you want to explicitly specify the content types and field names to migrate.
        /// </summary>
        /// <param name="contentTypeBaseFieldMappings">This dictionary has as its key the content type bases, and the value dictionary for each has as its key the content type aliases to be mapped within that content type base.  Each one is then mapped to a list of fields that should be migrated from an integer to a UDI for that content type.  The field values can be single integers, or a comma-separated list of integers</param>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        /// <param name="retainInvalidData">If data is found that isn't a valid UDI or a valid ID of the right content type, should we keep that data, or remove it</param>
        protected IdToUdiMigration(IReadOnlyDictionary<ContentBaseType, IReadOnlyDictionary<string, IReadOnlyDictionary<string, ContentBaseType>>> contentTypeBaseFieldMappings, ISqlSyntaxProvider sqlSyntax, ILogger logger, bool retainInvalidData = false, bool raiseSaveAndPublishEvents = true)
            : base(contentTypeBaseFieldMappings.SelectMany(b => b.Value.Select(p => CreateIdToUdiMapper(b.Key, p.Key, p.Value, retainInvalidData, raiseSaveAndPublishEvents))), sqlSyntax, logger)
        {
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
        protected IdToUdiMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger, bool retainInvalidData = false, IEnumerable<string> includedDataTypes = null, IEnumerable<string> excludedDataTypes = null, bool raiseSaveAndPublishEvents = true)
            : base(sqlSyntax, logger)
        {
            if (includedDataTypes != null) _includedDataTypes = new List<string>(includedDataTypes);
            if (excludedDataTypes != null) _excludedDataTypes = new List<string>(excludedDataTypes);
            RetainInvalidData = retainInvalidData;
            RaiseSaveAndPublishEvents = raiseSaveAndPublishEvents;
        }

        protected virtual bool RetainInvalidData { get; }
        protected virtual bool RaiseSaveAndPublishEvents { get; }

        /// <inheritdoc />
        /// <summary>
        /// Finds all data types which have IDataTypeMigrators registered, where the data types are not barred by the include/exclude lists passed to the constructor.
        /// It then updates those data types to their new type, and finds all content types, media types, and member types associated with those data types and returns mappers for them.
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<IContentTransformMapper> LoadMappings()
        {
            var svc = ApplicationContext.Current.Services;
            var dts = svc.DataTypeService;

            var migrations = FindDataTypeMigrations(dts);
            var allDataTypes = new Dictionary<int, IPropertyMigration>(migrations.Sum(m => m.Value.DataTypes.Count));

            foreach (var migration in migrations.Values)
            {
                foreach (var dataType in migration.DataTypes)
                {
                    var propMigration = GetPropertyMigration(migration.Migrator, dataType);
                    if (propMigration == null) continue;

                    allDataTypes[dataType.Item1.Id] = propMigration;
                }
            }

            // Do this in two separate loops so that all the migrators can see the old data types before we convert any of them to the new data types
            foreach (var migration in migrations.Values)
            {
                foreach (var dataType in migration.DataTypes)
                {
                    // If we can't update the data type, we shouldn't try to migrate the content
                    if (!UpdateDataType(dts, migration.Migrator, dataType))
                        allDataTypes.Remove(dataType.Item1.Id);
                }
            }

            var documentMappings = FindMappings(svc.ContentTypeService.GetAllContentTypes(), ContentBaseType.Document, allDataTypes) ?? new IContentTransformMapper[0];
            var mediaMappings = FindMappings(svc.ContentTypeService.GetAllMediaTypes(), ContentBaseType.Media, allDataTypes) ?? new IContentTransformMapper[0];
            var memberMappings = FindMappings(svc.MemberTypeService.GetAll(), ContentBaseType.Member, allDataTypes) ?? new IContentTransformMapper[0];

            return documentMappings.Union(mediaMappings).Union(memberMappings);
        }

        protected virtual Dictionary<string, DataTypeMigrations> FindDataTypeMigrations(IDataTypeService dts)
        {
            var migrations = new Dictionary<string, DataTypeMigrations>(StringComparer.InvariantCultureIgnoreCase);
            var allDataTypes = dts.GetAllDataTypeDefinitions();

            foreach (var dataType in allDataTypes)
            {
                var name = dataType.Name;

                if ((_excludedDataTypes != null && _excludedDataTypes.Contains(name, StringComparer.InvariantCultureIgnoreCase)) ||
                    (_includedDataTypes != null && !_includedDataTypes.Contains(name, StringComparer.InvariantCultureIgnoreCase)))
                    continue;

                if (GetPreValues(dts, dataType, out var oldPreValues))
                    UpdateDataTypeMigrations(new Tuple<IDataTypeDefinition, IDictionary<string, PreValue>>(dataType, oldPreValues), migrations);
            }

            migrations.RemoveAll(m => m.Value == null);
            return migrations;
        }

        protected virtual IDataTypeMigrator CreateMigrator(Tuple<IDataTypeDefinition, IDictionary<string, PreValue>> dataType)
        {
            return DataTypeMigratorFactory.Instance.CreateDataTypeMigrator(dataType.Item1.PropertyEditorAlias);
        }

        protected virtual void UpdateDataTypeMigrations(Tuple<IDataTypeDefinition, IDictionary<string, PreValue>> dataType, IDictionary<string, DataTypeMigrations> knownMigrations)
        {
            var alias = dataType.Item1.PropertyEditorAlias;

            if (!knownMigrations.TryGetValue(alias, out var migration))
            {
                var migrator = CreateMigrator(dataType);
                migration = knownMigrations[alias] = migrator == null ? null : new DataTypeMigrations { Migrator = migrator, DataTypes = new List<Tuple<IDataTypeDefinition, IDictionary<string, PreValue>>>() };
            }

            if (migration?.Migrator != null && migration.Migrator.NeedsMigration(dataType.Item1, dataType.Item2))
                migration.DataTypes.Add(dataType);
        }

        protected virtual bool GetPreValues(IDataTypeService dts, IDataTypeDefinition dataType, out IDictionary<string, PreValue> oldPreValues)
        {
            oldPreValues = null;

            try
            {
                var preValueCollection = dts.GetPreValuesCollectionByDataTypeId(dataType.Id);
                oldPreValues = preValueCollection?.FormatAsDictionary() ?? new Dictionary<string, PreValue>();
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not retrieve the existing pre-values for the data type {dataType.Name} ({dataType.PropertyEditorAlias})", e);
                return false;
            }

            return true;
        }

        protected virtual IPropertyMigration GetPropertyMigration(IDataTypeMigrator migrator, Tuple<IDataTypeDefinition, IDictionary<string, PreValue>> dataType)
        {
            try
            {
                return migrator.GetPropertyMigration(dataType.Item1, dataType.Item2, RetainInvalidData);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not get the property migration for the data type {dataType.Item1.Name} ({dataType.Item1.PropertyEditorAlias})", e);
                return null;
            }
        }

        protected virtual bool UpdateDataType(IDataTypeService dts, IDataTypeMigrator migrator, Tuple<IDataTypeDefinition, IDictionary<string, PreValue>> dataType)
        {
            IDictionary<string, PreValue> newPreValues;
            DataTypeDatabaseType dbType;
            string newAlias;

            try
            {
                dbType = migrator.GetNewDatabaseType(dataType.Item1, dataType.Item2);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not find the database type for the data type {dataType.Item1.Name} ({dataType.Item1.PropertyEditorAlias})", e);
                return false;
            }

            try
            {
                newAlias = migrator.GetNewPropertyEditorAlias(dataType.Item1, dataType.Item2);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not find the new property editor alias for the data type {dataType.Item1.Name} ({dataType.Item1.PropertyEditorAlias})", e);
                return false;
            }

            try
            {
                newPreValues = migrator.GetNewPreValues(dataType.Item1, dataType.Item2) ?? new Dictionary<string, PreValue>();
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not get the new pre-values for the data type {dataType.Item1.Name}, when migrating from {dataType.Item1.PropertyEditorAlias} to {newAlias}", e);
                return false;
            }

            try
            {
                var sb = new StringBuilder($"Updating data type {dataType.Item1.Name}, from {dataType.Item1.PropertyEditorAlias} to {newAlias} with the following pre-values");
                var propMaps = (dataType.Item2 ?? new Dictionary<string, PreValue>()).ToDictionary(p => p.Key,
                    p => new Tuple<string, string>(p.Value?.Value, newPreValues != null && newPreValues.TryGetValue(p.Key, out var val) ? val?.Value : null));
                newPreValues.Where(p => dataType.Item2 == null || !dataType.Item2.ContainsKey(p.Key)).ToList().ForEach(p => propMaps[p.Key] = new Tuple<string, string>(null, p.Value?.Value));
                propMaps.ToList().ForEach(p => AppendKeyValues(sb, p.Key, p.Value.Item1, p.Value.Item2));
                LogHelper.Info<IdToUdiMigration>(sb.ToString());

                if (dataType.Item1.PropertyEditorAlias != newAlias || dataType.Item1.DatabaseType != dbType || !AreEquivalentPreValues(dataType.Item2, newPreValues))
                {
                    dataType.Item1.PropertyEditorAlias = newAlias;
                    dataType.Item1.DatabaseType = dbType;
                    dts.SaveDataTypeAndPreValues(dataType.Item1, newPreValues);
                }
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not update the data type {dataType.Item1.Name} from {dataType.Item1.PropertyEditorAlias} to {newAlias}", e);
                return false;
            }

            return true;
        }

        protected virtual bool AreEquivalentPreValues(IDictionary<string, PreValue> oldPreValues, IDictionary<string, PreValue> newPreValues)
        {
            if (ReferenceEquals(oldPreValues, newPreValues)) return true;
            if ((oldPreValues == null || oldPreValues.Count == 0) && (newPreValues == null || newPreValues.Count == 0)) return true;
            if (oldPreValues == null || newPreValues == null || newPreValues.Count != oldPreValues.Count) return false;

            foreach (var pair in oldPreValues)
            {
                if (!newPreValues.TryGetValue(pair.Key, out var value)) return false;
                if (string.IsNullOrEmpty(pair.Value?.Value) && string.IsNullOrEmpty(value?.Value)) continue;
                if (string.IsNullOrEmpty(pair.Value?.Value) || string.IsNullOrEmpty(value?.Value)) return false;
                if (pair.Value.Id != value.Id || pair.Value.SortOrder != value.SortOrder) return false;
            }

            return true;
        }

        protected virtual void AppendKeyValues(StringBuilder sb, string key, string value1, string value2)
        {
            sb.Append(",    {{");
            sb.Append(key);
            sb.Append(":   ");
            if (value1 != null) sb.Append(value1.Replace("{", "{{").Replace("}", "}}").Replace("\r", " ").Replace("\n", " "));
            sb.Append("   -->   ");
            if (value2 != null) sb.Append(value2.Replace("{", "{{").Replace("}", "}}").Replace("\r", " ").Replace("\n", " "));
            sb.Append("}}");
        }

        protected virtual IEnumerable<IContentTransformMapper> FindMappings(IEnumerable<IContentTypeBase> cts, ContentBaseType sourceType, IDictionary<int, IPropertyMigration> dataTypes)
        {
            var db = ApplicationContext.Current.DatabaseContext.Database;
            var headerShown = false;
            var directlyUsed = new List<IContentTypeBase>();
            var typeProperties = new Dictionary<int, (int DataTypeDefinitionId, string Alias)[]>();
            var mappers = new List<ContentTransformMapper>();
            var relations = db.Fetch<ContentType2ContentType>("SELECT ParentContentTypeId, ChildContentTypeId FROM cmsContentType2ContentType");
            var usedTypes = db.Fetch<ContentTypeRow>("SELECT DISTINCT contentType FROM cmsContent").Select(c => c.ContentType).ToList();

            foreach (var ct in cts)
            {
                typeProperties[ct.Id] = ct.PropertyTypes.Select(p => (p.DataTypeDefinitionId, p.Alias)).ToArray();
                relations.Add(new ContentType2ContentType { ChildContentTypeId = ct.Id, ParentContentTypeId = ct.ParentId });
                if (usedTypes.Contains(ct.Id)) directlyUsed.Add(ct);
            }

            var relatedIds = relations.ToLookup(r => r.ChildContentTypeId, r => r.ParentContentTypeId);
            foreach (var ct in directlyUsed)
            {
                var allTypeIds = new List<int>();
                AddRelatedTypes(ct.Id, allTypeIds, relatedIds);

                var allProperties = allTypeIds.SelectMany(id => typeProperties.TryGetValue(id, out var pair) ? pair : new (int DataTypeDefinitionId, string Alias)[0]).Distinct().ToList();
                var mappings = new List<IFieldMapper>();

                foreach (var property in allProperties)
                {
                    if (!dataTypes.TryGetValue(property.DataTypeDefinitionId, out var migrations)) continue;

                    mappings.Add(new FieldMapper(property.Alias, sourceType + " Picker", new[] {migrations}));
                }

                if (mappings.Count == 0) continue;

                if (!headerShown)
                {
                    LogHelper.Info<IdToUdiMigration>($"Updating properties for the following {sourceType} types");
                    headerShown = true;
                }

                var fields = string.Join(", ", mappings.Select(m => m.FieldName));
                LogHelper.Info<IdToUdiMigration>($"    {ct.Name} ({ct.Alias} - #{ct.Id}), in properties {fields}");
                mappers.Add(new ContentTransformMapper(new ContentsByTypeSource(sourceType, ct.Alias, false), mappings, RaiseSaveAndPublishEvents));
            }

            return mappers;
        }

        private void AddRelatedTypes(int typeId, List<int> allTypeIds, ILookup<int, int> relatedIds)
        {
            if (typeId <= 0 || allTypeIds.Contains(typeId)) return;

            allTypeIds.Add(typeId);

            foreach (var id in relatedIds[typeId]) AddRelatedTypes(id, allTypeIds, relatedIds);
        }

        protected class DataTypeMigrations
        {
            public IDataTypeMigrator Migrator { get; set; }
            public List<Tuple<IDataTypeDefinition, IDictionary<string, PreValue>>> DataTypes { get; set; }
        }

        private class ContentType2ContentType
        {
            public int ChildContentTypeId { get; set; }
            public int ParentContentTypeId { get; set; }
        }

        private class ContentTypeRow
        {
            public int ContentType { get; set; }
        }
    }
}
