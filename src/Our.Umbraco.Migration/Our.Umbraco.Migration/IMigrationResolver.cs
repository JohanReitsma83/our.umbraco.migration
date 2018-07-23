using System.Collections.Generic;
using Umbraco.Core.Models;

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
    /// Determines which products are available to apply, and which migrations need to be applied based on the current versions in the Umbraco database.
    /// </summary>
    public interface IMigrationResolver
    {
        /// <summary>
        /// Initializes the resolver with configuration values specified in the web.config file.
        /// </summary>
        /// <param name="configuration">The key/value pairs specified in the web.config file</param>
        void Initialize(IReadOnlyDictionary<string, string> configuration);

        /// <summary>
        /// Lists the names of the products that this resolver is able to apply.  This should NOT include "Umbraco", as that is reserved by the Umbraco core.
        /// </summary>
        /// <returns>The product names</returns>
        IEnumerable<string> GetProductNames();

        /// <summary>
        /// Determines which migrations need to be applied, given the migrations of each currently applied in the database.  Migration runners will be executed in the order returned.
        /// </summary>
        /// <param name="appliedMigrations">Specifies the migrations which have been applied for each product</param>
        /// <returns>All migration runners for all products that are needed to upgrade the database from the current version to the latest, in the order they should be applied</returns>
        IEnumerable<MigrationRunnerDetail> GetMigrationRunners(IReadOnlyDictionary<string, IEnumerable<IMigrationEntry>> appliedMigrations);
    }
}
