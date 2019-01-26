# Common Use Cases

**WARNING**  
The base migration classes used below have not been thoroughly tested. We use them on many sites we work on, and they save us a lot of time, but each Umbraco site is unique. It is entirely possible that unexpected problems may occur that result in data loss. Please test these migrations in a development environment before using them in production to prevent surprises.

## Convert Id pickers to Udi pickers
In Umbraco 7.6.0, Umbraco introduced the [UDI](https://our.umbraco.com/documentation/reference/querying/Udi). When an Umbraco site is upgraded to 7.6.0+, new versions of these pickers are added to Umbraco that pick by UDI instead of Id:  

 - Content Picker (`Umbraco.ContentPickerAlias`)
 - Media Picker (`Umbraco.MediaPicker`)
 - Member Picker (`Umbraco.MemberPicker`)
 - Multinode Tree Picker (`Umbraco.MultiNodeTreePicker`)
 - Multiple Media Picker (`Umbraco.MultipleMediaPicker`)

By implementing the abstract `IdToUdiMigration` base class, a migration can be created that will convert picked Id data to UDI as well as optionally converting the existing datatypes from the obsolete version to the UDI picker equivalent.

There are two ways to run the `IdToUdiMigration`. You can switch between the two modes by using the corresponding constructor.  

 1. Convert data and datatypes
 1. Convert only data

### Convert Data and DataTypes
For most situations, it is recommended to use the **Convert data and datatypes** mode. This will cause the datatypes to swap property editors from the obsolete id picker to the new UDI picker. It will also comb through all of the data and convert the picked ids to their corresponding UDIs.

```csharp
namespace Example.Migrations
{
    [Migration("1.0.0", 1, "Example")]
    public class V1IdMigration : IdToUdiMigration
    {
        // Calling the default base constructor this way will trigger a full conversion of all id pickers to udi
        // pickers and all of the picked ids to udis.
        public V1IdMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger, null, null)
        {
        }
    }
}

```

The datatypes being converted can be restricted by passing in a list of excluded data types and/or included data types like this. In the following example, the conversion will only happen to the "Rate Picker" and "Blog Post Picker" datatypes and associated data.

```csharp
namespace Example.Migrations
{
    [Migration("1.0.0", 1, "Example")]
    public class V1IdMigration : IdToUdiMigration
    {
        private static readonly List<string> includedDatatypes = new List<string>
        {
            "Rate Picker",
            "Blog Post Picker"
        };

        // Calling the default base constructor this way will trigger a full conversion of id pickers to udi
        // pickers ONLY for the specified datatypes
        public V1IdMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger, includedDatatypes, null)
        {
        }
    }
}

```

### Convert Only Data
The **Convert only data** mode is only meant for situations where the datatypes have ALREADY been changed from id pickers to UDI pickers but the property data in Umbraco is still picking ids. This is a data integrity problem. You can use this migration to finish converting the data from id to UDI to match the property editors.

```csharp
namespace Example.Migrations
{
    [Migration("1.0.0", 1, "Example")]
    public class V1IdMigration : IdToUdiMigration
    {
        // create a map of doctypes to properties that need the conversion
        private static readonly IDictionary<string, IDictionary<string, ContentBaseType>> ContentTypeFields = new Dictionary<string, IDictionary<string, ContentBaseType>>
        {
            ["categoryPage"] = new Dictionary<string, ContentBaseType>
            {
                ["rates"] = ContentBaseType.Document,
                ["blogPosts"] = ContentBaseType.Document,
                ["featuredImage"] = ContentBaseType.Media
            }
        };

        // Calling the default base constructor with the contentTypeFieldMappings argument will trigger a migration of data but
        // not of data types.
        public V1IdMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(ContentTypeFields, sqlSyntax, logger)
        {
        }
    }
}
```

### Forcing a Full Cache Refresh
Once the migration is finished, the Umbraco cache can end up in a strange state. Try forcing a full republish using the ContentService once the base migration is finished.

```csharp
namespace Example.Migrations
{
    [Migration("1.0.0", 1, "Example")]
    public class V1IdMigration : IdToUdiMigration
    {
        public V1IdMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger, null, null)
        {
        }

        // Force a full republish once the migration has completed
        public override void Up()
        {
            try
            {
                // Execute migration of ids to udis
                base.Up();

                var svc = ApplicationContext.Current.Services;
                var cs = svc.ContentService;

                // Force update cache
                cs.RePublishAll();
                LogHelper.Info<V1IdMigration>("Successfully upgraded the IDs to UDIs");
            }
            catch (Exception e)
            {
                LogHelper.Error<V1IdMigration>("Could not upgrade the IDs to UDIs", e);
            }
        }
    }
}
```

---

## Convert Inheritance Style DocType Structure to Composition
The `PropertiesToCompositionMigration` migration can be used to pluck properties off of a document type and move them to a composition. By creating a series of these migrations or creating a single migration with many mappings returned by the `LoadMappings` method, you can begin to flatten your document type structure.

Imagine the following doctype structure where the `Content Master` document type has a `Content` tab with a rich text editor on it. It might make sense for the `Home` and `Blog Post` document types to inherit the rich text editor but not the `Blog Folder` and `Sitemap XML`. It would normally be difficult to remove the rich text editor from the `Blog Folder` and `Sitemap XML` without losing all of the rich text data entered on years of blog post nodes.

```
Content Master
    Home
    Blog Folder
        Blog Post
    Sitemap XML
```

By implementing the `PropertiesToCompositionMigration` base class, you can move a property or sets of properties off of a document type and into a new composition. After the new composition is created, the migration will apply that composition to all of the child document types, so no data is lost in the transition.

**WARNING**  
Compositions can only be applied to leaf document types. In this example, any rich text data entered on the `Blog Folder` document type would be lost. This composition assumes that the intermediate master document types do not need to retain the property.

```csharp
namespace Extensions.Migrations
{
    [Migration("1.1.0", 1, "Example")]
    public class RichTextCompositionMigration : PropertiesToCompositionMigration
    {
        private readonly string _contentTypeAlias;
        private readonly List<string> _propertyAliases;

        public RichTextCompositionMigration(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
            // Identify the document type alias and the properties on that document type that need to be relocated
            _contentTypeAlias = "contentMaster";
            _propertyAliases = new List<string>
            {
                "bodyText"
            };
        }

        // Define the composition that the properties should be relocated to. If the composition does not
        // exist, it will automatically be created
        protected override IEnumerable<PropertiesToCompositionMapping> LoadMappings()
        {
            var compositionMappings = new List<PropertiesToCompositionMapping>();

            var composition = new Composition
            {
                ContainerName = "Compositions",         // folder to put the composition under. Will be created if doesn't exist
                CompositionName = "[comp] Body Text",   // Name of the composition. The alias will automatically be generated
                Icon = "icon-brick color-red"           // doctype icon for the composition doctype
            };

            // This example moves properties from a single document type to a single composition.
            // You could potentially map properties from several different document types to several
            // different compositions. This is where you create those mappings.
            var compositionMapping = new PropertiesToCompositionMapping
            {
                Composition = composition,              // The new composition
                Source = new CompositionSourceData      // The source property(s) to be relocated
                {
                    SourceContentTypeAlias = _contentTypeAlias,
                    PropertyAliases = _propertyAliases
                }
            };
            compositionMappings.Add(compositionMapping);

            return compositionMappings;
        }
    }
}
```

After running the above migration, the document type structure should look like this. The `bodyText` property should be missing from the `Content Master` doctype and the `[comp] Body Text` composition should be applied to all leaf document types. A developer can now remove the composition from the `Sitemap XML`. The property will already have been removed from the `Blog Folder` because the composition could only be applied to leaf document types.

```
Compositions
    [comp] Body Text
Content Master
    Home
    Blog Folder
        Blog Post
    Sitemap XML
```
