using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.RelatedLinks")]
    public class RelatedLinksMigrator : JsonContentMigrator<JArray>
    {
        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => "Umbraco.RelatedLinks2";

        protected override IEnumerable<IJsonPropertyTransform<JArray>> GetJsonPropertyTransforms(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues, bool retainInvalidData)
        {
            yield return new JsonPropertyTransform<JArray>
            {
                Migration = new RelatedLinkMigration(),
                PropertyValuesAndSetters = GetValuesAndSetters
            };
        }

        public static IEnumerable<(string, Action<JArray, string>)> GetValuesAndSetters(JArray token)
        {
            for (var i = 0; i < token.Count; i++)
            {
                var idx = i;
                yield return (
                    token[i].ToString(),
                    (o, value) => PropertySetter(o, idx, value)
                    );
            }
        }

        public static void PropertySetter(JArray token, int index, string value)
        {
            if (token == null || token.Count <= index) return;
            var jobj = JsonConvert.DeserializeObject<JObject>(value);
            token[index] = jobj;
        }

        private class RelatedLinkMigration : IPropertyMigration
        {
            public IPropertyTransform Upgrader { get; } = new RelatedLinkTransform();

            public IPropertyTransform Downgrader => throw new NotImplementedException();
        }

        public class RelatedLinkTransform : IPropertyTransform
        {
            public object Map(ServiceContext ctx, object from)
            {
                if (!(from is string str)) return from;
                var obj = JsonConvert.DeserializeObject<JObject>(str);
                if (obj == null) return from;

                var linkStr = obj["link"]?.ToString();
                if (!int.TryParse(linkStr, out _)) return from;

                var udi = IdToUdiTransform.MapToUdi(ctx, linkStr, ContentBaseType.Document, true, out var node);
                if (string.IsNullOrEmpty(udi) || node == null) return from;

                obj["link"] = udi;
                obj["internal"] = udi;
                obj["internalName"] = node.Name;
                obj["internalIcon"] = node.GetContentType().Icon;

                var to = JsonConvert.SerializeObject(obj);
                return to;
            }

            public void Set(IContentBase content, string field, object value)
            {
                content?.SetValue(field, value);
            }

            public bool TryGet(IContentBase content, string field, out object value)
            {
                value = null;

                if (content == null || !content.HasProperty(field)) return false;

                value = content.GetValue<string>(field);
                return value != null;
            }
        }
    }
}
