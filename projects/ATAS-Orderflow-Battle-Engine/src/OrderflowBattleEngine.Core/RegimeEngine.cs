using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public enum VolatilityRegime { Unknown, Low, Normal, High, Extreme }
public enum TrendRegime { Unknown, Range, Up, Down, Transition }
public enum SessionSegment { Unknown, Overnight, PreOpen, Open30, Open90, Midday, Final90, AfterHours }

public sealed record RegimeState(VolatilityRegime Volatility, TrendRegime Trend, SessionSegment Session, double AtrPercentile, double DirectionalEfficiency);

public sealed class RegimeEngine
{
    private readonly PriorOnlyStatistics _atrStats = new(1000);

    public TimeSpan RthOpen { get; init; } = new(9,30,0);
    public TimeSpan RthClose { get; init; } = new(16,0,0);

    public RegimeState Update(BarSnapshot bar, IReadOnlyList<MarketLeg> legs)
    {
        var atrObs = _atrStats.ObserveThenAdd((double)Math.Max(0, bar.Atr));
        var vol = atrObs.PriorSampleCount < 50 ? VolatilityRegime.Unknown
            : atrObs.PriorPercentile >= .97 ? VolatilityRegime.Extreme
            : atrObs.PriorPercentile >= .80 ? VolatilityRegime.High
            : atrObs.PriorPercentile <= .20 ? VolatilityRegime.Low
            : VolatilityRegime.Normal;

        var trend = TrendRegime.Unknown;
        double efficiency = 0;
        if (legs.Count > 0)
        {
            var current = legs[^1];
            efficiency = current.Efficiency;
            if (current.Bars < 2) trend = TrendRegime.Transition;
            else if (current.Efficiency < .35) trend = TrendRegime.Range;
            else trend = current.Direction == FlowSide.Buy ? TrendRegime.Up : current.Direction == FlowSide.Sell ? TrendRegime.Down : TrendRegime.Unknown;
        }

        return new(vol, trend, Segment(bar.Time.TimeOfDay), atrObs.PriorPercentile, efficiency);
    }

    private SessionSegment Segment(TimeSpan time)
    {
        if (time < RthOpen.Subtract(TimeSpan.FromHours(2))) return SessionSegment.Overnight;
        if (time < RthOpen) return SessionSegment.PreOpen;
        if (time < RthOpen.Add(TimeSpan.FromMinutes(30))) return SessionSegment.Open30;
        if (time < RthOpen.Add(TimeSpan.FromMinutes(90))) return SessionSegment.Open90;
        if (time < RthClose.Subtract(TimeSpan.FromMinutes(90))) return SessionSegment.Midday;
        if (time <= RthClose) return SessionSegment.Final90;
        return SessionSegment.AfterHours;
    }
}
