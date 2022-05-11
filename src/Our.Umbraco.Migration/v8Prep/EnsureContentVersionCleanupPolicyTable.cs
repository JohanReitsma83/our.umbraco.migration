using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Our.Umbraco.Migration.v8Prep
{
    public class EnsureContentVersionCleanupPolicyTable : MigrationBase
    {
        /// <summary>
        /// Resolves probley upgrading directly from Umbraoc 7 to Umbraco 8.18.x.
        /// The umbracoContentVersionCleanupPolicy needs to be created in advance for the upgrade wizard to complete
        /// </summary>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        public EnsureContentVersionCleanupPolicyTable(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
        }

        public override void Up()
        {
            var tables = SqlSyntax.GetTablesInSchema(Context.Database).ToArray();
            if (tables.InvariantContains("umbracoContentVersionCleanupPolicy"))
            {
                return;
            }

            Create.Table<UmbracoContentVersionCleanupPolicy>();
            //Create.ForeignKey()
            //    .FromTable("umbracoContentVersionCleanupPolicy")
            //    .ForeignColumn("contentTypeId")
            //    .ToTable("cmsContentType")
            //    .PrimaryColumn("nodeId");
        }

        public override void Down()
        {
            throw new NotImplementedException();
        }

        [TableName("umbracoContentVersionCleanupPolicy")]
        [ExplicitColumns]
        internal class UmbracoContentVersionCleanupPolicy
        {
            [Column("contentTypeId")]
            public int ContentTypeId { get; set; }

            [Column("preventCleanup")]
            public bool BlogPostUmbracoId { get; set; }

            [Column("keepAllVersionsNewerThanDays")]
            public int? KeepAllVersionsNewerThanDays { get; set; }

            [Column("keepLatestVersionPerDayForDays")]
            public int? KeepLatestVersionPerDayForDays { get; set; }

            [Column("updated")]
            public DateTime Updated { get; set; }
        }
    }
}
