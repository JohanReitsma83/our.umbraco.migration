using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    /// <summary>
    /// A transformation to convert a field from one value to another
    /// </summary>
    public interface IPropertyTransform
    {
        /// <summary>
        /// Gets the current value of the field
        /// </summary>
        /// <param name="content">The current content node</param>
        /// <param name="field">The field to retrieve a value from</param>
        /// <param name="value">The value retrieved</param>
        /// <returns>True if a value was successfully retrieved from the given field, false otherwise</returns>
        bool TryGet(IContentBase content, string field, out object value);

        /// <summary>
        /// Sets the value of a field
        /// </summary>
        /// <param name="content">The current content node</param>
        /// <param name="field">The field to set a value to</param>
        /// <param name="value">The value to set</param>
        void Set(IContentBase content, string field, object value);

        /// <summary>
        /// Maps a value from its previous state into the future state
        /// </summary>
        /// <param name="ctx">A service context for lookups if needed</param>
        /// <param name="from">The previous value</param>
        /// <returns>The mapped value</returns>
        object Map(ServiceContext ctx, object from);
    }
}
