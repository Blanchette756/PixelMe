using System.Windows;

namespace PixelMe.Modules;

public interface IIslandModule : IDisposable
{
    event EventHandler? ActivityChanged;

    string Id { get; }

    string DisplayName { get; }

    string Glyph { get; }

    int Priority { get; }

    bool HasActivity { get; }

    double CompactWidth { get; }

    FrameworkElement CompactView { get; }

    FrameworkElement ExpandedView { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    void RefreshText();
}
