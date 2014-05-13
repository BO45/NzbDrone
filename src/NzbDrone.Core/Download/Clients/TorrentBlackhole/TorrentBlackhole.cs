﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.MediaFiles;
using NLog;
using Omu.ValueInjecter;
using FluentValidation.Results;

namespace NzbDrone.Core.Download.Clients.TorrentBlackhole
{
    public class TorrentBlackhole : DownloadClientBase<TorrentBlackholeSettings>
    {
        private readonly IDiskScanService _diskScanService;
        private readonly IHttpProvider _httpProvider;

        public TorrentBlackhole(IDiskScanService diskScanService,
                                IHttpProvider httpProvider,
                                IConfigService configService,
                                IDiskProvider diskProvider,
                                IParsingService parsingService,
                                Logger logger)
            : base(configService, diskProvider, parsingService, logger)
        {
            _diskScanService = diskScanService;
            _httpProvider = httpProvider;
        }

        public override DownloadProtocol Protocol
        {
            get
            {
                return DownloadProtocol.Torrent;
            }
        }

        public override string Download(RemoteEpisode remoteEpisode)
        {
            var url = remoteEpisode.Release.DownloadUrl;
            var title = remoteEpisode.Release.Title;

            title = FileNameBuilder.CleanFileName(title);

            var filename = Path.Combine(Settings.TorrentFolder, String.Format("{0}.torrent", title));

            _logger.Debug("Downloading torrent from: {0} to: {1}", url, filename);
            _httpProvider.DownloadFile(url, filename);
            _logger.Debug("Torrent Download succeeded, saved to: {0}", filename);

            return null;
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            foreach (var folder in _diskProvider.GetDirectories(Settings.WatchFolder))
            {
                var title = FileNameBuilder.CleanFileName(Path.GetFileName(folder));

                var files = _diskProvider.GetFiles(folder, SearchOption.AllDirectories);

                var historyItem = new DownloadClientItem
                {
                    DownloadClient = Definition.Name,
                    DownloadClientId = Definition.Name + "_" + Path.GetFileName(folder) + "_" + _diskProvider.FolderGetCreationTimeUtc(folder).Ticks,
                    Title = title,

                    TotalSize = files.Select(_diskProvider.GetFileSize).Sum(),

                    OutputPath = folder
                };

                if (files.Any(_diskProvider.IsFileLocked))
                {
                    historyItem.Status = DownloadItemStatus.Downloading;
                }
                else
                {
                    historyItem.Status = DownloadItemStatus.Completed;

                    historyItem.RemainingTime = TimeSpan.Zero;
                }

                historyItem.RemoteEpisode = GetRemoteEpisode(historyItem.Title);
                if (historyItem.RemoteEpisode == null) continue;

                yield return historyItem;
            }

            foreach (var videoFile in _diskScanService.GetVideoFiles(Settings.WatchFolder, false))
            {
                var title = FileNameBuilder.CleanFileName(Path.GetFileName(videoFile));

                var historyItem = new DownloadClientItem
                {
                    DownloadClient = Definition.Name,
                    DownloadClientId = Definition.Name + "_" + Path.GetFileName(videoFile) + "_" + _diskProvider.FileGetLastWriteUtc(videoFile).Ticks,
                    Title = title,

                    TotalSize = _diskProvider.GetFileSize(videoFile),

                    OutputPath = videoFile
                };

                if (_diskProvider.IsFileLocked(videoFile))
                {
                    historyItem.Status = DownloadItemStatus.Downloading;
                }
                else
                {
                    historyItem.Status = DownloadItemStatus.Completed;
                    historyItem.RemainingTime = TimeSpan.Zero;
                }

                historyItem.RemoteEpisode = GetRemoteEpisode(historyItem.Title);
                if (historyItem.RemoteEpisode == null) continue;

                yield return historyItem;
            }
        }

        public override void RemoveItem(string id)
        {
            throw new NotSupportedException();
        }

        public override String RetryDownload(string id)
        {
            throw new NotSupportedException();
        }

        public override DownloadClientStatus GetStatus()
        {
            return new DownloadClientStatus
            {
                IsLocalhost = true,
                OutputRootFolders = new List<string> { Settings.WatchFolder }
            };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestFolder(Settings.TorrentFolder, "TorrentFolder"));
            failures.AddIfNotNull(TestFolder(Settings.WatchFolder, "WatchFolder"));
        }
    }
}
