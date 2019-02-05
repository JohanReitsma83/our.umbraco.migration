namespace Our.Umbraco.Migration
{
    /// <summary>
    /// Specifies the upgrade and downgrade transforms to apply during migration
    /// </summary>
    public interface IPropertyMigration
    {
        /// <summary>
        /// The upgrade transform
        /// </summary>
        IPropertyTransform Upgrader { get; }

        /// <summary>
        /// The downgrade transform
        /// </summary>
        IPropertyTransform Downgrader { get; }
    }

    public class PropertyMigration : IPropertyMigration
    {
        public PropertyMigration(IPropertyTransform upgrader, IPropertyTransform downgrader)
        {
            Upgrader = upgrader;
            Downgrader = downgrader;
        }

        public IPropertyTransform Upgrader { get; }
        public IPropertyTransform Downgrader { get; }
    }
}
