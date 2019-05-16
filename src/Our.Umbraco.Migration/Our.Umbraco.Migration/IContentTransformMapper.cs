using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public interface IContentTransformMapper
    {
        IContentBaseSource Source { get; }
        object RetrievePreChangeState(ServiceContext ctx, IContentBase content);
        ICollection<IFieldMapper> FieldMappers { get; }
        void SaveChanges(ServiceContext ctx, IContentBase content, object preChangeState);
    }

    public interface IFieldMapper
    {
        string FieldName { get; }
        string Type { get; }
        ICollection<IPropertyMigration> Migrations { get; }
    }

    public class FieldMapper : IFieldMapper
    {
        public FieldMapper(string fieldName, string type, ICollection<IPropertyMigration> migrations)
        {
            FieldName = fieldName;
            Type = type;
            Migrations = migrations;
        }

        public string FieldName { get; }
        public string Type { get; }
        public ICollection<IPropertyMigration> Migrations { get; }
    }
}
