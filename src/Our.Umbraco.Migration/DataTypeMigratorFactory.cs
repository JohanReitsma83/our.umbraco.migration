using System;
using System.Collections.Generic;
using System.Reflection;

namespace Our.Umbraco.Migration
{
    public interface IDataTypeMigratorFactory
    {
        void RegisterDataTypeMigrators(Assembly assembly);
        void RegisterDataTypeMigrator(string EditorAlias, Func<IDataTypeMigrator> constructor);
        IDataTypeMigrator CreateDataTypeMigrator(string EditorAlias);
    }

    public static class DataTypeMigratorFactory
    {
        private static IDataTypeMigratorFactory _instance;

        public static IDataTypeMigratorFactory Instance => _instance ?? (_instance = new DefaultDataTypeMigratorFactory());

        private class DefaultDataTypeMigratorFactory : IDataTypeMigratorFactory
        {
            private readonly Dictionary<string, IDataTypeMigrator> _knownMigrations = new Dictionary<string, IDataTypeMigrator>(StringComparer.InvariantCultureIgnoreCase);
            private readonly Dictionary<string, Func<IDataTypeMigrator>> _constructors = new Dictionary<string, Func<IDataTypeMigrator>>(StringComparer.InvariantCultureIgnoreCase);

            public DefaultDataTypeMigratorFactory()
            {
                RegisterDataTypeMigrators(Assembly.GetExecutingAssembly());
            }

            public void RegisterDataTypeMigrators(Assembly assembly)
            {
                var intType = typeof(IDataTypeMigrator);

                foreach (var type in assembly.ExportedTypes)
                {
                    if (!intType.IsAssignableFrom(type)) continue;

                    var aliases = new string[0];
                    var attr = type.GetCustomAttribute(typeof(DataTypeMigratorAttribute)) as DataTypeMigratorAttribute;
                    if (attr == null)
                    {
                        if (type.Name.EndsWith("DataTypeMigrator")) aliases = new[] {type.Name.Substring(0, type.Name.Length - 16)};
                        else if (type.Name.EndsWith("Migrator")) aliases = new[] { type.Name.Substring(0, type.Name.Length - 8)};
                    }
                    else
                    {
                        aliases = attr.EditorAliases;
                    }

                    var constructor = type.GetConstructor(new Type[0]);
                    if (constructor == null) continue;

                    foreach (var alias in aliases)
                    {
                        _constructors[alias] = () => constructor.Invoke(new object[0]) as IDataTypeMigrator;
                    }
                }
            }

            public void RegisterDataTypeMigrator(string EditorAlias, Func<IDataTypeMigrator> constructor)
            {
                _constructors[EditorAlias] = constructor;
            }

            public IDataTypeMigrator CreateDataTypeMigrator(string EditorAlias)
            {
                if (!_knownMigrations.TryGetValue(EditorAlias, out var migrator))
                    _knownMigrations[EditorAlias] = migrator = !_constructors.TryGetValue(EditorAlias, out var constructor) ? null : constructor?.Invoke();

                return migrator;
            }
        }
    }
}
