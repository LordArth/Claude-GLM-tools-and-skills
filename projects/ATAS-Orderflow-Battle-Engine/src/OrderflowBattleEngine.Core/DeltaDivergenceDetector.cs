using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderflowBattleEngine.Core;

public sealed record DeltaDivergenceResult(bool Bullish, bool Bearish, double BullStrength, double BearStrength, IReadOnlyList<string> Reasons);

public sealed class DeltaDivergenceDetector
{
    public int LookbackBars { get; init; } = 12;
    public decimal MinimumPriceBreak { get; init; } = 0m;

    public DeltaDivergenceResult Analyze(IReadOnlyList<BarSnapshot> priorBars, BarSnapshot current)
    {
        if (priorBars.Count < 3)
            return new(false, false, 0, 0, Array.Empty<string>());

        var window = priorBars.TakeLast(Math.Min(LookbackBars, priorBars.Count)).ToArray();
        var priorLowBar = window.OrderBy(x => x.Low).First();
        var priorHighBar = window.OrderByDescending(x => x.High).First();

        bool priceLowerLow = current.Low < priorLowBar.Low - MinimumPriceBreak;
        bool priceHigherHigh = current.High > priorHighBar.High + MinimumPriceBreak;

        // Bullish divergence: price makes a new low, but current delta is less negative / more positive.
        bool bull = priceLowerLow && current.Delta > priorLowBar.Delta;
        bool bear = priceHigherHigh && current.Delta < priorHighBar.Delta;

        double bullStrength = 0, bearStrength = 0;
        var reasons = new List<string>();

        if (bull)
        {
            decimal deltaImprovement = current.Delta - priorLowBar.Delta;
            decimal denom = Math.Max(1m, Math.Abs(priorLowBar.Delta));
            bullStrength = Math.Clamp((double)(deltaImprovement / denom) * 70 + (double)current.CloseLocation * 30, 0, 100);
            reasons.Add("DDIV_BUY");
        }

        if (bear)
        {
            decimal deltaWeakening = priorHighBar.Delta - current.Delta;
            decimal denom = Math.Max(1m, Math.Abs(priorHighBar.Delta));
            bearStrength = Math.Clamp((double)(deltaWeakening / denom) * 70 + (double)(1m - current.CloseLocation) * 30, 0, 100);
            reasons.Add("DDIV_SELL");
        }

        return new(bull, bear, bullStrength, bearStrength, reasons);
    }
}
