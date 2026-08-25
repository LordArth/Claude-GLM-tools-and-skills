# SELL-ONLY LONG-HORIZON RESEARCH FREEZE V1

## Status

Research lane only. Not a production signal and not permission to modify the general indicator using the exposed 30-day outcomes.

## Why frozen separately

The exposed 30-day diagnostic showed:
- the general candidate engine is unstable
- BUY has no demonstrated positive edge
- SELL is stronger than BUY
- longer horizons looked better than the original 100-minute primary horizon
- the sample has severe temporal instability and winner concentration

Therefore the only justified action is to **freeze a new SELL-only hypothesis before new outcomes arrive**.

## Frozen hypothesis

A SELL candidate is eligible for this research lane only when the existing causal engine identifies a temporally valid bearish mechanism chain. Do not change thresholds based on the already-exposed 30-day outcomes.

Primary research horizon: 40 M5 bars / 200 minutes.
Secondary horizons: 20 and 10 M5 bars.

Entry reference: next consecutive M5 open after a completed candidate bar.
Research costs: 4 ticks round trip primary, 8 ticks stress.
Any session discontinuity or missing consecutive bar censors the event.

## Required causal chain

At minimum:
1. bearish context or failed upside auction / accepted seller structure
2. buyer attempt or bullish counterflow
3. evidence of poor buyer result, absorption, trapped buyers, failed imbalance or seller defense
4. bearish reclaim/rejection/acceptance after the cause
5. no strong contradictory bullish acceptance
6. causal order valid
7. at least three independent evidence families

## Frozen cluster policy

Keep only the first eligible SELL event in any overlapping 40-bar window. This is outcome-blind.

## Matched controls

Every event must be compared to:
- recent three-bar price direction control
- always-SELL at the same event timestamps
- original non-causal SELL candidate direction

## Minimum prospective evidence before interpretation

Do not promote or tune until at least:
- 50 eligible SELL events
- 15 complete sessions minimum; 20 preferred
- no single session > 25% of positive net ticks
- top five positive events <= 50% of positive net ticks
- session bootstrap / block bootstrap lower confidence bound >= 0 for primary expectancy
- primary PF >= 1.25 after 4 ticks
- stress expectancy >= 0 after 8 ticks
- first-half / second-half directionally consistent
- causal subset beats or clearly improves risk versus matched controls

## Important

The 30-day sample is already exposed. It may generate hypotheses but cannot be used to optimize this lane. New data after this freeze is the evaluation sample.
