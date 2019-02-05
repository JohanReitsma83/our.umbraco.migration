using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    /// <summary>
    /// A helper class to use as a base for field transformation migrations.  For simple ID to UDI migrations, extend the IdToUdiMigration class
    /// </summary>
    public abstract class FieldTransformMigration : MigrationBase
    {
        private readonly List<IContentTransformMapper> _mappings;
        private bool _hasMappings;

        /// <summary>
        /// Creates a new FieldTransformMigration instance
        /// </summary>
        /// <param name="mappings">All content base mappings that should be performed</param>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        protected FieldTransformMigration(IEnumerable<IContentTransformMapper> mappings, ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
            _mappings = new List<IContentTransformMapper>(mappings);
            _hasMappings = true;
        }

        /// <summary>
        /// Creates a new FieldTransformMigration instance, delaying the loading of mappings until later with a LoadMappings overload
        /// </summary>
        /// <param name="sqlSyntax"></param>
        /// <param name="logger"></param>
        protected FieldTransformMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
            _mappings = new List<IContentTransformMapper>();
        }

        public override void Up()
        {
            EnsureMappings();
            Transform(true);
        }

        public override void Down()
        {
            EnsureMappings();
            Transform(false);
        }

        private void EnsureMappings()
        {
            if (_hasMappings) return;

            var mappings = LoadMappings();
            if (mappings != null) _mappings.AddRange(mappings);
            _hasMappings = true;
        }

        /// <summary>
        /// When overridden by a derived class, provides the transform mappings to process at the time of migration, rather than loading them in the constructor.
        /// This is what should be used if the list is dynamic.  If the list is a static list, it should be passed to the constructor instead.
        /// </summary>
        /// <returns>The list of mappings to process</returns>
        protected virtual IEnumerable<IContentTransformMapper> LoadMappings()
        {
            throw new NotImplementedException("You must override the LoadMappings method if you do not pass in mappings to the constructor");
        }

        /// <summary>
        /// Performs all the transformations specified in the mappings parameter passed to the constructor
        /// </summary>
        /// <param name="upgrading">True if upgrading, false if downgrading</param>
        protected void Transform(bool upgrading)
        {
            var direction = upgrading ? "up" : "down";
            var counts = new Dictionary<string, Dictionary<string, Counts>>();

            try
            {
                var ctx = ApplicationContext.Current.Services;

                foreach (var mapping in _mappings)
                {
                    try
                    {
                        if (!counts.TryGetValue(mapping.Source.SourceName, out var typeCounts)) typeCounts = counts[mapping.Source.SourceName] = new Dictionary<string, Counts>(); 
                        foreach (var content in mapping.Source.GetContents(Logger, ctx))
                        {
                            RemapContent(upgrading, mapping, ctx, content, direction, typeCounts);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(GetType(), $"Could not {direction}grade content for {mapping?.Source?.SourceName}", e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(GetType(), $"Could not {direction}grade content", e);
            }
            finally
            {
                if (counts.Count > 0)
                {
                    var sb = new StringBuilder();
                    var maxDocType = new[] { 8 }.Union(counts.Keys.Select(k => k.Length)).Max();
                    var maxField = new[] { 5 }.Union(counts.Values.SelectMany(k => k.Keys.Select(v => v.Length))).Max();
                    var format = "{0,-" + maxDocType + "} {1,-" + maxField + "} {2,12} {3,12} {4,12} {5,12} {5,12}\r\n";

                    sb.AppendLine("Transformed the following:");
                    sb.AppendFormat(format, "Doc Type", "Field", "Success", "Unchanged", "Map Errors", "Save Errors", "Other Errors");
                    sb.AppendLine();
                    foreach (var dtPair in counts)
                    {
                        foreach (var fPair in dtPair.Value)
                        {
                            sb.AppendFormat(format, dtPair.Key, fPair.Key, fPair.Value.Success, fPair.Value.Unchanged, fPair.Value.MapError, fPair.Value.SaveError, fPair.Value.OtherError);
                        }
                    }

                    Logger.Info(GetType(), sb.ToString());
                } else Logger.Info(GetType(), "No data transformed");
            }
        }

        private void RemapContent(bool upgrading, IContentTransformMapper mapping, ServiceContext ctx, IContentBase content, string direction, Dictionary<string, Counts> counts)
        {
            var myCounts = new Dictionary<string, Counts>(mapping.FieldMappers.Count);

            try
            {
                var state = mapping.RetrievePreChangeState(ctx, content);
                var changed = false;

                foreach (var fieldMapper in mapping.FieldMappers)
                {
                    if (!myCounts.TryGetValue(fieldMapper.Key, out var count)) count = myCounts[fieldMapper.Key] = new Counts();

                    try
                    {
                        var fieldChanged = TryMap(ctx, content, fieldMapper.Key, fieldMapper.Value, upgrading);
                        changed |= fieldChanged;

                        if (fieldChanged)
                        {
                            count.Success++;
                        }
                        else
                        {
                            count.Unchanged++;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(GetType(), $"Could not {direction}grade {mapping.Source.SourceName} content #{content.Id} in field {fieldMapper.Key}", e);
                    }
                }

                if (!changed) return;

                try
                {
                    mapping.SaveChanges(ctx, content, state);
                }
                catch (Exception e)
                {
                    foreach (var count in myCounts.Values)
                    {
                        count.SaveError = count.Success;
                        count.Success = 0;
                    }
                    Logger.Error(GetType(), $"Could not {direction}grade {mapping.Source.SourceName} content #{content.Id} in saving", e);
                }
            }
            catch (Exception e)
            {
                foreach (var count in myCounts.Values)
                {
                    count.OtherError = count.Success;
                    count.Success = 0;
                }
                Logger.Error(GetType(), $"Could not {direction}grade {mapping.Source.SourceName} content #{content.Id}", e);
            }
            finally
            {
                foreach (var count in myCounts)
                {
                    if (!counts.TryGetValue(count.Key, out var fieldCounts)) counts[count.Key] = count.Value;
                    else
                    {
                        fieldCounts.MapError += count.Value.MapError;
                        fieldCounts.OtherError += count.Value.OtherError;
                        fieldCounts.SaveError += count.Value.SaveError;
                        fieldCounts.Success += count.Value.Success;
                        fieldCounts.Unchanged += count.Value.Unchanged;
                    }
                }
            }
        }

        private class Counts
        {
            public int Success { get; set; }
            public int OtherError { get; set; }
            public int MapError { get; set; }
            public int SaveError { get; set; }
            public int Unchanged { get; set; }
        }

        /// <summary>
        /// Tries to apply a transform mapping to a given field, returning true if the value was changed
        /// </summary>
        /// <param name="ctx">The current service context</param>
        /// <param name="content">The content node being modified</param>
        /// <param name="field">The field to change</param>
        /// <param name="mappers">The transform mappers that determines how to change the field, and whether or not its value needs to be changed</param>
        /// <param name="upgrading">Whether to use the Upgrader or the Downgrader</param>
        /// <returns>True if the value was present, mapped, and changed</returns>
        protected static bool TryMap(ServiceContext ctx, IContentBase content, string field, IEnumerable<IPropertyMigration> mappers, bool upgrading)
        {
            var changed = false;

            foreach (var mapper in mappers)
            {
                var transform = upgrading ? mapper.Upgrader : mapper.Downgrader;

                if (!transform.TryGet(content, field, out var from)) continue;

                var to = transform.Map(ctx, from);
                if ((from == null && to == null) || (from != null && from.Equals(to)) || (to != null && to.Equals(from))) continue;

                transform.Set(content, field, to);
                changed = true;
            }

            return changed;
        }
    }
}
