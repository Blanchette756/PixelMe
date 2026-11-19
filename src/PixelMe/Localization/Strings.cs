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
