using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Entities;
using Umbraco.Core.Services;
using Umbraco.Web.PropertyEditors;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
    public abstract class JsonContentMigrator<T> : IDataTypeMigrator where T : class 
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Dictionary<string, IPropertyMigration> KnownValidMigrators = new Dictionary<string, IPropertyMigration>(StringComparer.InvariantCultureIgnoreCase);

        public virtual bool NeedsMigration(IDataType dataType, object oldConfig)
        {
            var transforms = GetJsonPropertyTransforms(dataType, oldConfig, false);
            return transforms != null && transforms.Any();
        }

        public virtual ValueStorageType GetNewDatabaseType(IDataType dataType, object oldConfig) => dataType.DatabaseType;
        public virtual object GetNewConfiguration(IDataType dataType, object oldConfig) => oldConfig;
        public virtual string GetNewEditorAlias(IDataType dataType, object oldConfig) => dataType.EditorAlias;

        public virtual IPropertyMigration GetPropertyMigration(IDataType dataType, object oldConfig, bool retainInvalidData)
        {
            var transforms = GetJsonPropertyTransforms(dataType, oldConfig, retainInvalidData)?.ToList();
            return transforms != null && transforms.Count > 0 ? new JsonMigration<T>(transforms) : null;
        }

        protected abstract IEnumerable<IJsonPropertyTransform<T>> GetJsonPropertyTransforms(IDataType dataType, object oldConfig, bool retainInvalidData);

        protected virtual IPropertyMigration GetValidPropertyMigration(string dataTypeGuid, bool retainInvalidData)
        {
            if (dataTypeGuid == null) return null;
            if (KnownValidMigrators.TryGetValue(dataTypeGuid, out var migration)) return migration;

            if (!Guid.TryParse(dataTypeGuid, out var guid)) return KnownValidMigrators[dataTypeGuid] = null;

            var dts = Current.Services.DataTypeService;
            var dt = dts.GetDataType(guid);
            if (dt == null) return KnownValidMigrators[dataTypeGuid] = null;

            var pv = dt.ConfigurationAs<MultiNodePickerConfiguration>();
            var migrator = DataTypeMigratorFactory.Instance.CreateDataTypeMigrator(dt.EditorAlias);

            if (migrator != null && !migrator.NeedsMigration(dt, pv)) return KnownValidMigrators[dataTypeGuid] = null;

            return KnownValidMigrators[dataTypeGuid] = migrator?.GetPropertyMigration(dt, pv, retainInvalidData);
        }
    }

    public class JsonMigration<T> : IPropertyMigration where T : class
    {
        public JsonMigration(List<IJsonPropertyTransform<T>> transforms)
        {
            var trs = transforms;

            Upgrader = new JsonTransform<T> { Transforms = trs, Upgrading = true };
            Downgrader = new JsonTransform<T> { Transforms = trs, Upgrading = false };
        }

        public IPropertyTransform Upgrader { get; }

        public IPropertyTransform Downgrader { get; }
    }

    public class JsonTransform<T> : IPropertyTransform where T : class
    {
        public bool Upgrading { get; set; }
        public List<IJsonPropertyTransform<T>> Transforms { get; set; }

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

            var token = JsonConvert.DeserializeObject<T>(val);
            var changed = false;

            Transforms.ForEach(t =>
            {
                var valuesMigrationsAndSetters = t.GetPropertyValuesMigrationsAndSetters(token);
                if (valuesMigrationsAndSetters == null) return;

                foreach (var valueMigrationAndSetter in valuesMigrationsAndSetters)
                {
                    var propertyValue = valueMigrationAndSetter.Item1;
                    if (propertyValue == null) continue;

                    var tr = Upgrading ? valueMigrationAndSetter.Item2.Upgrader : valueMigrationAndSetter.Item2.Downgrader;
                    var vc = new VirtualContent(propertyValue);
                    if (!tr.TryGet(vc, VirtualContent.ValuePropertyName, out var fr)) continue;

                    var to = tr.Map(ctx, fr);
                    if ((fr == null && to == null) || (fr != null && fr.Equals(to)) || (to != null && to.Equals(fr))) continue;

                    tr.Set(vc, VirtualContent.ValuePropertyName, to);
                    changed = true;

                    valueMigrationAndSetter.Item3.Invoke(token, vc.Value?.ToString());
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

            public void SetCultureName(string value, string culture)
            {
                throw new NotImplementedException();
            }

            public string GetCultureName(string culture)
            {
                throw new NotImplementedException();
            }

            public bool IsCultureAvailable(string culture)
            {
                throw new NotImplementedException();
            }

            public DateTime? GetUpdateDate(string culture)
            {
                throw new NotImplementedException();
            }

            public object GetValue(string propertyTypeAlias, string culture = null, string segment = null, bool published = false)
            {
                throw new NotImplementedException();
            }

            public TValue GetValue<TValue>(string propertyTypeAlias, string culture = null, string segment = null, bool published = false)
            {
                throw new NotImplementedException();
            }

            public void SetValue(string propertyTypeAlias, object value, string culture = null, string segment = null)
            {
                throw new NotImplementedException();
            }

            public void SetParent(ITreeEntity parent)
            {
                throw new NotImplementedException();
            }

            public void ResetWereDirtyProperties()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> GetWereDirtyProperties()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> GetDirtyProperties()
            {
                throw new NotImplementedException();
            }

            public int ContentTypeId { get; }
            public Guid Version { get; }
            public PropertyCollection Properties { get; set; }
            public IEnumerable<PropertyGroup> PropertyGroups { get; }
            public IEnumerable<PropertyType> PropertyTypes { get; }

            public ISimpleContentType ContentType => throw new NotImplementedException();

            public int WriterId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public int VersionId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public ContentCultureInfosCollection CultureInfos { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public IEnumerable<string> AvailableCultures => throw new NotImplementedException();

            public DateTime? DeleteDate { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }
    }

    public interface IJsonPropertyTransform<T> where T : class
    {
        IEnumerable<Tuple<string, IPropertyMigration, Action<T, string>>> GetPropertyValuesMigrationsAndSetters(T token);
    }

    public class JsonPropertyTransform<T> : IJsonPropertyTransform<T> where T : class
    {
        public virtual Func<T, IEnumerable<Tuple<string, Action<T, string>>>> PropertyValuesAndSetters { get; set; }
        public virtual IPropertyMigration Migration { get; set; }

        public IEnumerable<Tuple<string, IPropertyMigration, Action<T, string>>> GetPropertyValuesMigrationsAndSetters(T token)
        {
            var valuesAndSetters = PropertyValuesAndSetters?.Invoke(token);
            if (valuesAndSetters == null) yield break;

            foreach (var propertyValuesAndSetter in valuesAndSetters)
            {
                yield return new Tuple<string, IPropertyMigration, Action<T, string>>(propertyValuesAndSetter.Item1, Migration, propertyValuesAndSetter.Item2);
            }
        }
    }
}
