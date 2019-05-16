using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
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
                            var allTypeIds = GetIdAndDescendentIds(ctype, ctx.ContentTypeService.GetAllContentTypes());
                            return allTypeIds.SelectMany(id => ctx.ContentService.GetContentOfContentType(id));
                        }
                        break;
                    case ContentBaseType.Media:
                        var mtype = ctx.ContentTypeService.GetMediaType(SourceName);
                        if (mtype != null)
                        {
                            var allTypeIds = GetIdAndDescendentIds(mtype, ctx.ContentTypeService.GetAllMediaTypes());
                            return allTypeIds.SelectMany(id => ctx.MediaService.GetMediaOfMediaType(id));
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

        private static Dictionary<int, HashSet<int>> GetRelationships(IEnumerable<IContentTypeComposition> allCompositions)
        {
            var relations = new Dictionary<int, HashSet<int>>();

            foreach (var composition in allCompositions)
            {
                var id = composition.Id;
                var parentId = composition.ParentId;
                var compIds = composition.CompositionIds();

                if (parentId > 0) AddRelation(relations, parentId, id);
                if (compIds != null)
                {
                    foreach (var compId in compIds)
                    {
                        AddRelation(relations, compId, id);
                    }
                }
            }

            return relations;
        }

        private static void AddRelation(IDictionary<int, HashSet<int>> relations, int parentId, int childId)
        {
            if (!relations.TryGetValue(parentId, out var list)) list = relations[parentId] = new HashSet<int>();
            list.Add(childId);
        }

        private static IEnumerable<int> GetIdAndDescendentIds(IEntity parent, IEnumerable<IContentTypeComposition> allCompositions)
        {
            var relations = GetRelationships(allCompositions);
            return GetIdAndDescendentIds(parent.Id, relations);
        }

        private static IEnumerable<int> GetIdAndDescendentIds(int parentId, IReadOnlyDictionary<int, HashSet<int>> relations)
        {
            yield return parentId;

            if (!relations.TryGetValue(parentId, out var set)) yield break;

            foreach (var childId in set)
            {
                foreach (var descendent in GetIdAndDescendentIds(childId, relations))
                {
                    yield return descendent;
                }
            }
        }
    }
}
