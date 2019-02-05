using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Imulus.Archetype")]
    public class ArchetypeMigrator : JsonContentMigrator
    {
        private static readonly Dictionary<Guid, IPropertyMigration> KnownMigrations = new Dictionary<Guid, IPropertyMigration>();

        protected override IEnumerable<JsonPropertyTransform> GetJsonPropertyTransforms(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            if (oldPreValues == null || !oldPreValues.TryGetValue("archetypeConfig", out var cfgPreVal) || string.IsNullOrWhiteSpace(cfgPreVal?.Value)) yield break;

            var dts = ApplicationContext.Current.Services.DataTypeService;
            var config = JsonConvert.DeserializeObject<JObject>(cfgPreVal.Value);
            var fieldsets = config?["fieldsets"];
            if (fieldsets == null) yield break;

            foreach (var fieldset in fieldsets)
            {
                var properties = fieldset?["properties"];
                if (properties == null) continue;

                foreach (var property in properties)
                {
                    var alias = property?["alias"]?.ToString();
                    if (string.IsNullOrWhiteSpace(alias)) continue;

                    var dtGuid = property["dataTypeGuid"];
                    if (!Guid.TryParse(dtGuid?.ToString(), out var guid)) continue;

                    if (!KnownMigrations.TryGetValue(guid, out var migration))
                    {
                        var dt = dts.GetDataTypeDefinitionById(guid);
                        var dtm = DataTypeMigratorFactory.Instance.CreateDataTypeMigrator(dt.PropertyEditorAlias);

                        if (dtm != null)
                        {
                            var preValueCollection = dts.GetPreValuesCollectionByDataTypeId(dt.Id);
                            var opv = preValueCollection?.FormatAsDictionary() ?? new Dictionary<string, PreValue>();
                            migration = dtm.NeedsMigration(dt, opv) ? dtm.GetPropertyMigration(dt, opv) : null;
                        }
                        else migration = null;

                        KnownMigrations[guid] = migration;
                    }

                    if (migration != null)
                        yield return new JsonPropertyTransform
                        {
                            Migration = migration,
                            PropertyValuesAndSetters = o => GetValuesAndSetters(o, alias)
                        };
                }
            }
        }

        public static IEnumerable<Tuple<string, Action<object, string>>> GetValuesAndSetters(object obj, string alias)
        {
            if (!(obj is JObject token)) yield break;

            var fieldSets = token["fieldsets"];
            if (fieldSets == null) yield break;

            var fIdx = -1;
            foreach (var fieldSet in fieldSets)
            {
                fIdx++;

                var properties = fieldSet?["properties"];
                if (properties == null) continue;

                var pIdx = -1;
                foreach (var property in properties)
                {
                    pIdx++;

                    var pAlias = property?["alias"]?.ToString();
                    if (pAlias == null || pAlias != alias) continue;

                    var f = fIdx;
                    var p = pIdx;
                    yield return new Tuple<string, Action<object, string>>(
                        property["value"]?.ToString(),
                        (o, value) => PropertySetter(o, f, p, value)
                        );
                }
            }
        }

        public static void PropertySetter(object obj, int fieldSetIndex, int propertyIndex, string value)
        {
            if (!(obj is JObject token)) return;
            if (!(token["fieldsets"] is JArray fieldSets) || fieldSets.Count < fieldSetIndex || !(fieldSets[fieldSetIndex] is JObject fieldSet)
                || !(fieldSet["properties"] is JArray properties) || properties.Count < propertyIndex || !(properties[propertyIndex] is JObject property)) return;

            property["value"] = value;
        }
    }
}
