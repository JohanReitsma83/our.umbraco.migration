using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("RJP.MultiUrlPicker")]
    public class RJPMultiUrlPickerMigrator : JsonContentMigrator<JArray>
    {
        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => "Umbraco.MultiUrlPicker";
        protected override IEnumerable<IJsonPropertyTransform<JArray>> GetJsonPropertyTransforms(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues, bool retainInvalidData)
        {
            yield return new JsonPropertyTransform<JArray>
            {
                Migration = new RJPMultiUrlPickerMigration(),
                PropertyValuesAndSetters = GetValuesAndSetters
            };
        }

        public static IEnumerable<(string, Action<JArray, string>)> GetValuesAndSetters(JArray token)
        {
            for (var i = 0; i < token.Count; i++)
            {
                var idx = i;
                yield return (token[i].ToString(), (o, value) => PropertySetter(o, idx, value));
            }
        }

        public static void PropertySetter(JArray token, int index, string value)
        {
            if (token == null || token.Count <= index)
            {
                return;
            }

            var jobj = JsonConvert.DeserializeObject<JObject>(value);
            token[index] = jobj;
        }

        private class RJPMultiUrlPickerMigration : IPropertyMigration
        {
            public IPropertyTransform Upgrader { get; } = new RJPMultiUrlPickerTransform();

            public IPropertyTransform Downgrader => throw new NotImplementedException();
        }

        public class RJPMultiUrlPickerTransform : IPropertyTransform
        {
            public object Map(ServiceContext ctx, object from)
            {
                if (!(from is string str))
                {
                    return from;
                }

                var obj = JsonConvert.DeserializeObject<JObject>(str);
                if (obj == null)
                {
                    return from;
                }

                var udi = "";
                var IdStr = obj["id"]?.ToString();
                int.TryParse(IdStr, out var nodeId);
                if(nodeId > 0)
                {
                    var isMediaStr = obj["isMedia"]?.ToString();
                    Boolean.TryParse(isMediaStr, out var isMedia);
                    udi = IdToUdiTransform.MapToUdi(ctx, IdStr, isMedia ? ContentBaseType.Media : ContentBaseType.Document, true, out _);
                }
                
                if (!String.IsNullOrWhiteSpace(udi))
                {
                    obj["udi"] = udi;
                    obj.Remove("url");
                }

                var caption = obj["caption"]?.ToString();
                if (!String.IsNullOrWhiteSpace(caption))
                {
                    obj["name"] = caption;
                }

                obj.Remove("caption");
                obj.Remove("id");
                obj.Remove("isMedia");
                obj.Remove("icon");

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

                if (content == null || !content.HasProperty(field))
                {
                    return false;
                }

                value = content.GetValue<string>(field);
                return value != null;
            }
        }
    }
}