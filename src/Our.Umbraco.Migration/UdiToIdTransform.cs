using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class UdiToIdTransform : IPropertyTransform
    {
        private static readonly Dictionary<string, string> KnownUdis = new Dictionary<string, string>();

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
            if (!(from is string udis)) return from;

            var ids = udis.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(udi => MapToId(ctx, udi) ?? udi);
            var newIds = string.Join(",", ids);

            return newIds;
        }

        private string MapToId(ServiceContext ctx, string udi)
        {
            if (KnownUdis.TryGetValue(udi, out var id)) return id;
            if (!Uri.TryCreate(udi, UriKind.Absolute, out var u) || string.IsNullOrWhiteSpace(u.Host) || !Guid.TryParse(u.AbsolutePath.TrimStart('/'), out var g)) return null;

            IContentBase node = null;
            switch (u.Host)
            {
                case "document":
                    node = ctx.ContentService.GetById(g);
                    break;
                case "media":
                    node = ctx.MediaService.GetById(g);
                    break;
                case "member":
                    node = ctx.MemberService.GetByKey(g);
                    break;
            }

            id = node?.Id.ToString();

            KnownUdis[udi] = id;

            return id;
        }
    }
}
