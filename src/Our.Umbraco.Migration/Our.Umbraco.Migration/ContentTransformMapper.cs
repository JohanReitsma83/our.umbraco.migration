using System;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class ContentTransformMapper : ContentBaseTransformMapper
    {
        public override object RetrievePreChangeState(ServiceContext ctx, IContentBase content)
        {
            return (content as IContent)?.Published;
        }

        public override void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState)
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
