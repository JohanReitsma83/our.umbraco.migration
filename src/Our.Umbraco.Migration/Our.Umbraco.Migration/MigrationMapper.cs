namespace Our.Umbraco.Migration
{
    /// <summary>
    /// Specifies the upgrade and downgrade transforms to apply during migration
    /// </summary>
    public class MigrationMapper
    {
        /// <summary>
        /// The upgrade transform
        /// </summary>
        public ITransformMapper Upgrader { get; set; }

        /// <summary>
        /// The downgrade transform
        /// </summary>
        public ITransformMapper Downgrader { get; set; }
    }
}
