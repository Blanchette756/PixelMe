using PixelMe.Infrastructure;

namespace PixelMe.Native;

public sealed class HotkeyManager : IDisposable
{
    private readonly MessageWindow window;
    private readonly Dictionary<int, Action> actions = [];
    private int nextId = 0x5100;

    public HotkeyManager(MessageWindow window)
    {
        this.window = window;
        window.MessageReceived += OnMessage;
    }

    public int Register(uint modifiers, uint key, Action action)
    {
        var id = nextId++;
        if (!NativeMethods.RegisterHotKey(window.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, key))
        {
            Log.Warn($"Hotkey 0x{modifiers:X}+0x{key:X} is already used by another app");
            return 0;
        }

        actions[id] = action;
        return id;
    }

    public void Unregister(int id)
    {
        if (id != 0 && actions.Remove(id))
        {
            NativeMethods.UnregisterHotKey(window.Handle, id);
        }
    }

    public void Dispose()
    {
        window.MessageReceived -= OnMessage;
        foreach (var id in actions.Keys.ToList())
        {
            Unregister(id);
        }
    }

    private void OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error("Hotkey action failed", ex);
            }
        }
    }
}
