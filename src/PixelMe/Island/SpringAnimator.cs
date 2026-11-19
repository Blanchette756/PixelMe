using System.Diagnostics;
using System.Windows.Media;

namespace PixelMe.Island;

public sealed class SpringAnimator
{
    private const double StepSeconds = 1.0 / 240;
    private const double MaxFrameSeconds = 1.0 / 20;
    private const double RestDistance = 0.01;
    private const double RestSpeed = 0.05;

    private readonly double[] values;
    private readonly double[] velocities;
    private readonly double[] targets;
    private readonly Action<double[]> apply;
    private readonly Stopwatch clock = new();
    private TimeSpan lastTick;
    private bool running;

    public SpringAnimator(double[] initial, Action<double[]> apply)
    {
        values = (double[])initial.Clone();
        targets = (double[])initial.Clone();
        velocities = new double[initial.Length];
        this.apply = apply;
        apply(values);
    }

    public double Stiffness { get; set; } = 420;

    public double Damping { get; set; } = 36;

    public void AnimateTo(double[] next)
    {
        CopyTargets(next);
        if (running)
        {
            return;
        }

        running = true;
        clock.Restart();
        lastTick = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    public void JumpTo(double[] next)
    {
        CopyTargets(next);
        Array.Copy(targets, values, values.Length);
        Array.Clear(velocities);
        Stop();
        apply(values);
    }

    private void CopyTargets(double[] next)
    {
        if (next.Length != targets.Length)
        {
            throw new ArgumentException("Target length does not match the animated values.", nameof(next));
        }

        Array.Copy(next, targets, targets.Length);
    }

    private void Stop()
    {
        if (!running)
        {
            return;
        }

        running = false;
        clock.Stop();
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = clock.Elapsed;
        var remaining = Math.Min((now - lastTick).TotalSeconds, MaxFrameSeconds);
        lastTick = now;
        if (remaining <= 0)
        {
            return;
        }

        while (remaining > 0)
        {
            var step = Math.Min(StepSeconds, remaining);
            remaining -= step;
            for (var i = 0; i < values.Length; i++)
            {
                var force = (-Stiffness * (values[i] - targets[i])) - (Damping * velocities[i]);
                velocities[i] += force * step;
                values[i] += velocities[i] * step;
            }
        }

        if (IsAtRest())
        {
            Array.Copy(targets, values, values.Length);
            Array.Clear(velocities);
            Stop();
        }

        apply(values);
    }

    private bool IsAtRest()
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (Math.Abs(values[i] - targets[i]) > RestDistance || Math.Abs(velocities[i]) > RestSpeed)
            {
                return false;
            }
        }

        return true;
    }
}
