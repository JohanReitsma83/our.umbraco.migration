using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class IdToUdiMapper : ITransformMapper
    {
        private ContentBaseType _type;
        private string _typeName;

        public ContentBaseType Type
        {
            get => _type;
            set
            {
                _type = value;
                _typeName = value.ToString().ToLowerInvariant();
            }
        }
        public IDictionary<int, string> KnownIds { get; set; } = new Dictionary<int, string>();

        public bool TryGet(IContentBase content, string field, out object value)
        {
            value = null;

            if (content == null || !content.HasProperty(field)) return false;

            value = content.GetValue<string>(field);
            return value != null;
        }

        public void Set(IContentBase content, string field, object value)
        {
            content?.SetValue(field, value);
        }

        public object Map(ServiceContext ctx, object from)
        {
            if (!(from is string ids)) return from;

            var udis = ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(id => (int.TryParse(id, out var i) ? MapToUdi(ctx, i) : null) ?? id);
            var newIds = string.Join(",", udis);

            return newIds;
        }

        private string MapToUdi(ServiceContext ctx, int id)
        {
            if (KnownIds.TryGetValue(id, out var udi)) return udi;

            IContentBase node = null;
            switch (Type)
            {
                case ContentBaseType.Document:
                    node = ctx.ContentService.GetById(id);
                    break;
                case ContentBaseType.Media:
                    node = ctx.MediaService.GetById(id);
                    break;
                case ContentBaseType.Member:
                    node = ctx.MemberService.GetById(id);
                    break;
            }

            var guid = node?.Key.ToString("N");
            if (guid != null) udi = $"umb://{_typeName}/{guid}";

            KnownIds[id] = udi;

            return udi;
        }
    }
}
