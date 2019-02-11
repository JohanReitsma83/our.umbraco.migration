using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Migration.DataTypeMigrators;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.GridAliasMigrators
{
    public interface IGridAliasMigrator
    {
        IEnumerable<IJsonPropertyTransform<JToken>> GetJsonPropertyTransforms(string alias);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class GridAliasMigratorAttribute : Attribute
    {
        public string[] GridControlAliases { get; }

        public GridAliasMigratorAttribute(params string[] gridControlAliases)
        {
            GridControlAliases = gridControlAliases;
        }
    }
}
