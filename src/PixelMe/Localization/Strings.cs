using System.Globalization;
using PixelMe.Settings;

namespace PixelMe.Localization;

public static class Strings
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");
    private static bool useIndonesian;

    public static CultureInfo Culture => useIndonesian ? Indonesian : English;

    public static string Settings => T("Settings", "Pengaturan");

    public static string Exit => T("Exit", "Keluar");

    public static string StartWithWindows => T("Start with Windows", "Jalankan saat Windows menyala");

    public static string HideInFullscreen => T("Hide while a fullscreen app is open", "Sembunyikan saat ada aplikasi layar penuh");

    public static string OpenLogFolder => T("Open log folder", "Buka folder log");

    public static string ViewOnGitHub => T("View on GitHub", "Lihat di GitHub");

    public static string Appearance => T("Appearance", "Tampilan");

    public static string Behavior => T("Behavior", "Perilaku");

    public static string About => T("About", "Tentang");

    public static string Theme => T("Theme", "Tema");

    public static string ThemeGlass => T("Glass", "Kaca");

    public static string ThemeBlack => T("Black", "Hitam");

    public static string ThemeGraphite => "Graphite";

    public static string ThemeLight => T("Light", "Terang");

    public static string Style => T("Island style", "Gaya island");

    public static string StyleFloating => T("Floating island", "Island melayang");

    public static string StyleNotch => "Notch";

    public static string Language => T("Language", "Bahasa");

    public static string LanguageAuto => T("Follow Windows", "Ikuti Windows");

    public static string Version => T("Version", "Versi");

    public static string License => T(
        "PixelMe is free and open source software, licensed under the GNU GPL-3.0.",
        "PixelMe adalah software gratis dan open source dengan lisensi GNU GPL-3.0.");

    public static string StartupFailed => T(
        "PixelMe could not start. Details were written to the log folder.",
        "PixelMe gagal dijalankan. Detailnya tersimpan di folder log.");

    public static string Clock => T("Clock", "Jam");

    public static string Music => T("Music", "Musik");

    public static string NothingPlaying => T("Nothing playing", "Tidak ada yang diputar");

    public static string NothingPlayingHint => T(
        "Play something in Spotify, YouTube or any media app.",
        "Putar sesuatu di Spotify, YouTube, atau aplikasi musik lain.");

    public static string UnknownTitle => T("Unknown title", "Judul tidak diketahui");

    public static string Previous => T("Previous", "Sebelumnya");

    public static string Next => T("Next", "Berikutnya");

    public static string PlayPause => T("Play / pause", "Putar / jeda");

    public static string Mute => T("Mute", "Bisukan");

    public static string WebViewMissing => T(
        "The web drawer needs the Microsoft Edge WebView2 Runtime. Install it from microsoft.com/edge/webview2 and try again.",
        "Web drawer butuh Microsoft Edge WebView2 Runtime. Install dari microsoft.com/edge/webview2 lalu coba lagi.");

    public static string Back => T("Back", "Kembali");

    public static string Reload => T("Reload", "Muat ulang");

    public static string OpenInBrowser => T("Open in browser", "Buka di browser");

    public static string Close => T("Close", "Tutup");

    public static string ClipboardTitle => T("Clipboard", "Clipboard");

    public static string ClearUnpinned => T("Clear", "Bersihkan");

    public static string All => T("All", "Semua");

    public static string TextKind => T("Text", "Teks");

    public static string Links => T("Links", "Link");

    public static string Images => T("Images", "Gambar");

    public static string FilesKind => T("Files", "File");

    public static string ClipboardEmpty => T("Nothing copied yet", "Belum ada yang disalin");

    public static string ClipboardEmptyHint => T("Everything you copy shows up here. Click an item to copy it again.", "Semua yang kamu salin muncul di sini. Klik item untuk menyalin lagi.");

    public static string Image => T("Image", "Gambar");

    public static string Pin => T("Pin", "Sematkan");

    public static string Unpin => T("Unpin", "Lepas sematan");

    public static string Remove => T("Remove", "Hapus");

    public static string ClickToCopy => T("Click to copy", "Klik untuk menyalin");

    public static string FilesShelf => T("File shelf", "Rak file");

    public static string SendToPhone => T("Send to phone", "Kirim ke HP");

    public static string Clear => T("Clear", "Bersihkan");

    public static string FilesCount => T("{0} files", "{0} file");

    public static string ShelfEmpty => T("Drop files here", "Taruh file di sini");

    public static string ShelfEmptyHint => T("Drag files onto the island to keep them handy, then drag them into any app.", "Seret file ke island untuk disimpan sementara, lalu seret ke aplikasi mana pun.");

    public static string ShareNoFiles => T("Add files to the shelf first.", "Tambahkan file ke rak dulu.");

    public static string ScanWithPhone => T("Scan with your phone", "Scan pakai HP");

    public static string ScanHint => T("Your phone must be on the same Wi-Fi. The link works only while this code is on screen.", "HP harus di Wi-Fi yang sama. Link hanya aktif selama kode ini tampil.");

    public static string StopSharing => T("Stop sharing", "Berhenti berbagi");

    public static string ShareFailed => T("Sharing could not start. Check that you are connected to Wi-Fi.", "Berbagi gagal dimulai. Pastikan kamu terhubung ke Wi-Fi.");

    public static string RemindersTitle => T("Reminders", "Pengingat");

    public static string ReminderLabel => T("Remind me to…", "Ingatkan aku untuk…");

    public static string Minutes => T("min", "menit");

    public static string Add => T("Add", "Tambah");

    public static string NoReminders => T("No reminders. Pick a quick timer above.", "Belum ada pengingat. Pilih timer cepat di atas.");

    public static string Now => T("Now!", "Sekarang!");

    public static string Timer => T("Timer", "Timer");

    public static string Snooze => T("+5 min", "+5 menit");

    public static string RepeatToggle => T("Repeat", "Ulangi");

    public static string Done => T("Done", "Selesai");

    public static string Microphone => T("Microphone", "Mikrofon");

    public static string ToggleMic => T("Mute or unmute the microphone", "Bisukan atau nyalakan mikrofon");

    public static string Muted => T("Muted", "Dibisukan");

    public static string Live => T("Live", "Aktif");

    public static string MicMuted => T("Microphone muted", "Mikrofon dibisukan");

    public static string MicLive => T("Microphone on", "Mikrofon menyala");

    public static string NoCall => T("No app is using the microphone", "Tidak ada aplikasi yang memakai mikrofon");

    public static string InCallFor => T("In a call for {0}", "Sedang telepon {0}");

    public static string HotkeyOff => T("The Ctrl+Alt+M shortcut is turned off in Settings.", "Shortcut Ctrl+Alt+M dimatikan di Pengaturan.");

    public static string HotkeyTaken => T("Ctrl+Alt+M is already used by another app.", "Ctrl+Alt+M sudah dipakai aplikasi lain.");

    public static string HotkeyHint => T("Press Ctrl+Alt+M anywhere to mute your microphone in every app.", "Tekan Ctrl+Alt+M di mana saja untuk membisukan mikrofon di semua aplikasi.");

    public static string QuickControls => T("Quick controls", "Kontrol cepat");

    public static string PowerEfficiency => T("Efficiency", "Hemat");

    public static string PowerBalanced => T("Balanced", "Seimbang");

    public static string PowerPerformance => T("Performance", "Performa");

    public static string Volume => T("Volume", "Volume");

    public static string Brightness => T("Brightness", "Kecerahan");

    public static string PowerMode => T("Power mode", "Mode daya");

    public static string WiFi => "Wi-Fi";

    public static string Bluetooth => "Bluetooth";

    public static string NightLight => T("Night light", "Lampu malam");

    public static string Focus => T("Focus", "Fokus");

    public static string PowerUnavailable => T("Not available on this PC", "Tidak tersedia di PC ini");

    public static string Widgets => T("Widgets", "Widget");

    public static string WorldClock => T("World clock", "Jam dunia");

    public static string WeatherTitle => T("Weather", "Cuaca");

    public static string Battery => T("Battery", "Baterai");

    public static string Start => T("Start", "Mulai");

    public static string Pause => T("Pause", "Jeda");

    public static string Reset => T("Reset", "Reset");

    public static string Stopwatch => T("Stopwatch", "Stopwatch");

    public static string PcStats => T("PC stats", "Statistik PC");

    public static string AddTask => T("Add a task…", "Tambah tugas…");

    public static string Todo => T("To-do", "Tugas");

    public static string PhotosHint => T("Choose a photos folder in Settings.", "Pilih folder foto di Pengaturan.");

    public static string Charging => T("Charging", "Mengisi daya");

    public static string OnBattery => T("On battery", "Pakai baterai");

    public static string WeatherHint => T("Set your city in Settings.", "Atur kotamu di Pengaturan.");

    public static string WeatherFailed => T("Weather is unavailable.", "Cuaca tidak tersedia.");

    public static string NotificationsTitle => T("Notifications", "Notifikasi");

    public static string NotificationsUnsupported => T("Windows does not allow PixelMe to read notifications on this PC.", "Windows tidak mengizinkan PixelMe membaca notifikasi di PC ini.");

    public static string NotificationsOff => T("Show your Windows notifications in the island.", "Tampilkan notifikasi Windows di island.");

    public static string NotificationsDenied => T("Access was denied. Allow it in Windows Settings > Privacy > Notifications.", "Akses ditolak. Izinkan di Pengaturan Windows > Privasi > Notifikasi.");

    public static string Enable => T("Enable", "Aktifkan");

    public static string NoNotifications => T("No notifications yet.", "Belum ada notifikasi.");

    public static string AiAgents => T("AI agents", "AI agent");

    public static string CopyClaudeHook => T("Claude Code hook", "Hook Claude Code");

    public static string CopyCodexHook => T("Codex config", "Config Codex");

    public static string Copied => T("Copied!", "Tersalin!");

    public static string NeedsInput => T("needs your input", "butuh jawabanmu");

    public static string AgentsHint => T(
        "Copy the hook for your agent and paste it into its config (Claude Code: ~/.claude/settings.json, Codex: ~/.codex/config.toml). PixelMe will tell you when an agent is waiting for you.",
        "Salin hook untuk agent-mu lalu tempel ke config-nya (Claude Code: ~/.claude/settings.json, Codex: ~/.codex/config.toml). PixelMe akan memberi tahu saat agent menunggumu.");

    public static string AgentWaiting => T("Waiting", "Menunggu");

    public static string AgentWorking => T("Working", "Bekerja");

    public static string AgentDone => T("Done", "Selesai");

    public static string WebDrawer => T("Web", "Web");

    public static string SearchOrAddress => T("Search or type an address", "Cari atau ketik alamat");

    public static string Open => T("Open", "Buka");

    public static string StreamTitle => T("Stream", "Stream");

    public static string LiveChat => T("Live chat", "Live chat");

    public static string ChatHint => T("Add your Twitch channel or YouTube live link in Settings.", "Tambahkan channel Twitch atau link live YouTube di Pengaturan.");

    public static string ObsOff => T("Turn on the OBS connection in Settings.", "Nyalakan koneksi OBS di Pengaturan.");

    public static string Recording => T("Recording", "Merekam");

    public static string ObsConnected => T("Connected, not recording", "Terhubung, tidak merekam");

    public static string ObsConnecting => T("Connecting to OBS…", "Menghubungkan ke OBS…");

    public static string Fullscreen => T("When a fullscreen app is open", "Saat ada aplikasi layar penuh");

    public static string FullscreenStrip => T("Show a stats strip", "Tampilkan strip statistik");

    public static string FullscreenHide => T("Hide the island", "Sembunyikan island");

    public static string FullscreenStay => T("Keep the island", "Tetap tampilkan island");

    public static string MicHotkeySetting => T("Ctrl+Alt+M mutes the microphone", "Ctrl+Alt+M membisukan mikrofon");

    public static string LyricsSetting => T("Show synced lyrics (looks up songs on lrclib.net)", "Tampilkan lirik (mencari lagu di lrclib.net)");

    public static string Features => T("Features", "Fitur");

    public static string WeatherCity => T("Weather city (uses open-meteo.com)", "Kota cuaca (memakai open-meteo.com)");

    public static string WorldClocksSetting => T("World clocks, comma separated (e.g. Asia/Tokyo)", "Jam dunia, pisahkan dengan koma (mis. Asia/Tokyo)");

    public static string PhotosFolder => T("Photos folder", "Folder foto");

    public static string Browse => T("Browse…", "Pilih…");

    public static string PinnedSitesSetting => T("Pinned websites, one per line", "Situs yang disematkan, satu per baris");

    public static string TwitchChannel => T("Twitch channel", "Channel Twitch");

    public static string YouTubeLive => T("YouTube live link or video ID", "Link live YouTube atau ID video");

    public static string ObsEnable => T("Connect to OBS (Tools > WebSocket Server Settings)", "Hubungkan ke OBS (Tools > WebSocket Server Settings)");

    public static string ObsHost => T("OBS host", "Host OBS");

    public static string ObsPort => T("OBS port", "Port OBS");

    public static string ObsPassword => T("OBS password", "Password OBS");

    public static void Use(AppLanguage language)
    {
        useIndonesian = language switch
        {
            AppLanguage.Indonesian => true,
            AppLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "id",
        };
    }

    private static string T(string english, string indonesian) => useIndonesian ? indonesian : english;
}
