# Implementation Status

## Implemented and CI-tested in the platform-neutral Core
- persistent `MarketStoryState`
- structural bias / auction mode state machine
- ATR-aware structural leg tracking (ordinary counter candles do not automatically flip trend)
- micro pullback detection inside a structural leg
- impulse-versus-pullback quality comparison
- persistent buyer/seller inventory, Big Trade and absorption zones
- zone retest, defense, weakening and invalidation state
- diagonal and stacked footprint imbalance scanning
- absorption heuristic
- exhaustion heuristic
- POC extraction and POC/value migration/stall logic
- prior-only delta divergence detector
- prior-only rolling percentile + robust MAD z-score utility
- sweep/reclaim + failed-auction detector (sweep alone is not promoted to failed auction)
- Big Trade response classifier: accepted / failed / absorbed / trapped / neutral
- family-capped score/contradiction engine
- session / trend / volatility regime model
- bullish/bearish reload story classification
- ARMED / CONFIRMED decision foundation
- deterministic JSONL research logger
- execution-neutral forward MFE/MAE labeling
- deterministic library of 180 research hypotheses
- dependency-free synthetic test runner
- GitHub Actions .NET 8 CI

## ATAS adapter implemented from current official API documentation
- `GetCandle(bar)` footprint extraction
- `GetAllPriceLevels()` price-level conversion
- candle Bid / Ask / Delta / MaxDelta / MinDelta / VWAP ingestion
- live `OnCumulativeTrade` handling
- `OnUpdateCumulativeTrade` handling without treating every update as a fresh event
- historical `RequestForCumulativeTrades`
- <=7-day historical request limitation respected using non-overlapping 6-day chunks
- `OnCumulativeTradesResponse` history ingestion
- chronological prior-only Big Trade percentile reconstruction
- guarded recalculation after cumulative-trade history arrives

## Still required before calling the ATAS indicator production-ready
The ATAS-specific project cannot be truthfully claimed compiled until it is built against the exact ATAS assemblies installed on the trading PC.

Remaining major work:
- wire real prior-only ATR into the ATAS adapter (currently Core supports ATR but adapter placeholder is zero)
- verify every ATAS cumulative-trade member/signature against installed assemblies
- optimize historical Big Trade lookup/bucketing for large datasets
- chart rendering: BUY/SELL/ARMED markers, reasons, zones and battle panel
- ATAS alerts
- session timezone/exchange-session validation
- richer value-area / session profile context from installed feed/API
- cumulative-delta state and divergence
- explicit Big Trade defense/retest classifier integrated into narrative state
- research database (SQLite) in addition to JSONL
- 180-hypothesis execution/screening harness over real historical data
- best-five deep optimization + ablations
- walk-forward validation and untouched final test
- score calibration
- no-repaint replay verification inside ATAS

## CI status
The Core project is continuously built and its deterministic tests run through `.github/workflows/atas-orderflow-core.yml`. CI has already caught and forced correction of a real footprint compile error, which is why new Core changes must stay green before being trusted.

## Truth statement
No profitability, win-rate or accuracy claim exists yet. Implementation progress is not evidence of trading edge. Real historical/replay research and out-of-sample validation remain mandatory.
