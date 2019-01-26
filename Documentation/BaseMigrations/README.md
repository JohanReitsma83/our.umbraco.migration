# Migration Resolvers
`Our.Umbraco.Migrations` includes two main components. The first is a migration resolver that is tasked with determining which migrations have not been applied for one or more products.  The second is a [MigrationStartupHandler](src/master/src/Our.Umbraco.Migration/Our.Umbraco.Migration/MigrationStartupHandler.cs) class that queries the migration resolvers during startup to determine unapplied migrations, and then applying them.  The startup handler uses configuration in the web.config to determine which migration resolvers to use and to configure those resolvers.

## Default Migration Resolver

`Our.Umbraco.Migration` comes with a single, default migration resolver called the [ProductMigrationResolver](src/master/src/Our.Umbraco.Migration/Our.Umbraco.Migration/ProductMigrationResolver.cs). 

The `ProductMigrationResolver` is a simple product-based migration resolver. It allows developers to configure migrations by product name to run automatically on `ApplicationStarted`. A comma-separated list of product names can be entered.

```xml
<migrationResolvers>
  <add>
    <add key="MonitoredProductNames" value="MyCustomDataType,MyExampleProduct" />
  </add>
</migrationResolvers>
```

The above configuration will cause the `ProductMigrationResolver` to automatically run all migrations using the `MyCustomDataType` and `MyExampleProduct` product names.

This custom migration using the `MyCustomDataType` migration would automatically be run if it hasn't been run already.

```csharp
namespace Example.DataType.Migrations
{
    [Migration("1.0.1", 1, "MyCustomDataType")]
    public class MyCustomDataType101 : MigrationBase
    {
        public MyCustomDataType101(ISqlSyntaxProvider sqlSyntax, ILogger logger) : base(sqlSyntax, logger)
        {
        }

        public override void Up()
        {
            // Convert data from xml to JSON
            // ...
        }

        public override void Down()
        {
            // Convert data from JSON to xml
            // ...
        }
    }
}
```

Umbraco stores records of completed migrations in the `umbracoMigration` table. The following fields are tracked for every migration:
 - name (the product name)
 - version (the version id)
 - createDate (the date/time when the migration was completed)

Assuming that no record was found in the `umbracoMigration` table for `MyCustomDataType` with version `1.0.1`, the default `ProductMigrationResolver` would execute the above `MyCustomDataType101` migration.

## Writing a Custom Migration Resolver
The sole responsibility of a migration resolver is to decide which migrations should be executed. All migration resolvers must implement [IMigrationResolver](src/master/src/Our.Umbraco.Migration/Our.Umbraco.Migration/IMigrationResolver.cs).

