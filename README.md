# Neowtwork Mod

Neowtwork is an early Slay the Spire 2 statistics mod.

The current prototype improves the card compendium by showing compact card analytics and adding stat-based sorting controls.

## Current features

- Adds a compact card compendium stat overlay:
  - Win Rate with victory-loss record
  - Pick Rate with picked-seen record
- Adds a card-library stats dropdown for sorting by:
  - Win Rate
  - Pick Rate
  - Victories
  - Losses
  - Picked
  - Skipped
  - Seen
- Selecting the same stat sort twice reverses the exact sort order.
- Includes a read-only run-history scanner that currently logs summary information.
- Reads local progress/run history for analytics without modifying save files.

## Friend testing

For manual tester instructions, see [FRIEND_TEST.md](./FRIEND_TEST.md).

## Backlog

For planned UX, analytics, sync, packaging, and release work, see [BACKLOG.md](./BACKLOG.md).

## Changelog

For version history, see [CHANGELOG.md](./CHANGELOG.md).

## Requirements

- Slay the Spire 2 on Steam
- BaseLib for Slay the Spire 2
- MegaDot / Godot .NET setup compatible with Slay the Spire 2 modding
- .NET SDK 9 or newer

This has been tested locally on macOS / Apple Silicon using the public Slay the Spire 2 build.

## Important save note

Slay the Spire 2 separates vanilla and modded profile progress. During local development, Steam Cloud can overwrite local modded save changes.

Current Slay the Spire 2 public beta builds copy base-game progress into the modded save lane on first modded launch.

Neowtwork does not import, sync, overwrite, or delete save files. It does not control Steam Cloud or resolve Steam Cloud conflicts automatically.

## Development status

This is an early public-beta mod and is being prepared for a private Steam Workshop test release.

Planned next areas:

- More card history stats from `.run` files
- Relic stats from run history
- Better UI polish for the stat sort controls
- Workshop private upload validation
- Multiplayer Workshop validation
