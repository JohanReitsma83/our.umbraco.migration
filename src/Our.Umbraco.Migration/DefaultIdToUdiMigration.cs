using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Our.Umbraco.Migration
{
    [Migration("1.0.0", 1, "ProWorks.Default.IdToUdi")]
    public class DefaultIdToUdiMigration : IdToUdiMigration
    {
        public DefaultIdToUdiMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
        }
    }
}
