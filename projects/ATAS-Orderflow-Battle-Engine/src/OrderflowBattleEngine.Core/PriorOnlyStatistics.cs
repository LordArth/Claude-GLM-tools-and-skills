using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed class PriorOnlyStatistics
{
    private readonly int _capacity;
    private readonly Queue<double> _values = new();

    public PriorOnlyStatistics(int capacity = 1000) => _capacity = Math.Max(20, capacity);
    public int Count => _values.Count;
    public bool IsWarm(int minimumSamples = 100) => Count >= minimumSamples;
    public void Reset() => _values.Clear();

    public double PercentileOf(double value)
    {
        if (_values.Count == 0) return 0.5;
        int below = _values.Count(x => x < value);
        int equal = _values.Count(x => Math.Abs(x - value) < 1e-12);
        return Math.Clamp((below + 0.5 * equal) / _values.Count, 0, 1);
    }

    public double RobustZ(double value)
    {
        if (_values.Count < 5) return 0;
        var sorted = _values.OrderBy(x => x).ToArray();
        double median = Quantile(sorted, .5);
        var deviations = sorted.Select(x => Math.Abs(x - median)).OrderBy(x => x).ToArray();
        double mad = Quantile(deviations, .5);
        return mad <= 1e-12 ? 0 : 0.67448975 * (value - median) / mad;
    }

    public Observation ObserveThenAdd(double value)
    {
        var result = new Observation(value, PercentileOf(value), RobustZ(value), Count);
        _values.Enqueue(value);
        while (_values.Count > _capacity) _values.Dequeue();
        return result;
    }

    private static double Quantile(double[] sorted, double q)
    {
        if (sorted.Length == 0) return 0;
        double pos = (sorted.Length - 1) * q;
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}

public sealed record Observation(double Value, double PriorPercentile, double PriorRobustZ, int PriorSampleCount);
