using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Imulus.Archetype")]
    public class ArchetypeMigrator : JsonContentMigrator<JObject>
    {
        protected override IEnumerable<IJsonPropertyTransform<JObject>> GetJsonPropertyTransforms(IDataType dataType, object oldConfig, bool retainInvalidData)
        {
            if (oldConfig == null || !(oldConfig is JObject config)) yield break;

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
                    var migration = GetValidPropertyMigration(dtGuid?.ToString(), retainInvalidData);

                    if (migration != null)
                        yield return new JsonPropertyTransform<JObject>
                        {
                            Migration = migration,
                            PropertyValuesAndSetters = o => GetValuesAndSetters(o, alias)
                        };
                }
            }
        }

        public static IEnumerable<Tuple<string, Action<JObject, string>>> GetValuesAndSetters(JObject token, string alias)
        {
            var fieldSets = token?["fieldsets"];
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
                    yield return new Tuple<string, Action<JObject, string>>(
                        property["value"]?.ToString(),
                        (o, value) => PropertySetter(o, f, p, value)
                        );
                }
            }
        }

        public static void PropertySetter(JObject token, int fieldSetIndex, int propertyIndex, string value)
        {
            if (token == null) return;
            if (!(token["fieldsets"] is JArray fieldSets) || fieldSets.Count < fieldSetIndex || !(fieldSets[fieldSetIndex] is JObject fieldSet)
                || !(fieldSet["properties"] is JArray properties) || properties.Count < propertyIndex || !(properties[propertyIndex] is JObject property)) return;

            property["value"] = value;
        }
    }
}
