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
            if (preChangeState is bool b && b) ctx.ContentService.SaveAndPublishWithStatus(content as IContent);
            else ctx.ContentService.Save(content as IContent);
        }
    }
}
