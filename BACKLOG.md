# Neowtwork Mod Backlog

This backlog tracks likely next steps for Neowtwork.

Keep the spirit of the game alive: small, readable, native-feeling improvements before big systems.

## Current focus

- Restore compatibility with the latest Slay the Spire 2 public beta and latest BaseLib.
- Prepare Neowtwork for friend testing and eventual Workshop publishing.
- Card Library analytics should feel like a feature Mega Crit could have shipped.
- Prefer built-in game stats when available.
- Use run-history parsing for richer stats only when built-in progress data is not enough.
- Avoid save-file writes unless the user explicitly approves a careful migration/sync feature.
- Current public/friend builds should keep Neowtwork progress tools read-only and trust Slay the Spire 2's native first-run modded progress setup.

## Latest public beta compatibility

- Update BaseLib.
  - Current reports indicate BaseLib is failing after recent Mega Crit updates.
  - Update NuGet/package dependency and installed local/Steam dependency as needed.
  - Rebuild after updating and confirm the mod loads.
- Review Mega Crit modding changes from June/July 2026 for Neowtwork impact.
  - First modded launch now copies unmodded saves to the modded save directory.
  - Non-gameplay-affecting mods are excluded from serialization/hash behavior more correctly.
  - Non-gameplay mod mismatch warnings log correct mod lists.
  - Steam Workshop mods can take precedence over local mods when the Workshop version is greater.
  - XML documentation for `STS2.dll` is beginning to ship.
- Run a full latest-beta smoke test:
  - modded launch
  - BaseLib loads
  - Mod Configuration opens
  - progress sync/status panel opens without errors
  - Card Library stats overlay renders
  - Card Stats sorting works in both directions
  - in-run reward card stats work
  - shop card stats work
  - event option hover stats work
  - relic hover stats work
  - Compendium dashboard opens
  - multiplayer with matching mod lists does not produce a false mismatch
- Inspect the latest game logs after smoke testing.
  - Look specifically for Neowtwork Harmony patch failures, missing node names, save sync errors, and BaseLib errors.
- Update `min_game_version` if the current mod no longer supports older public beta builds.
- Prefer published `STS2.dll` XML documentation over decompiled code when investigating new APIs.

## Near-term UX polish

- Review the new native-shell Card Library Card Stats sort control.
  - First cleanup pass reuses the game's native `NCardViewSortButton` visual shell with a transparent popup hitbox.
  - Confirm the closed state now looks native beside `A - Z`.
  - Confirm dropdown selection and reverse-order behavior still feel understandable.
  - If it still feels off, refine the open-state menu next rather than rebuilding the closed state again.
- Review the latest Card Stats sorter placement in-game.
  - It should look like the existing `A - Z` sort row when closed.
  - If the back arrow crowds it, decide whether to move it above `A - Z` or redesign the sort area.
- Improve closed Card Stats sort affordance.
  - Prefer matching native sort/list icon styling.
  - Keep active labels short: `Win Rate`, `Pick Rate`, etc.
  - Preserve `↓` high-to-low and `↑` low-to-high unless a better native icon is found.
- Review overlay readability across card sizes.
  - Current intended format:
    - `88% Win Rate`
    - `(14 victories - 2 losses)`
    - `83% Pick Rate`
    - `(19 picks / 23 seen)`
  - Keep only Win Rate and Pick Rate in the overlay.
  - Keep parenthetical sample-size context.
- Consider hiding Card Stats sort controls unless `View Stats` is enabled.
  - Only do this if the base game checkbox state can be hooked safely.

## Sorting behavior

- Preserve all current Card Stats sort options:
  - Win Rate
  - Pick Rate
  - Victories
  - Losses
  - Picked
  - Skipped
  - Seen
- Confirm every sort option works in both directions.
  - First selection: high-to-low.
  - Same selection again: exact reverse order.
  - New selection: reset to high-to-low.
- Confirm secondary sort remains stable and native-feeling.

## In-run quality of life

- Continue polishing card stats while making card choices.
  - `Off`: never show stats.
  - `Show`: always show stats.
  - `Hover`: show stats only while hovering a card, so players can still enjoy the card art.
  - Current primary supported surfaces: card rewards and shop card choices.
  - Avoid combat hand, deck, draw pile, discard pile, exhaust pile, and other already-owned card views.
- Add card stats to event-room card previews.
  - Example: `Bugslayer` shows an event preview card beside event options.
  - These appear to use the game’s `CardPreviewStyle.EventLayout` / `EventCardPreviewContainer` path rather than the reward/shop layouts.
  - Reuse the same underlying card stats and `Off` / `Show` / `Hover` setting.
  - Treat event previews as their own layout case; do not assume reward/shop overlay coordinates will fit.
  - Start with a diagnostic placement pass before polishing.
  - Do not push until an in-game screenshot confirms placement, because this surface has a different scale, parent container, and nearby event option buttons.
- Add shop analytics.
  - Track what the player usually spends gold on:
    - cards
    - relics
    - potions
    - card removals
    - other shop services if exposed by the game
  - Show how often each purchase type or purchased item leads to a win.
  - Prefer run-history parsing first if shop purchase events are present there.
  - If run history is not enough, investigate safe hooks for shop purchase events without changing gameplay.
- Add a single top-menu-bar button during runs that opens the Card Library quickly.
  - Goal: reduce clicks when checking card stats mid-run.
  - Prefer a small native-feeling button/icon that belongs with existing top bar controls.
  - Avoid covering or disrupting existing run UI, multiplayer UI, map controls, or settings controls.
  - Decide whether the Card Library should open read-only and return cleanly to the current run screen.
  - Confirm it works safely during combat, rewards, shops, events, and map screens before release.

## Data and analytics

- Review the first-pass Neowtwork Compendium dashboard.
  - Confirm the hidden Leaderboards slot is a good long-term home for the Neowtwork dashboard button.
  - Confirm tabs and filters are readable in the Compendium context.
  - Decide which sections need stronger visual hierarchy, charts, icons, or cards instead of text-heavy lists.
  - Keep the Mod Configuration analytics section as a bottom-of-page raw/debug dump.
- Improve event-option analytics beyond chosen-count/win-rate.
  - Current run history records choices that were selected, not every option that was offered.
  - To show true event pick rate, start tracking offered event options prospectively.
  - Keep that tracking read-only with respect to gameplay and save-safe with respect to profiles.
- Review relic hover stats in-game.
  - Current implementation attaches stats to relic hover tips broadly, not only the relic collection page.
  - Decide whether broad relic hover stats feel helpful or too noisy.
- Expand native-feeling filtering to run-history analytics.
  - Current first pass includes character, ascension, singleplayer vs multiplayer, and win/loss.
  - Future filters to consider:
    - date
    - game version
    - act reached
    - ancient path
    - minimum sample size
    - include/exclude multiplayer
- Keep using built-in card stats where possible:
  - `TimesWon`
  - `TimesLost`
  - `TimesPicked`
  - `TimesSkipped`
- Use derived card stats:
  - `Win Rate = TimesWon / (TimesWon + TimesLost)`
  - `Seen = TimesPicked + TimesSkipped`
  - `Pick Rate = TimesPicked / Seen`
- Investigate richer card lifecycle data from `.run` files:
  - cards gained
  - card choices
  - cards skipped
  - cards transformed
  - cards upgraded
  - cards enchanted
  - final deck contents
- Investigate relic stats from `.run` files.
  - Built-in aggregate relic stats do not appear to be available yet.
  - Derive relic performance from historical runs.
- Polish ancient analytics.
  - Current first pass summarizes ancient offers/picks/win rate from run history where available.
  - Split Neow/act-start ancients by act or ancient identity if the run file exposes that reliably.
  - Consider showing offered relics, picked relics, pick rate, and win rate in a compact native panel.
- Decide how multiplayer runs should be represented.
  - Separate toggle/filter?
  - Combined stats by default?
  - Clear labeling when multiplayer data is included?

## Save and progress sync

- Keep public/friend builds read-only for progress.
  - Neowtwork should not import, sync, overwrite, or delete save files.
  - Trust Slay the Spire 2's native first-run vanilla-to-modded copy.
  - Validate cross-machine behavior by checking whether Steam/Slay the Spire 2 syncs modded runs from one computer to another.
  - If cross-machine modded progress still does not sync, document the limitation before building any Neowtwork file-writing tool.
- Deprecate Neowtwork's first-run vanilla-to-modded import as the main path.
  - Mega Crit now copies unmodded saves into the modded save directory on first modded launch.
  - Reframe Neowtwork's import UI as advanced recovery/manual fallback only.
  - Rename/reword `Progress Import` toward `Progress Sync` or `Save Tools`.
  - Remove or suppress automatic first-run import prompts if the base game now handles the scenario safely.
  - Keep a manual recovery button only if it remains useful after latest-beta testing.
- Rework the current opt-in sync feature around the new base-game behavior.
  - Purpose: prevent ongoing drift after first launch, not replace Mega Crit's initial copy.
  - Sync must handle either side as the newest source:
    - vanilla → modded when vanilla has newer local data
    - modded → vanilla when modded has newer local data
  - Show a preview/status before manual sync:
    - files only in vanilla
    - files only in modded
    - files newer in vanilla
    - files newer in modded
    - conflicts skipped
    - last sync time
  - Ask for confirmation before enabling automatic sync.
  - Create timestamped backups before every write.
  - Never silently delete unique runs or profile files from either side.
  - Skip ambiguous conflicts instead of guessing.
  - Explain Steam Cloud limits clearly: Neowtwork can copy local files, but Steam may still present cloud conflicts.
- Audit the existing sync implementation for release readiness.
  - Confirm it excludes `_neowtwork`, backup, temp, and cloud/system files.
  - Investigate latest-beta smoke-test errors where the game attempted to delete `modded/profile2/saves/*.save.backup` files and failed.
    - Determine whether these are game-created backups, old Neowtwork leftovers, or sync-created artifacts.
    - Ensure Neowtwork never leaves backup files directly inside active `saves` folders.
  - Confirm it does not copy active lock/temp files.
  - Confirm same-content files are detected by hash.
  - Confirm copied file timestamps are preserved.
  - Confirm backups are easy to find and not included in analytics scans.
  - Confirm auto-sync does not run while a save write is in progress.
- Decide whether a helper script is still needed.
  - It may be unnecessary now that the base game handles first-run copy.
  - Keep manual friend instructions only as a fallback.

## Friend testing

- Keep `FRIEND_TEST.md` current with the actual UI.
- Update friend-test instructions for latest public beta behavior.
  - Base game should copy vanilla progress to modded on first modded launch.
  - BaseLib must be updated before testing Neowtwork.
  - Manual save copying should be last-resort only.
  - Include how to verify whether Steam Workshop or local Neowtwork is being loaded.
- Fix the manual friend-test zip/package flow.
  - Package must include the exact current build and install instructions.
  - Confirm the zip works for both Mac and Windows users.
  - Goal: Brandon and a friend can both install the same Neowtwork build and play a multiplayer run with the mod enabled.
- Package a fresh manual test zip after significant UX or install changes.
- Ask testers to report:
  - OS
  - Steam/Workshop BaseLib vs manual BaseLib
  - whether the game launches modded
  - whether the overlay appears
  - whether Card Stats sorting works
  - screenshots of any weird layout
- Avoid asking friends to touch save files unless explicitly testing sync.

## Packaging and release

- Prepare a latest-beta release candidate after BaseLib compatibility is restored.
  - Bump version only after compatibility and smoke test pass.
  - Verify `affects_gameplay` remains `false`.
  - Verify no gameplay models/content are registered by Neowtwork.
  - Verify friend-test package and Workshop package contain the same intended version.
- Define local-vs-Workshop development workflow.
  - Steam Workshop mods can take precedence over local mods when Workshop version is greater.
  - During development, confirm whether local or Workshop Neowtwork is loaded.
  - Consider using explicit dev/pre-release versions for local testing.
- Maintain manual zip packaging for early testers.
- Keep release zips limited to:
  - `Neowtwork/Neowtwork.dll`
  - `Neowtwork/Neowtwork.json`
  - `Neowtwork/Neowtwork.pck`
  - `INSTALL.txt`
- Do not package logs, saves, backups, `.godot`, or local machine paths.
- Prepare Steam Workshop release only after:
  - latest public beta compatibility passes
  - BaseLib dependency/install story is clear
  - save/progress behavior is documented
  - friend testing has passed on at least Mac and Windows
  - UI no longer feels like a prototype

## Technical cleanup

- Look for a safer/native way to reuse existing Compendium sort button visuals.
- Consider extracting stat formatting into a small helper to make overlay behavior easier to test.
- Consider renaming `CardLibraryWinRateSortPatch.cs` now that it handles all Card Stats sorts.
- Add lightweight comments only where Harmony patches rely on private fields or fragile scene structure.
- Keep generated Godot `.uid` files tracked when Godot creates them for tracked scripts.

## Validation checklist

Before calling a backlog item done:

1. Build succeeds.
2. Local mod copy is updated.
3. In-game screenshot or tester report confirms the UX if visual.
4. Logs have no new Neowtwork errors.
5. Commit and push after successful validation unless the user asks to keep changes local.
