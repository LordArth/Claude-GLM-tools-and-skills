using System.Collections.Generic;

namespace OrderflowBattleEngine.Core;

public sealed record ResearchHypothesis(string Id, string Class, string Location, string Trigger, string Secondary, string Confirmation);

public static class HypothesisLibrary
{
    public static IReadOnlyList<ResearchHypothesis> Build180()
    {
        var result = new List<ResearchHypothesis>(180);
        int id = 1;
        string[] locations = { "prior-day extreme", "overnight extreme", "RTH/session extreme", "confirmed swing", "VWAP/value edge", "opening-range edge" };
        (string,string)[] longRev = {
            ("sell aggression anomaly","bullish absorption"), ("large sell event","trapped seller"),
            ("stacked bid imbalance","bid imbalance failure"), ("new low with weaker delta","bullish delta divergence"),
            ("repeated sell hits","replenishment-like bid defense") };
        (string,string)[] shortRev = {
            ("buy aggression anomaly","bearish absorption"), ("large buy event","trapped buyer"),
            ("stacked ask imbalance","ask imbalance failure"), ("new high with weaker delta","bearish delta divergence"),
            ("repeated buy lifts","replenishment-like ask defense") };
        string[] longConf = { "reclaim anchor", "close upper 35% of bar" };
        string[] shortConf = { "reject below anchor", "close lower 35% of bar" };

        AddReversals(result, ref id, "LONG_REV", locations, longRev, longConf);
        AddReversals(result, ref id, "SHORT_REV", locations, shortRev, shortConf);

        string[] contLocations = { "breakout of prior-day extreme", "breakout of overnight extreme", "opening-range breakout", "VWAP reclaim/reject", "fresh session extreme" };
        (string,string)[] longCont = { ("large buy accepted","pullback defends event price"), ("stacked ask imbalance accepted","imbalance zone holds"), ("delta expansion","POC migrates upward") };
        (string,string)[] shortCont = { ("large sell accepted","pullback rejects event price"), ("stacked bid imbalance accepted","imbalance zone holds"), ("negative delta expansion","POC migrates downward") };
        AddContinuations(result, ref id, "LONG_CONT", contLocations, longCont, new[]{"new high after retest","two closes accepted"});
        AddContinuations(result, ref id, "SHORT_CONT", contLocations, shortCont, new[]{"new low after retest","two closes accepted"});

        if (result.Count != 180) throw new System.InvalidOperationException($"Hypothesis library must contain 180 items, got {result.Count}.");
        return result;
    }

    private static void AddReversals(List<ResearchHypothesis> dst, ref int id, string cls, string[] locations, (string Trigger,string Secondary)[] flows, string[] confirms)
    {
        foreach (var location in locations)
        foreach (var flow in flows)
        foreach (var confirmation in confirms)
            dst.Add(new($"H{id++:000}", cls, location, flow.Trigger, flow.Secondary, confirmation));
    }

    private static void AddContinuations(List<ResearchHypothesis> dst, ref int id, string cls, string[] locations, (string Trigger,string Secondary)[] flows, string[] confirms)
        => AddReversals(dst, ref id, cls, locations, flows, confirms);
}
