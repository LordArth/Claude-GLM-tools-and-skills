using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record AblationResult(
    string RemovedFamily,
    double BaselineStrength,
    double AblatedStrength,
    double Contribution,
    bool SignalSurvives);

public sealed record CausalRobustnessReport(
    FlowSide Thesis,
    double BaselineStrength,
    IReadOnlyList<AblationResult> Ablations,
    double LargestSingleFamilyContribution,
    double TopTwoContributionShare,
    bool OverConcentrated,
    bool Robust);

public sealed class CausalAblationEngine
{
    public double SignalThreshold { get; init; } = 72;
    public double MaxSingleFamilyShare { get; init; } = .55;
    public double MaxTopTwoShare { get; init; } = .80;

    public CausalRobustnessReport Evaluate(CausalEngine source, FlowSide thesis, DateTime at)
    {
        var baseline = source.Evaluate(thesis, at);
        var events = source.Events.Where(x => x.Time <= at).ToArray();
        var families = events.Select(x => FamilyOf(x.Kind)).Distinct().ToArray();
        var results = new List<AblationResult>();

        foreach (var family in families)
        {
            var clone = new CausalEngine { MaxChainAge = source.MaxChainAge, MaxEvents = source.MaxEvents };
            clone.AddRange(events.Where(x => FamilyOf(x.Kind) != family));
            var ablated = clone.Evaluate(thesis, at);
            results.Add(new(family, baseline.Strength, ablated.Strength,
                Math.Max(0, baseline.Strength - ablated.Strength), ablated.Strength >= SignalThreshold));
        }

        double total = results.Sum(x => x.Contribution);
        var ordered = results.OrderByDescending(x => x.Contribution).ToArray();
        double largest = ordered.FirstOrDefault()?.Contribution ?? 0;
        double topTwo = ordered.Take(2).Sum(x => x.Contribution);
        double largestShare = total <= 1e-9 ? 0 : largest / total;
        double topTwoShare = total <= 1e-9 ? 0 : topTwo / total;
        bool concentrated = largestShare > MaxSingleFamilyShare || topTwoShare > MaxTopTwoShare;
        bool robust = baseline.Strength >= SignalThreshold && !concentrated && results.Count(x => !x.SignalSurvives) <= Math.Max(1, results.Count / 3);

        return new(thesis, baseline.Strength, results, largestShare, topTwoShare, concentrated, robust);
    }

    private static string FamilyOf(CausalKind kind) => kind switch
    {
        CausalKind.Context => "context",
        CausalKind.Initiative or CausalKind.Counterflow => "flow",
        CausalKind.Absorption or CausalKind.Exhaustion or CausalKind.Defense => "passive_response",
        CausalKind.BigTrade or CausalKind.Trap => "inventory",
        CausalKind.Sweep or CausalKind.Reclaim => "auction",
        CausalKind.ValueMigration => "value",
        CausalKind.Divergence => "divergence",
        CausalKind.StructureDamage => "structure",
        CausalKind.Acceptance or CausalKind.Rejection or CausalKind.Confirmation => "response",
        CausalKind.Invalidation => "invalidation",
        _ => kind.ToString()
    };
}
