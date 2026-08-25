using System;
using System.Collections.Generic;
using System.Linq;
using ATAS.Indicators;
using OrderflowBattleEngine.Core;

namespace OrderflowBattleEngine.ATAS;

// This project must be compiled against the exact ATAS assemblies installed on the trading PC.
// The cumulative-trade hooks below follow the current official ATAS Indicator API.
public class OrderflowBattleIndicator : Indicator
{
    private readonly NarrativeEngine _engine = new();
    private readonly object _tradeGate = new();
    private readonly List<BigTradeEvent> _liveTrades = new();
    private readonly List<BigTradeEvent> _historicalTrades = new();
    private readonly HashSet<int> _pendingRequestIds = new();
    private readonly PriorOnlyStatistics _bigTradeVolume = new(3000);
    private bool _historicalLoaded;
    private bool _recalcRequestedAfterHistory;

    public OrderflowBattleIndicator() : base(true)
    {
        DenyToChangePanel = true;
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar < 1) return;

        var c = GetCandle(bar);
        var levels = c.GetAllPriceLevels()
            .Where(x => x != null)
            .Select(x => new FootprintLevel(x.Price, x.Bid, x.Ask, x.Volume, x.Ticks))
            .OrderBy(x => x.Price)
            .ToArray();

        decimal poc = levels.Length == 0 ? c.Close : levels.OrderByDescending(x => x.Volume).First().Price;
        var snapshot = new BarSnapshot(c.Time, c.Open, c.High, c.Low, c.Close, c.Bid, c.Ask, c.Delta,
            c.MaxDelta, c.MinDelta, c.VWAP, poc, 0m, levels);

        BigTradeEvent[] trades;
        lock (_tradeGate)
        {
            var source = _historicalLoaded ? _historicalTrades : _liveTrades;
            trades = source.Where(x => x.Time >= c.Time && x.Time <= c.LastTime).ToArray();
            if (!_historicalLoaded)
                _liveTrades.RemoveAll(x => x.Time <= c.LastTime);
        }

        // Closed-bar truth: the latest in-progress bar is not finalized.
        if (bar < CurrentBar - 1)
            _engine.OnClosedBar(snapshot, trades);
    }

    protected override void OnCumulativeTrade(CumulativeTrade trade) => UpsertLiveTrade(trade);

    protected override void OnUpdateCumulativeTrade(CumulativeTrade trade) => UpsertLiveTrade(trade);

    protected override void OnFinishRecalculate()
    {
        if (_historicalLoaded || CurrentBar <= 0)
            return;

        var from = GetCandle(0).Time;
        var end = GetCandle(CurrentBar - 1).LastTime;

        // ATAS currently limits a cumulative-trade request to <= 7 days.
        // Six-day non-overlapping chunks avoid duplicate boundary events.
        while (from <= end)
        {
            var to = from.AddDays(6);
            if (to > end) to = end;
            var request = new CumulativeTradesRequest(from, to, 0, 0);
            _pendingRequestIds.Add(request.RequestId);
            RequestForCumulativeTrades(request);
            if (to >= end) break;
            from = to.AddTicks(1);
        }
    }

    protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
    {
        if (!_pendingRequestIds.Remove(request.RequestId))
            return;

        lock (_tradeGate)
        {
            foreach (var trade in cumulativeTrades)
                _historicalTrades.Add(ConvertRawTrade(trade));
        }

        if (_pendingRequestIds.Count == 0)
        {
            lock (_tradeGate)
            {
                _historicalTrades.Sort((a,b) => a.Time.CompareTo(b.Time));
                _bigTradeVolume.Reset();
                for (int i = 0; i < _historicalTrades.Count; i++)
                {
                    var evt = _historicalTrades[i];
                    var observation = _bigTradeVolume.ObserveThenAdd((double)evt.Volume);
                    _historicalTrades[i] = evt with { PriorPercentile = observation.PriorPercentile };
                }
            }

            _historicalLoaded = true;

            // Re-run bars exactly once now that the historical event stream exists.
            if (!_recalcRequestedAfterHistory)
            {
                _recalcRequestedAfterHistory = true;
                RecalculateValues();
            }
        }
    }

    private void UpsertLiveTrade(CumulativeTrade trade)
    {
        var raw = ConvertRawTrade(trade);
        var percentile = _bigTradeVolume.PercentileOf((double)raw.Volume);
        var converted = raw with { PriorPercentile = percentile };

        lock (_tradeGate)
        {
            // Updates to one cumulative event replace it; only genuinely new events enter
            // the rolling distribution, avoiding update-volume double counting.
            int index = _liveTrades.FindIndex(x => x.Time == converted.Time && x.Side == converted.Side && x.PriceLow == converted.PriceLow);
            if (index >= 0)
            {
                _liveTrades[index] = converted;
            }
            else
            {
                _liveTrades.Add(converted);
                _bigTradeVolume.ObserveThenAdd((double)converted.Volume);
            }
        }
    }

    private static BigTradeEvent ConvertRawTrade(CumulativeTrade trade)
    {
        var lo = Math.Min(trade.FirstPrice, trade.Lastprice);
        var hi = Math.Max(trade.FirstPrice, trade.Lastprice);
        var side = trade.Direction == TradeDirection.Buy ? FlowSide.Buy : FlowSide.Sell;
        return new BigTradeEvent(Guid.NewGuid(), trade.Time, side, trade.Volume, lo, hi, (lo + hi) / 2m, 0.0);
    }
}
