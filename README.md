# PHMoreSlotID.BepInEx
This is a version of PHMoreSlotID converted to a BepInEx patcher. It's a soft patch, which means that the game files no longer need to be modified. Simply place the new patcher inside BepInEx/patchers and it will work like the old one.

### Changes from the original version:
- No longer need to edit game files
- Less log spam which can noticeably improve loading time (especially if you have console open)
- Recreates list directories if they are removed for some reason instead of crashing

### How to use
1. You need at least BepInEx v5.0 installed.
2. If you had the hard patch version of this mod, go to each of your `*_Data/Managed` folders and rename `Assembly-CSharp.dll.original` to `Assembly-CSharp.dll` (replacing the current dll).
3. Get the latest release and extract the .dll files into the `BepInEx\patchers` folder.
4. Additional speed up can be achieved by downloading **PHListSeparator.exe** [from here](https://github.com/nx98304/PHMoreSlotIDFaster/releases/tag/v1.0) , extract the zip into PH folder and run the exe, so that all PH vanilla lists are separated into individual txt files. **You only have to do this once**, unless you later installed new mods that uses internal embedded lists. The extracted list files will be placed into the correct lists folder automatically, and you don't have to anything additional. They are named like `cf_top_hsad_list.txt`, which entirely rely on their own filename and filepath to match up with corresponding abdatas. **Do not confuse them with Mlist.txt**, they are slightly different in that Mlist's first line can assign the abdata at arbitrary location within `PH/abdata` folder. Note that **PHListSeparator** may create a few big swap files that you will need to remove manually.

## Remarks with regard to PHMoreSlotIDFaster v1.0

- Vastly speed up PH's game initialization. Vastly speed up PH's chara maker initialization.

### Technical Details

- `PHMoreSlotID.dll` and `PHMoreSlotIDPatchContainer.dll` are changed so they: 
  - Prioritizes `abdata/thumbnail`'s packaged dds thumbnails assetbundles folder over `abdata/thumbnail_R`, because individual PNGs are slow to load and unpack. 
  - `AssetBundleController.LoadAsset` function modified so that it determines the `AssetBundle.LoadFromFile` call, which effectively delays loading any assetbundles as late as possible. 
  - Removed the need of writing out to a temp file when loading Mlist.txts.
- The game's `AssetBundleController.OpenFromFile` no longer calls LoadFromFile, which is patched inside `PHMoreSlotIDFaster.dll`. 
- Patched `EditMode.CreateData` functions so that it doesn't load any thumbnail during initialization phase. 
  - Instead, the task of loading thumbnails will be queued and consumed in `EditMode.Update`.
  - Further create a timer to gradually update the thumbnails on the menu, if the menu happens to be opened at the moment. 
  - Thus, **it is normal** that after you loaded into chara maker, FPS is lowered for a while. Because it is loading thumbnails in the background. 
