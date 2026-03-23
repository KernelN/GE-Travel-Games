# Project Guide

## Overview
This repository is organized around three product areas:

- `Assets/_SnakeAirlinesGame`: current implemented Unity game content.
- `Assets/_TapGallery`: second game, tap/shooting gallery, specified but not yet implemented.
- `Assets/_PrizeManager`: planned shared prize system, UGS integration layer, kiosk-facing runtime support, and staff admin app.

SnakeAirlines is the only currently implemented game. TapGallery is specified and ready for implementation. The prize system is planned but not yet fully implemented.

## Subsystem Roles

### SnakeAirlines
- Contains the current 2D snake prototype.
- Owns its gameplay scripts, scene, art assets, and project-specific setup.
- Should not become the default home for shared prize infrastructure.

### TapGallery
- The second game. Lives under `Assets/_TapGallery`.
- Requirements and gameplay loop are fully documented in `docs/TAPGALLERY_GAME.md`.
- Specified but not yet implemented — implementation may begin from that document.

### Prize Manager
- Planned home for shared prize drawing logic used by one or more games.
- Planned home for Unity-side kiosk/runtime client code that calls the backend.
- Planned home for the staff-only admin app used to import seed/config data, export won prizes, and run support operations.
- Planned home for any local docs, samples, and helper scripts tied to prize operations.

## Ownership Boundaries

### Game Runtime Code
- Game-specific gameplay logic belongs in each game folder.
- Shared prize-consumption code belongs in `_PrizeManager`, then gets called by each game as needed.

### Prize Kiosk Runtime Code
- Unity client code that signs in, requests draws, displays win/no-win outcomes, collects claimant data, and submits claims belongs in `_PrizeManager`.
- This code should remain backend-driven for authoritative results.

### Staff Admin App Code
- Staff-only screens, utilities, and workflows for importing/exporting prize data belong in `_PrizeManager`.
- The admin app is part of the same repo, but it is not a public gameplay surface.

### Cloud / Backend Code and Ops Scripts
- Cloud Code scripts, deployment helpers, and backend-oriented operational assets should be grouped with the prize subsystem rather than scattered across game folders.
- Configuration that controls draw behavior should be documented with the prize system, even when the runtime uses Unity cloud services.

## Current Inconsistencies
- `README.md` previously referred to `Assets/_Game`, while the current folder layout uses `Assets/_SnakeAirlinesGame`.
- `ProjectSettings/EditorBuildSettings.asset` still references a legacy `Assets/_Game/Scenes/SampleScene.unity` path.
- These inconsistencies should be treated as migration drift, not as a sign that `_Game` is still the preferred structure.

## Working Assumption for New Changes
- Use the current folder layout for new docs and new code placement.
- Keep subsystem responsibilities explicit so future work on TapGallery and the prize backend does not blur into SnakeAirlines-only code.
