# Changelog

All notable changes to Neowtwork will be tracked here.

Neowtwork uses semantic versioning.

## Unreleased

## 0.1.1 - 2026-06-23

### Added

- Backlog tracking for planned UX, analytics, import, packaging, and release work.
- Planned in-run Card Library shortcut backlog item.
- Planned card reward/shop stats toggle and shop analytics backlog items.

### Changed

- Ongoing Card Stats sorter closed-state polish.

### Fixed

- Stabilized the Card Stats sort row so the visible row and clickable target are the same control.

## 0.1.0 - 2026-06-23

### Added

- Compact Card Library analytics overlay:
  - Win Rate with victory/loss sample size.
  - Pick Rate with picked/seen sample size.
- Card Stats sorting for:
  - Win Rate
  - Pick Rate
  - Victories
  - Losses
  - Picked
  - Skipped
  - Seen
- Reverse sorting by selecting the same Card Stats metric again.
- First-run base-game import prompt for copying vanilla progress into the matching modded profile.
  - Prompts before copying.
  - Creates a backup first.
  - Copies vanilla profile data into modded only.
  - Does not copy modded data back into vanilla.
  - Does not control Steam Cloud.
- Read-only run-history scanner diagnostics.
- Friend test guide with Mac and Windows install notes.
- Repository instructions in `AGENTS.md`.

### Changed

- Refined Card Library overlay language to avoid developer shorthand.
- Reworked Card Stats sorting controls from small metric buttons into a dropdown/menu.
- Improved friend-test documentation around import behavior and Steam Cloud caution.

### Fixed

- Tightened import prompt eligibility so tiny placeholder profiles do not trigger import.
- Improved Card Stats sort row click target.
