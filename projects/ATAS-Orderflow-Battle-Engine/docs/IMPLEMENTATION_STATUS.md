# Implementation Status

## Implemented and previously CI-tested in the platform-neutral Core
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
- Big Trade response classifier and persistent Big Trade response memory
- family-capped score/contradiction engine
- session / trend / volatility regime model
- bullish/bearish reload story classification
- ARMED / CONFIRMED decision foundation
- deterministic JSONL research logger
- execution-neutral forward MFE/MAE labeling
- deterministic library of 180 research hypotheses
- dependency-free synthetic test runner
- GitHub Actions .NET 8 CI

## Newly implemented in this pass - prospective research discipline
- frozen `ProspectiveProtocol.FrozenV1`
- fixed 5/10/20/40-bar horizons with 20 bars primary
- fixed 2/4/8-tick BASE/PRIMARY/STRESS costs
- deterministic outcome-blind 20-bar first-signal cooldown
- BUY / SELL / aggregate statistics kept separate
- Wilson 95% hit-rate intervals
- primary profit factor, expectancy and total-tick metrics
- top-five positive-event concentration gate
- best-positive-session concentration gate
- minimum event / side / session promotion gates
- stress-expectancy gate
- side-expectancy gates
- frozen horizon-robustness gate
- matched three-bar price-direction control
- paired research-vs-control incremental outcome diagnostics
- 180-hypothesis screening output with sample-size and concentration diagnostics
- explicit prospective freeze document `docs/PROSPECTIVE_EVALUATOR_FREEZE_V1.md`
- deterministic tests for cooldown, side separation and matched control

These newest prospective-evaluator changes are committed and must be treated as unverified until the corresponding GitHub Actions run is observed green.

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
- research database (SQLite) in addition to JSONL
- feed the real ResearchDataBattle CSV/schema into the prospective evaluator without changing the frozen rules
- 180-hypothesis execution over fresh prospective historical/forward observations
- only after raw event gates survive: realistic stop/target/adverse-first/fill/latency simulation
- chronological calibration and untouched evaluation
- best-five deep optimization + ablations on training/calibration data only
- walk-forward validation and untouched final test
- score calibration
- no-repaint replay verification inside ATAS

## CI status
The Core project is continuously built and its deterministic tests run through `.github/workflows/atas-orderflow-core.yml`. CI previously caught and forced correction of a real footprint compile error. New Core changes are not trusted until that workflow is green.

## Truth statement
The attached 25 Aug ResearchDataBattle report is preliminary: 22 events across four sessions, strong SELL/weak BUY asymmetry, high concentration, and a matched price-only control with higher mean expectancy. The 63.6% 20-bar cost-adjusted hit rate is therefore not treated as a proven win rate and is not used to retune thresholds. Fresh prospective evidence is required before optimization or promotion.
