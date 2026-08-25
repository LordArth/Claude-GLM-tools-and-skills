# Prospective Evaluator Freeze V1

Frozen: 2026-08-25
Scope: NQ/MNQ order-flow research only; manual decision support; no order execution.

## Purpose
This protocol is frozen before using newly arriving outcomes. Its job is to prevent threshold and evaluation drift after seeing results. The exposed 22-event sample is hypothesis-generating only and must not be used to tune this protocol.

## Frozen event semantics
- Decision exists only after a completed closed bar.
- Entry reference is the next bar's open; this is a research reference, not an asserted fill.
- Frozen horizons: 5, 10, 20, 40 bars.
- Primary horizon: 20 bars.
- Tick size: 0.25 points.
- Cost scenarios: 2 ticks BASE, 4 ticks PRIMARY, 8 ticks STRESS round trip.
- Hit: PRIMARY net ticks > 0. Zero is a tie/non-win.
- BUY and SELL statistics are always reported separately and together.
- Wilson 95% interval is reported for hit rate.

## Frozen cluster rule
Use outcome-blind first-signal retention with a 20-bar cooldown. Sort by signal bar, decision time, then event ID. Keep the first event; reject later events until 20 bars have elapsed. Score and future outcome never influence retention.

## Frozen control
For each accepted event with sufficient history, create a matched three-bar price-direction control at the same event and same entry/horizon/cost. The control direction is the sign of price change from the open three bars back to the signal-bar close. Report paired mean increment and pairwise wins/losses/ties.

## Promotion gates
All must pass simultaneously:
1. >= 50 completed primary-horizon events.
2. >= 15 BUY events.
3. >= 15 SELL events.
4. >= 10 complete sessions.
5. Top-five positive-event share <= 50%.
6. Best positive-session share <= 35%.
7. Primary 20-bar PRIMARY-cost profit factor >= 1.25.
8. STRESS-cost mean expectancy > 0.
9. BUY mean PRIMARY expectancy > 0.
10. SELL mean PRIMARY expectancy > 0.
11. Every frozen non-primary horizon has positive mean expectancy after PRIMARY cost.

Passing these gates is necessary but not sufficient for production promotion. It only authorizes the next research stage: realistic stop/target/adverse-first/fill simulation, chronological calibration, robustness/ablation, implementation parity and untouched prospective evaluation.

## Hypothesis screening
The 180 hypotheses may be screened only on frozen prospective observations. Each result must report N, wins, hit rate, PRIMARY mean ticks, PF, total ticks, STRESS mean ticks and top-three positive-event concentration. A hypothesis with N < 20 is descriptive only and cannot be ranked as validated.

## Anti-overfitting rules
- Do not alter thresholds based on the exposed 22-event sample.
- Do not delete losing BUY events or promote SELL-only logic because of the exposed side asymmetry.
- Do not optimize horizon after seeing results; 20 bars remains primary.
- Do not replace the price-only matched control after seeing its performance.
- Do not remove the cooldown because overlapping events look profitable.
- Any future protocol revision increments the version and restarts the prospective evidence window.

## Engineering implementation
Core implementation lives in `ProspectiveEvaluator.cs` and `HypothesisScreeningRunner.cs`. Deterministic unit coverage lives in `tests/PrimitiveTests.cs` and must remain green in GitHub Actions.
