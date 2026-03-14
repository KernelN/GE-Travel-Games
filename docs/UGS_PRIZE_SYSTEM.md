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
- No-prize behavior is independent from prize weights and is adjusted in stepped soft fashion as the current day approaches `maxPrizesPerDay`.
- Won-prize claimant data is intended to be exported and then purged after the operational retention window.

## High-Level Data Concepts
- `PrizeDefinition`: a prize that can potentially be drawn.
- `PrizeReservation`: a temporary lock on a specific prize after a successful draw.
- `WonPrizeRecord`: a finalized claimed result containing both claimant data and a snapshot of the prize that was awarded.
- `PrizeRuntimeConfig`: backend-controlled tuning values such as no-prize behavior, time-window logic, and day-limit settings.
- `ScheduleRule`: a time/day rule that affects eligibility or draw behavior.

These are conceptual contracts for documentation and implementation planning. This document does not lock a final storage schema.

## High-Level Cloud Operations
- `DrawPrize`: returns either no prize or a reserved prize with reservation metadata.
- `ClaimPrize`: finalizes a reservation into a won-prize record after claimant data is submitted.
- `CancelReservation`: releases a reservation before it expires.
- `ReleaseExpiredReservations`: cleanup operation that returns stale reservations to the available pool.
- `ExportWonPrizes`: operational export of claimed prizes for staff handling.
- `ImportPrizeSeedFromCsv`: imports prize seed/config data from CSV into cloud-authoritative storage.

These names describe intended responsibilities, not final API signatures.

## CSV Support
Initial prize seeding and won-prize export are expected to use CSV-based workflows. Runtime settings such as prize weights, hour/day rule sets, and stepped no-prize behavior are also intended to be manageable through CSV-driven admin flows.

This document intentionally keeps CSV support at a conceptual level only.

## Deferred Detailed CSV Specification
The exact CSV headers, schedule encoding, weight-override representation, false-prize step format, validation rules, and import precedence are intentionally deferred to a later dedicated specification.
