using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public enum CausalKind
{
    Context,
    Initiative,
    Counterflow,
    Absorption,
    Exhaustion,
    BigTrade,
    Trap,
    Defense,
    Sweep,
    Reclaim,
    Acceptance,
    Rejection,
    ValueMigration,
    Divergence,
    StructureDamage,
    Confirmation,
    Invalidation
}

public sealed record CausalEvent(
    Guid Id,
    DateTime Time,
    CausalKind Kind,
    FlowSide Side,
    string Code,
    double Strength,
    decimal? Price = null,
    IReadOnlyList<Guid>? ParentIds = null,
    bool HistoricalConfirmed = true);

public sealed record CausalChain(
    FlowSide Thesis,
    double Strength,
    IReadOnlyList<CausalEvent> OrderedEvents,
    bool TemporalValid,
    bool HasIndependentFamilies,
    bool HasResponse,
    bool HasContradiction,
    string[] Reasons);

public sealed class CausalEngine
{
    private readonly List<CausalEvent> _events = new();
    public TimeSpan MaxChainAge { get; init; } = TimeSpan.FromMinutes(120);
    public int MaxEvents { get; init; } = 2000;

    public IReadOnlyList<CausalEvent> Events => _events;

    public void Add(CausalEvent evt)
    {
        _events.Add(evt);
        _events.Sort((a,b) => a.Time.CompareTo(b.Time));
        while (_events.Count > MaxEvents) _events.RemoveAt(0);
        var cutoff = evt.Time - MaxChainAge;
        _events.RemoveAll(x => x.Time < cutoff);
    }

    public void AddRange(IEnumerable<CausalEvent> events)
    {
        foreach (var e in events.OrderBy(x => x.Time)) Add(e);
    }

    public CausalChain Evaluate(FlowSide thesis, DateTime at)
    {
        var window = _events.Where(x => x.Time <= at && x.Time >= at - MaxChainAge).ToArray();
        var same = window.Where(x => x.Side == thesis || x.Side == FlowSide.Unknown).ToArray();
        var opp = window.Where(x => x.Side != FlowSide.Unknown && x.Side != thesis).ToArray();

        var selected = SelectNarrative(same, thesis);
        bool temporal = IsTemporalOrderValid(selected);
        bool response = selected.Any(x => x.Kind is CausalKind.Reclaim or CausalKind.Acceptance or CausalKind.Rejection or CausalKind.Confirmation);
        bool independent = selected.Select(x => FamilyOf(x.Kind)).Distinct().Count() >= 3;
        bool contradiction = HasStrongContradiction(selected, opp, at);

        double raw = selected.Sum(x => Math.Clamp(x.Strength,0,100) * Weight(x.Kind));
        double strength = Math.Clamp(raw / Math.Max(1.0, selected.Count * .70), 0, 100);
        if (!temporal) strength *= .25;
        if (!response) strength *= .65;
        if (!independent) strength *= .70;
        if (contradiction) strength *= .45;

        var reasons = selected.OrderByDescending(x => x.Strength * Weight(x.Kind)).Take(8).Select(x => x.Code).ToList();
        if (!temporal) reasons.Add("CAUSAL_ORDER_FAIL");
        if (!response) reasons.Add("NO_RESPONSE");
        if (!independent) reasons.Add("LOW_INDEPENDENCE");
        if (contradiction) reasons.Add("CONTRADICTION");

        return new(thesis, strength, selected, temporal, independent, response, contradiction, reasons.ToArray());
    }

    private static CausalEvent[] SelectNarrative(CausalEvent[] events, FlowSide thesis)
    {
        // Preserve chronology and choose the strongest event in each causal phase. This keeps the
        // engine from counting ten correlated delta observations as ten independent causes.
        var phases = events.GroupBy(e => FamilyOf(e.Kind))
            .Select(g => g.OrderByDescending(e => e.Strength).ThenBy(e => e.Time).First())
            .OrderBy(e => e.Time)
            .ToArray();
        return phases;
    }

    private static bool IsTemporalOrderValid(IReadOnlyList<CausalEvent> events)
    {
        if (events.Count == 0) return true;
        // Hard rule: confirmation/acceptance/reclaim cannot precede the evidence it is supposed to confirm.
        var firstCause = events.Where(x => x.Kind is CausalKind.Initiative or CausalKind.Counterflow or CausalKind.BigTrade or CausalKind.Sweep or CausalKind.Absorption)
            .Select(x => (DateTime?)x.Time).FirstOrDefault();
        var firstResponse = events.Where(x => x.Kind is CausalKind.Reclaim or CausalKind.Acceptance or CausalKind.Rejection or CausalKind.Confirmation)
            .Select(x => (DateTime?)x.Time).FirstOrDefault();
        if (firstCause.HasValue && firstResponse.HasValue && firstResponse.Value < firstCause.Value) return false;

        foreach (var e in events)
        {
            if (e.ParentIds is null || e.ParentIds.Count == 0) continue;
            foreach (var pid in e.ParentIds)
            {
                var parent = events.FirstOrDefault(x => x.Id == pid);
                if (parent is not null && parent.Time > e.Time) return false;
            }
        }
        return true;
    }

    private static bool HasStrongContradiction(IReadOnlyList<CausalEvent> selected, IReadOnlyList<CausalEvent> opposite, DateTime at)
    {
        var latestSupport = selected.Count == 0 ? DateTime.MinValue : selected.Max(x => x.Time);
        return opposite.Any(x => x.Time >= latestSupport.AddMinutes(-5)
            && x.Strength >= 70
            && x.Kind is CausalKind.Acceptance or CausalKind.StructureDamage or CausalKind.Trap or CausalKind.Confirmation);
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

    private static double Weight(CausalKind kind) => kind switch
    {
        CausalKind.Context => .35,
        CausalKind.Initiative => .75,
        CausalKind.Counterflow => .60,
        CausalKind.Absorption => 1.0,
        CausalKind.Exhaustion => .70,
        CausalKind.BigTrade => .55,
        CausalKind.Trap => 1.15,
        CausalKind.Defense => .90,
        CausalKind.Sweep => .60,
        CausalKind.Reclaim => 1.0,
        CausalKind.Acceptance => 1.10,
        CausalKind.Rejection => 1.0,
        CausalKind.ValueMigration => .80,
        CausalKind.Divergence => .70,
        CausalKind.StructureDamage => 1.0,
        CausalKind.Confirmation => 1.15,
        CausalKind.Invalidation => -1.25,
        _ => .5
    };
}
