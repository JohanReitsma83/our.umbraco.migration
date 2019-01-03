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
        private readonly List<string> _includedDataTypes;
        private readonly List<string> _excludedDataTypes;

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance.  Use this overload if you want to explicitly specify the content types and field names to migrate.
        /// </summary>
        /// <param name="contentTypeFieldMappings">This dictionary has as its key the content type aliases to be mapped.  Each one is then mapped to a list of fields that should be migrated from an integer to a UDI for that content type.  The field values can be single integers, or a comma-separated list of integers</param>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        protected IdToUdiMigration(IDictionary<string, IDictionary<string, ContentBaseType>> contentTypeFieldMappings, ISqlSyntaxProvider sqlSyntax, ILogger logger)
            : base(contentTypeFieldMappings.Select(m => new IdToUdiTransformMapper(m.Key, m.Value)), sqlSyntax, logger)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance.  Use this overload if you want the migration to determine which data types need to be migrated and automatically convert data types and migrate content.
        /// </summary>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        /// <param name="includedDataTypes">The list of data types to include in the migration.  If specified, only these data types, and their related content, will be migrated.  By default includes all data types</param>
        /// <param name="excludedDataTypes">The list of data types to exclude from the migration.  If specified, only these data types, and their related content, will be migrated.  By default no data types are excluded</param>
        protected IdToUdiMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger, IEnumerable<string> includedDataTypes = null, IEnumerable<string> excludedDataTypes = null)
            : base(sqlSyntax, logger)
        {
            if (includedDataTypes != null) _includedDataTypes = new List<string>(includedDataTypes);
            if (excludedDataTypes != null) _excludedDataTypes = new List<string>(excludedDataTypes);
        }

        /// <inheritdoc />
        /// <summary>
        /// Finds all data types which have IDataTypeMigrators registered, where the data types are not barred by the include/exclude lists passed to the constructor.
        /// It then updates those data types to their new type, and finds all content types, media types, and member types associated with those data types and returns mappers for them.
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<ContentBaseTransformMapper> LoadMappings()
        {
            var svc = ApplicationContext.Current.Services;
            var dts = svc.DataTypeService;

            var migrations = FindDataTypeMigrations(dts);
            var allDataTypes = new Dictionary<int, ContentBaseType>(migrations.Sum(m => m.Value.DataTypes.Count));

            foreach (var migration in migrations.Values)
            {
                foreach (var dataType in migration.DataTypes)
                {
                    if (!UpdateDataType(dts, migration.Migrator, dataType, out var type)) continue;
                    allDataTypes[dataType.Id] = type;
                }
            }

            var documentMappings = FindMappings(svc.ContentTypeService.GetAllContentTypes(), ContentBaseType.Document, allDataTypes) ?? new ContentBaseTransformMapper[0];
            var mediaMappings = FindMappings(svc.ContentTypeService.GetAllMediaTypes(), ContentBaseType.Media, allDataTypes) ?? new ContentBaseTransformMapper[0];
            var memberMappings = FindMappings(svc.MemberTypeService.GetAll(), ContentBaseType.Member, allDataTypes) ?? new ContentBaseTransformMapper[0];

            return documentMappings.Union(mediaMappings).Union(memberMappings);
        }

        private Dictionary<string, DataTypeMigrations> FindDataTypeMigrations(IDataTypeService dts)
        {
            var migrations = new Dictionary<string, DataTypeMigrations>(StringComparer.InvariantCultureIgnoreCase);
            var allDataTypes = dts.GetAllDataTypeDefinitions();

            foreach (var dataType in allDataTypes)
            {
                var name = dataType.Name;
                var alias = dataType.PropertyEditorAlias;

                if ((_excludedDataTypes != null && _excludedDataTypes.Contains(name, StringComparer.InvariantCultureIgnoreCase)) ||
                    (_includedDataTypes != null && !_includedDataTypes.Contains(name, StringComparer.InvariantCultureIgnoreCase)))
                    continue;

                if (!migrations.TryGetValue(alias, out var migration))
                {
                    var migrator = DataTypeMigratorFactory.Instance.CreateDataTypeMigrator(alias);
                    migrations[alias] = migrator == null ? null : new DataTypeMigrations {Migrator = migrator, DataTypes = new List<IDataTypeDefinition> {dataType}};
                }
                else
                    migration?.DataTypes.Add(dataType);
            }

            migrations.RemoveAll(m => m.Value == null);
            return migrations;
        }

        private static bool UpdateDataType(IDataTypeService dts, IDataTypeMigrator migrator, IDataTypeDefinition dataType,  out ContentBaseType type)
        {
            IDictionary<string, PreValue> oldPreValues, newPreValues;
            DataTypeDatabaseType dbType = dataType.DatabaseType;
            string newAlias;
            type = ContentBaseType.Document;

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

            try
            {
                type = migrator.GetContentBaseType(dataType.PropertyEditorAlias, oldPreValues);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not find the base content type for the data type {dataType.Name} ({dataType.PropertyEditorAlias})", e);
                return false;
            }

            try
            {
                dbType = migrator.GetNewDatabaseType(dataType.PropertyEditorAlias, dbType);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not find the database type for the data type {dataType.Name} ({dataType.PropertyEditorAlias})", e);
                return false;
            }

            try
            {
                newAlias = migrator.GetNewPropertyEditorAlias(dataType.PropertyEditorAlias);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not find the new property editor alias for the data type {dataType.Name} ({dataType.PropertyEditorAlias}), a {type} picker", e);
                return false;
            }

            try
            {
                newPreValues = migrator.GetNewPreValues(dataType.PropertyEditorAlias, oldPreValues) ?? new Dictionary<string, PreValue>();
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not get the new pre-values for the data type {dataType.Name}, a {type} picker, when migrating from {dataType.PropertyEditorAlias} to {newAlias}", e);
                return false;
            }

            try
            {
                var sb = new StringBuilder($"Updating data type {dataType.Name}, a {type} picker, from {dataType.PropertyEditorAlias} to {newAlias} with the following pre-values");
                var propMaps = oldPreValues.ToDictionary(p => p.Key,
                    p => new Tuple<string, string>(p.Value?.Value, newPreValues != null && newPreValues.TryGetValue(p.Key, out var val) ? val?.Value : null));
                newPreValues.Where(p => oldPreValues == null || !oldPreValues.ContainsKey(p.Key)).ToList().ForEach(p => propMaps[p.Key] = new Tuple<string, string>(null, p.Value?.Value));
                propMaps.ToList().ForEach(p => AppendKeyValues(sb, p.Key, p.Value.Item1, p.Value.Item2));
                LogHelper.Info<IdToUdiMigration>(sb.ToString());

                dataType.PropertyEditorAlias = newAlias;
                dataType.DatabaseType = dbType;
                dts.SaveDataTypeAndPreValues(dataType, newPreValues);
            }
            catch (Exception e)
            {
                LogHelper.Error<IdToUdiMigration>($"Could not update the data type {dataType.Name} from {dataType.PropertyEditorAlias} to {newAlias}", e);
                return false;
            }

            return migrator.MigrateIdToUdi;
        }

        private static void AppendKeyValues(StringBuilder sb, string key, string value1, string value2)
        {
            sb.Append(",    {{");
            sb.Append(key);
            sb.Append(":   ");
            if (value1 != null) sb.Append(value1.Replace("{", "{{").Replace("}", "}}").Replace("\r", " ").Replace("\n", " "));
            sb.Append("   -->   ");
            if (value2 != null) sb.Append(value2.Replace("{", "{{").Replace("}", "}}").Replace("\r", " ").Replace("\n", " "));
            sb.Append("}}");
        }

        private static IEnumerable<ContentBaseTransformMapper> FindMappings(IEnumerable<IContentTypeBase> cts, ContentBaseType sourceType, IDictionary<int, ContentBaseType> dataTypes)
        {
            var headerShown = false;

            foreach (var ct in cts)
            {
                var mappings = new Dictionary<string, ContentBaseType>();

                foreach (var property in ct.PropertyTypes)
                {
                    if (!dataTypes.TryGetValue(property.DataTypeDefinitionId, out var type)) continue;

                    mappings[property.Alias] = type;
                }

                if (mappings.Count == 0) continue;

                if (!headerShown)
                {
                    LogHelper.Info<IdToUdiMigration>($"Updating properties for the following {sourceType} types");
                    headerShown = true;
                }

                var fields = string.Join(", ", mappings.Keys);
                LogHelper.Info<IdToUdiMigration>($"    {ct.Name} ({ct.Alias} - #{ct.Id}), in properties {fields}");
                yield return new IdToUdiTransformMapper(sourceType, ct.Alias, mappings);
            }
        }

        private class DataTypeMigrations
        {
            public IDataTypeMigrator Migrator { get; set; }
            public List<IDataTypeDefinition> DataTypes { get; set; }
        }
    }
}
