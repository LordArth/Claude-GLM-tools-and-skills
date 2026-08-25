using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record ProspectiveProtocol(
    decimal TickSize,
    int[] Horizons,
    int PrimaryHorizon,
    decimal BaseCostTicks,
    decimal PrimaryCostTicks,
    decimal StressCostTicks,
    int CooldownBars,
    int MinimumEvents,
    int MinimumPerSide,
    int MinimumSessions,
    double MaximumTopFivePositiveShare,
    double MaximumBestSessionPositiveShare,
    double MinimumPrimaryProfitFactor)
{
    public static ProspectiveProtocol FrozenV1 => new(
        TickSize: .25m,
        Horizons: new[] { 5, 10, 20, 40 },
        PrimaryHorizon: 20,
        BaseCostTicks: 2m,
        PrimaryCostTicks: 4m,
        StressCostTicks: 8m,
        CooldownBars: 20,
        MinimumEvents: 50,
        MinimumPerSide: 15,
        MinimumSessions: 10,
        MaximumTopFivePositiveShare: .50,
        MaximumBestSessionPositiveShare: .35,
        MinimumPrimaryProfitFactor: 1.25);
}

public sealed record ResearchEvent(
    string Id,
    DateTime DecisionTime,
    string SessionId,
    FlowSide Direction,
    int SignalBarIndex,
    decimal EntryPrice,
    double Score,
    IReadOnlyDictionary<string, double>? Features = null,
    IReadOnlyCollection<string>? HypothesisIds = null);

public sealed record EventMarkout(
    string EventId,
    DateTime DecisionTime,
    string SessionId,
    FlowSide Direction,
    int HorizonBars,
    decimal GrossTicks,
    decimal BaseNetTicks,
    decimal PrimaryNetTicks,
    decimal StressNetTicks,
    decimal MfeTicks,
    decimal MaeTicks,
    bool FavorableThresholdFirst);

public sealed record SideStatistics(
    int N,
    int Wins,
    double HitRate,
    (double Low, double High) Wilson95,
    double MeanTicks,
    double ProfitFactor,
    decimal TotalTicks);

public sealed record PromotionGate(string Name, bool Passed, string Detail);

public sealed record ProspectiveEvaluation(
    ProspectiveProtocol Protocol,
    IReadOnlyList<EventMarkout> Markouts,
    SideStatistics All,
    SideStatistics Buy,
    SideStatistics Sell,
    IReadOnlyList<PromotionGate> Gates,
    double TopFivePositiveShare,
    double BestSessionPositiveShare,
    bool PromotionEligible);

public sealed class ProspectiveEvaluator
{
    private readonly ProspectiveProtocol _protocol;
    private readonly ForwardLabeler _labeler = new();

    public ProspectiveEvaluator(ProspectiveProtocol? protocol = null)
        => _protocol = protocol ?? ProspectiveProtocol.FrozenV1;

    public ProspectiveEvaluation Evaluate(IReadOnlyList<BarSnapshot> bars, IReadOnlyList<ResearchEvent> rawEvents)
    {
        var accepted = ApplyOutcomeBlindCooldown(rawEvents);
        var markouts = new List<EventMarkout>();

        foreach (var e in accepted)
        {
            if (e.SignalBarIndex < 0 || e.SignalBarIndex >= bars.Count - 1) continue;
            if (e.Direction == FlowSide.Unknown) continue;

            var labels = _labeler.Label(bars, e.SignalBarIndex, e.Direction, e.EntryPrice, _protocol.Horizons);
            foreach (var l in labels)
            {
                decimal grossTicks = l.Return / _protocol.TickSize;
                markouts.Add(new(
                    e.Id, e.DecisionTime, e.SessionId, e.Direction, l.HorizonBars,
                    grossTicks,
                    grossTicks - _protocol.BaseCostTicks,
                    grossTicks - _protocol.PrimaryCostTicks,
                    grossTicks - _protocol.StressCostTicks,
                    l.Mfe / _protocol.TickSize,
                    l.Mae / _protocol.TickSize,
                    l.PositiveMoveBeforeNegativeMove));
            }
        }

        var primary = markouts.Where(x => x.HorizonBars == _protocol.PrimaryHorizon).ToArray();
        var all = Stats(primary);
        var buy = Stats(primary.Where(x => x.Direction == FlowSide.Buy));
        var sell = Stats(primary.Where(x => x.Direction == FlowSide.Sell));
        var topFiveShare = PositiveConcentration(primary, 5);
        var bestSessionShare = BestSessionPositiveShare(primary);
        var sessions = primary.Select(x => x.SessionId).Distinct(StringComparer.Ordinal).Count();

        bool horizonsPositive = _protocol.Horizons
            .Where(h => h != _protocol.PrimaryHorizon)
            .All(h => Mean(markouts.Where(x => x.HorizonBars == h).Select(x => x.PrimaryNetTicks)) > 0);

        var gates = new List<PromotionGate>
        {
            new("minimum-events", primary.Length >= _protocol.MinimumEvents, $"{primary.Length}/{_protocol.MinimumEvents}"),
            new("minimum-buy", buy.N >= _protocol.MinimumPerSide, $"{buy.N}/{_protocol.MinimumPerSide}"),
            new("minimum-sell", sell.N >= _protocol.MinimumPerSide, $"{sell.N}/{_protocol.MinimumPerSide}"),
            new("minimum-sessions", sessions >= _protocol.MinimumSessions, $"{sessions}/{_protocol.MinimumSessions}"),
            new("top-five-concentration", topFiveShare <= _protocol.MaximumTopFivePositiveShare, $"{topFiveShare:P1} <= {_protocol.MaximumTopFivePositiveShare:P0}"),
            new("session-concentration", bestSessionShare <= _protocol.MaximumBestSessionPositiveShare, $"{bestSessionShare:P1} <= {_protocol.MaximumBestSessionPositiveShare:P0}"),
            new("primary-profit-factor", all.ProfitFactor >= _protocol.MinimumPrimaryProfitFactor, $"{all.ProfitFactor:F3} >= {_protocol.MinimumPrimaryProfitFactor:F2}"),
            new("stress-expectancy", Mean(primary.Select(x => x.StressNetTicks)) > 0, $"mean={Mean(primary.Select(x => x.StressNetTicks)):F2} ticks"),
            new("side-expectancy-buy", buy.MeanTicks > 0, $"mean={buy.MeanTicks:F2}"),
            new("side-expectancy-sell", sell.MeanTicks > 0, $"mean={sell.MeanTicks:F2}"),
            new("horizon-robustness", horizonsPositive, "all non-primary frozen horizons positive after primary cost")
        };

        return new(_protocol, markouts, all, buy, sell, gates, topFiveShare, bestSessionShare, gates.All(x => x.Passed));
    }

    public IReadOnlyList<ResearchEvent> ApplyOutcomeBlindCooldown(IReadOnlyList<ResearchEvent> events)
    {
        var ordered = events.OrderBy(x => x.SignalBarIndex).ThenBy(x => x.DecisionTime).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var kept = new List<ResearchEvent>();
        int last = int.MinValue / 2;
        foreach (var e in ordered)
        {
            if (e.SignalBarIndex - last < _protocol.CooldownBars) continue;
            kept.Add(e);
            last = e.SignalBarIndex;
        }
        return kept;
    }

    public static SideStatistics Stats(IEnumerable<EventMarkout> source)
    {
        var rows = source.ToArray();
        int wins = rows.Count(x => x.PrimaryNetTicks > 0);
        decimal total = rows.Sum(x => x.PrimaryNetTicks);
        double mean = rows.Length == 0 ? 0 : (double)(total / rows.Length);
        decimal grossWin = rows.Where(x => x.PrimaryNetTicks > 0).Sum(x => x.PrimaryNetTicks);
        decimal grossLoss = -rows.Where(x => x.PrimaryNetTicks < 0).Sum(x => x.PrimaryNetTicks);
        double pf = grossLoss <= 0 ? (grossWin > 0 ? double.PositiveInfinity : 0) : (double)(grossWin / grossLoss);
        double p = rows.Length == 0 ? 0 : (double)wins / rows.Length;
        return new(rows.Length, wins, p, Wilson(wins, rows.Length), mean, pf, total);
    }

    public static (double Low, double High) Wilson(int wins, int n)
    {
        if (n <= 0) return (0, 0);
        const double z = 1.959963984540054;
        double p = (double)wins / n;
        double z2 = z * z;
        double denom = 1 + z2 / n;
        double center = (p + z2 / (2 * n)) / denom;
        double margin = z * Math.Sqrt((p * (1 - p) + z2 / (4 * n)) / n) / denom;
        return (Math.Max(0, center - margin), Math.Min(1, center + margin));
    }

    private static double PositiveConcentration(IEnumerable<EventMarkout> rows, int topN)
    {
        var positives = rows.Select(x => x.PrimaryNetTicks).Where(x => x > 0).OrderByDescending(x => x).ToArray();
        decimal total = positives.Sum();
        return total <= 0 ? 1 : (double)(positives.Take(topN).Sum() / total);
    }

    private static double BestSessionPositiveShare(IEnumerable<EventMarkout> rows)
    {
        var positives = rows.Where(x => x.PrimaryNetTicks > 0).ToArray();
        decimal total = positives.Sum(x => x.PrimaryNetTicks);
        if (total <= 0) return 1;
        decimal best = positives.GroupBy(x => x.SessionId).Select(g => g.Sum(x => x.PrimaryNetTicks)).DefaultIfEmpty(0).Max();
        return (double)(best / total);
    }

    private static double Mean(IEnumerable<decimal> values)
    {
        var a = values.ToArray();
        return a.Length == 0 ? 0 : (double)a.Average();
    }
}
