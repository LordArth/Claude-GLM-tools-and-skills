using System;
using System.Collections.Generic;
using OrderflowBattleEngine.Core;

namespace OrderflowBattleEngine.Tests;

public static class PrimitiveTests
{
    public static void RunAll()
    {
        PriorOnlyStatisticDoesNotLeakCurrentObservation();
        BigTradeCanBecomeTrapped();
        SweepRequiresReclaimForFailedAuction();
        FamilyCapsPreventDoubleCounting();
        HypothesisLibraryContainsExactly180();
        ForwardLabelsRespectDirection();
    }

    private static void PriorOnlyStatisticDoesNotLeakCurrentObservation()
    {
        var s = new PriorOnlyStatistics();
        s.ObserveThenAdd(1); s.ObserveThenAdd(2); s.ObserveThenAdd(3);
        var o = s.ObserveThenAdd(100);
        if (o.PriorSampleCount != 3 || o.PriorPercentile < .99)
            throw new Exception("Prior-only percentile leaked current observation or ranked incorrectly.");
    }

    private static void BigTradeCanBecomeTrapped()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var bt = new BigTradeEvent(Guid.NewGuid(), t, FlowSide.Buy, 500, 100, 100, 100, .995);
        var bars = new[] { B(t.AddMinutes(1),100,101,97,98), B(t.AddMinutes(2),98,99,96,97) };
        var r = new BigTradeClassifier { MinAcceptanceMove = 1 }.Classify(bt, bars);
        if (r.Disposition != BigTradeDisposition.Trapped)
            throw new Exception($"Expected trapped buyer, got {r.Disposition}.");
    }

    private static void SweepRequiresReclaimForFailedAuction()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var fp = new FootprintFeatures(0,0,0,0,.8,0,0,0,99);
        var noReclaim = new AuctionFailureDetector().Analyze(B(t,100,100,95,96), 97, null, fp);
        if (!noReclaim.BullishSweep || noReclaim.BullishFailedAuction)
            throw new Exception("Sweep was incorrectly promoted to failed auction without reclaim.");
        var reclaim = new AuctionFailureDetector().Analyze(B(t,100,101,95,100), 97, null, fp);
        if (!reclaim.BullishFailedAuction)
            throw new Exception("Expected bullish failed-auction confirmation after reclaim + absorption.");
    }

    private static void FamilyCapsPreventDoubleCounting()
    {
        var s = new ScoreEngine().Calculate(new[] {
            new Evidence("A1", FlowSide.Buy, "aggression", 1, 10),
            new Evidence("A2", FlowSide.Buy, "aggression", 1, 10),
            new Evidence("A3", FlowSide.Buy, "aggression", 1, 10)
        });
        if (s.LongScore > 10.0001)
            throw new Exception("Correlated aggression evidence exceeded family cap.");
    }

    private static void HypothesisLibraryContainsExactly180()
    {
        if (HypothesisLibrary.Build180().Count != 180)
            throw new Exception("Research hypothesis library count changed.");
    }

    private static void ForwardLabelsRespectDirection()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var bars = new[] { B(t,100,101,99,100), B(t.AddMinutes(1),100,104,99,103), B(t.AddMinutes(2),103,106,102,105) };
        var labels = new ForwardLabeler().Label(bars, 0, FlowSide.Buy, 100, 2);
        if (labels.Count != 1 || labels[0].Return <= 0 || labels[0].Mfe <= labels[0].Mae)
            throw new Exception("Forward labeler did not preserve long-direction excursion semantics.");
    }

    private static BarSnapshot B(DateTime t, decimal o, decimal h, decimal l, decimal c)
    {
        var levels = new List<FootprintLevel> { new(l,100,30,130,10), new((h+l)/2,80,80,160,10), new(h,30,100,130,10) };
        return new(t,o,h,l,c,210,210,0,0,0,(h+l)/2,(h+l)/2,4,levels);
    }
}
