# Neowtwork Mod Backlog

This backlog tracks likely next steps for Neowtwork.

Keep the spirit of the game alive: small, readable, native-feeling improvements before big systems.

## Current focus

- Card Library analytics should feel like a feature Mega Crit could have shipped.
- Prefer built-in game stats when available.
- Use run-history parsing for richer stats only when built-in progress data is not enough.
- Avoid save-file writes unless the user explicitly approves a careful migration/sync feature.

## Near-term UX polish

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

- Show card stats while making card choices.
  - Add a persistent `Show card stats` checkbox below the `Skip` button on card reward screens.
  - Default to off for a clean vanilla-like first impression.
  - Remember the player's choice between sessions.
  - When enabled, show the same compact Win Rate / Pick Rate overlay used in the Card Library.
  - Prioritize card reward screens first, then extend to shop card choices if the UI hook is safe.
  - Avoid changing combat card views until explicitly designed.
- Add future card stat display modes for choice screens.
  - `Off`: never show stats.
  - `On`: always show stats.
  - `Hover`: show stats only while hovering a card, so players can still enjoy the card art.
  - Start with the simpler checkbox version before introducing a three-state control.
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
- Decide how multiplayer runs should be represented.
  - Separate toggle/filter?
  - Combined stats by default?
  - Clear labeling when multiplayer data is included?

## Save and progress sync

- Test and polish the first-run vanilla-to-modded progress import flow.
  - Must ask before copying anything.
  - Must explain what will happen.
  - Must create timestamped backups first.
  - Must never silently overwrite user progress.
- Current first pass detects likely sync states:
  - vanilla has progress, modded is empty
  - both have progress
  - modded has newer progress
  - missing or unreadable save folders
- Decide whether a helper script is also needed for users who cannot use the in-game prompt.
- Document manual sync steps for friends only after the safe flow is understood.

## Friend testing

- Keep `FRIEND_TEST.md` current with the actual UI.
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

- Maintain manual zip packaging for early testers.
- Keep release zips limited to:
  - `Neowtwork/Neowtwork.dll`
  - `Neowtwork/Neowtwork.json`
  - `Neowtwork/Neowtwork.pck`
  - `INSTALL.txt`
- Do not package logs, saves, backups, `.godot`, or local machine paths.
- Prepare Steam Workshop release only after:
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
