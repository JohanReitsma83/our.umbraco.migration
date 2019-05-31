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
        private static readonly Dictionary<ContentBaseType, Dictionary<int, (string, IContentBase)>> KnownIds = new Dictionary<ContentBaseType, Dictionary<int, (string, IContentBase)>>();

        private readonly Dictionary<int, (string, IContentBase)> _knownIds;
        private readonly string _typeName;

        public IdToUdiTransform(ContentBaseType type, bool retainInvalidData)
        {
            Type = type;
            _typeName = type.ToString().ToLowerInvariant();
            _knownIds = !KnownIds.TryGetValue(type, out var val) ? KnownIds[type] = new Dictionary<int, (string, IContentBase)>() : val;
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

        private string MapToUdi(ServiceContext ctx, string idOrUdi) => MapToUdi(ctx, idOrUdi, Type, RetainInvalidData, out _, _typeName, _knownIds);

        internal static string MapToUdi(ServiceContext ctx, string idOrUdi, ContentBaseType type, bool retainInvalidData, out IContentBase node, string typeName = null, Dictionary<int, (string udi, IContentBase node)> knownIds = null)
        {
            node = null;
            if (typeName == null) typeName = type.ToString().ToLowerInvariant();
            if (knownIds == null) knownIds = !KnownIds.TryGetValue(type, out var val) ? KnownIds[type] = new Dictionary<int, (string, IContentBase)>() : val;

            if (!int.TryParse(idOrUdi, out var id))
            {
                if (Udi.TryParse(idOrUdi, out _)) return idOrUdi;
                return retainInvalidData ? idOrUdi : null;
            }
            if (knownIds.TryGetValue(id, out var element))
            {
                node = element.node;
                return element.udi;
            }

            element = (null, null);
            switch (type)
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
            if (guid != null) element = ($"umb://{typeName}/{guid}", node);

            knownIds[id] = element;

            return element.udi;
        }
    }
}
