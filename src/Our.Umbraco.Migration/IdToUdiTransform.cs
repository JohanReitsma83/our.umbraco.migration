using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class IdToUdiTransform : IPropertyTransform
    {
        private static readonly Dictionary<ContentBaseType, Dictionary<int, string>> KnownIds = new Dictionary<ContentBaseType, Dictionary<int, string>>();

        private readonly Dictionary<int, string> _knownIds;
        private readonly string _typeName;

        public IdToUdiTransform(ContentBaseType type, bool retainInvalidData)
        {
            Type = type;
            _typeName = type.ToString().ToLowerInvariant();
            _knownIds = !KnownIds.TryGetValue(type, out var val) ? KnownIds[type] = new Dictionary<int, string>() : val;
            RetainInvalidData = retainInvalidData;
        }

        public ContentBaseType Type { get; }
        public bool RetainInvalidData { get; }

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

            var udis = ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(id => MapToUdi(ctx, id)).Where(i => i != null);
            var newIds = string.Join(",", udis);

            return newIds;
        }

        private string MapToUdi(ServiceContext ctx, string idOrUdi)
        {
            if (!int.TryParse(idOrUdi, out var id))
            {
                if (Udi.TryParse(idOrUdi, out _)) return idOrUdi;
                return RetainInvalidData ? idOrUdi : null;
            }
            if (_knownIds.TryGetValue(id, out var udi)) return udi;

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

            _knownIds[id] = udi;

            return udi;
        }
    }
}
