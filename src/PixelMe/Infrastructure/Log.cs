using System.Text;

namespace PixelMe.Infrastructure;

public static class Log
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    private static string FilePath => Path.Combine(AppPaths.LogFolder, "pixelme.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(' ')
                .Append(level)
                .Append(' ')
                .Append(message);

            if (exception is not null)
            {
                line.AppendLine().Append(exception);
            }

            line.AppendLine();

            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogFolder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, line.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}
