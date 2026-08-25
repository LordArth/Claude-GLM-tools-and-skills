# CAUSAL ENGINE V1

## Why this exists

The 30-day sample invalidated the idea that a general reload score can be promoted simply because an early small sample looked good. The next architecture must model **ordered market mechanisms**, not just confluence.

This engine uses the word "causal" operationally: evidence must form a temporally valid mechanism chain and survive ablation. It does **not** claim philosophical or econometric proof of causation from observational market data.

## Core reversal mechanism

`meaningful location -> aggressive attempt -> poor price result -> absorption/exhaustion/trap -> reclaim/rejection -> confirmation`

## Core continuation/reload mechanism

`accepted initiative -> controlled counterflow -> remembered inventory/defense zone -> counterflow inefficiency -> renewed defense/absorption -> reclaim -> renewed acceptance`

## Hard temporal rules

- response cannot precede the event it confirms
- a trapped Big Trade cannot be declared on the event bar when only full-bar OHLC is available
- future POC/value movement cannot support an earlier historical decision
- rolling percentiles are prior-only
- current-bar provisional evidence cannot be rewritten into historical closed-bar certainty

## Causal graph

Each event has:
- timestamp
- kind
- side
- strength
- optional price
- optional parent IDs
- historical/live availability

Key kinds:
- Context
- Initiative
- Counterflow
- Absorption
- Exhaustion
- BigTrade
- Trap
- Defense
- Sweep
- Reclaim
- Acceptance
- Rejection
- ValueMigration
- Divergence
- StructureDamage
- Confirmation
- Invalidation

## Independence

The graph collapses correlated observations into evidence families. Three delta-derived observations are not three independent causes.

A high-quality chain should normally contain at least three independent families and a genuine response family.

## Contradictions

Strong recent opposite-side acceptance, structure damage, trapped inventory or confirmation sharply discounts the current thesis.

## Counterfactual ablation

For every confirmed research signal, remove one evidence family at a time and recompute the chain.

Questions:
- Does the signal vanish without the trap family?
- Does it vanish without the location family?
- Is nearly all score coming from one family?
- Does it survive when one noisy feature is absent?

A mechanism that depends almost entirely on one feature is flagged as fragile.

## Required research comparison

For each candidate family compare:
1. original score-based candidate
2. causal-order-valid subset
3. causal-order + independence subset
4. causal-order + independence + ablation-robust subset
5. matched price-only control

The causal engine only earns complexity if the stricter subsets improve out-of-sample behavior or materially reduce adverse excursion/false positives.

## No promotion rule

Causal structure is not automatically profitable. It is another hypothesis layer and must pass the same prospective, chronological, cost-stressed validation gates.
