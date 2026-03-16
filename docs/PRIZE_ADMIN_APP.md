# Prize Admin App

## Purpose
The prize admin app is a planned staff-only Unity surface that belongs to the `_PrizeManager` subsystem. Its role is to manage prize data and operational workflows without exposing those controls to kiosk users.

This admin app is part of the same repository. It is not a separate product line, but it should remain clearly separated from public gameplay flows.

## Responsibilities
The v1 admin app is expected to support:

- initializing the prize pool from a local `Prizes.csv` file
- adding more prizes from a local `Prizes.csv` file
- importing runtime settings from a local `Settings.csv` file
- exporting the won-prize list to CSV
- validating files before upload
- previewing changes before applying them
- applying updates to the prize catalog and runtime configuration
- releasing stuck reservations when needed
- triggering or assisting cleanup/export operations

The admin app should be treated as an operational tool for staff, not as a fallback location for gameplay logic.

## Relationship to the Prize Backend
- The admin app writes to the same cloud-authoritative backend used by the kiosk runtime.
- It does not bypass the backend by making local files the runtime source of truth.
- It only deals with local CSV files that staff choose on the machine; it does not manage live spreadsheet sources.
- It should expose operational controls in a safer and more usable form than raw dashboard-only maintenance.

## Intended Placement
The admin app should live under `Assets/_PrizeManager` alongside other prize-system client code, shared models, and operational helpers.

Recommended separation inside the subsystem:

- kiosk-facing runtime flow
- staff-facing admin flow
- shared prize models and service wrappers
- backend deployment or support assets

The exact folder layout can be decided later, but those responsibilities should remain distinct.

## Security and Access Expectations
- The admin app is staff-only.
- Kiosk users should never reach import, export, override, or cleanup screens.
- Backend-side authorization still matters even if the app is distributed only to trusted operators.

## CSV Workflow Scope
CSV-based workflows are planned for:

- initial prize seeding
- additive prize imports
- runtime prize/config updates
- won-prize exports

The admin app should own the human-friendly import/export flow, including validation and preview, while the backend remains authoritative for stored data.

The admin flow should explicitly separate:

- `Initialize prizes using csv`: replace current available prizes/templates with the imported file while preserving won-prize history
- `Add prizes from csv`: add instances from the imported file and reject conflicting template reuse
- `Import settings from csv`: replace the active runtime settings and threshold curves
- `Export won prizes to csv`: write one row per claimed prize instance to a local file

The parser should:

- expect a header row but ignore header text
- read columns strictly by position
- support localized spreadsheet exports by auto-detecting comma and semicolon delimiters
- validate before upload so operators see row-level issues before changing backend state

The detailed CSV contract lives in `docs/PRIZE_CSV_SPEC.md`.
