// Compile this project against the ATAS assemblies installed on the trading PC.
// Exact installed SDK signatures are the final authority; this adapter intentionally keeps
// platform-specific code isolated from the deterministic Core engine.

using System;
using System.Collections.Generic;
using System.Linq;
using ATAS.Indicators;
using ATAS.Indicators.Technical;
using ATAS.DataFeedsCore;
using OrderflowBattleEngine.Core;

namespace OrderflowBattleEngine.ATAS;

public class OrderflowBattleIndicator : Indicator
{
    private readonly NarrativeEngine _engine = new();
    private readonly object _tradeGate = new();
    private readonly List<BigTradeEvent> _pendingBigTrades = new();

    public OrderflowBattleIndicator() : base(true)
    {
        DenyToChangePanel = true;
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar < 1) return;
        var c = GetCandle(bar);
        var levels = c.GetAllPriceLevels()
            .Select(x => new FootprintLevel(x.Price, x.Bid, x.Ask, x.Volume, x.Ticks))
            .OrderBy(x => x.Price)
            .ToArray();

        decimal poc = levels.Length == 0 ? c.Close : levels.OrderByDescending(x => x.Volume).First().Price;
        // ATR is intentionally zero until wired to a prior-only ATR series in the installed SDK build.
        var snapshot = new BarSnapshot(c.Time, c.Open, c.High, c.Low, c.Close, c.Bid, c.Ask, c.Delta,
            c.MaxDelta, c.MinDelta, c.VWAP, poc, 0m, levels);

        BigTradeEvent[] trades;
        lock (_tradeGate)
        {
            trades = _pendingBigTrades.Where(x => x.Time <= c.LastTime).ToArray();
            _pendingBigTrades.RemoveAll(x => x.Time <= c.LastTime);
        }

        // Closed-bar truth: only finalize bar N after bar N+1 exists.
        if (bar < CurrentBar - 1)
            _engine.OnClosedBar(snapshot, trades);

        // Rendering is deliberately separated from research truth. Wire arrows/text after
        // installed-ATAS compilation verifies the drawing API version.
    }

    // Wire these callbacks to the exact cumulative-trade overrides/events exposed by the
    // installed ATAS version. Official ATAS API supports historical cumulative-trade requests
    // and CumulativeTrade objects containing direction, volume, price and timestamps.
    private void AcceptCumulativeTrade(DateTime time, bool isBuy, decimal volume, decimal firstPrice, decimal lastPrice)
    {
        var lo = Math.Min(firstPrice, lastPrice); var hi = Math.Max(firstPrice, lastPrice);
        var evt = new BigTradeEvent(Guid.NewGuid(), time, isBuy ? FlowSide.Buy : FlowSide.Sell,
            volume, lo, hi, (lo + hi) / 2m, 0.0);
        lock (_tradeGate) _pendingBigTrades.Add(evt);
    }
}
