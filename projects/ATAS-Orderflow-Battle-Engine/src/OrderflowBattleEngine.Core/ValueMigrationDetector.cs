using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record ValueMigrationResult(bool PocUp, bool PocDown, bool BullishStall, bool BearishStall, double Strength, IReadOnlyList<string> Reasons);

public sealed class ValueMigrationDetector
{
    public int LookbackBars { get; init; } = 5;

    public ValueMigrationResult Analyze(IReadOnlyList<BarSnapshot> priorBars, BarSnapshot current)
    {
        if (priorBars.Count == 0)
            return new(false, false, false, false, 0, Array.Empty<string>());

        var window = priorBars.TakeLast(Math.Min(LookbackBars, priorBars.Count)).ToArray();
        decimal avgPoc = window.Average(x => x.Poc);
        decimal avgClose = window.Average(x => x.Close);
        decimal pocMove = current.Poc - avgPoc;
        decimal priceMove = current.Close - avgClose;

        bool pocUp = pocMove > 0;
        bool pocDown = pocMove < 0;

        // Price probes lower while value refuses to migrate down => bullish stall.
        bool bullishStall = priceMove < 0 && !pocDown;
        // Price probes higher while value refuses to migrate up => bearish stall.
        bool bearishStall = priceMove > 0 && !pocUp;

        double strength = 0;
        var reasons = new List<string>();

        decimal scale = Math.Max(.25m, current.Atr > 0 ? current.Atr : Math.Max(.25m, current.Range));
        if (bullishStall)
        {
            strength = Math.Clamp((double)(Math.Abs(priceMove) / scale) * 50 + (double)current.CloseLocation * 50, 0, 100);
            reasons.Add("POC_STALL_BUY");
        }
        else if (bearishStall)
        {
            strength = Math.Clamp((double)(Math.Abs(priceMove) / scale) * 50 + (double)(1m - current.CloseLocation) * 50, 0, 100);
            reasons.Add("POC_STALL_SELL");
        }
        else if (pocUp) reasons.Add("POC_UP");
        else if (pocDown) reasons.Add("POC_DN");

        return new(pocUp, pocDown, bullishStall, bearishStall, strength, reasons);
    }
}
