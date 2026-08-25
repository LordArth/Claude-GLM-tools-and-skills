using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record TrackedBigTrade(BigTradeEvent Event, BigTradeDisposition Disposition, int AgeBars, decimal FavorableExcursion, decimal AdverseExcursion, int Retests, bool Defended);

public sealed class BigTradeMemory
{
    private readonly List<TrackedBigTrade> _events = new();
    public int ClassificationHorizonBars { get; init; } = 4;
    public int MaxAgeBars { get; init; } = 30;
    public decimal AcceptanceAtrFraction { get; init; } = .20m;
    public IReadOnlyList<TrackedBigTrade> Active => _events;

    public void Add(IEnumerable<BigTradeEvent> events)
    {
        foreach (var evt in events.Where(x => x.PriorPercentile >= .95))
        {
            if (_events.Any(x => x.Event.Id == evt.Id)) continue;
            _events.Add(new(evt, BigTradeDisposition.Pending, 0, 0, 0, 0, false));
        }
    }

    public IReadOnlyList<TrackedBigTrade> Update(BarSnapshot bar)
    {
        for (int i = 0; i < _events.Count; i++)
        {
            var t = _events[i];
            if (bar.Time < t.Event.Time) continue;

            decimal p = t.Event.RepresentativePrice;
            decimal acceptance = Math.Max(.25m, bar.Atr > 0 ? bar.Atr * AcceptanceAtrFraction : 1m);
            decimal fav = t.FavorableExcursion;
            decimal adv = t.AdverseExcursion;
            bool crossedBack;

            if (t.Event.Side == FlowSide.Buy)
            {
                fav = Math.Max(fav, bar.High - p);
                adv = Math.Max(adv, p - bar.Low);
                crossedBack = bar.Close < p;
            }
            else
            {
                fav = Math.Max(fav, p - bar.Low);
                adv = Math.Max(adv, bar.High - p);
                crossedBack = bar.Close > p;
            }

            bool retest = bar.Low <= t.Event.PriceHigh && bar.High >= t.Event.PriceLow && t.AgeBars > 0;
            int retests = t.Retests + (retest ? 1 : 0);
            bool sameSideClose = t.Event.Side == FlowSide.Buy ? bar.Close >= p : bar.Close <= p;
            bool defended = t.Defended || (retest && sameSideClose && t.AgeBars >= 1);

            var disposition = t.Disposition;
            if (disposition == BigTradeDisposition.Pending || disposition == BigTradeDisposition.Neutral)
            {
                if (crossedBack && adv >= acceptance && adv > fav)
                    disposition = BigTradeDisposition.Trapped;
                else if (fav >= acceptance && sameSideClose)
                    disposition = BigTradeDisposition.InitiativeAccepted;
                else if (t.AgeBars >= ClassificationHorizonBars && fav < acceptance)
                    disposition = BigTradeDisposition.Absorbed;
                else if (t.AgeBars >= ClassificationHorizonBars)
                    disposition = BigTradeDisposition.Neutral;
            }

            if (defended && disposition == BigTradeDisposition.InitiativeAccepted)
                disposition = BigTradeDisposition.Defended;

            _events[i] = t with
            {
                Disposition = disposition,
                AgeBars = t.AgeBars + 1,
                FavorableExcursion = fav,
                AdverseExcursion = adv,
                Retests = retests,
                Defended = defended
            };
        }

        _events.RemoveAll(x => x.AgeBars > MaxAgeBars);
        return _events;
    }

    public IEnumerable<TrackedBigTrade> Near(decimal price, decimal distance)
        => _events.Where(x => price >= x.Event.PriceLow - distance && price <= x.Event.PriceHigh + distance);
}
