using System.Windows;

namespace PixelMe.Modules;

public interface IIslandModule : IDisposable
{
    string Id { get; }

    string DisplayName { get; }

    string Glyph { get; }

    int Priority { get; }

    FrameworkElement CompactView { get; }

    FrameworkElement ExpandedView { get; }

    Task InitializeAsync(CancellationToken cancellationToken);
}
