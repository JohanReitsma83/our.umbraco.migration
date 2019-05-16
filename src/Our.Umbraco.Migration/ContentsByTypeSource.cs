using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Entities;
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
                        var ctype = ctx.ContentTypeService.Get(SourceName);
                        if (ctype != null)
                        {
                            var allTypeIds = GetIdAndDescendentIds(ctype, ctx.ContentTypeService.GetAll());
                            return GetContentOfType(allTypeIds, (ids, page, size) => { var r = ctx.ContentService.GetPagedOfTypes(ids, page, size, out var total, null); return (total, r); });
                        }
                        break;
                    case ContentBaseType.Media:
                        var mtype = ctx.ContentTypeService.Get(SourceName);
                        if (mtype != null)
                        {
                            var allTypeIds = GetIdAndDescendentIds(mtype, ctx.MediaTypeService.GetAll());
                            return GetContentOfType(allTypeIds, (ids, page, size) => { var r = ctx.MediaService.GetPagedOfTypes(ids, page, size, out var total, null); return (total, r); });
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

        private static IEnumerable<IContentBase> GetContentOfType(IEnumerable<int> ids, Func<int[], long, int, (long, IEnumerable<IContentBase>)> getPagedOfTypes)
        {
            long total;
            var idx = 0;
            var arr = ids.ToArray();

            do
            {
                var results = getPagedOfTypes(arr, idx / 100, 100);
                total = results.Item1;
                foreach (var result in results.Item2)
                {
                    idx++;
                    yield return result;
                }
            }
            while (idx < total);
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
