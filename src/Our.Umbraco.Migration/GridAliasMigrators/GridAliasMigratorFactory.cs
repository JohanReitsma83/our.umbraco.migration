using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Our.Umbraco.Migration.GridAliasMigrators
{
    public interface IGridAliasMigratorFactory
    {
        void RegisterGridAliasMigrators(Assembly assembly);
        void RegisterGridAliasMigrator(string gridControlAlias, Func<IGridAliasMigrator> constructor, bool overwriteExistingRegistration);
        void RegisterGridAliasMigrator(string gridControlAlias, IGridAliasMigrator migrator, bool overwriteExistingRegistration);
        IGridAliasMigrator CreateGridAliasMigrator(string gridControlAlias);
    }

    public static class GridAliasMigratorFactory
    {
        private static IGridAliasMigratorFactory _instance;

        public static IGridAliasMigratorFactory Instance => _instance ?? (_instance = new GridAliasMigratorFactory.DefaultGridAliasMigratorFactory());

        private class DefaultGridAliasMigratorFactory : IGridAliasMigratorFactory
        {
            private readonly Dictionary<string, IGridAliasMigrator> _knownMigrators = new Dictionary<string, IGridAliasMigrator>(StringComparer.InvariantCultureIgnoreCase);
            private readonly Dictionary<string, Func<IGridAliasMigrator>> _constructors = new Dictionary<string, Func<IGridAliasMigrator>>(StringComparer.InvariantCultureIgnoreCase);

            public DefaultGridAliasMigratorFactory()
            {
                RegisterGridAliasMigrators(Assembly.GetExecutingAssembly());
            }

            public void RegisterGridAliasMigrators(Assembly assembly)
            {
                var intType = typeof(IGridAliasMigrator);

                foreach (var type in assembly.ExportedTypes)
                {
                    if (!intType.IsAssignableFrom(type)) continue;

                    var aliases = new string[0];
                    var attr = type.GetCustomAttribute(typeof(GridAliasMigratorAttribute)) as GridAliasMigratorAttribute;
                    if (attr == null)
                    {
                        if (type.Name.EndsWith("GridAliasMigrator")) aliases = new[] { type.Name.Substring(0, type.Name.Length - 16) };
                        else if (type.Name.EndsWith("Migrator")) aliases = new[] { type.Name.Substring(0, type.Name.Length - 8) };
                    }
                    else
                    {
                        aliases = attr.GridControlAliases;
                    }

                    var constructor = type.GetConstructor(new Type[0]);
                    if (constructor == null) continue;

                    foreach (var alias in aliases)
                    {
                        _constructors[alias] = () => constructor.Invoke(new object[0]) as IGridAliasMigrator;
                    }
                }
            }

            public void RegisterGridAliasMigrator(string gridControlAlias, Func<IGridAliasMigrator> constructor, bool overwriteExistingRegistration)
            {
                if (overwriteExistingRegistration || !_constructors.ContainsKey(gridControlAlias)) _constructors[gridControlAlias] = constructor;
            }

            public void RegisterGridAliasMigrator(string gridControlAlias, IGridAliasMigrator migrator, bool overwriteExistingRegistration)
            {
                if (overwriteExistingRegistration || (!_constructors.ContainsKey(gridControlAlias) || !_knownMigrators.ContainsKey(gridControlAlias)))
                    _knownMigrators[gridControlAlias] = migrator;
            }

            public IGridAliasMigrator CreateGridAliasMigrator(string gridControlAlias)
            {
                if (!_knownMigrators.TryGetValue(gridControlAlias, out var migrator))
                    _knownMigrators[gridControlAlias] = migrator = !_constructors.TryGetValue(gridControlAlias, out var constructor) ? null : constructor?.Invoke();

                return migrator;
            }
        }
    }
}
