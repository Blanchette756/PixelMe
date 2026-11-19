using System.Windows.Media;

namespace PixelMe.Modules.Media;

public sealed record MediaState
{
    public static readonly Color DefaultAccent = Color.FromRgb(0xF4, 0x72, 0xB6);

    public static readonly MediaState Empty = new();

    public bool HasSession { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public ImageSource? Art { get; init; }

    public Color Accent { get; init; } = DefaultAccent;

    public bool IsPlaying { get; init; }

    public bool CanPlayPause { get; init; }

    public bool CanPrevious { get; init; }

    public bool CanNext { get; init; }

    public bool CanSeek { get; init; }

    public TimeSpan StartTime { get; init; }

    public TimeSpan Position { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTimeOffset PositionUpdatedAt { get; init; }

    public double Rate { get; init; } = 1;

    public IReadOnlyList<LyricLine>? Lyrics { get; init; }

    public TimeSpan CurrentPosition(DateTimeOffset now)
    {
        var position = Position;
        if (IsPlaying)
        {
            var elapsed = now - PositionUpdatedAt;
            if (elapsed > TimeSpan.Zero)
            {
                position += TimeSpan.FromTicks((long)(elapsed.Ticks * Rate));
            }
        }

        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
    }
}
