using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Our.Umbraco.Migration
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a helper class that performs migrations of IDs to UDIs in content nodes, by content type and field name.  The field values can be single integer IDs, or a comma-separated list of integers.
    /// </summary>
    public abstract class IdToUdiMigration : FieldTransformMigration
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new migration instance
        /// </summary>
        /// <param name="contentTypeFieldMappings">This dictionary has as its key the content type aliases to be mapped.  Each one is then mapped to a list of fields that should be migrated from an integer to a UDI for that content type.  The field values can be single integers, or a comma-separated list of integers</param>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        protected IdToUdiMigration(IDictionary<string, IDictionary<string, ContentBaseType>> contentTypeFieldMappings, ISqlSyntaxProvider sqlSyntax, ILogger logger)
            : base(contentTypeFieldMappings.Select(m => new IdToUdiTransformMapper(m.Key, m.Value)), sqlSyntax, logger)
        {
        }
    }
}
