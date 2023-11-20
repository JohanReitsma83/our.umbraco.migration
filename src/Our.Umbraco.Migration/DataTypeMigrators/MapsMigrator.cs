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
    public class MapsMigrator : IDataTypeMigrator
    {
        public bool NeedsMigration(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues)
        {
            return true;
        }

        public string GetNewPropertyEditorAlias(IDataTypeDefinition dataType,
            IDictionary<string, PreValue> oldPreValues) => "Our.Umbraco.GMaps";
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
            return new MapsMigration();
        }



    }

    public class MapsMigration : IPropertyMigration
    {
        public IPropertyTransform Upgrader => new MapsTransform();

        public IPropertyTransform Downgrader { get; }
    }

    public class MapsTransform : IPropertyTransform
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
            if (!string.IsNullOrWhiteSpace(from.ToString()))
            {
                var coordinates = from.ToString().Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (coordinates.Length == 3)
                {
                    var lat = coordinates[0];
                    var lng = coordinates[1];

                    var mapsDto = new MapsDto()
                    {
                        Address = new Address()
                        {
                            Coordinates = new Coordinates()
                            {
                                Lat = lat,
                                Lng = lng
                            }
                        },
                        Mapconfig = new Mapconfig()
                        {
                            CenterCoordinates = new Coordinates()
                            {
                                Lng = lng,
                                Lat = lat
                            },
                            Maptype = "roadmap",
                            Zoom = 17
                        }
                    };

                    return MapsDto.ToJson(mapsDto);
                }
            }
            
            return from;
        }


    }

    public partial class MapsDto
    {
        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("mapconfig")]
        public Mapconfig Mapconfig { get; set; }

        public static string ToJson(MapsDto self) => JsonConvert.SerializeObject(self);
    }

    public partial class Address
    {
        [JsonProperty("coordinates")]
        public Coordinates Coordinates { get; set; }
    }

    public partial class Coordinates
    {
        [JsonProperty("lat")]
        public string Lat { get; set; }

        [JsonProperty("lng")]
        public string Lng { get; set; }
    }

    public partial class Mapconfig
    {
        [JsonProperty("zoom")]
        public long Zoom { get; set; }

        [JsonProperty("maptype")]
        public string Maptype { get; set; }

        [JsonProperty("centerCoordinates")]
        public Coordinates CenterCoordinates { get; set; }
    }



}
