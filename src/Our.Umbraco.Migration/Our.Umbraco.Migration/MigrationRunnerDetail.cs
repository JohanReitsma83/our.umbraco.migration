using System;
using Semver;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Services;

/* Copyright 2018 ProWorks, Inc.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
namespace Our.Umbraco.Migration
{
    /// <summary>
    /// Stores information needed to run migrations, exposing the product and version information for logging purposes
    /// </summary>
    public class MigrationRunnerDetail
    {
        private readonly Func<IMigrationEntryService, ILogger, MigrationRunner> _runnerCreator;

        /// <summary>
        /// Creates a new MigrationRunnerDetail, specifying the migrations that need to be applied.  The MigrationRunner will be created using the defaults provided
        /// </summary>
        /// <param name="productName">The name of the product these migrations relate to</param>
        /// <param name="currentVersion">The current version in the database</param>
        /// <param name="targetVersion">The version that will be recorded in the database once all migrations are applied</param>
        /// <param name="migrations">The migrations to apply, or no migrations to rely on the MigrationRunner to create and determine the migrations needed</param>
        public MigrationRunnerDetail(string productName, SemVersion currentVersion, SemVersion targetVersion, params IMigration[] migrations)
            : this(productName, currentVersion, targetVersion, (s, l) => new MigrationRunner(s, l, currentVersion, targetVersion, productName, migrations))
        {
        }

        /// <summary>
        /// Creates a new MigrationRunnerDetail, specifying a method which will create the migration runner when needed
        /// </summary>
        /// <param name="productName">The name of the product these migrations relate to</param>
        /// <param name="currentVersion">The current version in the database</param>
        /// <param name="targetVersion">The version that will be recorded in the database once all migrations are applied</param>
        /// <param name="runnerCreator">The method which will create a MigrationRunner when needed</param>
        public MigrationRunnerDetail(string productName, SemVersion currentVersion, SemVersion targetVersion, Func<IMigrationEntryService, ILogger, MigrationRunner> runnerCreator)
        {
            ProductName = productName;
            CurrentVersion = currentVersion;
            TargetVersion = targetVersion;
            _runnerCreator = runnerCreator;
        }

        /// <summary>
        /// The name of the product
        /// </summary>
        public string ProductName { get; }

        /// <summary>
        /// The current version in the database
        /// </summary>
        public SemVersion CurrentVersion { get; }

        /// <summary>
        /// The version that will be recorded in the database once all migrations are applied
        /// </summary>
        public SemVersion TargetVersion { get; }

        /// <summary>
        /// Creates a MigrationRunner to apply the migrations
        /// </summary>
        /// <param name="entryService">The entry service to use with the runners</param>
        /// <param name="logger">The logger to use with the runners</param>
        /// <returns>A MigrationRunner to apply the migrations</returns>
        public MigrationRunner CreateRunner(IMigrationEntryService entryService, ILogger logger)
        {
            return _runnerCreator?.Invoke(entryService, logger);
        }
    }
}
