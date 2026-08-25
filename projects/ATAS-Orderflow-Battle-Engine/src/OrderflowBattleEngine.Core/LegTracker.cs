using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed class LegTracker
{
    private readonly List<MarketLeg> _legs = new();
    private FlowSide _direction = FlowSide.Unknown;
    private DateTime _startTime;
    private decimal _startPrice;
    private decimal _extreme;
    private decimal _volume;
    private decimal _delta;
    private int _bars;

    public decimal MinReversalPoints { get; init; } = 1m;
    public decimal ReversalAtrFraction { get; init; } = 0.55m;
    public IReadOnlyList<MarketLeg> Legs => _legs;
    public FlowSide CurrentDirection => _direction;

    public IReadOnlyList<MarketLeg> Update(BarSnapshot bar)
    {
        if (_direction == FlowSide.Unknown)
        {
            _direction = bar.Close >= bar.Open ? FlowSide.Buy : FlowSide.Sell;
            Start(bar, _direction, bar.Open);
            UpdateCurrent(bar);
            return _legs;
        }

        decimal threshold = Math.Max(MinReversalPoints, bar.Atr > 0 ? bar.Atr * ReversalAtrFraction : MinReversalPoints);

        if (_direction == FlowSide.Buy)
        {
            _extreme = Math.Max(_extreme, bar.High);
            if (bar.Close <= _extreme - threshold)
            {
                FinalizeCurrent(bar.Time, _extreme);
                _direction = FlowSide.Sell;
                Start(bar, _direction, _extreme);
            }
        }
        else
        {
            _extreme = Math.Min(_extreme, bar.Low);
            if (bar.Close >= _extreme + threshold)
            {
                FinalizeCurrent(bar.Time, _extreme);
                _direction = FlowSide.Buy;
                Start(bar, _direction, _extreme);
            }
        }

        UpdateCurrent(bar);
        return _legs;
    }

    private void Start(BarSnapshot bar, FlowSide direction, decimal startPrice)
    {
        _startTime = bar.Time;
        _startPrice = startPrice;
        _extreme = direction == FlowSide.Buy ? Math.Max(startPrice, bar.High) : Math.Min(startPrice, bar.Low);
        _volume = 0;
        _delta = 0;
        _bars = 0;
        _legs.Add(new(direction, bar.Time, bar.Time, startPrice, bar.Close, 0, 0, 0, 0, 0));
        Trim();
    }

    private void UpdateCurrent(BarSnapshot bar)
    {
        _volume += bar.TotalVolume;
        _delta += bar.Delta;
        _bars++;
        _extreme = _direction == FlowSide.Buy ? Math.Max(_extreme, bar.High) : Math.Min(_extreme, bar.Low);
        decimal move = Math.Abs(bar.Close - _startPrice);
        double effort = Math.Max(1.0, (double)Math.Abs(_delta));
        double efficiency = (double)move / Math.Max(.25, effort / 1000.0);
        var current = _legs[^1];
        _legs[^1] = current with
        {
            EndTime = bar.Time,
            EndPrice = bar.Close,
            TotalVolume = _volume,
            TotalDelta = _delta,
            Efficiency = efficiency,
            Bars = _bars
        };
    }

    private void FinalizeCurrent(DateTime time, decimal endPrice)
    {
        if (_legs.Count == 0) return;
        var current = _legs[^1];
        _legs[^1] = current with { EndTime = time, EndPrice = endPrice };
    }

    private void Trim()
    {
        if (_legs.Count > 30)
            _legs.RemoveRange(0, _legs.Count - 30);
    }
}
