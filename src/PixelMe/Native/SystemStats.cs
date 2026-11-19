using System.Diagnostics;
using System.Runtime.InteropServices;
using PixelMe.Infrastructure;

namespace PixelMe.Native;

public readonly record struct StatsSnapshot(double Cpu, double Ram, double? Gpu, double UsedGb, double TotalGb);

public sealed class SystemStats : IDisposable
{
    private readonly List<PerformanceCounter> gpuCounters = [];
    private long lastIdle;
    private long lastTotal;
    private int samplesSinceRefresh = int.MaxValue;
    private bool gpuUnavailable;

    public StatsSnapshot Sample()
    {
        return new StatsSnapshot(SampleCpu(), SampleRam(out var used, out var total), SampleGpu(), used, total);
    }

    public void Dispose()
    {
        ClearGpu();
    }

    private double SampleCpu()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var total = kernel + user;
        var idleDelta = idle - lastIdle;
        var totalDelta = total - lastTotal;
        var first = lastTotal == 0;
        lastIdle = idle;
        lastTotal = total;
        return first || totalDelta <= 0 ? 0 : Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0, 100);
    }

    private static double SampleRam(out double usedGb, out double totalGb)
    {
        var status = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            usedGb = totalGb = 0;
            return 0;
        }

        totalGb = status.ullTotalPhys / 1073741824.0;
        usedGb = (status.ullTotalPhys - status.ullAvailPhys) / 1073741824.0;
        return status.dwMemoryLoad;
    }

    private double? SampleGpu()
    {
        if (gpuUnavailable)
        {
            return null;
        }

        try
        {
            if (++samplesSinceRefresh > 10)
            {
                RefreshGpuCounters();
            }

            var sum = 0.0;
            foreach (var counter in gpuCounters)
            {
                try
                {
                    sum += counter.NextValue();
                }
                catch (InvalidOperationException)
                {
                }
            }

            return Math.Clamp(sum, 0, 100);
        }
        catch (Exception ex)
        {
            Log.Warn("GPU usage is unavailable", ex);
            gpuUnavailable = true;
            ClearGpu();
            return null;
        }
    }

    private void RefreshGpuCounters()
    {
        samplesSinceRefresh = 0;
        ClearGpu();
        var category = new PerformanceCounterCategory("GPU Engine");
        foreach (var instance in category.GetInstanceNames().Where(n => n.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)))
        {
            var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, readOnly: true);
            counter.NextValue();
            gpuCounters.Add(counter);
        }
    }

    private void ClearGpu()
    {
        foreach (var counter in gpuCounters)
        {
            counter.Dispose();
        }

        gpuCounters.Clear();
    }
}
