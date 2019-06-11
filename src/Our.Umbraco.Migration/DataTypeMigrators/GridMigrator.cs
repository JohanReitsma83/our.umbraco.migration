using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Migration.GridAliasMigrators;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.Grid")]
    public class GridMigrator : JsonContentMigrator<JObject>
    {
        private static List<string> _allAliases;

        protected override IEnumerable<IJsonPropertyTransform<JObject>> GetJsonPropertyTransforms(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues, bool retainInvalidData)
        {
            if (oldPreValues == null || !oldPreValues.TryGetValue("items", out var cfgPreVal) || string.IsNullOrWhiteSpace(cfgPreVal?.Value)) yield break;

            var config = JsonConvert.DeserializeObject<JObject>(cfgPreVal.Value);
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
            var config = UmbracoConfig.For.GridConfig(ApplicationContext.Current.ProfilingLogger.Logger,
                ApplicationContext.Current.ApplicationCache.RuntimeCache,
                new System.IO.DirectoryInfo(HttpContext.Current.Server.MapPath(SystemDirectories.AppPlugins)),
                new System.IO.DirectoryInfo(HttpContext.Current.Server.MapPath(SystemDirectories.Config)),
                HttpContext.Current == null || HttpContext.Current.IsDebuggingEnabled);
            var aliases = new List<string>();
            var editors = config.EditorsConfig.Editors;

            foreach (var editor in editors)
            {
                if (editor?.Config == null) continue;

                var alias = editor.Alias;
                if (!string.IsNullOrWhiteSpace(alias)) aliases.Add(alias);

                if (editor.Config.TryGetValue("editors", out var val) && val is JArray subEditors)
                {
                    RegisterPropertyMigrations(alias, retainInvalidData, subEditors.Select(s => (s?["dataType"]?.ToString(), s?["alias"]?.ToString())), p => new GenericEditorMigrator { PropertyMigrations = p });
                }
                if (editor.Config.TryGetValue("allowedDocTypes", out val) && val is JArray docTypes)
                {
                    foreach (var docType in docTypes)
                    {
                        var docTypeAlias = (docType as JValue)?.Value as string;
                        if (string.IsNullOrWhiteSpace(docTypeAlias)) continue;

                        var ct = ApplicationContext.Current.Services.ContentTypeService.GetContentType(docTypeAlias);
                        if (ct == null) continue;

                        RegisterPropertyMigrations(alias, retainInvalidData, ct.PropertyTypes.Select(p => (p.DataTypeDefinitionId.ToString(), p.Alias)), p => new DocTypeMigrator { PropertyMigrations = p });
                    }
                }
            }

            return aliases;
        }

        protected virtual void RegisterPropertyMigrations(string alias, bool retainInvalidData, IEnumerable<(string DataTypeGuid, string PropertyAlias)> properties, Func<Dictionary<string, IPropertyMigration>, IGridAliasMigrator> migratorConstructor)
        {
            var propertyMigrations = new Dictionary<string, IPropertyMigration>();

            foreach ((string id, string al) in properties)
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(al)) continue;

                var migration = GetValidPropertyMigration(id, retainInvalidData);
                if (migration != null) propertyMigrations[al] = migration;
            }

            if (propertyMigrations.Count > 0) GridAliasMigratorFactory.Instance.RegisterGridAliasMigrator(alias, migratorConstructor(propertyMigrations), false);
        }

        protected class GridPropertyTransform : IJsonPropertyTransform<JObject>
        {
            public Dictionary<string, IGridAliasMigrator> Migrators { get; set; }

            public IEnumerable<(string, IPropertyMigration, Action<JObject, string>)> GetPropertyValuesMigrationsAndSetters(JObject token)
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

                                        yield return (valueMigrationAndSetting.PropertyValue,
                                            valueMigrationAndSetting.Migration,
                                            (o, val) => PropertySetter(o, s, r, a, c, val, valueMigrationAndSetting.SetPropertyValue));
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
