using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record PullbackSnapshot(bool IsCounterMove, FlowSide ParentDirection, int Bars, decimal Range, decimal Volume, decimal Delta, double Efficiency, double Quality);

public sealed class PullbackAnalyzer
{
    public int MaxLookbackBars { get; init; } = 8;

    public PullbackSnapshot Analyze(IReadOnlyList<BarSnapshot> bars, MarketLeg? parentLeg)
    {
        if (parentLeg is null || bars.Count < 2 || parentLeg.Direction == FlowSide.Unknown)
            return new(false, FlowSide.Unknown, 0, 0, 0, 0, 0, 0);

        int start = bars.Count - 1;
        int count = 0;
        decimal volume = 0, delta = 0;
        decimal high = bars[^1].High, low = bars[^1].Low;

        for (int i = bars.Count - 1; i > 0 && count < MaxLookbackBars; i--)
        {
            bool counter = parentLeg.Direction == FlowSide.Buy
                ? bars[i].Close < bars[i - 1].Close
                : bars[i].Close > bars[i - 1].Close;
            if (!counter) break;

            start = i;
            count++;
            volume += bars[i].TotalVolume;
            delta += bars[i].Delta;
            high = Math.Max(high, bars[i].High);
            low = Math.Min(low, bars[i].Low);
        }

        if (count == 0)
            return new(false, parentLeg.Direction, 0, 0, 0, 0, 0, 0);

        decimal range = parentLeg.Direction == FlowSide.Buy
            ? Math.Max(0, bars[start - 1].Close - bars[^1].Close)
            : Math.Max(0, bars[^1].Close - bars[start - 1].Close);
        double effort = Math.Max(1.0, (double)Math.Abs(delta));
        double efficiency = (double)range / Math.Max(.25, effort / 1000.0);

        decimal parentRange = Math.Max(.25m, Math.Abs(parentLeg.EndPrice - parentLeg.StartPrice));
        double rangeRatio = (double)(range / parentRange);
        double volumeRatio = parentLeg.TotalVolume <= 0 ? 1 : (double)(volume / parentLeg.TotalVolume);
        double efficiencyRatio = parentLeg.Efficiency <= 0 ? 1 : efficiency / parentLeg.Efficiency;

        // High quality means the counter move is relatively small, low-volume and inefficient.
        double quality = Math.Clamp(100 - (rangeRatio * 40 + volumeRatio * 30 + efficiencyRatio * 30), 0, 100);
        return new(true, parentLeg.Direction, count, range, volume, delta, efficiency, quality);
    }
}
