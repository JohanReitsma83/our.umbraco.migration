using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Our.Umbraco.Migration
{
    public interface IContentBaseSource
    {
        string SourceName { get; }
        ContentBaseType SourceType { get; }
        IEnumerable<IContentBase> GetContents(ILogger logger, ServiceContext ctx);
    }
}
