using System;
using System.Collections.Generic;

namespace OrderflowBattleEngine.Core;

public sealed record AuctionFailureResult(bool BullishSweep, bool BearishSweep, bool BullishFailedAuction, bool BearishFailedAuction, double BullStrength, double BearStrength, IReadOnlyList<string> Reasons);

public sealed class AuctionFailureDetector
{
    public decimal ReclaimBuffer { get; init; } = 0m;

    public AuctionFailureResult Analyze(BarSnapshot current, decimal? referenceLow, decimal? referenceHigh, FootprintFeatures fp)
    {
        bool bullSweep = referenceLow.HasValue && current.Low < referenceLow.Value;
        bool bearSweep = referenceHigh.HasValue && current.High > referenceHigh.Value;
        bool bullReclaim = bullSweep && current.Close >= referenceLow!.Value + ReclaimBuffer;
        bool bearReclaim = bearSweep && current.Close <= referenceHigh!.Value - ReclaimBuffer;

        double bull = 0, bear = 0;
        var reasons = new List<string>();

        if (bullSweep) { bull += 15; reasons.Add("SWP_LOW"); }
        if (bearSweep) { bear += 15; reasons.Add("SWP_HIGH"); }
        if (bullReclaim) { bull += 35; reasons.Add("RCL_LOW"); }
        if (bearReclaim) { bear += 35; reasons.Add("RCL_HIGH"); }
        if (bullReclaim && fp.BullAbsorption >= .45) { bull += 25; reasons.Add("FA_BUY"); }
        if (bearReclaim && fp.BearAbsorption >= .45) { bear += 25; reasons.Add("FA_SELL"); }
        if (bullReclaim && current.CloseLocation >= .65m) bull += 15;
        if (bearReclaim && current.CloseLocation <= .35m) bear += 15;

        return new(bullSweep, bearSweep, bullReclaim && bull >= 60, bearReclaim && bear >= 60,
            Math.Clamp(bull,0,100), Math.Clamp(bear,0,100), reasons);
    }
}
