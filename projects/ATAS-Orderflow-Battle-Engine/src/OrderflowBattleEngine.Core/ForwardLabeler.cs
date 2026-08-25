using System;
using System.Collections.Generic;

namespace OrderflowBattleEngine.Core;

public sealed record ForwardLabel(int HorizonBars, decimal Return, decimal Mfe, decimal Mae, bool PositiveMoveBeforeNegativeMove);

public sealed class ForwardLabeler
{
    public IReadOnlyList<ForwardLabel> Label(IReadOnlyList<BarSnapshot> bars, int signalIndex, FlowSide direction, decimal entryPrice, params int[] horizons)
    {
        var output = new List<ForwardLabel>();
        foreach (var h in horizons)
        {
            int end = Math.Min(bars.Count - 1, signalIndex + h);
            if (signalIndex < 0 || signalIndex >= bars.Count || end <= signalIndex) continue;

            decimal mfe = 0, mae = 0;
            bool? firstThresholdWasPositive = null;
            decimal threshold = Math.Max(.25m, bars[signalIndex].Atr > 0 ? bars[signalIndex].Atr * .25m : 1m);

            for (int i = signalIndex + 1; i <= end; i++)
            {
                var b = bars[i];
                decimal fav = direction == FlowSide.Buy ? b.High - entryPrice : entryPrice - b.Low;
                decimal adv = direction == FlowSide.Buy ? entryPrice - b.Low : b.High - entryPrice;
                mfe = Math.Max(mfe, fav);
                mae = Math.Max(mae, adv);

                if (firstThresholdWasPositive is null)
                {
                    bool hitFav = fav >= threshold;
                    bool hitAdv = adv >= threshold;
                    if (hitFav || hitAdv)
                        firstThresholdWasPositive = hitFav && !hitAdv;
                }
            }

            var last = bars[end].Close;
            decimal ret = direction == FlowSide.Buy ? last - entryPrice : entryPrice - last;
            output.Add(new(h, ret, mfe, mae, firstThresholdWasPositive == true));
        }
        return output;
    }
}
