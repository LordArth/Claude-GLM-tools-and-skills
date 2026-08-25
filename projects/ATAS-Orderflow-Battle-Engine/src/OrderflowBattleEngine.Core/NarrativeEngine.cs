using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record EngineDecision(MarketStoryState Story, string State, FlowSide Direction, double Score, IReadOnlyList<string> Reasons, decimal Invalidation);

public sealed class NarrativeEngine
{
    private readonly FootprintAnalyzer _footprint = new();
    private readonly List<BarSnapshot> _bars = new();
    private readonly List<MarketLeg> _legs = new();
    private readonly List<MemoryZone> _zones = new();
    private readonly List<NarrativeTransition> _transitions = new();
    private MarketStoryState? _story;

    public MarketStoryState Story => _story ?? MarketStoryState.Empty(DateTime.MinValue);

    public EngineDecision OnClosedBar(BarSnapshot bar, IReadOnlyList<BigTradeEvent>? bigTrades = null)
    {
        _bars.Add(bar);
        if (_bars.Count > 500) _bars.RemoveAt(0);
        var fp = _footprint.Analyze(bar);
        UpdateLegs(bar);
        UpdateZones(bar, fp, bigTrades ?? Array.Empty<BigTradeEvent>());

        var prior = _story ?? MarketStoryState.Empty(bar.Time);
        var reasons = new List<string>();
        var impulse = _legs.Count >= 2 ? _legs[^2] : null;
        var current = _legs.Count >= 1 ? _legs[^1] : null;

        double bull=prior.BullConfidence*0.82, bear=prior.BearConfidence*0.82;
        if(current?.Direction==FlowSide.Buy){ bull+=18+Math.Min(12,current.Efficiency*8); reasons.Add("UP_LEG"); }
        if(current?.Direction==FlowSide.Sell){ bear+=18+Math.Min(12,current.Efficiency*8); reasons.Add("DOWN_LEG"); }
        if(fp.BullAbsorption>.45){bull+=18*fp.BullAbsorption; reasons.Add("ABS_BUY");}
        if(fp.BearAbsorption>.45){bear+=18*fp.BearAbsorption; reasons.Add("ABS_SELL");}
        if(fp.MaxAskStack>=3){bull+=8; reasons.Add("SAI");}
        if(fp.MaxBidStack>=3){bear+=8; reasons.Add("SBI");}

        bool bullPullback = impulse?.Direction==FlowSide.Buy && current?.Direction==FlowSide.Sell;
        bool bearPullback = impulse?.Direction==FlowSide.Sell && current?.Direction==FlowSide.Buy;
        double pullbackQuality=0,reload=0,damage=0;

        if(bullPullback && impulse is not null && current is not null)
        {
            pullbackQuality=PullbackQuality(impulse,current);
            if(pullbackQuality>55){bull+=15; reasons.Add("WEAK_SELL_PULLBACK");}
            if(IsInOwnedZone(bar.Close,FlowSide.Buy)){bull+=12; reasons.Add("BUYER_ZONE");}
            if(fp.BullAbsorption>.45){reload+=25;}
            reload += pullbackQuality*.45;
            damage = Math.Max(0, 100-pullbackQuality);
        }
        else if(bearPullback && impulse is not null && current is not null)
        {
            pullbackQuality=PullbackQuality(impulse,current);
            if(pullbackQuality>55){bear+=15; reasons.Add("WEAK_BUY_PULLBACK");}
            if(IsInOwnedZone(bar.Close,FlowSide.Sell)){bear+=12; reasons.Add("SELLER_ZONE");}
            if(fp.BearAbsorption>.45){reload+=25;}
            reload += pullbackQuality*.45;
            damage = Math.Max(0,100-pullbackQuality);
        }

        bull=Math.Clamp(bull,0,100); bear=Math.Clamp(bear,0,100); reload=Math.Clamp(reload,0,100);
        var bias = bull-bear>25?StructuralBias.StrongBull:bull-bear>10?StructuralBias.Bull:bear-bull>25?StructuralBias.StrongBear:bear-bull>10?StructuralBias.Bear:StructuralBias.Neutral;
        var mode = DetermineMode(prior.Mode,bullPullback,bearPullback,reload,damage,current);
        if(mode!=prior.Mode) _transitions.Add(new(bar.Time,prior.Mode,mode,reasons.ToArray(),Math.Max(bull,bear)/100.0));
        if(_transitions.Count>100) _transitions.RemoveAt(0);

        var scores=new StoryScores(bull,bear,Math.Clamp(100-damage,0,100),pullbackQuality,reload,Math.Clamp(damage,0,100),Math.Max(bull,bear));
        _story=new(bar.Time,bias,mode,scores,_legs.TakeLast(8).ToArray(),_zones.Where(z=>z.Status is ZoneStatus.Active or ZoneStatus.Defended or ZoneStatus.Weakening).ToArray(),_transitions.TakeLast(20).ToArray(),bull,bear);

        FlowSide dir=FlowSide.Unknown; double score=0; string state="NO_TRADE"; decimal invalidation=0;
        if(mode==AuctionMode.BullishReload && bull>=72 && bull-bear>=15){dir=FlowSide.Buy;score=bull;state=bull>=82?"CONFIRMED":"ARMED";invalidation=bar.Low;}
        else if(mode==AuctionMode.BearishReload && bear>=72 && bear-bull>=15){dir=FlowSide.Sell;score=bear;state=bear>=82?"CONFIRMED":"ARMED";invalidation=bar.High;}
        return new(_story,state,dir,score,reasons.Distinct().ToArray(),invalidation);
    }

    private static double PullbackQuality(MarketLeg impulse, MarketLeg counter)
    {
        decimal ir=Math.Abs(impulse.EndPrice-impulse.StartPrice), cr=Math.Abs(counter.EndPrice-counter.StartPrice);
        double rangeRatio=ir<=0?1:(double)(cr/ir);
        double volumeRatio=impulse.TotalVolume<=0?1:(double)(counter.TotalVolume/impulse.TotalVolume);
        double efficiencyRatio=impulse.Efficiency<=0?1:counter.Efficiency/impulse.Efficiency;
        return Math.Clamp(100-(rangeRatio*35+volumeRatio*30+efficiencyRatio*35),0,100);
    }

    private AuctionMode DetermineMode(AuctionMode prior,bool bullPb,bool bearPb,double reload,double damage,MarketLeg? current)
    {
        if(bullPb && reload>=55 && damage<55) return AuctionMode.BullishReload;
        if(bearPb && reload>=55 && damage<55) return AuctionMode.BearishReload;
        if(bullPb) return damage<60?AuctionMode.PullbackInUpAuction:AuctionMode.Transition;
        if(bearPb) return damage<60?AuctionMode.PullbackInDownAuction:AuctionMode.Transition;
        return current?.Direction==FlowSide.Buy?AuctionMode.InitiativeUp:current?.Direction==FlowSide.Sell?AuctionMode.InitiativeDown:AuctionMode.Balanced;
    }

    private void UpdateLegs(BarSnapshot b)
    {
        FlowSide dir=b.Close>b.Open?FlowSide.Buy:b.Close<b.Open?FlowSide.Sell:(_legs.LastOrDefault()?.Direction??FlowSide.Unknown);
        double effort=(double)Math.Max(1m,Math.Abs(b.Delta)); double eff=(double)(Math.Abs(b.Close-b.Open)/Math.Max(.25m,(decimal)effort/1000m));
        if(_legs.Count==0 || _legs[^1].Direction!=dir)
            _legs.Add(new(dir,b.Time,b.Time,b.Open,b.Close,b.TotalVolume,b.Delta,eff,0,1));
        else {
            var l=_legs[^1]; decimal vol=l.TotalVolume+b.TotalVolume; decimal delta=l.TotalDelta+b.Delta;
            double legEff=(double)(Math.Abs(b.Close-l.StartPrice)/Math.Max(.25m,Math.Abs(delta)/1000m));
            _legs[^1]=l with {EndTime=b.Time,EndPrice=b.Close,TotalVolume=vol,TotalDelta=delta,Efficiency=legEff,Bars=l.Bars+1};
        }
        if(_legs.Count>30) _legs.RemoveAt(0);
    }

    private void UpdateZones(BarSnapshot b, FootprintFeatures fp, IReadOnlyList<BigTradeEvent> trades)
    {
        foreach(var t in trades.Where(x=>x.PriorPercentile>=.99))
            _zones.Add(new(t.Id,ZoneType.BigTrade,t.PriceLow,t.PriceHigh,t.Time,70,0,0,ZoneStatus.Active,t.Side));
        if(fp.BullAbsorption>.65) _zones.Add(new(Guid.NewGuid(),ZoneType.Absorption,b.Low,b.Low+Math.Max(.25m,b.Atr*.08m),b.Time,65,0,0,ZoneStatus.Active,FlowSide.Buy));
        if(fp.BearAbsorption>.65) _zones.Add(new(Guid.NewGuid(),ZoneType.Absorption,b.High-Math.Max(.25m,b.Atr*.08m),b.High,b.Time,65,0,0,ZoneStatus.Active,FlowSide.Sell));
        for(int i=0;i<_zones.Count;i++){
            var z=_zones[i]; bool touch=b.Low<=z.High && b.High>=z.Low;
            if(!touch) continue;
            bool holds=z.Owner==FlowSide.Buy?b.Close>=z.Low:b.Close<=z.High;
            double trend=holds?z.DefenseResponseTrend+.1:z.DefenseResponseTrend-.3;
            _zones[i]=z with {Tests=z.Tests+1,DefenseResponseTrend=trend,Strength=Math.Clamp(z.Strength+(holds?3:-18),0,100),Status=!holds&&z.Strength<35?ZoneStatus.Invalidated:holds?ZoneStatus.Defended:ZoneStatus.Weakening};
        }
        _zones.RemoveAll(z=>z.Status==ZoneStatus.Invalidated || (b.Time-z.Created).TotalHours>30);
        if(_zones.Count>200) _zones.RemoveRange(0,_zones.Count-200);
    }

    private bool IsInOwnedZone(decimal p,FlowSide side)=>_zones.Any(z=>z.Owner==side && z.Status!=ZoneStatus.Invalidated && p>=z.Low && p<=z.High);
}
