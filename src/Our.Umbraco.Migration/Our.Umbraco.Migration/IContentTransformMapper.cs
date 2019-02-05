using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public interface IContentTransformMapper
    {
        IContentBaseSource Source { get; }
        object RetrievePreChangeState(ServiceContext ctx, IContentBase content);
        IReadOnlyDictionary<string, ICollection<IPropertyMigration>> FieldMappers { get; }
        void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState);
    }
}
