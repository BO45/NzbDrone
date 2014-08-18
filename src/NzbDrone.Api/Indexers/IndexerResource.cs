using System;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Api.Indexers
{
    public class IndexerResource : ProviderResource
    {
        public Boolean EnableRss { get; set; }
        public Boolean EnableSearch { get; set; }
        public Boolean SupportsSearching { get; set; }
        public DownloadProtocol Protocol { get; set; }
    }
}