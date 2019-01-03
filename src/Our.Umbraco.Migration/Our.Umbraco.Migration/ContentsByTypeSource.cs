using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;

namespace Our.Umbraco.Migration
{
    public class ContentsByTypeSource : IContentBaseSource
    {
        public ContentsByTypeSource(string contentTypeAlias) : this(ContentBaseType.Document, contentTypeAlias)
        {
        }

        public ContentsByTypeSource(ContentBaseType type, string alias)
        {
            SourceType = type;
            SourceName = alias;
        }

        public string SourceName { get; }
        public ContentBaseType SourceType { get; }

        public IEnumerable<IContentBase> GetContents(ILogger logger, ServiceContext ctx)
        {
            if (SourceName != null)
            {
                switch (SourceType)
                {
                    case ContentBaseType.Document:
                        var ctype = ctx.ContentTypeService.GetContentType(SourceName);
                        if (ctype != null)
                        {
                            var childTypes = ctx.ContentTypeService.GetContentTypeChildren(ctype.Id);
                            return ctx.ContentService.GetContentOfContentType(ctype.Id).Union(childTypes.SelectMany(child => ctx.ContentService.GetContentOfContentType(child.Id)));
                        }
                        break;
                    case ContentBaseType.Media:
                        var mtype = ctx.ContentTypeService.GetMediaType(SourceName);
                        if (mtype != null)
                        {
                            var childTypes = ctx.ContentTypeService.GetMediaTypeChildren(mtype.Id);
                            return ctx.MediaService.GetMediaOfMediaType(mtype.Id).Union(childTypes.SelectMany(child => ctx.MediaService.GetMediaOfMediaType(child.Id)));
                        }
                        break;
                    case ContentBaseType.Member:
                        var btype = ctx.MemberTypeService.Get(SourceName);
                        if (btype != null)
                        {
                            return ctx.MemberService.GetMembersByMemberType(btype.Id);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            logger.Warn<ContentsByTypeSource>($"Could not find {SourceType} type with alias {SourceName}");
            return new IContentBase[0];
        }
    }
}
