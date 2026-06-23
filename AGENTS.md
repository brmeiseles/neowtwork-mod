# AGENTS.md

## Project

This repository contains `neowtwork-mod`, an early Slay the Spire 2 mod.

The mod currently targets the card compendium/card library and adds:

- expanded card stats overlays
- compact card stat sorting buttons
- a read-only run-history scanner used for local diagnostics

Keep the mod focused on statistics and UI. Do not change gameplay unless the user explicitly asks for that later.

## Current environment assumptions

- Primary development machine: macOS / Apple Silicon.
- Game: Slay the Spire 2 through Steam.
- Mod framework dependency: BaseLib for Slay the Spire 2.
- Godot runtime: MegaDot / Godot .NET compatible with Slay the Spire 2.
- .NET SDK: 9 or newer.

The local project has been configured to build and copy the mod into the local Slay the Spire 2 mods folder.

## Build and package

From the repo root:

```sh
dotnet build Neowtwork.sln --configuration Debug --no-restore
```

To publish/export the `.pck`:

```sh
dotnet publish Neowtwork.csproj --configuration Debug --no-restore
```

MegaDot may print an editor-time `sts2` assembly warning during publish while still successfully exporting the `.pck`. Check the final result and game log before treating that warning as fatal.

Manual test packages should contain only:

```text
Neowtwork/
  Neowtwork.dll
  Neowtwork.json
  Neowtwork.pck
INSTALL.txt
```

Do not include saves, logs, backups, `.godot`, build output folders, or local machine paths in release zips.

## Save and Steam Cloud safety

Be very careful with save files.

Slay the Spire 2 separates vanilla and modded profile progress. Steam Cloud can overwrite local modded progress.

Rules:

- Do not edit save files unless the user explicitly asks.
- Do not touch Steam Cloud data unless the user explicitly asks.
- Treat vanilla saves as source-of-truth if syncing is requested.
- Always make timestamped backups before copying save files.
- Prefer read-only parsing of `.run` history files for stats.

The mod itself should not intentionally write to save files.

## Stats architecture

Prefer existing game data before custom tracking.

Currently used built-in card stats:

- `TimesWon`
- `TimesLost`
- `TimesPicked`
- `TimesSkipped`

Derived built-in card stats:

- `Win Rate = TimesWon / (TimesWon + TimesLost)`
- `Seen = TimesPicked + TimesSkipped`
- `Pick Rate = TimesPicked / Seen`

For richer card/relic stats, use `.run` history files read-only. Useful run-history fields include:

- `cards_gained`
- `card_choices`
- `cards_removed`
- `cards_transformed`
- `upgraded_cards`
- `downgraded_cards`
- `cards_enchanted`
- final deck contents
- final relic contents

Relic performance stats are not currently available as built-in aggregate progress stats; derive them from run history.

## UI guidance

The current prototype patches the card library through Harmony.

Keep first-pass UI changes small and reversible:

- avoid rewriting whole scenes if a targeted patch works
- avoid moving core navigation such as the back arrow
- prefer compact controls in the sidebar
- keep card overlay text readable

The compact stat sort buttons currently mean:

- `WR` = Win Rate
- `V` = Victories
- `L` = Losses
- `P` = Picked
- `S` = Skipped

Clicking the same stat sort button twice should reverse the exact sort order.

## Git hygiene

- Commit logical milestones.
- Do not commit local saves, logs, backups, or packaged zips.
- Keep generated Godot `.uid` files for tracked scripts if Godot creates them.
- Push source changes to `origin/main` after verified milestones.

## Verification

Before handing off code changes:

1. Build the mod.
2. If UI/assets changed, publish/export the `.pck` when needed.
3. Launch Slay the Spire 2 modded only when appropriate.
4. Check the latest game log for Neowtwork errors.
5. Ask the user for screenshot/UX feedback for visual changes.

Do not assume the visual layout is correct just because the build succeeds.
