# Friend Test Guide

Thanks for helping test Neowtwork.

Neowtwork is an early Slay the Spire 2 mod that adds extra card stats to the card compendium.

This is a manual local test build, not a Steam Workshop release yet.

## What you should see

In the card compendium, with `View Stats` enabled, cards should show:

- Win Rate with a victory-loss record
- Pick Rate with a picked-seen record

The card library should also have a `Card Stats` sorting dropdown with:

- Win Rate
- Pick Rate
- Victories
- Losses
- Picked
- Skipped
- Seen

Selecting the same sort twice should reverse the order.

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

On Windows/PC, that folder is usually:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\
```

If your Steam library is on another drive, it may be something like:

```text
D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\
```

To find it from Steam:

1. Right-click Slay the Spire 2 in your Steam Library.
2. Choose `Manage`.
3. Choose `Browse local files`.
4. Create or open the `mods` folder there.
5. Copy the `Neowtwork` folder into that `mods` folder.

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
3. If Neowtwork asks `Import base-game data?`, choose whether to import your vanilla progress into the modded profile.
4. Open the compendium/card library.
5. Enable `View Stats`.
6. Check a few cards.
7. Try the `Card Stats` sorting dropdown.

## What to report back

Please send:

- Did the game launch?
- Did BaseLib load?
- Did Neowtwork load?
- Did Neowtwork ask to import base-game data?
- If you chose Import, did your compendium/progress appear afterward?
- Did the compendium open?
- Did the extra card stats show?
- Did the `Card Stats` sorting dropdown work?
- Did anything crash or look broken?
- Screenshot if possible.

## Important note about progress

Slay the Spire 2 separates vanilla and modded profile progress.

If your modded profile looks empty or the compendium is locked, your vanilla progress is probably still safe; the game is just using a separate modded save lane.

Neowtwork may offer to import base-game data. This copies your vanilla profile into the matching modded profile after creating a backup.

- Choosing `Import` creates a backup first, then copies vanilla progress and run history into modded.
- Choosing `Not Now` does not change save files.
- Neowtwork does not copy modded data back into vanilla.
- Neowtwork does not control Steam Cloud.

Do not manually copy or edit save files unless Brandon explicitly asks you to.

## Steam Cloud warning

Steam Cloud can complicate modded-profile testing.

- Neowtwork imports local vanilla data into the local modded profile.
- Neowtwork does not resolve Steam Cloud conflicts.
- Do not choose random cloud conflict options just to make progress appear.
- If Steam shows a Cloud Conflict dialog after an intentional import, the local save is usually the one you want to keep.
- If unsure, pause and ask Brandon before choosing.
