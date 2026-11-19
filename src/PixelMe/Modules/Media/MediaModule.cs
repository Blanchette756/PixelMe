using System.Windows;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Native.Audio;
using Windows.Foundation;
using Windows.Media.Control;

namespace PixelMe.Modules.Media;

public sealed class MediaModule(ModuleServices services) : IIslandModule
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly SystemVolume volume = new();
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private MediaPanel? panel;
    private MediaCompact? compact;
    private MediaState state = MediaState.Empty;
    private int propertiesVersion;
    private bool disposed;

    public event EventHandler? ActivityChanged;

    public event EventHandler? AttentionRequested
    {
        add
        {
        }

        remove
        {
        }
    }

    public bool ShowsTab => true;

    public int ActivityRank => 10;

    public string Id => "media";

    public string DisplayName => Strings.Music;

    public string Glyph => "";

    public int Priority => 10;

    public bool HasActivity => state.HasSession && state.IsPlaying;

    public double CompactWidth => 230;

    public FrameworkElement CompactView => compact ??= CreateCompact();

    public FrameworkElement ExpandedView => panel ??= CreatePanel();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        volume.Changed += (level, muted) => panel?.ShowVolume(level, muted);
        manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
        manager.CurrentSessionChanged += OnCurrentSessionChanged;
        manager.SessionsChanged += OnSessionsChanged;
        AttachBestSession();
    }

    public void RefreshText()
    {
        panel?.Render(state);
    }

    public void Dispose()
    {
        disposed = true;
        if (manager is not null)
        {
            manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            manager.SessionsChanged -= OnSessionsChanged;
        }

        DetachSession();
        volume.Dispose();
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession? candidate)
    {
        try
        {
            return candidate?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    private static MediaState ReadPlayback(GlobalSystemMediaTransportControlsSession source, MediaState into)
    {
        var info = source.GetPlaybackInfo();
        if (info is null)
        {
            return into;
        }

        var controls = info.Controls;
        return into with
        {
            IsPlaying = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            CanPlayPause = controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled,
            CanPrevious = controls.IsPreviousEnabled,
            CanNext = controls.IsNextEnabled,
            CanSeek = controls.IsPlaybackPositionEnabled,
            Rate = info.PlaybackRate is > 0 ? info.PlaybackRate.Value : 1,
        };
    }

    private static MediaState ReadTimeline(GlobalSystemMediaTransportControlsSession source, MediaState into)
    {
        var timeline = source.GetTimelineProperties();
        if (timeline is null)
        {
            return into;
        }

        var duration = timeline.EndTime - timeline.StartTime;
        var updated = timeline.LastUpdatedTime;
        var now = DateTimeOffset.Now;
        if (updated.Year < 2000 || updated > now.AddMinutes(1))
        {
            updated = now;
        }

        return into with
        {
            StartTime = timeline.StartTime,
            Duration = duration > TimeSpan.Zero ? duration : TimeSpan.Zero,
            Position = timeline.Position - timeline.StartTime,
            PositionUpdatedAt = updated,
        };
    }

    private MediaCompact CreateCompact()
    {
        var view = new MediaCompact();
        view.Render(state);
        return view;
    }

    private MediaPanel CreatePanel()
    {
        var view = new MediaPanel();
        view.PreviousRequested += () => Send(s => s.TrySkipPreviousAsync());
        view.PlayPauseRequested += () => Send(s => s.TryTogglePlayPauseAsync());
        view.NextRequested += () => Send(s => s.TrySkipNextAsync());
        view.SeekRequested += Seek;
        view.VolumeRequested += volume.SetLevel;
        view.MuteToggleRequested += volume.ToggleMute;
        view.Shown += volume.Refresh;
        view.Render(state);
        return view;
    }

    private void Post(Action action)
    {
        if (!disposed)
        {
            dispatcher.InvokeAsync(() =>
            {
                if (!disposed)
                {
                    action();
                }
            });
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        Post(AttachBestSession);
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        Post(AttachBestSession);
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        Post(() => _ = RefreshPropertiesAsync());
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        Post(() =>
        {
            if (session is not null)
            {
                Apply(Safe(() => ReadTimeline(session, ReadPlayback(session, state))));
            }

            AttachBestSession();
        });
    }

    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        Post(() =>
        {
            if (session is not null)
            {
                Apply(Safe(() => ReadTimeline(session, state)));
            }
        });
    }

    private GlobalSystemMediaTransportControlsSession? PickSession()
    {
        if (manager is null)
        {
            return null;
        }

        try
        {
            var current = manager.GetCurrentSession();
            if (IsPlaying(current))
            {
                return current;
            }

            var sessions = manager.GetSessions();
            return sessions.FirstOrDefault(IsPlaying) ?? current ?? sessions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warn("Listing media sessions failed", ex);
            return null;
        }
    }

    private void AttachBestSession()
    {
        var best = PickSession();
        if (ReferenceEquals(best, session))
        {
            return;
        }

        DetachSession();
        session = best;

        if (session is null)
        {
            Apply(MediaState.Empty);
            return;
        }

        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged += OnTimelineChanged;

        var fresh = MediaState.Empty with { HasSession = true };
        Apply(Safe(() => ReadTimeline(session, ReadPlayback(session, fresh)), fresh));
        _ = RefreshPropertiesAsync();
    }

    private void DetachSession()
    {
        if (session is null)
        {
            return;
        }

        try
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        catch (Exception ex)
        {
            Log.Warn("Detaching media session failed", ex);
        }

        session = null;
    }

    private async Task RefreshPropertiesAsync()
    {
        var target = session;
        var version = ++propertiesVersion;
        if (target is null)
        {
            return;
        }

        try
        {
            var properties = await target.TryGetMediaPropertiesAsync();
            if (version != propertiesVersion || disposed || properties is null)
            {
                return;
            }

            var artist = string.IsNullOrWhiteSpace(properties.Artist) ? properties.AlbumArtist : properties.Artist;
            Apply(state with { Title = properties.Title ?? string.Empty, Artist = artist ?? string.Empty });

            var artwork = await LoadArtworkAsync(properties);
            if (version != propertiesVersion || disposed)
            {
                return;
            }

            Apply(state with
            {
                Art = artwork?.Image,
                Accent = artwork?.Accent ?? MediaState.DefaultAccent,
                Lyrics = null,
            });

            if (services.Settings.Current.Lyrics)
            {
                var lyrics = await Lyrics.FindAsync(state.Artist, state.Title, state.Duration);
                if (version == propertiesVersion && !disposed)
                {
                    Apply(state with { Lyrics = lyrics });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Reading media details failed", ex);
        }
    }

    private static async Task<Artwork?> LoadArtworkAsync(GlobalSystemMediaTransportControlsSessionMediaProperties properties)
    {
        try
        {
            return await Artwork.LoadAsync(properties.Thumbnail);
        }
        catch (Exception ex)
        {
            Log.Warn("Loading album art failed", ex);
            return null;
        }
    }

    private MediaState Safe(Func<MediaState> read, MediaState? fallback = null)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            Log.Warn("Reading media state failed", ex);
            return fallback ?? state;
        }
    }

    private void Apply(MediaState next)
    {
        var wasActive = HasActivity;
        state = next;
        compact?.Render(state);
        panel?.Render(state);
        if (wasActive != HasActivity)
        {
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Seek(double fraction)
    {
        if (!state.CanSeek || state.Duration <= TimeSpan.Zero)
        {
            return;
        }

        var offset = TimeSpan.FromTicks((long)(state.Duration.Ticks * fraction));
        Apply(state with { Position = offset, PositionUpdatedAt = DateTimeOffset.Now });
        var target = (state.StartTime + offset).Ticks;
        Send(s => s.TryChangePlaybackPositionAsync(target));
    }

    private async void Send(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        var target = session;
        if (target is null)
        {
            return;
        }

        try
        {
            await command(target);
        }
        catch (Exception ex)
        {
            Log.Warn("Media command failed", ex);
        }
    }
}
