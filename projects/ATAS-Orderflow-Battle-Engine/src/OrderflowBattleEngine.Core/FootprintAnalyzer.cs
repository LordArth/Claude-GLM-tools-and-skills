using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record FootprintFeatures(int AskImbalances, int BidImbalances, int MaxAskStack, int MaxBidStack, double BullAbsorption, double BearAbsorption, double BullExhaustion, double BearExhaustion, decimal Poc);

public sealed class FootprintAnalyzer
{
    public double ImbalanceRatio { get; init; } = 3.0;
    public decimal MinComparedVolume { get; init; } = 10m;

    public FootprintFeatures Analyze(BarSnapshot bar)
    {
        var levels = bar.Levels.OrderBy(x => x.Price).ToArray();
        if (levels.Length < 2) return new(0,0,0,0,0,0,0,0,bar.Poc);
        int ask=0,bid=0,askStack=0,bidStack=0,maxAsk=0,maxBid=0;
        for (int i=1;i<levels.Length;i++)
        {
            var lower=levels[i-1]; var upper=levels[i];
            bool ai = lower.Bid >= MinComparedVolume && (double)(upper.Ask / lower.Bid) >= ImbalanceRatio;
            bool bi = upper.Ask >= MinComparedVolume && (double)(lower.Bid / upper.Ask) >= ImbalanceRatio;
            if(ai){ask++; maxAsk=Math.Max(maxAsk,++askStack);} else askStack=0;
            if(bi){bid++; maxBid=Math.Max(maxBid,++bidStack);} else bidStack=0;
        }

        int edge=Math.Max(1,levels.Length/4);
        var low=levels.Take(edge).ToArray(); var high=levels.Skip(levels.Length-edge).ToArray();
        decimal lowSell=low.Sum(x=>x.Bid), highBuy=high.Sum(x=>x.Ask);
        decimal total=Math.Max(1m,bar.TotalVolume);
        double lowerReject=(double)bar.CloseLocation;
        double upperReject=1.0-lowerReject;
        double bullAbs=Math.Clamp((double)(lowSell/total)*2.5*lowerReject,0,1);
        double bearAbs=Math.Clamp((double)(highBuy/total)*2.5*upperReject,0,1);

        double bullEx=Exhaustion(low.Reverse().Select(x=>(double)x.Bid).ToArray());
        double bearEx=Exhaustion(high.Select(x=>(double)x.Ask).ToArray());
        decimal poc=levels.OrderByDescending(x=>x.Volume).First().Price;
        return new(ask,bid,maxAsk,maxBid,bullAbs,bearAbs,bullEx,bearEx,poc);
    }

    private static double Exhaustion(double[] towardExtreme)
    {
        if(towardExtreme.Length<3) return 0;
        int declines=0;
        for(int i=1;i<towardExtreme.Length;i++) if(towardExtreme[i] < towardExtreme[i-1]) declines++;
        return (double)declines/(towardExtreme.Length-1);
    }
}
