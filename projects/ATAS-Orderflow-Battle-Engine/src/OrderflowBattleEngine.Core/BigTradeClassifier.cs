using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record BigTradeResponse(BigTradeEvent Event, BigTradeDisposition Disposition, decimal FavorableExcursion, decimal AdverseExcursion, bool CrossedBackThroughEvent, bool Accepted, IReadOnlyList<string> Reasons);

public sealed class BigTradeClassifier
{
    public int HorizonBars { get; init; } = 3;
    public decimal MinAcceptanceMove { get; init; } = 1m;

    public BigTradeResponse Classify(BigTradeEvent trade, IReadOnlyList<BarSnapshot> futureBars)
    {
        if (futureBars.Count == 0)
            return new(trade, BigTradeDisposition.Pending, 0, 0, false, false, Array.Empty<string>());

        var bars = futureBars.Take(Math.Max(1, HorizonBars)).ToArray();
        decimal p = trade.RepresentativePrice;
        decimal favorable = 0, adverse = 0;
        bool crossedBack = false;
        bool accepted = false;
        var reasons = new List<string>();

        foreach (var b in bars)
        {
            if (trade.Side == FlowSide.Buy)
            {
                favorable = Math.Max(favorable, b.High - p);
                adverse = Math.Max(adverse, p - b.Low);
                crossedBack |= b.Close < p;
                accepted |= b.Close >= p + MinAcceptanceMove;
            }
            else if (trade.Side == FlowSide.Sell)
            {
                favorable = Math.Max(favorable, p - b.Low);
                adverse = Math.Max(adverse, b.High - p);
                crossedBack |= b.Close > p;
                accepted |= b.Close <= p - MinAcceptanceMove;
            }
        }

        BigTradeDisposition disposition;
        if (accepted && favorable > adverse)
        {
            disposition = BigTradeDisposition.InitiativeAccepted;
            reasons.Add("ACCEPTED");
        }
        else if (crossedBack && adverse > favorable)
        {
            disposition = BigTradeDisposition.Trapped;
            reasons.Add(trade.Side == FlowSide.Buy ? "TRAPPED_BUYER" : "TRAPPED_SELLER");
        }
        else if (!accepted && favorable <= MinAcceptanceMove && adverse <= MinAcceptanceMove)
        {
            disposition = BigTradeDisposition.Absorbed;
            reasons.Add("POOR_PRICE_PROGRESS");
        }
        else if (!accepted && crossedBack)
        {
            disposition = BigTradeDisposition.InitiativeFailed;
            reasons.Add("FAILED_INITIATIVE");
        }
        else
        {
            disposition = BigTradeDisposition.Neutral;
        }

        return new(trade, disposition, favorable, adverse, crossedBack, accepted, reasons);
    }
}
