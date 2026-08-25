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
        DeltaDivergenceRecognizesLowerLowWithImprovingDelta();
        PocStallRecognizesPriceProbeWithoutValueMigration();
        SmallCounterCandleDoesNotFlipStructuralLeg();
        ProspectiveCooldownIsOutcomeBlindAndDeterministic();
        ProspectiveEvaluatorKeepsSidesSeparate();
        MatchedControlProducesPairedDiagnostics();
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

    private static void DeltaDivergenceRecognizesLowerLowWithImprovingDelta()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var prior = new[] {
            B(t,100,101,98,99, -800, 99),
            B(t.AddMinutes(1),99,100,97,98, -1400, 98),
            B(t.AddMinutes(2),98,100,98,99, -500, 99)
        };
        var current = B(t.AddMinutes(3),99,100,96,99, -400, 99);
        var r = new DeltaDivergenceDetector().Analyze(prior, current);
        if (!r.Bullish)
            throw new Exception("Expected bullish price/delta divergence on lower low with improving delta.");
    }

    private static void PocStallRecognizesPriceProbeWithoutValueMigration()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var prior = new[] { B(t,102,103,101,102,0,102), B(t.AddMinutes(1),102,103,101,102,0,102) };
        var current = B(t.AddMinutes(2),102,102,99,100,0,102);
        var r = new ValueMigrationDetector().Analyze(prior, current);
        if (!r.BullishStall)
            throw new Exception("Expected bullish POC stall when price drops but POC does not migrate down.");
    }

    private static void SmallCounterCandleDoesNotFlipStructuralLeg()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var tracker = new LegTracker { MinReversalPoints = 1m, ReversalAtrFraction = .55m };
        tracker.Update(B(t,100,104,99,104,800,102,4));
        tracker.Update(B(t.AddMinutes(1),104,109,103,108,900,107,4));
        tracker.Update(B(t.AddMinutes(2),108,109,106.5m,107, -200,107,4));
        if (tracker.CurrentDirection != FlowSide.Buy)
            throw new Exception("Small counter candle incorrectly flipped structural leg.");
    }

    private static void ProspectiveCooldownIsOutcomeBlindAndDeterministic()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var events = new[] {
            new ResearchEvent("B", t.AddMinutes(1), "S1", FlowSide.Buy, 11, 100, 80),
            new ResearchEvent("A", t, "S1", FlowSide.Sell, 10, 100, 40),
            new ResearchEvent("C", t.AddMinutes(30), "S1", FlowSide.Buy, 31, 100, 99)
        };
        var kept = new ProspectiveEvaluator().ApplyOutcomeBlindCooldown(events);
        if (kept.Count != 2 || kept[0].Id != "A" || kept[1].Id != "C")
            throw new Exception("Cooldown used score/outcome or failed deterministic first-event retention.");
    }

    private static void ProspectiveEvaluatorKeepsSidesSeparate()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var bars = new List<BarSnapshot>();
        decimal p = 100;
        for (int i=0;i<70;i++)
        {
            decimal next = i < 30 ? p + 1 : p - 1;
            bars.Add(B(t.AddMinutes(i), p, Math.Max(p,next)+1, Math.Min(p,next)-1, next));
            p = next;
        }
        var events = new[] {
            new ResearchEvent("L1", t.AddMinutes(5), "S1", FlowSide.Buy, 5, bars[6].Open, 80),
            new ResearchEvent("S1", t.AddMinutes(35), "S2", FlowSide.Sell, 35, bars[36].Open, 80)
        };
        var eval = new ProspectiveEvaluator(ProspectiveProtocol.FrozenV1 with { CooldownBars = 1 }).Evaluate(bars, events);
        if (eval.Buy.N != 1 || eval.Sell.N != 1)
            throw new Exception("Prospective evaluator merged or dropped side-specific statistics.");
    }

    private static void MatchedControlProducesPairedDiagnostics()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var bars = new List<BarSnapshot>();
        decimal p = 100;
        for (int i=0;i<60;i++)
        {
            decimal next = p + (i < 25 ? 1 : -1);
            bars.Add(B(t.AddMinutes(i), p, Math.Max(p,next)+1, Math.Min(p,next)-1, next));
            p = next;
        }
        var events = new[] { new ResearchEvent("E1", t.AddMinutes(10), "S1", FlowSide.Buy, 10, bars[11].Open, 75) };
        var r = new HypothesisScreeningRunner(ProspectiveProtocol.FrozenV1 with { CooldownBars = 1 }).CompareWithPriceOnlyControl(bars, events);
        if (r.N != 1)
            throw new Exception("Matched price-only control did not produce one paired observation.");
    }

    private static BarSnapshot B(DateTime t, decimal o, decimal h, decimal l, decimal c, decimal delta = 0, decimal? poc = null, decimal atr = 4)
    {
        var levels = new List<FootprintLevel> { new(l,100,30,130,10), new((h+l)/2,80,80,160,10), new(h,30,100,130,10) };
        decimal bid = 500m - delta / 2m;
        decimal ask = 500m + delta / 2m;
        return new(t,o,h,l,c,bid,ask,delta,delta,delta,(h+l)/2,poc ?? (h+l)/2,atr,levels);
    }
}
