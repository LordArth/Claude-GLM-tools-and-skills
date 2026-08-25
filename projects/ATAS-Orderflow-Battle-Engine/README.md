# ATAS Orderflow Battle Engine

Experimental NQ/MNQ order-flow decision-support indicator with persistent market narrative memory.

## Core idea

The engine does not judge each bar in isolation. It builds a causal auction story from every closed bar: directional legs, pullbacks, accepted/rejected aggression, absorption, stacked imbalance, failed auctions, Big Trades, defended/trapped inventory zones, POC/value migration and structural damage.

A key continuation sequence is:

`accepted bullish impulse -> controlled pullback -> weak seller efficiency -> buyer inventory retest -> absorption/trapped sellers -> reclaim -> BULLISH_RELOAD`

The mirror applies for bearish reloads. Reversal logic requires actual structural damage and acceptance, not one opposing candle.

## Status

This repository contains a platform-neutral deterministic core plus an ATAS adapter. The core is designed to be testable without ATAS. The adapter targets official ATAS concepts (`GetCandle`, `GetAllPriceLevels`, price-volume footprint data, cumulative trades) and must be compiled against the installed ATAS SDK on the trading machine.

No win-rate or profitability claim is made. Historical/live feature availability is explicit and no future data is allowed in closed-bar truth mode.

## Structure

- `src/OrderflowBattleEngine.Core` — market memory, feature extraction, detectors, scoring/state logic
- `src/OrderflowBattleEngine.ATAS` — ATAS integration layer
- `tests` — deterministic synthetic tests
- `docs` — implementation/research notes
