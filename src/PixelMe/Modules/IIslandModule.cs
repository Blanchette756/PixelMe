using System.Windows;

namespace PixelMe.Modules;

public interface IIslandModule : IDisposable
{
    event EventHandler? ActivityChanged;

    event EventHandler? AttentionRequested;

    string Id { get; }

    string DisplayName { get; }

    string Glyph { get; }

    int Priority { get; }

    bool ShowsTab { get; }

    bool HasActivity { get; }

    int ActivityRank { get; }

    double CompactWidth { get; }

    FrameworkElement CompactView { get; }

    FrameworkElement ExpandedView { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    void RefreshText();
}

public interface IFileDropTarget
{
    bool CanAccept(IDataObject data);

    void Accept(IDataObject data);
}

public abstract class IslandModule : IIslandModule
{
    private FrameworkElement? compactView;
    private FrameworkElement? expandedView;

    public event EventHandler? ActivityChanged;

    public event EventHandler? AttentionRequested;

    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public abstract string Glyph { get; }

    public abstract int Priority { get; }

    public virtual bool ShowsTab => true;

    public virtual bool HasActivity => false;

    public virtual int ActivityRank => Priority;

    public virtual double CompactWidth => 240;

    public FrameworkElement CompactView => compactView ??= BuildCompact();

    public FrameworkElement ExpandedView => expandedView ??= BuildExpanded();

    protected bool IsBuilt => expandedView is not null;

    public virtual Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual void RefreshText()
    {
        if (expandedView is not null)
        {
            Render();
        }
    }

    public virtual void Dispose()
    {
    }

    protected virtual FrameworkElement BuildCompact() => new System.Windows.Controls.Grid();

    protected abstract FrameworkElement BuildExpanded();

    protected virtual void Render()
    {
    }

    protected void RaiseActivityChanged() => ActivityChanged?.Invoke(this, EventArgs.Empty);

    protected void RequestAttention() => AttentionRequested?.Invoke(this, EventArgs.Empty);
}
