using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Migration.DataTypeMigrators;

namespace Our.Umbraco.Migration.GridAliasMigrators
{
    public class DocTypeMigrator : IGridAliasMigrator
    {
        public virtual IReadOnlyDictionary<string, IPropertyMigration> PropertyMigrations { get; set; }

        public IEnumerable<IJsonPropertyTransform<JToken>> GetJsonPropertyTransforms(string alias)
        {
            yield return new DocTypeTransform { PropertyMigrations = PropertyMigrations };
        }
    }

    public class DocTypeTransform : IJsonPropertyTransform<JToken>
    {
        public virtual IReadOnlyDictionary<string, IPropertyMigration> PropertyMigrations { get; set; }

        public IEnumerable<(string, IPropertyMigration, Action<JToken, string>)> GetPropertyValuesMigrationsAndSetters(JToken token)
        {
            if (token is JArray arr) return GetPropertyValuesMigrationsAndSetters(arr);
            if (token is JObject obj) return GetPropertyValuesMigrationsAndSetters(obj);
            return new (string, IPropertyMigration, Action<JToken, string>)[0];
        }

        public IEnumerable<(string, IPropertyMigration, Action<JToken, string>)> GetPropertyValuesMigrationsAndSetters(JArray arr)
        {
            var idx = -1;

            var all = (IEnumerable<(string, IPropertyMigration, Action<JToken, string>)>)new (string, IPropertyMigration, Action<JToken, string>)[0];
            foreach (var token in arr)
            {
                idx++;

                if (!(token is JObject obj)) continue;

                var vals = GetPropertyValuesMigrationsAndSetters(obj);
                var entryIdx = idx;
                all = all.Union(vals.Select(v => (v.PropertyValue, v.Migration, new Action<JToken, string>((o, val) => SetValue(v.SetPropertyValue, o, val, entryIdx)))));
            }

            return all;
        }

        private static void SetValue(Action<JToken, string> setter, JToken jToken, string val, int entryIdx)
        {
            var entry = jToken?[entryIdx];
            if (entry == null) return;

            setter(entry, val);
        }

        public IEnumerable<(string PropertyValue, IPropertyMigration Migration, Action<JToken, string> SetPropertyValue)> GetPropertyValuesMigrationsAndSetters(JObject obj)
        {
            var value = obj?["value"];
            if (value == null) return new (string, IPropertyMigration, Action<JToken, string>)[0];

            return GetValuePropertyValuesMigrationsAndSetters(value).Select(v => ((string)v.PropertyValue, (IPropertyMigration)v.Migration, new Action<JToken, string>((o,s) => v.SetPropertyValue?.Invoke(o?["value"], s))));
        }

        private IEnumerable<(string PropertyValue, IPropertyMigration Migration, Action<JToken, string> SetPropertyValue)> GetValuePropertyValuesMigrationsAndSetters(JToken token)
        {
            if (token is JArray arr) return GetValuePropertyValuesMigrationsAndSetters(arr);
            if (token is JObject obj) return GetValuePropertyValuesMigrationsAndSetters(obj);
            return new (string, IPropertyMigration, Action<JToken, string>)[0];
        }

        public IEnumerable<(string PropertyValue, IPropertyMigration Migration, Action<JToken, string> SetPropertyValue)> GetValuePropertyValuesMigrationsAndSetters(JArray arr)
        {
            var idx = -1;

            var all = (IEnumerable<(string, IPropertyMigration, Action<JToken, string>)>)new (string, IPropertyMigration, Action<JToken, string>)[0];
            foreach (var token in arr)
            {
                idx++;

                if (!(token is JObject obj)) continue;

                var vals = GetValuePropertyValuesMigrationsAndSetters(obj);
                var entryIdx = idx;
                all = all.Union(vals.Select(v => (v.Item1, v.Item2, new Action<JToken, string>((o, val) => SetValue(v.Item3, o, val, entryIdx)))));
            }

            return all;
        }

        public IEnumerable<(string PropertyValue, IPropertyMigration Migration, Action<JToken, string> SetPropertyValue)> GetValuePropertyValuesMigrationsAndSetters(JObject obj)
        {
            foreach (var pair in PropertyMigrations)
            {
                var alias = pair.Key;

                yield return (
                    obj?[alias]?.ToString(),
                    pair.Value,
                    (o, val) =>
                    {
                        if (o != null) o[alias] = val;
                    }
                );
            }
        }
    }
}
