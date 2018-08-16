using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public abstract class ContentBaseTransformMapper
    {
        public virtual IContentBaseSource Source { get; set; }
        public abstract object RetrievePreChangeState(ServiceContext ctx, IContentBase content);
        public virtual IReadOnlyDictionary<string, MigrationMapper> FieldMappers { get; set; }
        public abstract void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState);
    }
}
