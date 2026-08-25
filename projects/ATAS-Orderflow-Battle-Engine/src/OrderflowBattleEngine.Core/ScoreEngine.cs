using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record Evidence(string Code, FlowSide Side, string Family, double Strength, double Weight, bool Veto = false)
{
    public double RawContribution => Math.Clamp(Strength, 0, 1) * Math.Max(0, Weight);
}

public sealed record ScoreResult(double LongScore, double ShortScore, double Dominance, bool LongVeto, bool ShortVeto, IReadOnlyList<string> LongReasons, IReadOnlyList<string> ShortReasons);

public sealed class ScoreEngine
{
    private readonly Dictionary<string,double> _familyCaps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["context"] = 12,
        ["aggression"] = 10,
        ["imbalance"] = 12,
        ["absorption"] = 16,
        ["exhaustion"] = 8,
        ["auction"] = 15,
        ["bigtrade"] = 16,
        ["divergence"] = 10,
        ["response"] = 18,
        ["story"] = 18
    };

    public ScoreResult Calculate(IEnumerable<Evidence> evidence)
    {
        var items = evidence.ToArray();
        var longItems = items.Where(x => x.Side == FlowSide.Buy).ToArray();
        var shortItems = items.Where(x => x.Side == FlowSide.Sell).ToArray();

        double longScore = Score(longItems);
        double shortScore = Score(shortItems);
        bool longVeto = longItems.Any(x => x.Veto) || shortScore >= 78 && longScore >= 78;
        bool shortVeto = shortItems.Any(x => x.Veto) || shortScore >= 78 && longScore >= 78;

        return new(Math.Clamp(longScore,0,100), Math.Clamp(shortScore,0,100), longScore-shortScore,
            longVeto, shortVeto,
            longItems.Where(x=>!x.Veto).OrderByDescending(x=>x.RawContribution).Select(x=>x.Code).Distinct().Take(6).ToArray(),
            shortItems.Where(x=>!x.Veto).OrderByDescending(x=>x.RawContribution).Select(x=>x.Code).Distinct().Take(6).ToArray());
    }

    private double Score(IEnumerable<Evidence> evidence)
    {
        double total = 0;
        foreach (var family in evidence.Where(x=>!x.Veto).GroupBy(x=>x.Family, StringComparer.OrdinalIgnoreCase))
        {
            double cap = _familyCaps.TryGetValue(family.Key, out var c) ? c : 10;
            total += Math.Min(cap, family.Sum(x=>x.RawContribution));
        }
        return total;
    }
}
