using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class ContentTransformMapper : IContentTransformMapper
    {
        public ContentTransformMapper(IContentBaseSource source, IReadOnlyDictionary<string, IEnumerable<IPropertyMigration>> fieldMappers)
        {
            Source = source;
            FieldMappers = fieldMappers?.ToDictionary(p => p.Key, p => (ICollection<IPropertyMigration>)p.Value?.ToList() ?? new List<IPropertyMigration>());
        }

        public IContentBaseSource Source { get; }

        public IReadOnlyDictionary<string, ICollection<IPropertyMigration>> FieldMappers { get; }

        public object RetrievePreChangeState(ServiceContext ctx, IContentBase content)
        {
            return (content as IContent)?.Published;
        }

        public void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState)
        {
            switch (Source.SourceType)
            {
                case ContentBaseType.Document:
                    if (preChangeState is bool b && b) ctx.ContentService.SaveAndPublishWithStatus(content as IContent);
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
