using System;
using System.Collections.Generic;
using OrderflowBattleEngine.Core;

namespace OrderflowBattleEngine.Tests;

public static class NarrativeEngineSyntheticTests
{
    // Lightweight dependency-free smoke tests. Convert to xUnit/NUnit when local SDK/build environment is available.
    public static void BullishImpulseThenWeakPullbackDoesNotInstantlyFlipBearish()
    {
        var e=new NarrativeEngine(); var t=new DateTime(2026,8,25,13,30,0,DateTimeKind.Utc);
        e.OnClosedBar(B(t,100,104,99,104,900,2100));
        e.OnClosedBar(B(t.AddMinutes(1),104,109,103,108,1000,2500));
        var d=e.OnClosedBar(B(t.AddMinutes(2),108,108,106,107,700,450));
        if(d.Story.Bias is StructuralBias.StrongBear or StructuralBias.Bear)
            throw new Exception("A weak one-bar pullback incorrectly destroyed bullish story state.");
    }

    public static void RunAll()=>BullishImpulseThenWeakPullbackDoesNotInstantlyFlipBearish();

    private static BarSnapshot B(DateTime t,decimal o,decimal h,decimal l,decimal c,decimal bid,decimal ask)
    {
        var mid=(h+l)/2m;
        var levels=new List<FootprintLevel>{new(l,bid*.35m,ask*.15m,(bid+ask)*.25m,20),new(mid,bid*.35m,ask*.35m,(bid+ask)*.35m,30),new(h,bid*.15m,ask*.35m,(bid+ask)*.25m,20)};
        return new(t,o,h,l,c,bid,ask,ask-bid,ask-bid,ask-bid,mid,mid,4m,levels);
    }
}
