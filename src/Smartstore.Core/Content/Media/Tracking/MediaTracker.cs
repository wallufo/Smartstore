﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using Microsoft.EntityFrameworkCore;
using Smartstore.Caching;
using Smartstore.Collections;
using Smartstore.Core.Configuration;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Stores;
using Smartstore.Data;
using Smartstore.Data.Batching;
using Smartstore.Domain;
using Smartstore.Utilities;
using EfState = Microsoft.EntityFrameworkCore.EntityState;

namespace Smartstore.Core.Content.Media
{
    public class MediaTracker : IMediaTracker
    {
        public Localizer T { get; set; } = NullLocalizer.Instance;

        internal const string TrackedPropertiesKey = "media:trackedprops:all";

        private readonly ICacheManager _cache;
        private readonly SmartDbContext _db;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IAlbumRegistry _albumRegistry;
        private readonly IFolderService _folderService;
        private readonly MediaSettings _mediaSettings;
        private readonly IIndex<Type, IAlbumProvider> _albumProviderFactory;

        private bool _makeFilesTransientWhenOrphaned;

        public MediaTracker(
            ICacheManager cache,
            SmartDbContext db,
            ISettingService settingService,
            IStoreContext storeContext,
            IAlbumRegistry albumRegistry,
            IFolderService folderService,
            MediaSettings mediaSettings,
            IIndex<Type, IAlbumProvider> albumProviderFactory)
        {
            _cache = cache;
            _db = db;
            _settingService = settingService;
            _storeContext = storeContext;
            _albumRegistry = albumRegistry;
            _folderService = folderService;
            _mediaSettings = mediaSettings;
            _albumProviderFactory = albumProviderFactory;

            _makeFilesTransientWhenOrphaned = _mediaSettings.MakeFilesTransientWhenOrphaned;
        }

        public IDisposable BeginScope(bool makeFilesTransientWhenOrphaned)
        {
            var makeTransient = _makeFilesTransientWhenOrphaned;
            _makeFilesTransientWhenOrphaned = makeFilesTransientWhenOrphaned;

            return new ActionDisposable(() => _makeFilesTransientWhenOrphaned = makeTransient);
        }

        public Task TrackAsync<TSetting>(TSetting settings, int? prevMediaFileId, Expression<Func<TSetting, int>> path) where TSetting : ISettings, new()
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(path, nameof(path));

            return TrackSettingAsync(settings, path.ExtractPropertyInfo().Name, prevMediaFileId, path.CompileFast().Invoke(settings));
        }

        public Task TrackAsync<TSetting>(TSetting settings, int? prevMediaFileId, Expression<Func<TSetting, int?>> path) where TSetting : ISettings, new()
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(path, nameof(path));

            return TrackSettingAsync(settings, path.ExtractPropertyInfo().Name, prevMediaFileId, path.CompileFast().Invoke(settings));
        }

        protected async Task TrackSettingAsync<TSetting>(
            TSetting settings,
            string propertyName,
            int? prevMediaFileId,
            int? currentMediaFileId) where TSetting : ISettings, new()
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.NotEmpty(propertyName, nameof(propertyName));

            var key = nameof(TSetting) + "." + propertyName;
            var storeId = _storeContext.CurrentStoreIdIfMultiStoreMode;

            var settingEntity = await _settingService.GetSettingEntityByKeyAsync(key, storeId);
            if (settingEntity != null)
            {
                await this.UpdateTracksAsync(settingEntity, prevMediaFileId, currentMediaFileId, propertyName);
            }
        }

        public Task TrackAsync(BaseEntity entity, int mediaFileId, string propertyName)
        {
            Guard.NotNull(entity, nameof(entity));
            Guard.NotEmpty(propertyName, nameof(propertyName));

            return TrackSingleAsync(entity, mediaFileId, propertyName, MediaTrackOperation.Track);
        }

        public Task UntrackAsync(BaseEntity entity, int mediaFileId, string propertyName)
        {
            Guard.NotNull(entity, nameof(entity));
            Guard.NotEmpty(propertyName, nameof(propertyName));

            return TrackSingleAsync(entity, mediaFileId, propertyName, MediaTrackOperation.Untrack);
        }

        protected virtual async Task TrackSingleAsync(BaseEntity entity, int mediaFileId, string propertyName, MediaTrackOperation operation)
        {
            Guard.NotNull(entity, nameof(entity));

            if (mediaFileId < 1 || entity.IsTransientRecord())
                return;

            var file = await _db.MediaFiles.FindByIdAsync(mediaFileId);
            if (file != null)
            {
                var album = (await _folderService.FindAlbumAsync(file))?.Value;
                if (album == null)
                {
                    throw new InvalidOperationException(T("Admin.Media.Exception.TrackUnassignedFile"));
                }
                else if (!album.CanDetectTracks)
                {
                    // No support for tracking on album level, so get outta here.
                    return;
                }

                var track = new MediaTrack
                {
                    EntityId = entity.Id,
                    EntityName = entity.GetEntityName(),
                    MediaFileId = mediaFileId,
                    Property = propertyName,
                    Album = album.Name
                };

                if (operation == MediaTrackOperation.Track)
                {
                    file.Tracks.Add(track);
                }
                else
                {
                    var dbTrack = file.Tracks.FirstOrDefault(x => x == track);
                    if (dbTrack != null)
                    {
                        file.Tracks.Remove(track);
                        _db.TryChangeState(dbTrack, EfState.Deleted);
                    }
                }

                if (file.Tracks.Count > 0)
                {
                    // A file with tracks can NEVER be transient
                    file.IsTransient = false;
                }
                else if (_makeFilesTransientWhenOrphaned)
                {
                    // But an untracked file can OPTIONALLY be transient
                    file.IsTransient = true;
                }

                await _db.SaveChangesAsync();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task TrackManyAsync(IEnumerable<MediaTrack> actions)
        {
            return TrackManyCoreAsync(actions, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task TrackManyAsync(string albumName, IEnumerable<MediaTrack> tracks)
        {
            return TrackManyCoreAsync(tracks, albumName);
        }

        protected virtual async Task TrackManyCoreAsync(IEnumerable<MediaTrack> tracks, string albumName)
        {
            Guard.NotNull(tracks, nameof(tracks));

            if (!tracks.Any())
                return;

            using (var scope = new DbContextScope(_db, hooksEnabled: false, autoDetectChanges: false))
            {
                // Get the album (necessary later to set FolderId)...
                MediaFolderNode albumNode = albumName.HasValue()
                    ? (await _folderService.GetNodeByPathAsync(albumName))?.Value
                    : null;

                // Get distinct ids of all detected files...
                var mediaFileIds = tracks.Select(x => x.MediaFileId).Distinct().ToArray();

                // fetch these files from database...
                var query = _db.MediaFiles
                    .Include(x => x.Tracks)
                    .Where(x => mediaFileIds.Contains(x.Id));

                var files = await query.ToDictionaryAsync(x => x.Id);

                // for each media file relation to an entity...
                foreach (var track in tracks)
                {
                    // fetch the file from local dictionary by its id...
                    if (files.TryGetValue(track.MediaFileId, out var file))
                    {
                        if (track.Album.IsEmpty())
                        {
                            if (albumNode != null)
                            {
                                // Overwrite track album if scope album was passed.
                                track.Album = albumNode.Name;
                            }
                            else if (file.FolderId.HasValue)
                            {
                                // Determine album from file
                                albumNode = (await _folderService.FindAlbumAsync(file))?.Value;
                                track.Album = albumNode?.Name;
                            }
                        }

                        if (track.Album.IsEmpty())
                            continue; // cannot track without album

                        if ((albumNode ?? (await _folderService.FindAlbumAsync(file))?.Value)?.CanDetectTracks == false)
                            continue; // should not track in albums that do not support track detection

                        // add or remove the track from file
                        if (track.Operation == MediaTrackOperation.Track)
                        {
                            file.Tracks.Add(track);
                        }
                        else
                        {
                            var dbTrack = file.Tracks.FirstOrDefault(x => x == track);
                            if (dbTrack != null)
                            {
                                file.Tracks.Remove(track);
                                _db.TryChangeState(dbTrack, EfState.Deleted);
                            }
                        }

                        if (file.Tracks.Count > 0)
                        {
                            // A file with tracks can NEVER be transient
                            file.IsTransient = false;
                        }
                        else if (_makeFilesTransientWhenOrphaned)
                        {
                            // But an untracked file can OPTIONALLY be transient
                            file.IsTransient = true;
                        }
                    }
                }

                // Save whole batch to database
                int num = await _db.SaveChangesAsync();

                if (num > 0)
                {
                    // Breathe
                    _db.DetachEntities<MediaFile>(deep: true);
                }
            }
        }

        public Task<int> DeleteAllTracksAsync(string albumName)
        {
            Guard.NotEmpty(albumName, nameof(albumName));
            return _db.MediaTracks.Where(x => x.Album == albumName).BatchDeleteAsync();
        }

        public async Task DetectAllTracksAsync(string albumName, CancellationToken cancelToken = default)
        {
            Guard.NotEmpty(albumName, nameof(albumName));

            // Get album info...
            var albumInfo = await _albumRegistry.GetAlbumByNameAsync(albumName);
            if (albumInfo == null)
            {
                throw new InvalidOperationException(T("Admin.Media.Exception.AlbumNonexistent", albumName));
            }

            // load corresponding detector provider for current album...
            var provider = _albumProviderFactory[albumInfo.ProviderType] as IMediaTrackDetector;
            if (provider == null)
            {
                throw new InvalidOperationException(T("Admin.Media.Exception.AlbumNoTrack", albumName));
            }

            // first delete all tracks for current album...
            await DeleteAllTracksAsync(albumName);

            // >>>>> DO detection (potentially a very long process)...
            var tracks = provider.DetectAllTracksAsync(albumName, cancelToken);

            // (perf) batch result data...
            await foreach (var batch in tracks.SliceAsync(500))
            {
                // process the batch
                await TrackManyCoreAsync(batch, albumName);
            }
        }

        public bool TryGetTrackedPropertiesFor(Type forType, out IEnumerable<TrackedMediaProperty> properties)
        {
            properties = null;

            var allTrackedProps = GetAllTrackedProperties();
            if (allTrackedProps.ContainsKey(forType))
            {
                properties = allTrackedProps[forType];
                return true;
            }

            return false;
        }

        protected virtual Multimap<Type, TrackedMediaProperty> GetAllTrackedProperties()
        {
            var propsMap = _cache.Get(TrackedPropertiesKey, () =>
            {
                var map = new Multimap<Type, TrackedMediaProperty>();
                var props = _albumRegistry.GetAllAlbumsAsync().Await()
                    .Where(x => x.TrackedProperties != null && x.TrackedProperties.Length > 0)
                    .SelectMany(x => x.TrackedProperties);

                foreach (var prop in props)
                {
                    map.Add(prop.EntityType, prop);
                }

                return map;
            });

            return propsMap;
        }
    }
}