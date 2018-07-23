# Umbraco Migrations

An [Umbraco](http://umbraco.com/) package that allows developers to easily add product-specific migrations and have them automatically applied when Umbraco restarts.  By default, Umbraco does not apply migrations, but allows the developer to determine which migrations to apply and either add a start-up handler or a custom dashboard page to apply the migrations.  This gives the developer complete control, but forces them to do a lot of common implementation themselves.  This package automates common tasks such as identifying which migrations need to be applied, and then applying these migrations during Umbraco startup.

## Installation

Umbraco Migrations is available for installation through NuGet:

`Install-Package Our.Umbraco.Migration`

## Licensing

This project is licensed under the [MIT license](https://opensource.org/licenses/MIT).

## Usage

Umbraco Migrations includes two main components.  The first is a migration resolver that is tasked with determining which migrations have not been applied for one or more products.  The second is a [MigrationStartupHandler](src/Our.Umbraco.Migration/Our.Umbraco.Migration/MigrationStartupHandler.cs) class that queries the migration resolvers during startup to determine unapplied migrations, and then applying them.  The startup handler uses the web.config file to determine which migration resolvers to use, and to configure those resolvers.

The package includes a default migration resolver called the [ProductMigrationResolver](src/Our.Umbraco.Migration/Our.Umbraco.Migration/ProductMigrationResolver.cs).  This resolver has a single configuration setting, set in the web.config file, that specifies a comma-separated list of product names to search for.  The resolver then searches the entire code base to find IMigration implementations that have a MigrationAttribute on the class where the product name in the attribute matches one of the specified product names.

The simplest usage is to modify the web.config to customize the list of product names:
```
<migrationResolvers>
  <add>
    <add key="MonitoredProductNames" value="MyProduct1,MyProduct2" />
  </add>
</migrationResolvers>
```

With this simple usage, any Migration classes in the solution that have a product name of MyProduct1 or MyProduct2 will be queried during startup.  The umbracoMigration table will be consulted, and if any of the migrations have a version that is greater than the version listed in the table (or 0.0.0 if the product is not yet listed), those migrations will be applied in order.

A more advanced usage for a custom IMigrationResolver.  This would allow for custom code to determine which migrations to apply, what order to apply them in, and even to potentially skip migrations where needed.