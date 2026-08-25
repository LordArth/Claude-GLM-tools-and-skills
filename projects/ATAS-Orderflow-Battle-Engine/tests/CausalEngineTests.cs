using System;
using OrderflowBattleEngine.Core;

namespace OrderflowBattleEngine.Tests;

public static class CausalEngineTests
{
    public static void RunAll()
    {
        ValidReloadChainPassesTemporalOrder();
        ResponseBeforeCauseIsRejected();
        SingleFamilyChainIsPenalized();
        AblationFlagsConcentratedMechanism();
    }

    private static void ValidReloadChainPassesTemporalOrder()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var c = new CausalEngine();
        c.Add(new(Guid.NewGuid(), t, CausalKind.Context, FlowSide.Sell, "SELL_CONTEXT", 70));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(1), CausalKind.Counterflow, FlowSide.Sell, "BUY_COUNTERFLOW_FAIL", 72));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(2), CausalKind.Trap, FlowSide.Sell, "TRAPPED_BUYER", 88));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(3), CausalKind.Rejection, FlowSide.Sell, "REJECT_HIGH", 84));
        var r = c.Evaluate(FlowSide.Sell, t.AddMinutes(3));
        if (!r.TemporalValid || !r.HasIndependentFamilies || !r.HasResponse)
            throw new Exception("Expected valid multi-family bearish causal chain.");
    }

    private static void ResponseBeforeCauseIsRejected()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var c = new CausalEngine();
        c.Add(new(Guid.NewGuid(), t, CausalKind.Confirmation, FlowSide.Sell, "EARLY_CONFIRM", 90));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(1), CausalKind.BigTrade, FlowSide.Sell, "BIG_BUY_EVENT", 80));
        var r = c.Evaluate(FlowSide.Sell, t.AddMinutes(2));
        if (r.TemporalValid)
            throw new Exception("Causal engine accepted response before cause.");
    }

    private static void SingleFamilyChainIsPenalized()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var c = new CausalEngine();
        c.Add(new(Guid.NewGuid(), t, CausalKind.Initiative, FlowSide.Sell, "DELTA1", 90));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(1), CausalKind.Counterflow, FlowSide.Sell, "DELTA2", 90));
        var r = c.Evaluate(FlowSide.Sell, t.AddMinutes(1));
        if (r.HasIndependentFamilies || r.Strength >= 72)
            throw new Exception("Correlated single-family chain was not penalized.");
    }

    private static void AblationFlagsConcentratedMechanism()
    {
        var t = new DateTime(2026,8,25,14,0,0,DateTimeKind.Utc);
        var c = new CausalEngine();
        c.Add(new(Guid.NewGuid(), t, CausalKind.Context, FlowSide.Sell, "CTX", 35));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(1), CausalKind.Trap, FlowSide.Sell, "TRAP", 100));
        c.Add(new(Guid.NewGuid(), t.AddMinutes(2), CausalKind.Rejection, FlowSide.Sell, "RESP", 35));
        var r = new CausalAblationEngine { SignalThreshold = 30, MaxSingleFamilyShare = .40 }.Evaluate(c, FlowSide.Sell, t.AddMinutes(2));
        if (!r.OverConcentrated)
            throw new Exception("Ablation failed to flag single-family concentration.");
    }
}
