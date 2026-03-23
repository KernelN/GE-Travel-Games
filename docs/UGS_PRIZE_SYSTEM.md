# UGS Prize System

## Purpose
The prize system is a planned shared backend-driven service that can be called by one or more games in this repository. Its job is to manage a global pool of prizes, perform authoritative draw decisions, reserve prizes safely, collect claimant data, and maintain a won-prize history.

## V1 Service Stack
The intended Unity Gaming Services stack for v1 is:

- `Authentication`
- `Cloud Code`
- `Cloud Save Game Data`
- `Remote Config`

`Economy` is explicitly out of scope for v1. The system is managing finite real-world inventory rather than virtual balances or catalog purchases.

## Authority Split

### Local Unity Client
The kiosk-facing Unity client is responsible for:

- signing into the backend
- starting the draw flow
- showing animation, loading, and result UX
- collecting `name`, `office`, and `phone`
- validating form input for user experience
- submitting claim or cancel requests
- showing reservation countdown and operational errors

### Cloud Authority
The backend is responsible for:

- deciding win versus no-win
- filtering prizes by active time/day rules
- applying weight adjustments and no-prize behavior
- enforcing stepped soft behavior as the day approaches `maxPrizesPerDay`
- reserving a prize so it cannot be duplicated
- releasing expired or canceled reservations
- converting a reservation into a won-prize record
- exporting won prizes for downstream handling
- storing runtime state and operational counters

The client may present the experience, but it does not own the authoritative draw result.

## Chosen Business Rules
- One global prize pool is shared by the project.
- The expected deployment model is a shared kiosk flow.
- The final draw is server-authoritative.
- A won prize is first reserved, then either claimed or returned to the available pool if canceled or expired.
- Time and day rules use one fixed business timezone.
- No-prize behavior is independent from prize weights and is adjusted in stepped soft fashion as the day approaches `maxPrizesPerDay`.
- Some prizes may be marked as hour-sensitive and get a forced-hour pre-roll before the normal false-prize and weighted-prize pipeline.
- Won-prize claimant data is intended to be exported and then purged after the operational retention window.

## High-Level Data Concepts
- `PrizeTemplate`: category-scoped definition imported from `Prizes.csv`.
- `PrizeInstance`: unique available or won item derived from a template.
- `PrizeReservation`: a temporary lock on a specific prize after a successful draw.
- `WonPrizeRecord`: a finalized claimed result containing both claimant data and a snapshot of the prize that was awarded.
- `PrizeRuntimeSettings`: backend-controlled values such as no-prize behavior, forced-hour pre-roll behavior, time-window logic, and day-limit settings.

These are conceptual contracts for documentation and implementation planning. This document does not lock a final storage schema.

## High-Level Cloud Operations
- `DrawPrize`: returns either no prize or a reserved prize with reservation metadata.
- `ClaimPrize`: finalizes a reservation into a won-prize record after claimant data is submitted.
- `CancelReservation`: releases a reservation before it expires.
- `ReleaseExpiredReservations`: cleanup operation that returns stale reservations to the available pool.
- `ExportWonPrizes`: operational export of claimed prizes for staff handling.
- `InitializePrizesFromCsv`: replaces the current available prize pool and templates using a local `Prizes.csv` file.
- `AddPrizesFromCsv`: adds instances from a local `Prizes.csv` file and rejects conflicting template reuse.
- `ImportSettingsFromCsv`: replaces the active runtime settings using a local `Settings.csv` file.

These names describe intended responsibilities, not final API signatures.

## CSV Support
CSV workflows are part of the v1 design:

- `Prizes.csv` imports positional prize-template rows and expands each row into unique prize instances using `PrizeAmount`.
- `Settings.csv` imports positional runtime settings, including base chances and stepped threshold overrides.
- `WonPrizes.csv` exports one row per claimed prize instance with both the unique instance ID and the prize category ID.
- CSV files come from local spreadsheet exports and are parsed by column order, not localized header text.
- The admin app should auto-detect comma and semicolon delimiters before applying positional parsing.

The detailed CSV contract lives in `docs/PRIZE_CSV_SPEC.md`.

## Draw Logic Notes
- The normal draw flow is: filter eligible prizes, roll false-prize chance, then roll the weighted prize pool.
- If any currently eligible prize has `HasToComeOutDuringHour = true`, run a forced-hour pre-roll first.
- Forced-hour pre-roll success draws only from the currently eligible forced subset.
- Forced-hour pre-roll failure falls back to the normal false-prize plus weighted-prize flow.
- Forced-window prizes remain eligible in the normal weighted pool even after a failed forced-hour pre-roll.
