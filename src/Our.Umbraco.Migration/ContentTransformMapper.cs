using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class ContentTransformMapper : IContentTransformMapper
    {
        public ContentTransformMapper(IContentBaseSource source, IEnumerable<IFieldMapper> fieldMappers)
        {
            Source = source;
            FieldMappers = fieldMappers?.ToList() ?? new List<IFieldMapper>();
        }

        public IContentBaseSource Source { get; }

        public ICollection<IFieldMapper> FieldMappers { get; }

        public object RetrievePreChangeState(ServiceContext ctx, IContentBase content)
        {
            return (content as IContent)?.Published;
        }

        public void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState)
        {
            switch (Source.SourceType)
            {
                case ContentBaseType.Document:
                    if (preChangeState is bool b && b)
                    {
                        var result = ctx.ContentService.SaveAndPublishWithStatus(content as IContent);
                        if (result.Success) return;
                        if (result.Exception != null) throw result.Exception;
                        throw new Exception($"Could not save the document '{content.Name}' (#{content.Id})");
                    }
                    else ctx.ContentService.Save(content as IContent);
                    break;
                case ContentBaseType.Media:
                    ctx.MediaService.Save(content as IMedia);
                    break;
                case ContentBaseType.Member:
                    ctx.MemberService.Save(content as IMember);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
