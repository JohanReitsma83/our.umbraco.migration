using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
    public abstract class JsonContentMigrator : IDataTypeMigrator
    {
        public virtual bool NeedsMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            var transforms = GetJsonPropertyTransforms(dataType, oldPreValues);
            return transforms != null && transforms.Any();
        }

        public virtual DataTypeDatabaseType GetNewDatabaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => dataType.DatabaseType;
        public virtual IDictionary<string, PreValue> GetNewPreValues(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => oldPreValues;
        public virtual string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => dataType.PropertyEditorAlias;

        public virtual IPropertyMigration GetPropertyMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            var transforms = GetJsonPropertyTransforms(dataType, oldPreValues)?.ToList();
            return transforms != null && transforms.Count > 0 ? new JsonMigration(transforms) : null;
        }

        protected abstract IEnumerable<JsonPropertyTransform> GetJsonPropertyTransforms(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues);
    }

    public class JsonMigration : IPropertyMigration
    {
        public JsonMigration(List<JsonPropertyTransform> transforms)
        {
            var trs = transforms;

            Upgrader = new JsonTransform { Transforms = trs, Upgrading = true };
            Downgrader = new JsonTransform { Transforms = trs, Upgrading = false };
        }

        public IPropertyTransform Upgrader { get; }

        public IPropertyTransform Downgrader { get; }
    }

    public class JsonTransform : IPropertyTransform
    {
        public bool Upgrading { get; set; }
        public List<JsonPropertyTransform> Transforms { get; set; }

        public bool TryGet(IContentBase content, string field, out object value)
        {
            value = null;

            if (content == null || !content.HasProperty(field)) return false;

            value = content.GetValue<string>(field);
            return value != null;
        }

        public object Map(ServiceContext ctx, object from)
        {
            var val = from is string v ? v : from?.ToString();
            if (string.IsNullOrWhiteSpace(val)) return from;

            var token = JsonConvert.DeserializeObject(val);
            var changed = false;

            Transforms.ForEach(t =>
            {
                foreach (var valueAndSetter in t.PropertyValuesAndSetters.Invoke(token))
                {
                    var propertyValue = valueAndSetter.Item1;
                    if (propertyValue == null) continue;

                    var tr = Upgrading ? t.Migration.Upgrader : t.Migration.Downgrader;
                    var vc = new VirtualContent(propertyValue);
                    if (!tr.TryGet(vc, VirtualContent.ValuePropertyName, out var fr)) continue;

                    var to = tr.Map(ctx, fr);
                    if ((fr == null && to == null) || (fr != null && fr.Equals(to)) || (to != null && to.Equals(fr))) continue;

                    tr.Set(vc, VirtualContent.ValuePropertyName, to);
                    changed = true;

                    valueAndSetter.Item2.Invoke(token, vc.Value?.ToString());
                }
            });

            return changed ? JsonConvert.SerializeObject(token) : from;
        }

        public void Set(IContentBase content, string field, object value)
        {
            content?.SetValue(field, value);
        }

        private class VirtualContent : IContentBase
        {
            public const string ValuePropertyName = "Value";

            public object Value { get; private set; }
            private bool _valueChanged;

            public VirtualContent(object value)
            {
                Value = value;
            }

            public object DeepClone()
            {
                return new VirtualContent(Value);
            }

            public int Id { get; set; }
            public Guid Key { get; set; }
            public DateTime CreateDate { get; set; }
            public DateTime UpdateDate { get; set; }
            public bool HasIdentity => false;
            public DateTime? DeletedDate { get; set; }
            public bool IsDirty() => false;

            public bool IsPropertyDirty(string propName) => _valueChanged;

            public void ResetDirtyProperties()
            {
            }

            public bool WasDirty() => false;

            public bool WasPropertyDirty(string propertyName) => false;

            public void ForgetPreviouslyDirtyProperties()
            {
            }

            public void ResetDirtyProperties(bool rememberPreviouslyChangedProperties)
            {
            }

            public int CreatorId { get; set; }
            public int Level { get; set; }
            public string Name { get; set; }
            public int ParentId { get; set; }
            public string Path { get; set; }
            public int SortOrder { get; set; }
            public bool Trashed { get; }
            public IDictionary<string, object> AdditionalData { get; }
            public bool HasProperty(string propertyTypeAlias)
            {
                return ValuePropertyName.Equals(propertyTypeAlias, StringComparison.InvariantCultureIgnoreCase);
            }

            public object GetValue(string propertyTypeAlias)
            {
                if (!HasProperty(propertyTypeAlias)) throw new NotImplementedException($"The property '{propertyTypeAlias}' is not a valid property for this type");
                return Value;
            }

            public TPassType GetValue<TPassType>(string propertyTypeAlias)
            {
                if (!HasProperty(propertyTypeAlias)) throw new NotImplementedException($"The property '{propertyTypeAlias}' is not a valid property for this type");
                if (Value == null) return default(TPassType);
                if (Value is TPassType pt) return pt;
                var at = Value.TryConvertTo<TPassType>();
                return at.Success ? at.Result : default(TPassType);
            }

            public void SetValue(string propertyTypeAlias, object value)
            {
                if (!HasProperty(propertyTypeAlias)) throw new NotImplementedException($"The property '{propertyTypeAlias}' is not a valid property for this type");

                var val = value?.ToString();
                var curVal = Value?.ToString();
                if (val == curVal) return;

                _valueChanged = true;
                Value = val;
            }

            public bool IsValid() => true;

            public void ChangeTrashedState(bool isTrashed, int parentId = -20)
            {
            }

            public int ContentTypeId { get; }
            public Guid Version { get; }
            public PropertyCollection Properties { get; set; }
            public IEnumerable<PropertyGroup> PropertyGroups { get; }
            public IEnumerable<PropertyType> PropertyTypes { get; }
        }
    }

    public class JsonPropertyTransform
    {
        public Func<object, IEnumerable<Tuple<string, Action<object, string>>>> PropertyValuesAndSetters { get; set; }
        public IPropertyMigration Migration { get; set; }
    }
}
