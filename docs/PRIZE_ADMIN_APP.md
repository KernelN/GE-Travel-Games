# Prize Admin App

## Purpose
The prize admin app is a planned staff-only Unity surface that belongs to the `_PrizeManager` subsystem. Its role is to manage prize data and operational workflows without exposing those controls to kiosk users.

This admin app is part of the same repository. It is not a separate product line, but it should remain clearly separated from public gameplay flows.

## Responsibilities
The v1 admin app is expected to support:

- importing prize seed/config data from CSV into cloud-authoritative storage
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
- runtime prize/config updates
- won-prize exports

The admin app should own the human-friendly import/export flow, including validation and preview, while the backend remains authoritative for stored data.

## Deferred Detailed CSV Specification
This document intentionally does not define CSV columns, row formats, schedule syntax, special weight encoding, or false-prize step representation. A later dedicated CSV specification should cover those details before implementation begins.
