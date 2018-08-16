using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class ContentsByTypeSource : IContentBaseSource
    {
        public ContentsByTypeSource(string contentTypeAlias)
        {
            SourceName = contentTypeAlias;
        }

        public string SourceName { get; }
        public string SourceType => "content";

        public IEnumerable<IContentBase> GetContents(ILogger logger, ServiceContext ctx)
        {
            if (SourceName != null)
            {
                var type = ctx.ContentTypeService.GetContentType(SourceName);
                if (type != null)
                {
                    var childTypes = ctx.ContentTypeService.GetContentTypeChildren(type.Id);
                    return ctx.ContentService.GetContentOfContentType(type.Id).Union(childTypes.SelectMany(child => ctx.ContentService.GetContentOfContentType(child.Id)));
                }
            }

            logger.Warn<ContentsByTypeSource>($"Could not find document type with alias {SourceName}");
            return new IContentBase[0];
        }
    }
}
