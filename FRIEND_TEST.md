# Friend Test Guide

Thanks for helping test Neowtwork.

Neowtwork is an early Slay the Spire 2 mod that adds extra card stats to the card compendium.

This is a manual local test build, not a Steam Workshop release yet.

## What you should see

In the card compendium, with `View Stats` enabled, cards should show:

- Victories
- Losses
- Win Rate
- Picked
- Skipped
- Seen
- Pick Rate

The card library should also have compact stat sort buttons:

- `WR` = Win Rate
- `V` = Victories
- `L` = Losses
- `P` = Picked
- `S` = Skipped

Clicking the same sort button twice should reverse the order.

## Requirements

- Slay the Spire 2 on Steam
- BaseLib for Slay the Spire 2
- The Neowtwork release zip

## Install BaseLib

BaseLib is required.

The easiest route is to install BaseLib from the Slay the Spire 2 Steam Workshop if it works on your machine.

If Workshop BaseLib causes launch problems, use the manual BaseLib release from GitHub instead:

https://github.com/Alchyr/BaseLib-StS2/releases

## Install Neowtwork

1. Download the latest Neowtwork release zip:

   https://github.com/brmeiseles/neowtwork-mod/releases

2. Unzip it.

3. Find the folder named:

   ```text
   Neowtwork
   ```

4. Copy that whole `Neowtwork` folder into your Slay the Spire 2 local `mods` folder.

On macOS, that folder is usually inside the game app bundle:

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/
```

If the `mods` folder does not exist, create it.

After installing, you should have something like:

```text
mods/
  BaseLib/
  Neowtwork/
    Neowtwork.dll
    Neowtwork.json
    Neowtwork.pck
```

## Launch and test

1. Launch Slay the Spire 2.
2. Confirm the game says it is running modded.
3. Open the compendium/card library.
4. Enable `View Stats`.
5. Check a few cards.
6. Try the stat sort buttons: `WR`, `V`, `L`, `P`, `S`.

## What to report back

Please send:

- Did the game launch?
- Did BaseLib load?
- Did Neowtwork load?
- Did the compendium open?
- Did the extra card stats show?
- Did the stat sort buttons work?
- Did anything crash or look broken?
- Screenshot if possible.

## Important note about progress

Slay the Spire 2 separates vanilla and modded profile progress.

If your modded profile looks empty or the compendium is locked, your vanilla progress is probably still safe; the game is just using a separate modded save lane.

For this first test, it is okay if your stats are sparse or empty. The most important thing is whether the mod loads and the UI works.

Do not copy or edit save files unless Brandon explicitly asks you to.

## Steam Cloud warning

Steam Cloud can complicate modded-profile testing.

For this first friend test:

- Do not manually copy save files.
- Do not try to sync vanilla progress into the modded profile.
- Do not choose random cloud conflict options just to make progress appear.
- If Steam shows a Cloud Conflict dialog, pause and ask Brandon before choosing.

If we later decide to mirror vanilla progress into the modded profile, that should be done carefully with backups. It is not required for this first install/load test.
