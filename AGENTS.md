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

## Game and modding compatibility

Slay the Spire 2 is in active public beta. Mega Crit modding behavior can change substantially between builds.

Before release or after a game update:

- Review recent game/modding patch notes for save, hash, serialization, Workshop, multiplayer, and mod menu changes.
- Update BaseLib before diagnosing deeper Neowtwork breakage if BaseLib is failing to load.
- Prefer published `STS2.dll` XML documentation when available before falling back to decompiled snippets.
- Rebuild against the current public beta and smoke-test every Harmony-patched UI surface.
- Keep `affects_gameplay` set to `false` unless the user explicitly asks for gameplay-affecting features and we intentionally accept multiplayer/hash consequences.
- Do not register cards, relics, powers, saved properties, or gameplay models unless the project scope changes. Neowtwork should remain a UI/statistics mod.

Known high-risk compatibility surfaces:

- Card Library sort controls and stats overlays
- in-run reward/shop/event card stat overlays
- Compendium dashboard navigation
- Mod Configuration custom controls
- event-option hover tips
- relic hover tips
- save/profile sync paths

Steam Workshop/local precedence matters: Steam mods can take precedence over local mods when the Steam version is greater. During local development, confirm the intended local build is the one loaded. If needed, unsubscribe from the Workshop build, disable it, or bump the local dev version appropriately.

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

## Versioning and changelog

Use semantic versioning for Neowtwork releases.

- `MAJOR` changes for incompatible save/import behavior, breaking config changes, or large public API/data-model shifts.
- `MINOR` changes for new user-facing features, new stats, new sorting modes, new import flows, or meaningful UX additions.
- `PATCH` changes for bug fixes, copy changes, small UX polish, packaging fixes, and non-breaking internal cleanup.
- Pre-release identifiers may be used for friend testing, for example `0.2.0-test.1`.

Keep the version in `Neowtwork.json` aligned with the current release version.

Maintain `CHANGELOG.md` using a Keep-a-Changelog-style structure:

- Add new work under `Unreleased` while developing.
- Move completed release notes under a dated version heading when cutting a release.
- Include practical user-facing notes, especially install, import, save, cloud, and tester-impacting changes.
- Update the changelog before packaging a manual zip or publishing a GitHub/Workshop release.

## Save and Steam Cloud safety

Be very careful with save files.

Slay the Spire 2 separates vanilla and modded profile progress. Steam Cloud can overwrite local modded progress.

As of the June/July 2026 public beta updates, the base game copies unmodded saves into the modded save directory on first modded launch. Treat that native behavior as the primary first-run migration path. Neowtwork should not compete with it.

Rules:

- Do not edit save files unless the user explicitly asks or the user confirms an in-game Neowtwork sync/import prompt.
- Do not touch Steam Cloud data unless the user explicitly asks.
- Do not assume vanilla is always source-of-truth. For ongoing opt-in sync, either vanilla or modded may contain the newest local data.
- Always make timestamped backups before copying save files.
- Never delete unique save or run-history files during sync unless the user explicitly approves deletion.
- Prefer read-only parsing of `.run` history files for stats.

The old vanilla-to-modded import flow should be treated as an advanced recovery/manual fallback, not the default path. The future-facing save feature is explicit opt-in local sync:

- explain that the base game handles first-run vanilla-to-modded copying
- show what would be copied in each direction
- back up both sides before writing
- copy missing/newer files both directions when safe
- skip ambiguous conflicts instead of guessing
- never control Steam Cloud directly

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
- After a successful build or verification pass, push commits to `origin/main` unless the user explicitly asks to keep changes local.
- Do not push broken builds, save-file experiments, local-only paths, packaged zips, logs, or backups.
- If a change is experimental or visually uncertain, commit and push only after the smallest reasonable validation has passed.
- Do not commit local saves, logs, backups, or packaged zips.
- Keep generated Godot `.uid` files for tracked scripts if Godot creates them.

## Verification

Before handing off code changes:

1. Build the mod.
2. If UI/assets changed, publish/export the `.pck` when needed.
3. Launch Slay the Spire 2 modded only when appropriate.
4. Check the latest game log for Neowtwork errors.
5. Ask the user for screenshot/UX feedback for visual changes.

Do not assume the visual layout is correct just because the build succeeds.
