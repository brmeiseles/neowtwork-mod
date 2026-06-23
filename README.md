# Neowtwork Mod

Neowtwork is an early Slay the Spire 2 statistics mod.

The current prototype improves the card compendium by showing additional card stats and adding compact stat-based sorting controls.

## Current features

- Expands the card compendium stat overlay with:
  - Victories
  - Losses
  - Win Rate
  - Picked
  - Skipped
  - Seen
  - Pick Rate
- Adds compact card-library stat sort buttons:
  - `WR` = Win Rate
  - `V` = Victories
  - `L` = Losses
  - `P` = Picked
  - `S` = Skipped
- Clicking the same stat sort button twice reverses the exact sort order.
- Includes a read-only run-history scanner that currently logs summary information.

## Friend testing

For manual tester instructions, see [FRIEND_TEST.md](./FRIEND_TEST.md).

## Requirements

- Slay the Spire 2 on Steam
- BaseLib for Slay the Spire 2
- MegaDot / Godot .NET setup compatible with Slay the Spire 2 modding
- .NET SDK 9 or newer

This has been tested locally on macOS / Apple Silicon using the public Slay the Spire 2 build.

## Important save note

Slay the Spire 2 separates vanilla and modded profile progress. During local development, Steam Cloud can overwrite local modded save changes.

For local testing, Steam Cloud may need to be disabled temporarily if the modded profile appears to lose compendium or character progress.

This mod does not intentionally edit save files.

## Development status

This is a first-pass local development prototype. It is not ready for Steam Workshop publication yet.

Planned next areas:

- More card history stats from `.run` files
- Relic stats from run history
- Better UI polish for the stat sort controls
- Friend/manual test packaging
- Steam Workshop packaging later
