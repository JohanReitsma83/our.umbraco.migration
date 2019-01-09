using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Our.Umbraco.Migration.PropertiesToCompositionMigration.Models;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Our.Umbraco.Migration.PropertiesToCompositionMigration
{
    public abstract class PropertiesToCompositionMigration : MigrationBase
    {
        private readonly ILogger _logger;

        protected PropertiesToCompositionMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
            _logger = logger;
        }

        protected abstract IEnumerable<PropertiesToCompositionMapping> LoadMappings();

        public override void Up()
        {
            _logger.Info(typeof(PropertiesToCompositionMapping), "Beginning migration");
            var mappings = LoadMappings()?.ToList();

            if (!CreateCompositions(mappings))
            {
                _logger.Warn(typeof(PropertiesToCompositionMapping), "There was a problem creating compositions. Abort migration early");
                return;
            }

            if (!AssociatePropertiesWithComposition(mappings))
            {
                _logger.Warn(typeof(PropertiesToCompositionMapping), "There was a problem associating properties with compositions. Abort migration early");
                return;
            }

            if (!ApplyCompositionToDescendentContentTypes(mappings))
            {
                _logger.Warn(typeof(PropertiesToCompositionMapping), "There was a problem applying composition to leaf descendent contentTypes. Abort migration early");
                return;
            }

            if (!FinalizeContentTypes(mappings))
            {
                // Finalization is a nice to have. Still consider the migration a success if there were problems here
                _logger.Warn(typeof(PropertiesToCompositionMapping), "There was a problem finalizing the contentTypes and compositions involved in the migration.");
            }

            _logger.Info(typeof(PropertiesToCompositionMapping), "Finished migration successfully");
        }

        public override void Down()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Step 1: Create empty compositions to later hold properties
        /// </summary>
        /// <param name="mappings"></param>
        /// <returns>success/failure</returns>
        private bool CreateCompositions(IEnumerable<PropertiesToCompositionMapping> mappings)
        {
            _logger.Info(typeof(PropertiesToCompositionMigration), "CreateCompositions: Preparing to create compositions");
            var cts = ApplicationContext.Current.Services.ContentTypeService;

            foreach (var mapping in mappings)
            {
                _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Preparing to create composition {mapping.Composition.CompositionAlias}");
                try
                {
                    var composition = cts.GetContentType(mapping.Composition.CompositionAlias);
                    if (composition == null)
                    {
                        var compositionContainer = EnsureContainer(mapping.Composition.ContainerName);
                        _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Create new composition: {mapping.Composition.CompositionAlias}");
                        composition = new ContentType(compositionContainer?.Id ?? -1)
                        {
                            Alias = mapping.Composition.CompositionAlias,
                            Name = mapping.Composition.CompositionName,
                            Icon = mapping.Composition.Icon
                        };
                    }
                    else
                    {
                        _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Composition {mapping.Composition.CompositionAlias} already exists");
                    }

                    _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Preparing to add tabs to composition {mapping.Composition.CompositionAlias}");

                    var sourceContentType = cts.GetContentType(mapping.Source.SourceContentTypeAlias);
                    var tabs = sourceContentType.PropertyGroups
                        .Where(g => g.PropertyTypes.Any(t => mapping.Source.PropertyAliases.Contains(t.Alias)))
                        .ToList();

                    // Create any tabs that don't already exist on the composition
                    foreach (var propertyGroup in tabs.Where(t => !composition.PropertyGroups.Any(g => g.Name == t.Name)))
                    {
                        composition.AddPropertyGroup(propertyGroup.Name);
                    }

                    _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Finished adding tabs to composition {mapping.Composition.CompositionAlias}");

                    // Sort any tabs that are being mapped
                    _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Preparing to sort tabs on composition {composition.Alias}");
                    foreach (var propertyGroup in composition.PropertyGroups)
                    {
                        var matchingTab = tabs.First(t => t.Name == propertyGroup.Name);
                        if (matchingTab != null)
                        {
                            propertyGroup.SortOrder = matchingTab.SortOrder;
                        }
                    }
                    _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Finished sorting tabs on composition {composition.Alias}");

                    _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Saving composition {composition.Alias}");
                    cts.Save(composition);

                    _logger.Info(typeof(PropertiesToCompositionMigration), $"CreateCompositions: Finished setup of composition {composition.Alias}");
                }
                catch (Exception ex)
                {
                    _logger.Error(typeof(PropertiesToCompositionMigration), $"There was a problem and the {mapping.Composition.CompositionAlias} composition could not be added or edited.", ex);
                    return false;
                }
            }

            _logger.Info(typeof(PropertiesToCompositionMapping), "CreateCompositions: Finished creating compositions");
            return true;
        }

        /// <summary>
        /// Step 2: Associate properties with the new composition. Replaces existing association
        /// with source contentType.
        /// </summary>
        /// <param name="mappings"></param>
        /// <returns>success/failure</returns>
        private bool AssociatePropertiesWithComposition(IEnumerable<PropertiesToCompositionMapping> mappings)
        {
            _logger.Info(typeof(PropertiesToCompositionMigration), "AssociatePropertiesWithComposition: Preparing to associate properties to compositions");
            var cts = ApplicationContext.Current.Services.ContentTypeService;
            var sqlSb = new StringBuilder();

            try
            {
                // Generate SQL
                _logger.Info(typeof(PropertiesToCompositionMigration), "AssociatePropertiesWithComposition: Begin constructing SQL");
                foreach (var mapping in mappings)
                {
                    var composition = cts.GetContentType(mapping.Composition.CompositionAlias);

                    var sourceContentType = cts.GetContentType(mapping.Source.SourceContentTypeAlias);
                    var sourceTabs = sourceContentType.PropertyGroups.Where(g => g.PropertyTypes.Any(t => mapping.Source.PropertyAliases.Contains(t.Alias))).ToList();
                    foreach (var tab in sourceTabs)
                    {
                        var destTab = composition.PropertyGroups.First(t => t.Name == tab.Name);
                        var matchingPropertyAliases = tab.PropertyTypes
                            .Where(p => mapping.Source.PropertyAliases.Contains(p.Alias))
                            .Select(p => p.Alias)
                            .ToList();

                        sqlSb.AppendLine("UPDATE [cmsPropertyType]");
                        sqlSb.AppendLine($"SET contentTypeId = {composition.Id}");
                        sqlSb.AppendLine($"   ,propertyTypeGroupId = {destTab.Id}");
                        sqlSb.AppendLine($"WHERE contentTypeId = {sourceContentType.Id}");
                        sqlSb.AppendLine($"  AND [Alias] IN ({String.Join(",", matchingPropertyAliases.Select(a => $"'{a}'"))})");
                        sqlSb.AppendLine();
                    }
                }
                _logger.Info(typeof(PropertiesToCompositionMigration), $"AssociatePropertiesWithComposition: Finished constructing SQL: {sqlSb}");
            }
            catch (Exception ex)
            {
                _logger.Error(typeof(PropertiesToCompositionMigration), $"There was a problem generating the SQL necessary to associate properties with a composition.", ex);
                return false;
            }

            try
            {
                // Execute SQL
                _logger.Info(typeof(PropertiesToCompositionMigration), "AssociatePropertiesWithComposition: Finished constructing SQL");
                using (var connection = new SqlConnection(ApplicationContext.Current.DatabaseContext.ConnectionString))
                {
                    using (var cmd = new SqlCommand(sqlSb.ToString(), connection))
                    {
                        connection.Open();
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                        connection.Close();
                    }
                }
                _logger.Info(typeof(PropertiesToCompositionMigration), "AssociatePropertiesWithComposition: Finished running SQL");
            }
            catch (SqlException sex)
            {
                _logger.Error(typeof(PropertiesToCompositionMigration), $"There was a problem executing the SQL to associate properties with a composition: {sqlSb}", sex);
                return false;
            }

            _logger.Info(typeof(PropertiesToCompositionMigration), "AssociatePropertiesWithComposition: Finished associating properties to compositions");
            return true;
        }

        /// <summary>
        /// Step 3: Apply composition to all leaf descendent contentTypes of the source contentType.
        /// </summary>
        /// <param name="mappings"></param>
        /// <returns>success/failure</returns>
        private bool ApplyCompositionToDescendentContentTypes(IEnumerable<PropertiesToCompositionMapping> mappings)
        {
            _logger.Info(typeof(PropertiesToCompositionMigration), "ApplyCompositionToDescendentContentTypes: Preparing to apply compositions to leaf descendent contentTypes");
            var cts = ApplicationContext.Current.Services.ContentTypeService;
            var sqlSb = new StringBuilder();

            try
            {
                // Generate SQL
                _logger.Info(typeof(PropertiesToCompositionMigration), "ApplyCompositionToDescendentContentTypes: Begin constructing SQL");
                foreach (var mapping in mappings)
                {
                    var composition = cts.GetContentType(mapping.Composition.CompositionAlias);
                    var sourceContentType = cts.GetContentType(mapping.Source.SourceContentTypeAlias);
                    var leafContentTypes = GetLeafContentTypeDescendents(sourceContentType.Id)?.ToList() ?? new List<IContentType>();
                    if (leafContentTypes.Any())
                    {
                        sqlSb.AppendLine("INSERT INTO [cmsContentType2ContentType]");
                        sqlSb.AppendLine("Values");

                        foreach (var item in leafContentTypes.Select((ct, i) => new { contentType = ct, idx = i }))
                        {
                            var contentType = item.contentType;
                            sqlSb.AppendLine($"  ({composition.Id}, {contentType.Id})");
                            if (item.idx < leafContentTypes.Count - 1)
                            {
                                sqlSb.Append(",");
                            }
                        }
                    }
                }
                _logger.Info(typeof(PropertiesToCompositionMigration), $"ApplyCompositionToDescendentContentTypes: Finished constructing SQL: {sqlSb}");
            }
            catch (Exception ex)
            {
                _logger.Error(typeof(PropertiesToCompositionMigration), $"There was a problem generating the SQL necessary to apply the composition to leaf contentTypes.", ex);
                return false;
            }

            try
            {
                // Execute SQL
                _logger.Info(typeof(PropertiesToCompositionMigration), "ApplyCompositionToDescendentContentTypes: Begin run SQL");
                using (var connection = new SqlConnection(ApplicationContext.Current.DatabaseContext.ConnectionString))
                {
                    using (var cmd = new SqlCommand(sqlSb.ToString(), connection))
                    {
                        connection.Open();
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                        connection.Close();
                    }
                }
                _logger.Info(typeof(PropertiesToCompositionMigration), "ApplyCompositionToDescendentContentTypes: Finished running SQL");
            }
            catch (SqlException sex)
            {
                _logger.Error(typeof(PropertiesToCompositionMigration), $"There was a problem executing the SQL that will apply the composition to leaf contentTypes: {sqlSb}", sex);
                return false;
            }

            _logger.Info(typeof(PropertiesToCompositionMigration), "ApplyCompositionToDescendentContentTypes: Finished applying compositions to leaf descendent contentTypes");
            return true;
        }

        /// <summary>
        /// Step 4: Ensure UmbracoContext is sync'd with manual DB changes
        /// </summary>
        /// <param name="mappings"></param>
        /// <returns>success/failure</returns>
        private bool FinalizeContentTypes(IEnumerable<PropertiesToCompositionMapping> mappings)
        {
            _logger.Info(typeof(PropertiesToCompositionMigration), "FinalizeContentTypes: Preparing to finalize contentTypes and compositions involved in the migration");
            var cts = ApplicationContext.Current.Services.ContentTypeService;
            var isSuccessful = true;

            foreach (var mapping in mappings)
            {
                try
                {
                    // Resave composition. This is to force the UmbracoContext to sync up with the database after manual SQL changes
                    _logger.Info(typeof(PropertiesToCompositionMigration), $"FinalizeContentTypes: Syncing UmbracoContext with DB for composition: {mapping.Composition.CompositionAlias}");
                    var composition = cts.GetContentType(mapping.Composition.CompositionAlias);
                    cts.Save(composition);
                }
                catch (Exception ex)
                {
                    _logger.WarnWithException(typeof(PropertiesToCompositionMigration), $"There was a problem and the {mapping.Composition.CompositionAlias} composition could not be saved. Recycle the app pool to resolve this problem.", ex);
                    isSuccessful = false;
                }

                try
                {
                    // Resave contentType. This is to force the UmbracoContext to sync up with the database after manual SQL changes
                    _logger.Info(typeof(PropertiesToCompositionMigration), $"FinalizeContentTypes: Syncing UmbracoContext with DB for contentType: {mapping.Source.SourceContentTypeAlias}");
                    var sourceContentType = cts.GetContentType(mapping.Source.SourceContentTypeAlias);
                    cts.Save(sourceContentType);
                }
                catch (Exception ex)
                {
                    _logger.WarnWithException(typeof(PropertiesToCompositionMigration), $"There was a problem and the {mapping.Source.SourceContentTypeAlias} contentType could not be saved. Recycle the app pool to resolve this problem.", ex);
                    isSuccessful = false;
                }
            }

            _logger.Info(typeof(PropertiesToCompositionMapping), "FinalizeContentTypes: Finished finalizing contentTypes and compositions involved in the migration");
            return isSuccessful;
        }

        /// <summary>
        /// Get container by name. Create a container at root if one does not already exist
        /// </summary>
        /// <param name="containerName"></param>
        /// <returns></returns>
        private EntityContainer EnsureContainer(string containerName)
        {
            _logger.Info(typeof(PropertiesToCompositionMapping), "EnsureContainer: Preparing to ensure container exists");
            if (String.IsNullOrWhiteSpace(containerName))
            {
                _logger.Info(typeof(PropertiesToCompositionMapping), "EnsureContainer: No content container specified.");
                return null;
            }

            var cts = ApplicationContext.Current.Services.ContentTypeService;
            var allContainers = cts.GetContentTypeContainers(new int[0]);
            var compositionContainer = allContainers.FirstOrDefault(c => String.Equals(c.Name, containerName, StringComparison.InvariantCultureIgnoreCase));
            if (compositionContainer == null)
            {
                _logger.Info(typeof(PropertiesToCompositionMapping), "EnsureContainer: Composition container did not exist. Creating one in the root");

                cts.CreateContentTypeContainer(-1, containerName);
                allContainers = cts.GetContentTypeContainers(new int[0]);
                compositionContainer = allContainers.FirstOrDefault(c => String.Equals(c.Name, containerName, StringComparison.InvariantCultureIgnoreCase));
                if (compositionContainer != null)
                {
                    _logger.Warn(typeof(PropertiesToCompositionMapping), "EnsureContainer: Composition container was not able to be created.");
                }
            }

            _logger.Info(typeof(PropertiesToCompositionMapping), "EnsureContainer: Finished ensuring container exists");
            return compositionContainer;
        }

        private IEnumerable<IContentType> GetLeafContentTypeDescendents(int parentContentTypeId)
        {
            _logger.Debug(typeof(PropertiesToCompositionMapping), $"GetLeafContentTypeDescendents: Preparing to gather leaf descendent nodes of contentType {parentContentTypeId}");

            var leafContentTypes = new List<IContentType>();
            var cts = ApplicationContext.Current.Services.ContentTypeService;

            var childContentTypes = cts.GetContentTypeChildren(parentContentTypeId)?.ToList() ?? new List<IContentType>();
            if (!childContentTypes.Any())
            {
                _logger.Debug(typeof(PropertiesToCompositionMapping), $"GetLeafContentTypeDescendents: Found leaf contentType: {parentContentTypeId}");
                var currentContentType = cts.GetContentType(parentContentTypeId);
                leafContentTypes.Add(currentContentType);

                return leafContentTypes;
            }

            leafContentTypes = childContentTypes.SelectMany(ct => GetLeafContentTypeDescendents(ct.Id)).ToList();

            return leafContentTypes;
        }
    }
}
