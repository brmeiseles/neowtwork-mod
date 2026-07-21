# Neowtwork Workshop Release Checklist

Current target: `0.3.5`

## Current stance

Neowtwork is read-only for progress/save data.

- It may read local progress and run-history files for analytics.
- It must not import, sync, overwrite, or delete save files.
- Slay the Spire 2's native first-run modded progress setup is trusted.

## Pre-upload checks

- Build succeeds.
- Publish/export produces fresh:
  - `Neowtwork.dll`
  - `Neowtwork.json`
  - `Neowtwork.pck`
- Local launch through Steam succeeds.
- Log confirms:
  - BaseLib loads.
  - Neowtwork loads.
  - `Loaded 2 mods`.
  - No Neowtwork auto-sync/import messages.
  - No new Neowtwork errors.
- `Neowtwork.json` has:
  - version `0.3.5`
  - BaseLib dependency `3.3.6`
  - `affects_gameplay: false`

## Workshop workspace

Workspace path:

```text
workshop/neowtwork
```

Generated content path:

```text
workshop/neowtwork/content/Neowtwork
```

Expected content:

```text
Neowtwork.dll
Neowtwork.json
Neowtwork.pck
```

Preview image:

```text
workshop/neowtwork/image.png
```

The image must stay under 1MB.

## Metadata

Workshop metadata is in:

```text
workshop/neowtwork/workshop.json
```

Workshop item ID is tracked in:

```text
workshop/neowtwork/mod_id.txt
```

Current private Workshop item:

```text
3768751760
```

Initial release should remain:

```json
"visibility": "private"
```

BaseLib Workshop dependency:

```text
3737335127
```

In `workshop.json`, Workshop dependencies must be numeric Steam Workshop IDs, not strings.

Do not set `minBranch` / `maxBranch` in `workshop.json` for now. Steam accepted the upload only after those branch restrictions were removed; keeping compatibility in `Neowtwork.json` is enough for the current public-beta release.

## Upload

Use Mega Crit's official uploader.

Expected command shape:

```sh
ModUploader upload -w workshop/neowtwork
```

Only upload after confirming Steam is running and you are logged into the correct Steam account.

Before the first successful upload, confirm the Steam Workshop legal agreement is accepted:

```text
https://steamcommunity.com/sharedfiles/workshoplegalagreement
```

If an upload hangs at `PreparingConfig` or `PreparingContent`, check the legal agreement and Steam client state before retrying with the saved item ID:

```sh
ModUploader upload -w workshop/neowtwork -i 3768751760
```

## Private Workshop validation

After private upload:

1. Subscribe to the private Workshop item.
2. Temporarily remove or disable the local `Neowtwork` mod folder.
3. Launch Slay the Spire 2 from Steam.
4. Confirm the Workshop version loads.
5. Confirm BaseLib dependency loads.
6. Confirm no local save import/sync occurs.
7. Confirm:
   - Mod Configuration opens.
   - Card Library stats render.
   - Card Stats sorting works both directions.
   - in-run reward/shop stats work.
   - event option stats work.
   - relic hover stats work.
   - Compendium dashboard opens.
8. Check logs for Neowtwork errors.

## Multiplayer validation

Before public release:

1. Both players subscribe to Workshop BaseLib and Workshop Neowtwork.
2. Both players launch the same Slay the Spire 2 branch.
3. Confirm no mod mismatch warning.
4. Start or join a multiplayer run.
5. Confirm analytics UI does not interfere with play.

## Public release

Only switch to public after:

- private Workshop load test passes
- multiplayer same-mod-list test passes
- README no longer claims the mod is not Workshop-ready
- known limitations are documented
