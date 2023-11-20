using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web;


namespace Our.Umbraco.Migration.DataTypeMigrators
{

    [DataTypeMigrator("Our.Umbraco.NestedContent")]
    public class NestedContentMigrator : IDataTypeMigrator
    {
        public bool NeedsMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return true;
        }

        public string GetNewPropertyEditorAlias(IDataTypeDefinition dataType,
            IDictionary<string, PreValue> oldPreValues)
        {
            return "Umbraco.NestedContent";
        }
        public DataTypeDatabaseType GetNewDatabaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return DataTypeDatabaseType.Ntext;
        }

        public IDictionary<string, PreValue> GetNewPreValues(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return oldPreValues;
        }

        public IPropertyMigration GetPropertyMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues, bool retainInvalidData)
        {
            
            return new NestedContentMigration(UmbracoContext.Current.Application.Services.DataTypeService);
        }



    }

    public class NestedContentMigration : IPropertyMigration
    {
        private Dictionary<int, IDataTypeDefinition> _definitions;

        public NestedContentMigration(IDataTypeService dataTypeService)
        {
            var nuPickers = dataTypeService.GetAllDataTypeDefinitions().Where(r => r.PropertyEditorAlias.Contains("nuPickers") || r.PropertyEditorAlias.Contains("NestedContent") || r.PropertyEditorAlias.Contains("Picker") || r.PropertyEditorAlias.Contains("RelatedLinks")).ToList();
            _definitions = nuPickers.ToDictionary(n => n.Id);
        }

        public IPropertyTransform Upgrader => new NestedContentTransform(_definitions);

        public IPropertyTransform Downgrader { get; }
    }

    public class NestedContentTransform : IPropertyTransform
    {
        private Dictionary<int, IDataTypeDefinition> _definitions;
        string[] ncProps = new[] { "name", "ncContentTypeAlias", "key" };

        public NestedContentTransform(Dictionary<int, IDataTypeDefinition> definitions)
        {
            _definitions = definitions;
        }

        private int contentTypeId;
        public bool TryGet(IContentBase content, string field, out object value)
        {
            contentTypeId = content.ContentTypeId;
            value = content.GetValue(field);
            return true;
        }

        public void Set(IContentBase content, string field, object value)
        {
            if (value != null)
            {
                content.SetValue(field, value.ToString());
            }
        }

        public object Map(ServiceContext ctx, object from)
        {
            
            if (from != null)
            {
                return ConvertInnerNestedContent(ctx, from.ToString());
            }

            return from;


        }

        private string ConvertInnerNestedContent(ServiceContext ctx, object originalValue)
        {
            var ncArray = JsonConvert.DeserializeObject<JArray>(originalValue.ToString());
            var listOfNc = new List<JObject>();
            try
            {
                if (originalValue == null || string.IsNullOrWhiteSpace(originalValue.ToString()))
                    return string.Empty;
                
                foreach (var token in ncArray)
                {
                    var ncObj = JsonConvert.DeserializeObject<JObject>(token.ToString());
                    var clone = (JObject)(ncObj.ToObject<JObject>()).DeepClone();
                    clone.Add("key", Guid.NewGuid().ToString());
                    var nestedContentAlias = ncObj["ncContentTypeAlias"].ToString();
                    foreach (var prop in ncObj)
                    {
                        if (ncProps.Contains(prop.Key))
                            continue;
                        var contentType = ctx.ContentTypeService.GetContentType(nestedContentAlias);
                        var umbProperty = contentType.PropertyTypes.FirstOrDefault(p =>
                            p.Alias.Equals(prop.Key, StringComparison.InvariantCultureIgnoreCase));
                        if (umbProperty != null && _definitions.ContainsKey(umbProperty.DataTypeDefinitionId))
                        {
                            //convert value to new value....
                            if (umbProperty.PropertyEditorAlias.Contains("NestedContent"))
                            {
                                clone[prop.Key] = ConvertInnerNestedContent(ctx, prop.Value);
                            }
                            
                            else if (IsRJPPicker(umbProperty.PropertyEditorAlias))
                            {
                                var strValue = prop.Value.ToString();
                                if (strValue.IsNullOrWhiteSpace())
                                    continue;
                                var innerArray = JsonConvert.DeserializeObject<JArray>(strValue);
                                var newRjpList = new List<JObject>();
                                foreach (var item in innerArray)
                                {
                                    var transform = new RJPMultiUrlPickerMigrator.RJPMultiUrlPickerTransform();
                                    var temp = JsonConvert.DeserializeObject<JObject>(transform.Map(ctx, item.ToString()).ToString());
                                    newRjpList.Add(temp);
                                }
                                var newArray = JsonConvert.DeserializeObject<JArray>(JsonConvert.SerializeObject(newRjpList));
                                
                                clone[prop.Key] = newArray;
                            }
                            else if (IsNuPicker(umbProperty.PropertyEditorAlias))
                            {
                                //var transform = new NuPickerTransform();
                               // var newValue = JToken.FromObject(transform.Map(ctx, prop.Value.ToString()));
                                //clone[prop.Key] = newValue;
                            }
                            else
                            {
                                var transform = GetTransform(umbProperty.PropertyEditorAlias);
                                if (transform != null)
                                {
                                    clone[prop.Key] = transform.Map(ctx, prop.Value.ToString()).ToString();
                                }
                                else
                                {
                                    var picker = umbProperty.PropertyEditorAlias;
                                    Console.WriteLine(picker);
                                }
                                
                            }

                        }
                    }

                    listOfNc.Add(clone);
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return JsonConvert.SerializeObject(listOfNc);
        }

        private string GetUdiEntityType(string contentType)
        {
            switch (contentType.ToLowerInvariant())
            {
                case "file":
                case "image":
                    return Constants.UdiEntityType.Media;
                default:
                    return Constants.UdiEntityType.Document;
            }
        }

        private IPropertyTransform GetTransform(string type)
        {
            switch (type)
            {
                case "Umbraco.MediaPicker":
                case "Umbraco.MediaPicker2":
                    return new IdToUdiTransform(ContentBaseType.Media, true);
                case "Umbraco.MultiNodeTreePicker":
                case "Umbraco.MultiNodeTreePicker2":
                    return new IdToUdiTransform(ContentBaseType.Document, true);
                case "Umbraco.MultipleMediaPicker":
                case "Umbraco.MultipleMediaPicker2":
                    return new IdToUdiTransform(ContentBaseType.Media, true);
                case "Umbraco.RelatedLinks":
                case "Umbraco.RelatedLinks2":
                    return new RelatedLinksMigrator.RelatedLinkTransform();
            }

            return null;
        }

        private bool IsRJPPicker(string type)
        {
            return type.Contains("MultiUrlPicker");
        }

        private bool IsNuPicker(string type)
        {
            return type.Contains("NuPicker") || type.Contains("Umbraco.Community.Contentment.DataList");
        }

        


    }



}
