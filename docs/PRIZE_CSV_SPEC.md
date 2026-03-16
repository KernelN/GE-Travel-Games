# Prize CSV Specification

## Purpose
This document defines the CSV contract used by the prize admin workflow:

- `Prizes.csv` for prize-template imports
- `Settings.csv` for runtime-settings imports
- `WonPrizes.csv` for claimed-prize exports

These files are expected to be created in Excel, Google Sheets, or similar spreadsheet tools and then exported locally as CSV. The admin app only works with local files on disk.

## General Rules
- A header row is expected for operator usability, but header text is ignored completely.
- Parsing is positional: column order matters, column names do not.
- Header labels may be written in any language.
- The parser should auto-detect comma-delimited and semicolon-delimited CSV files.
- Empty trailing columns are allowed where the relevant row type does not need them.

## Prizes.csv

### File Meaning
- Each row defines one `PrizeTemplate`.
- `PrizeAmount` expands that template into that many unique `PrizeInstance` records in the available prize pool.
- The CSV `PrizeCategoryId` is the template/category identifier, not the unique instance identifier.

### Column Order
1. `PrizeCategoryId` (`ushort`)
2. `PrizeAmount` (`ushort`)
3. `PrizeName` (`string`)
4. `PrizeDescription` (`string`)
5. `PrizePriority` (`ushort`)
6. `PrizeHourStart` (`hour`)
7. `PrizeHourEnd` (`hour`)
8. `HasToComeOutDuringHour` (`bool`)
9. `PrizeDays` (`1|2|3...`)

### Value Rules
- `PrizePriority` is the base draw weight for v1.
- `PrizeDays` uses `|` as the in-cell separator.
- Weekdays use Monday = `1` through Sunday = `7`.
- Blank schedule fields mean no time/day restriction.
- If `HasToComeOutDuringHour` is true, valid `PrizeHourStart` and `PrizeHourEnd` values are required.
- Overnight windows are out of scope for v1 and should be rejected during import.

### Import Modes
- `Initialize prizes using csv`
  - Replaces the current available prize pool and prize-template definitions with the imported file.
  - Preserves already won-prize history.
- `Add prizes from csv`
  - Adds new prize instances from the imported file.
  - Reuses an existing template only when the incoming row matches the stored template on all non-amount fields.
  - Rejects a row when the same `PrizeCategoryId` changes name, description, priority, hour window, forced-hour flag, or days.

## Settings.csv

### File Meaning
- The first data row defines the base runtime settings.
- Later rows define stepped threshold overrides for false-prize chance, forced-hour pre-roll chance, or both.
- No row-type code is used; column position defines meaning.

### Column Order
1. `Timezone`
2. `PrizeReservationTimeoutMinutes`
3. `MaxPrizesPerDay`
4. `FalsePrizeChancePercent`
5. `FalsePrizeThresholdPercent`
6. `ForcedHourChancePercent`
7. `ForcedHourThresholdPercent`

### Base Row Rules
- The first data row is the base row.
- Columns 1-3 are required on the base row.
- Column 4 stores the base false-prize chance percentage.
- Column 5 is blank on the base row.
- Column 6 stores the base forced-hour pre-roll chance percentage.
- Column 7 is blank on the base row.

### Threshold Row Rules
- Later rows may leave columns 1-3 blank.
- Column 4 pairs with column 5 to define one false-prize step override.
- Column 6 pairs with column 7 to define one forced-hour pre-roll step override.
- A row may define only a false-prize step, only a forced-hour step, or both.
- Threshold and chance values are percentages from `0` to `100`.

### Threshold Selection
- False-prize threshold calculation uses `prizesToday / maxPrizesPerDay * 100`.
- Forced-hour threshold calculation uses `timePassed / timeAllowed * 100` during the active hour window.
- For each chance curve, the active value is the chance from the highest threshold less than or equal to the current computed percentage.
- If no threshold is matched beyond the base value, the base chance remains active.

## WonPrizes.csv

### File Meaning
- Export one row per claimed `PrizeInstance`.
- Include both the unique won-prize instance identifier and the original prize category identifier for traceability.

### Column Order
1. `WonPrizeInstanceId`
2. `PrizeCategoryId`
3. `PrizeName`
4. `PrizeDescription`
5. `WinnerOffice`
6. `WinnerName`
7. `WinnerPhoneNumber`

## Runtime Behavior Tied to CSV Data
- The normal draw flow is:
  1. filter prizes by active day/hour eligibility
  2. roll false-prize chance
  3. if false-prize fails, roll the eligible prize pool by `PrizePriority`
- If any currently eligible prize has `HasToComeOutDuringHour = true`, run a forced-hour pre-roll first.
- If the forced-hour pre-roll succeeds, draw only from the currently eligible forced subset.
- If the forced-hour pre-roll fails, continue into the normal false-prize and weighted-prize flow.
- Forced-window prizes remain eligible in the normal weighted pool for that same draw.

## Admin Operations
- `InitializePrizesFromCsv(localFilePath)`
- `AddPrizesFromCsv(localFilePath)`
- `ImportSettingsFromCsv(localFilePath)`
- `ExportWonPrizesToCsv(localFilePath)`

These names describe the intended admin operations and do not force final code signatures.
