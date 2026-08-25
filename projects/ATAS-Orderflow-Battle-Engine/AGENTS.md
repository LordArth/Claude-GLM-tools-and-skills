# AGENTS.md

## Mission
Build and validate the ATAS Orderflow Battle Engine. Do not reduce it to independent candle signals. Every closed bar updates a persistent auction story.

## Mandatory architecture
- deterministic platform-neutral Core
- thin ATAS adapter
- closed-bar truth mode
- prior-only rolling normalization
- persistent directional legs and impulse/pullback comparisons
- inventory/defense zones with strength and weakening
- footprint imbalance, absorption, exhaustion and auction failure
- true cumulative-trade Big Trades where feed/API supports them
- accepted/absorbed/trapped/defended Big Trade classification
- event genealogy to prevent duplicate signals
- regime context and contradictions
- ARMED -> CONFIRMED state machine
- research logs and reproducibility

## Key scenario
The engine must be capable of representing:

`accepted bullish impulse -> controlled pullback -> weaker seller effort/efficiency -> retest buyer inventory -> absorption/trapped sellers -> reclaim -> BULLISH_RELOAD -> continuation candidate`

and the exact bearish mirror.

A red candle alone cannot flip a bullish story. A green candle alone cannot flip a bearish story.

## Truth rules
Never fabricate ATAS members, data availability, fills, backtests, win rate or profitability. Installed SDK signatures are authoritative. Live-only depth cannot contaminate historical tests. Missing data is unavailable, not zero. Do not optimize and validate on the same data.

## Work loop
Inspect -> implement -> compile -> test -> diagnose -> improve -> research -> ablate -> walk-forward -> final untouched test.

Do not stop because the first hypothesis fails. Preserve correct infrastructure, diagnose false positives and continue. Select the best five hypotheses by robust out-of-sample behavior and deeply optimize those before drawing conclusions.
