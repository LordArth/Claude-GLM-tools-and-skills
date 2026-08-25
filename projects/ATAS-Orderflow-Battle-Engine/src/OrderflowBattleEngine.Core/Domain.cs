using System;
using System.Collections.Generic;

namespace OrderflowBattleEngine.Core;

public enum FlowSide { Unknown, Buy, Sell }
public enum StructuralBias { StrongBear=-2, Bear=-1, Neutral=0, Bull=1, StrongBull=2 }
public enum AuctionMode { Balanced, InitiativeUp, InitiativeDown, PullbackInUpAuction, PullbackInDownAuction, BullishReload, BearishReload, PotentialBullishReversal, PotentialBearishReversal, BreakoutAcceptanceUp, BreakoutAcceptanceDown, FailedBreakoutUp, FailedBreakoutDown, Transition }
public enum ZoneType { BuyerInventory, SellerInventory, Absorption, BigTrade, DefendedBigTrade, StackedImbalance, FailedAuction, BreakoutAcceptance, ValueEdge, HighVolumeNode, LowVolumeNode, SweepReclaim }
public enum ZoneStatus { Active, Defended, Weakening, Trapped, Invalidated, Resolved }
public enum DataAvailability { HistoricalConfirmed, HistoricalProxy, LiveOnly, Unavailable }

public sealed record FootprintLevel(decimal Price, decimal Bid, decimal Ask, decimal Volume, int Ticks)
{
    public decimal Delta => Ask - Bid;
}

public sealed record BarSnapshot(DateTime Time, decimal Open, decimal High, decimal Low, decimal Close, decimal Bid, decimal Ask, decimal Delta, decimal MaxDelta, decimal MinDelta, decimal Vwap, decimal Poc, decimal Atr, IReadOnlyList<FootprintLevel> Levels)
{
    public decimal Range => High - Low;
    public decimal TotalVolume => Bid + Ask;
    public decimal CloseLocation => Range <= 0 ? 0.5m : (Close - Low) / Range;
}

public sealed record BigTradeEvent(Guid Id, DateTime Time, FlowSide Side, decimal Volume, decimal PriceLow, decimal PriceHigh, decimal RepresentativePrice, double PriorPercentile, BigTradeDisposition Disposition = BigTradeDisposition.Pending);
public enum BigTradeDisposition { Pending, InitiativeAccepted, InitiativeFailed, Absorbed, Trapped, Defended, Neutral }

public sealed record MarketLeg(FlowSide Direction, DateTime StartTime, DateTime EndTime, decimal StartPrice, decimal EndPrice, decimal TotalVolume, decimal TotalDelta, double Efficiency, double PocMigration, int Bars);

public sealed record MemoryZone(Guid Id, ZoneType Type, decimal Low, decimal High, DateTime Created, double Strength, double DefenseResponseTrend, int Tests, ZoneStatus Status, FlowSide Owner);

public sealed record NarrativeTransition(DateTime Time, AuctionMode From, AuctionMode To, IReadOnlyList<string> Evidence, double Confidence);

public sealed record StoryScores(double BuyerControl, double SellerControl, double TrendIntegrity, double PullbackQuality, double Reload, double Reversal, double Continuation)
{
    public static StoryScores Neutral => new(50,50,50,0,0,0,0);
}

public sealed record MarketStoryState(DateTime Time, StructuralBias Bias, AuctionMode Mode, StoryScores Scores, IReadOnlyList<MarketLeg> RecentLegs, IReadOnlyList<MemoryZone> ActiveZones, IReadOnlyList<NarrativeTransition> Transitions, double BullConfidence, double BearConfidence, RegimeState? Regime = null)
{
    public static MarketStoryState Empty(DateTime time) => new(time, StructuralBias.Neutral, AuctionMode.Balanced, StoryScores.Neutral, Array.Empty<MarketLeg>(), Array.Empty<MemoryZone>(), Array.Empty<NarrativeTransition>(), 0, 0, null);
}
