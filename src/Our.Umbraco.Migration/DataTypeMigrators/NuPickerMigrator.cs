using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;


namespace Our.Umbraco.Migration.DataTypeMigrators
{
    /// <summary>
    /// nuPickers.DotNetCheckBoxPicker
    /// nuPickers.DotNetDropDownPicker
    /// nuPickers.EnumDropDownPicker
    /// </summary>
    [DataTypeMigrator("nuPickers.EnumDropDownPicker")]
    public class EnumNuPickerMigration : NuPickerMigrator
    {

        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return "nuPickers.EnumDropDownPicker"; //"Umbraco.Community.Contentment.DataList";
        }
    }

    /// <summary>
    /// nuPickers.DotNetCheckBoxPicker
    /// nuPickers.DotNetDropDownPicker
    /// nuPickers.EnumDropDownPicker
    /// </summary>
    [DataTypeMigrator("nuPickers.DotNetCheckBoxPicker")]
    public class CheckboxNuPickerMigration : NuPickerMigrator
    {

        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return "nuPickers.DotNetCheckBoxPicker"; //return "Umbraco.Community.Contentment.DataList";
        }
    }

    [DataTypeMigrator("nuPickers.DotNetDropDownPicker")]
    public class DropdownNuPickerMigration : NuPickerMigrator
    {

        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return "nuPickers.DotNetDropDownPicker";//return "Umbraco.Community.Contentment.DataList";
        }
    }

    
    public abstract class NuPickerMigrator : IDataTypeMigrator
    {
        public bool NeedsMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return true;
        }

        public abstract string GetNewPropertyEditorAlias(IDataTypeDefinition dataType,
            IDictionary<string, PreValue> oldPreValues);
        public DataTypeDatabaseType GetNewDatabaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return dataType.DatabaseType;
        }

        public IDictionary<string, PreValue> GetNewPreValues(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return oldPreValues;
        }

        public IPropertyMigration GetPropertyMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues, bool retainInvalidData)
        {
            return new NupickerMigration();
        }



    }

    public class NupickerMigration : IPropertyMigration
    {
        public NupickerMigration()
        {
            
        }

        public IPropertyTransform Upgrader => new NuPickerTransform();

        public IPropertyTransform Downgrader { get; }
    }

    public class NuPickerTransform : IPropertyTransform
    {
        public bool TryGet(IContentBase content, string field, out object value)
        {
            value = content.GetValue(field);
            return true;
        }

        public void Set(IContentBase content, string field, object value)
        {
            content.SetValue(field, value);   
        }

        public object Map(ServiceContext ctx, object from)
        {
            if (from != null && !string.IsNullOrWhiteSpace(from.ToString()))
            {
                var dto = NuPickerDto.FromJson(from.ToString());
                if (dto.Any())
                {
                    if (dto.Count > 1)
                    {
                        return $"[{string.Join(",", dto.Select(d => d.Key))}]";
                    }

                    if (dto.Count == 1)
                    {
                        return dto.First().Key;
                    }
                }
            }

            return from;
        }


    }

    public class NuPickerDto
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        public static List<NuPickerDto> FromJson(string json) => JsonConvert.DeserializeObject<List<NuPickerDto>>(json);
    }



}
