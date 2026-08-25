using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record HypothesisResult(
    string HypothesisId,
    int N,
    int Wins,
    double HitRate,
    double MeanTicks,
    double ProfitFactor,
    decimal TotalTicks,
    double StressMeanTicks,
    double TopThreePositiveShare,
    bool MinimumSampleReached);

public sealed record MatchedControlResult(
    int N,
    SideStatistics ResearchDirection,
    SideStatistics PriceOnlyDirection,
    double MeanIncrementTicks,
    decimal TotalIncrementTicks,
    int ResearchWinsPairwise,
    int ControlWinsPairwise,
    int Ties);

public sealed class HypothesisScreeningRunner
{
    private readonly ProspectiveProtocol _protocol;

    public HypothesisScreeningRunner(ProspectiveProtocol? protocol = null)
        => _protocol = protocol ?? ProspectiveProtocol.FrozenV1;

    public IReadOnlyList<HypothesisResult> Screen(
        ProspectiveEvaluation evaluation,
        IReadOnlyList<ResearchEvent> events,
        int minimumSample = 20)
    {
        var byId = events.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var primary = evaluation.Markouts
            .Where(x => x.HorizonBars == _protocol.PrimaryHorizon)
            .ToArray();

        var ids = events
            .SelectMany(x => x.HypothesisIds ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal);

        var results = new List<HypothesisResult>();
        foreach (var hypothesisId in ids)
        {
            var rows = primary.Where(m =>
                byId.TryGetValue(m.EventId, out var e) &&
                (e.HypothesisIds?.Contains(hypothesisId, StringComparer.Ordinal) ?? false)).ToArray();

            var stats = ProspectiveEvaluator.Stats(rows);
            var positives = rows.Select(x => x.PrimaryNetTicks).Where(x => x > 0).OrderByDescending(x => x).ToArray();
            decimal posTotal = positives.Sum();
            double concentration = posTotal <= 0 ? 1 : (double)(positives.Take(3).Sum() / posTotal);
            double stressMean = rows.Length == 0 ? 0 : (double)rows.Average(x => x.StressNetTicks);

            results.Add(new(
                hypothesisId,
                stats.N,
                stats.Wins,
                stats.HitRate,
                stats.MeanTicks,
                stats.ProfitFactor,
                stats.TotalTicks,
                stressMean,
                concentration,
                stats.N >= minimumSample));
        }

        return results
            .OrderByDescending(x => x.MinimumSampleReached)
            .ThenByDescending(x => x.MeanTicks)
            .ThenByDescending(x => x.ProfitFactor)
            .ThenByDescending(x => x.N)
            .ThenBy(x => x.HypothesisId, StringComparer.Ordinal)
            .ToArray();
    }

    public MatchedControlResult CompareWithPriceOnlyControl(
        IReadOnlyList<BarSnapshot> bars,
        IReadOnlyList<ResearchEvent> rawEvents)
    {
        var evaluator = new ProspectiveEvaluator(_protocol);
        var accepted = evaluator.ApplyOutcomeBlindCooldown(rawEvents);
        var research = new List<EventMarkout>();
        var control = new List<EventMarkout>();
        int rw = 0, cw = 0, ties = 0;
        decimal incrementTotal = 0;

        foreach (var e in accepted)
        {
            if (e.SignalBarIndex < 3 || e.SignalBarIndex >= bars.Count - 1) continue;
            int entryIndex = e.SignalBarIndex + 1;
            if (entryIndex >= bars.Count) continue;

            var priceOnlyDirection = ThreeBarPriceDirection(bars, e.SignalBarIndex);
            if (priceOnlyDirection == FlowSide.Unknown) continue;

            var r = MakePrimaryMarkout(bars, e, e.Direction);
            var c = MakePrimaryMarkout(bars, e, priceOnlyDirection);
            if (r is null || c is null) continue;

            research.Add(r);
            control.Add(c);
            decimal increment = r.PrimaryNetTicks - c.PrimaryNetTicks;
            incrementTotal += increment;
            if (increment > 0) rw++;
            else if (increment < 0) cw++;
            else ties++;
        }

        return new(
            research.Count,
            ProspectiveEvaluator.Stats(research),
            ProspectiveEvaluator.Stats(control),
            research.Count == 0 ? 0 : (double)(incrementTotal / research.Count),
            incrementTotal,
            rw,
            cw,
            ties);
    }

    private EventMarkout? MakePrimaryMarkout(IReadOnlyList<BarSnapshot> bars, ResearchEvent e, FlowSide direction)
    {
        if (direction == FlowSide.Unknown) return null;
        int end = e.SignalBarIndex + _protocol.PrimaryHorizon;
        if (end >= bars.Count) return null;

        decimal entry = e.EntryPrice;
        decimal mfe = 0, mae = 0;
        for (int i = e.SignalBarIndex + 1; i <= end; i++)
        {
            decimal fav = direction == FlowSide.Buy ? bars[i].High - entry : entry - bars[i].Low;
            decimal adv = direction == FlowSide.Buy ? entry - bars[i].Low : bars[i].High - entry;
            mfe = Math.Max(mfe, fav);
            mae = Math.Max(mae, adv);
        }

        decimal gross = (direction == FlowSide.Buy ? bars[end].Close - entry : entry - bars[end].Close) / _protocol.TickSize;
        return new(
            e.Id,
            e.DecisionTime,
            e.SessionId,
            direction,
            _protocol.PrimaryHorizon,
            gross,
            gross - _protocol.BaseCostTicks,
            gross - _protocol.PrimaryCostTicks,
            gross - _protocol.StressCostTicks,
            mfe / _protocol.TickSize,
            mae / _protocol.TickSize,
            false);
    }

    private static FlowSide ThreeBarPriceDirection(IReadOnlyList<BarSnapshot> bars, int signalIndex)
    {
        int start = signalIndex - 2;
        if (start < 0) return FlowSide.Unknown;
        decimal change = bars[signalIndex].Close - bars[start].Open;
        return change > 0 ? FlowSide.Buy : change < 0 ? FlowSide.Sell : FlowSide.Unknown;
    }
}
