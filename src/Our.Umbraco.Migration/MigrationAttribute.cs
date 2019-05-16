using System;
using System.Collections.Generic;
using Semver;

namespace Our.Umbraco.Migration
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MigrationAttribute : Attribute
    {
        /// <summary>
        /// The name of the product, which groups migrations together to form a chain
        /// </summary>
        public string ProductName { get; }

        /// <summary>
        /// One or more migrations which must be run before the current migration can be run.
        /// </summary>
        public IEnumerable<Type> DependentMigrations { get; }
    }
}