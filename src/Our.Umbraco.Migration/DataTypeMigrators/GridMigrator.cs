using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Migration.GridAliasMigrators;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web.PropertyEditors;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.Grid")]
    public class GridMigrator : JsonContentMigrator<JObject>
    {
        private static List<string> _allAliases;

        protected override IEnumerable<IJsonPropertyTransform<JObject>> GetJsonPropertyTransforms(IDataType dataType, object oldConfig, bool retainInvalidData)
        {
            if (!(oldConfig is GridConfiguration oldC) || oldC.Items == null) yield break;

            var config = oldC.Items;
            var layouts = config?["layouts"];
            if (layouts == null) yield break;

            var allAliases = _allAliases ?? GetAllAliasesAndRegisterGenericMigrators(retainInvalidData);
            if (_allAliases == null) _allAliases = allAliases;

            var migrators = GetMigratorMap(layouts, allAliases);
            migrators.RemoveAll(p => p.Value == null);

            if (migrators.Count == 0) yield break;

            yield return new GridPropertyTransform {Migrators = migrators};
        }

        protected virtual Dictionary<string, IGridAliasMigrator> GetMigratorMap(JToken layouts, ICollection<string> allAliases)
        {
            var migratorMap = new Dictionary<string, IGridAliasMigrator>();

            foreach (var layout in layouts)
            {
                var areas = layout?["areas"];
                if (areas == null) continue;

                foreach (var area in areas)
                {
                    var alloweds = new List<string>();
                    var allowAll = area?["allowAll"];
                    if (allowAll != null && allowAll.Type == JTokenType.Boolean && allowAll.ToString().ToLowerInvariant() == "true")
                    {
                        alloweds.AddRange(allAliases);
                    }
                    else
                    {
                        var allowedAttr = area?["allowed"];
                        if (allowedAttr == null) continue;

                        alloweds.AddRange(allowedAttr.Where(a => a != null && a.Type == JTokenType.String).Select(a => a.ToString()));
                    }

                    foreach (var allowed in alloweds)
                    {
                        if (!migratorMap.TryGetValue(allowed, out _)) migratorMap[allowed] = GridAliasMigratorFactory.Instance.CreateGridAliasMigrator(allowed);
                    }
                }
            }

            return migratorMap;
        }

        protected virtual List<string> GetAllAliasesAndRegisterGenericMigrators(bool retainInvalidData)
        {
            var aliases = new List<string>();
            var editorPath = HttpContext.Current.Server.MapPath("~/config/grid.editors.config.js");
            if (!System.IO.File.Exists(editorPath)) return aliases;

            var editorText = System.IO.File.ReadAllText(editorPath);
            var editors = JsonConvert.DeserializeObject<JArray>(editorText);

            foreach (var editor in editors)
            {
                var alias = editor?["alias"]?.ToString();
                if (!string.IsNullOrWhiteSpace(alias)) aliases.Add(alias);

                if (!(editor?["config"]?["editors"] is JArray subEditors)) continue;

                var propertyMigrations = new Dictionary<string, IPropertyMigration>();
                foreach (var subEditor in subEditors)
                {
                    var id = subEditor?["dataType"]?.ToString();
                    var al = subEditor?["alias"]?.ToString();
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(al)) continue;

                    var migration = GetValidPropertyMigration(id, retainInvalidData);
                    if (migration != null) propertyMigrations[al] = migration;
                }

                if (propertyMigrations.Count == 0) continue;

                GridAliasMigratorFactory.Instance.RegisterGridAliasMigrator(alias, new GenericEditorMigrator {PropertyMigrations = propertyMigrations}, false);
            }

            return aliases;
        }

        protected class GridPropertyTransform : IJsonPropertyTransform<JObject>
        {
            public Dictionary<string, IGridAliasMigrator> Migrators { get; set; }

            public IEnumerable<Tuple<string, IPropertyMigration, Action<JObject, string>>> GetPropertyValuesMigrationsAndSetters(JObject token)
            {
                var sections = token?["sections"];
                if (sections == null) yield break;

                var sIdx = -1;
                foreach (var section in sections)
                {
                    sIdx++;

                    var rows = section?["rows"];
                    if (rows == null) continue;

                    var rIdx = -1;
                    foreach (var row in rows)
                    {
                        rIdx++;

                        var areas = row?["areas"];
                        if (areas == null) continue;

                        var aIdx = -1;
                        foreach (var area in areas)
                        {
                            aIdx++;

                            var controls = area?["controls"];
                            if (controls == null) continue;

                            var cIdx = -1;
                            foreach (var control in controls)
                            {
                                cIdx++;

                                var value = control?["value"];
                                var editor = control?["editor"]?["alias"]?.ToString();
                                if (value == null || string.IsNullOrWhiteSpace(editor) || !Migrators.TryGetValue(editor, out var migrator) || migrator == null) continue;

                                var transforms = migrator.GetJsonPropertyTransforms(editor);
                                if (transforms == null) continue;

                                foreach (var transform in transforms)
                                {
                                    var valuesMigrationsAndSettings = transform?.GetPropertyValuesMigrationsAndSetters(value);
                                    if (valuesMigrationsAndSettings == null) continue;

                                    foreach (var valueMigrationAndSetting in valuesMigrationsAndSettings)
                                    {
                                        var s = sIdx;
                                        var r = rIdx;
                                        var a = aIdx;
                                        var c = cIdx;

                                        yield return new Tuple<string, IPropertyMigration, Action<JObject, string>>(valueMigrationAndSetting.Item1,
                                            valueMigrationAndSetting.Item2,
                                            (o, val) => PropertySetter(o, s, r, a, c, val, valueMigrationAndSetting.Item3));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private static void PropertySetter(JObject obj, int section, int row, int area, int control, string value, Action<JToken, string> setter)
            {
                var field = obj?["sections"]?[section]?["rows"]?[row]?["areas"]?[area]?["controls"]?[control]?["value"];
                if (field == null) return;

                setter(field, value);
            }
        }
    }
}
