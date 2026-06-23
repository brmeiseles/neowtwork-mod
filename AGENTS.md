# AGENTS.md

## Project

This repository contains `neowtwork-mod`, an early Slay the Spire 2 mod.

The mod currently targets the card compendium/card library and adds:

- compact card stats overlays
- card stat sorting dropdown
- a read-only run-history scanner used for local diagnostics

Keep the mod focused on statistics and UI. Do not change gameplay unless the user explicitly asks for that later.

## Working principles

Be thoughtful with the process.

- Do not needlessly waste tokens, tool calls, launches, rebuilds, or user attention.
- Prefer the smallest useful investigation before making a change.
- Prefer the smallest reversible change that proves or improves the idea.
- When uncertain, state the uncertainty and choose a low-risk next step.
- Keep a clear trail of what changed, why it changed, and how it was verified.
- Do not chase polish forever. Make a useful pass, get user feedback, and move forward.

Keep the spirit of Slay the Spire alive and well.

- Respect the game’s existing visual language, pacing, and UI hierarchy.
- Add information where it helps decision-making without making the game feel like a spreadsheet first.
- Prefer “feels native enough” over loud custom UI.
- Do not alter gameplay, balance, run outcomes, save data, or multiplayer behavior unless explicitly requested.

## Working modes

Use the right mode for the moment. Be explicit when switching modes if it helps the user follow along.

### Planning mode

Use planning mode when the desired behavior, scope, or UX is still fuzzy.

- Restate the goal in concrete terms.
- Identify the likely data source and UI surface.
- Name risks before touching code or saves.
- Offer a small first milestone instead of a huge rewrite.

### Execution mode

Use execution mode when the user has approved the direction.

- Make targeted changes.
- Avoid broad rewrites.
- Preserve existing working behavior.
- Keep implementation details scoped to Neowtwork unless the user asks otherwise.

### Testing mode

Use testing mode after implementation.

- Build first.
- Publish/export only when needed.
- Launch the game only when appropriate and ideally after confirming it is closed.
- Check logs for Neowtwork errors.
- Separate harmless known warnings from new failures.

### Validation mode

Use validation mode to confirm the feature actually behaves correctly.

- Verify the data source is the intended one.
- Confirm sorting/math rules with concrete examples when possible.
- For save/cloud work, verify file sizes, hashes, and timestamps before and after.
- Do not call a feature done only because it compiled.

### UX review mode

Use UX review mode for visual and interaction polish.

- Screenshots matter.
- Treat user feedback as the source of truth for whether the UI feels right.
- Make one or two focused layout tweaks at a time.
- Keep native game affordances recognizable.
- Stop polishing when the user says it is good enough for now.

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

The card stat sorting dropdown includes:

- Win Rate
- Pick Rate
- Victories
- Losses
- Picked
- Skipped
- Seen

Selecting the same stat sort twice should reverse the exact sort order.

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
