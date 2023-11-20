using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public class ContentTransformMapper : IContentTransformMapper
    {
        public ContentTransformMapper(IContentBaseSource source, IEnumerable<IFieldMapper> fieldMappers, bool raiseSaveAndPublishEvents)
        {
            Source = source;
            FieldMappers = fieldMappers?.ToList() ?? new List<IFieldMapper>();
            RaiseSaveAndPublishEvents = raiseSaveAndPublishEvents;
        }

        public IContentBaseSource Source { get; }

        public bool RaiseSaveAndPublishEvents { get; }

        public ICollection<IFieldMapper> FieldMappers { get; }

        public object RetrievePreChangeState(ServiceContext ctx, IContentBase content)
        {
            if (!(content is IContent c)) return false;
            var isPublished = (c.HasPublishedVersion || c.Published) && !c.DeletedDate.HasValue && (!c.ExpireDate.HasValue || c.ExpireDate.Value > DateTime.Now);
            return isPublished;
        }

        public void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState)
        {
            switch (Source.SourceType)
            {
                case ContentBaseType.Document:
                    if (preChangeState is bool b && b)
                    {
                        var result = ctx.ContentService.SaveAndPublishWithStatus(content as IContent, 0, RaiseSaveAndPublishEvents);
                        if (result.Success) return;
                        if (result.Exception != null) throw result.Exception;

                        ctx.ContentService.Save(content as IContent, 0, RaiseSaveAndPublishEvents);
                        throw new Exception($"Error publishing the document '{content.Name}' (#{content.Id}), {ResultMessage(result.Result)}");
                    }
                    else ctx.ContentService.Save(content as IContent, 0, RaiseSaveAndPublishEvents);
                    break;
                case ContentBaseType.Media:
                    ctx.MediaService.Save(content as IMedia, 0, RaiseSaveAndPublishEvents);
                    break;
                case ContentBaseType.Member:
                    ctx.MemberService.Save(content as IMember, RaiseSaveAndPublishEvents);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string ResultMessage(PublishStatus result)
        {
            if (result == null) return string.Empty;

            var sb = new StringBuilder(result.StatusType.ToString());

            var invalids = result.InvalidProperties?.Select(p => p.Alias).ToArray();
            if (invalids != null)
            {
                sb.Append(" - InvalidProperties: ");
                sb.Append(string.Join(", ", invalids));
            }

            if (result.EventMessages != null && result.EventMessages.Count > 0)
            {
                sb.AppendLine(" - Event Messages:");
                foreach (var m in result.EventMessages.GetAll())
                {
                    sb.Append('[');
                    sb.Append(m.Category);
                    sb.Append("] - ");
                    sb.Append(m.MessageType);
                    sb.Append(" - ");
                    sb.AppendLine(m.Message);
                }
            }

            return sb.ToString();
        }
    }
}
