using System.Configuration;

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
    public class MigrationResolverSection : ConfigurationSection
    {
        [ConfigurationProperty("", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(ResolversCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public ResolversCollection Resolvers => base[""] as ResolversCollection;
    }

    public class ResolversCollection : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.AddRemoveClearMap;

        protected override ConfigurationElement CreateNewElement()
        {
            return new ResolverElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as ResolverElement)?.Name ?? string.Empty;
        }
    }

    public class ResolverElement : ConfigurationElement
    {
        [ConfigurationProperty("name")]
        public string Name => base["name"] as string;

        [ConfigurationProperty("type")]
        public string Type => base["type"] as string;

        [ConfigurationProperty("", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(ResolversCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public KeyValueConfigurationCollection Settings => base[""] as KeyValueConfigurationCollection;
    }
}
