# Hearthgate 3D: plan, Nov 13 2026

The owner's decision (Nov 13 2026): "make the game in the Unity engine. All assets will be made in Blender and brought
together in Unity. The goal is 3D!" All four decisions below were approved the same day ("yes to everything").

## What carries over, what is remade
- **Kept as is:** the rules and every balance number (ported to C#: `unity/Hearthgate3D/Assets/Scripts/Core/HGCore.cs`, checked against the 2D game by a menu command: 37 checks match), the voice lines, music and sound, Magister Bram's script, the design rules in the skills (one steady working area, text floors, no slop).
- **Remade:** all art (Blender models), effects, the HUD and menus (Unity UI), the camera.
- The 2D build 13j passed every QA check and is committed; it stays the live version until the 3D one beats it.

## Decisions (approved)
1. **Engine and target:** Unity 6 (6000.6.7f1) with URP, WebGL for CrazyGames and Freeplay; first download 20 MB or less (now 10.4 MB).
2. **Art direction:** stylised, chunky, warm 3D matching the 2D style guide (art/STYLE.md): warm light from the upper left, plum and brass UI, Lilita One and Nunito.
3. **Camera and layout:** one fixed view from outside the walls. **The owner's layout (Nov 13 2026, 18:44, with a sketch):** "Make it so that monsters are walking up to the towers on a route in front of each tower. Close off the loop around town to make a protected town." The town sits inside a closed ring wall; the gatehouse is the front point of the ring with two element towers each side on the front arc; four lanes run up the screen from below its bottom edge, one to the foot of each tower, and the enemies attack the wall there. Fire caps are red and orange ("lets make them red and orange tops"); the owner likes the tower look. The numbers live in `tools/blender/hg_layout.py` (shared by the kit, the preview and `Field.cs`) and come from the search in `tools/layout/ring.py`.
4. **Pipeline:** Blender scripts (`tools/blender/hg_kit.py`, `hg_front.py`, `hg_city.py`) build and export every model; Unity wires them with one menu command (`Hearthgate > Set up everything`); the PC pulls the `unity-3d` branch from GitHub.

## Quality testing
- **LayoutCheck** (Unity, runs before every WebGL build and fails it): no overlapping structures, nothing on a lane, plots and fountain inside the ring, each lane ending in front of its own tower, every plot's roof line, the plaza and the lanes seen from the camera, nothing under the HUD or out of the view at 16:9, 2.2:1 and 1.2:1.
- **tools/layout/ring.py:** the search that placed the camera, towers, plaza and plots using the real model heights and every window shape.
- **hg_kit_test.py** (Blender): fails when a model part is rotated about a far origin, and runs one real export that must not move the model (the bug that mirrored the ring wall on its first export).
- **hg_scene.py:** renders the game view and a plan view in Blender for fast art reviews; every pass goes to a fresh-eyes reviewer that sees only the frames, then the real WebGL build is reviewed the same way.

## Phases
- **P0, pipeline slice:** done. Waves, four element towers, combos, Upgrade and Rebuild, city income, saves, offline earnings.
- **P1, art direction:** in progress. The owner's ring layout is in; hero gatehouse, towers with element caps, slime, beetle, golem, the ten city buildings and burnt ruins, lanes, ground, fountain, HUD in the game's style.
- **P2, the full kit:** enemy walk rigs, villagers, effects per element.
- **P3, the rest of the systems:** Ascension, constellations, relics, perks, events, quests, feats, the shop, butterflies, Magister Bram.
- **P4, UI and polish** to the game-polish rules.
- **P5, QA and resubmission.** The pinned CrazyGames note says never upload a rejected game as new: reply in the original support thread with a numbered list of what changed.

## Open question for the owner
- The health bar still says "Gate" and the button "Upgrade gate", but enemies now attack the wall at the towers. Keep "gate" as the name for the whole defence, or rename it to "walls"?

## Risks
- 3D on phones in a browser: the scene is about 175k triangles with soft shadows; check frame rate on the owner's Galaxy Z Fold 6 before release and drop shadows on mobile if needed.
- Unity and Blender run only on the owner's PC (Blender open with its MCP server; Unity in batch mode from Blender).
