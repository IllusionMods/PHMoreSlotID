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
