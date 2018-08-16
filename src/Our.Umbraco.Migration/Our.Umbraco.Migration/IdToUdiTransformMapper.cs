using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class IdToUdiTransformMapper : ContentTransformMapper
    {
        private readonly IContentBaseSource _source;
        private readonly IReadOnlyDictionary<string, MigrationMapper> _fieldMappers;
        private readonly Dictionary<ContentBaseType, Dictionary<int, string>> _knownIds = new Dictionary<ContentBaseType, Dictionary<int, string>>();
        private readonly Dictionary<string, string> _knownUdis = new Dictionary<string, string>();

        public IdToUdiTransformMapper(string contentTypeAlias, IDictionary<string, ContentBaseType> fields)
        {
            _source = new ContentsByTypeSource(contentTypeAlias);
            var map = new Dictionary<string, MigrationMapper>(fields.Count);
            foreach (var pair in fields)
            {
                if (!_knownIds.TryGetValue(pair.Value, out var known)) _knownIds[pair.Value] = known = new Dictionary<int, string>();
                map[pair.Key] = new MigrationMapper
                {
                    Upgrader = new IdToUdiMapper {Type = pair.Value, KnownIds = known},
                    Downgrader = new UdiToIdMapper {KnownUdis = _knownUdis}
                };
            }

            _fieldMappers = map;
        }

        public override IContentBaseSource Source => _source;
        public override IReadOnlyDictionary<string, MigrationMapper> FieldMappers => _fieldMappers;
    }
}
