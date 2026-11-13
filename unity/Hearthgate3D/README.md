# Hearthgate 3D (Unity)

The 3D remake of Hearthgate (owner, Nov 13 2026: "make the game in the Unity engine. All assets will be made in Blender
and brought together in Unity. The goal is 3D!"). Same rules as the 2D game: `Assets/Scripts/Core/HGCore.cs` is a port of
`js/core.js`, and `Hearthgate > Check rules against the 2D game` proves the numbers match.

## First run (on the PC)
1. Unity Hub > Add project from disk > this folder. Use Unity 6 (6000.0 LTS or newer) with the **WebGL Build Support** module.
2. Models: run `tools/blender/hg_kit.py` in Blender, pointing it at `Assets/Art/Models`:
   `blender -b --factory-startup -P tools/blender/hg_kit.py -- <this folder>\Assets\Art\Models`
3. In Unity: menu **Hearthgate > Set up everything**, then open `Assets/Scenes/Main.unity` and press Play.
4. WebGL build for CrazyGames: **Hearthgate > Build WebGL** (output in `Builds/WebGL`).

Batch mode (no window), same steps:
`Unity.exe -batchmode -quit -projectPath <this folder> -executeMethod Hearthgate.EditorTools.HearthgateSetup.SetupAll -logFile setup.log`

## What is in the slice
Waves walk the road to the gate; four towers on the wall shoot with the elements the city has (Fire burns, Frost slows,
Storm chains; SHATTER, SUPERCHAIN and FIRESTORM combos); kills pay Resonance; Upgrade [U] raises the gate (forks at the
stages); Rebuild [R] restores the next building, which hums out Resonance; the gate falling sends you back a wave; offline
earnings; saves. Missing models show as labelled stand-ins and a console warning (they never ship).
