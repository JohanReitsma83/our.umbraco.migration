# Umbraco Migrations

`Our.Umbraco.Migrations` is an [Umbraco](http://umbraco.com/) package that allows developers to bootstrap their own, custom migrations into an Umbraco site. With `Our.Umbraco.Migrations`, developers can skip the overhead of writing event handlers that run their migrations on ApplicationStarted and skip straight to developing the migration itself.

Base migration classes are included that can be used to convert id pickers to udi pickers and to help convert inheritance based doctype structures to compositions.

## The Problem
By default, Umbraco does not automatically run migrations. Running migrations requires a developer to write custom code (likely an ApplicationStarted event handler) for each migration that needs to be run. See this [cultiv.nl blog post](https://cultiv.nl/blog/using-umbraco-migrations-to-deploy-changes/) for great instructions about how to do this yourself.

`Our.Umbraco.Migrations` automates common tasks such as identifying which migrations need to be applied, and then applying these migrations during Umbraco startup.

## Installation

Umbraco Migrations is available for installation through NuGet:

`Install-Package Our.Umbraco.Migration`

## Licensing

This project is licensed under the [MIT license](https://opensource.org/licenses/MIT).

## [See Full Documentation](https://bitbucket.org/proworks/our.umbraco.migration/src/master/Documentation)
Docs on implementing `Our.Umbraco.Migration` base migration classes, `IMigrationResolver`, and other features.
See the [Migration.Demo](https://bitbucket.org/proworks/migration.demo) repository for further examples.

## Getting Started

`Our.Umbraco.Migrations` includes two main components. The first is a migration resolver that is tasked with determining which migrations have not been applied for one or more products.  The second is a [MigrationStartupHandler](https://bitbucket.org/proworks/our.umbraco.migration/src/Our.Umbraco.Migration/Our.Umbraco.Migration/MigrationStartupHandler.cs) class that queries the migration resolvers during startup to determine unapplied migrations, and then applying them.  The startup handler uses configuration in the web.config to determine which migration resolvers to use and to configure those resolvers.

The package includes a default migration resolver called the [ProductMigrationResolver](https://bitbucket.org/proworks/our.umbraco.migration/src/Our.Umbraco.Migration/Our.Umbraco.Migration/ProductMigrationResolver.cs).  This resolver has a single configuration setting, set in the web.config file, that specifies a comma-separated list of product names to search for.  The resolver then searches the entire code base to find `IMigration` implementations that have a `MigrationAttribute` where the product name in the attribute matches one of the specified product names.

The simplest usage is to modify the web.config to customize the list of product names:
```xml
<migrationResolvers>
  <add>
    <add key="MonitoredProductNames" value="MyProduct1,MyProduct2" />
  </add>
</migrationResolvers>
```

With this simple usage, any Migration classes in the solution that have a product name of MyProduct1 or MyProduct2 will be queried during startup.  The `umbracoMigration` table will be consulted, and if any of the migrations have a version that is greater than the version listed in the table (or 0.0.0 if the product is not yet listed), those migrations will be applied in order.

Custom migration resolvers can be created by implementing `IMigrationResolver`. This would allow for custom code to determine which migrations to apply, what order to apply them in, and even to potentially skip migrations where needed.

