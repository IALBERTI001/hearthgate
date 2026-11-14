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
4. **The town walk** (owner, Nov 13 2026, 19:41: "Add a button that makes you into the wizard that can walk into the town and upgrade the building from inside"): a wizard waits by the fountain; **Walk the town [T]** (or a tap on him) swings the camera into the town behind him, looking out toward the walls so the fight still shows. WASD / arrows / ZQSD or tap-to-walk. Next to a building a card offers its next level (output gain and cost; output doubles at levels 10, 25, 50, 100, the 2D game's rule) or, for a ruin the city can rebuild now, the rebuild; E or a tap. The towers keep fighting while he walks.
5. **Human scale and walk-in buildings** (owner, Nov 13 2026, 20:18 and 21:41): one scale for everything (storey 3.2 m, every door 1.2 x 2.4 m, windows 0.8 x 1.1 m, the wizard 1.8 m). Each building is a furnished room he walks into; its upper storey and roof hide while he is inside. The town grew to an 88 x 56 m ring with 6 m streets (tools/layout/town.py). Worked-on buildings stay still: sparks rise and the new level shows over the door. A signpost by every building shows its name, level and output while he walks.
6. **Pipeline:** Blender scripts (`tools/blender/hg_kit.py`, `hg_front.py`, `hg_city.py`) build and export every model; Unity wires them with one menu command (`Hearthgate > Set up everything`); the PC pulls the `unity-3d` branch from GitHub.

7. **Polish pass, Nov 14 2026** (owner: "Buttons are a mess, doesnt look polished ... the no road to gate part is a bit bad and the projectiles clipping the town is bad"):
   - One action-button kit: an icon rendered in Blender from the game's own model (tools/blender/hg_icons.py) in a brass medallion, the action in Lilita One, the cost with the gem (gold when affordable, rose when short), the key as a keycap on desktop; Upgrade gate is the one primary action with a breathing rim.
   - A laid cobble road with kerbs and milestones runs from the field to the gatehouse.
   - Towers shoot their own side first; a shot that must cross curves out in front of the gatehouse, never over the town. Shots keep their element's colour.
   - Sound: the 2D game's effects and music (streamed, so the first download stays at 10.6 MB), a click on every button and key, footsteps and a door knock in the town walk.
   - Settings (P or the cog by the wave): music and sound switches, every control, the build stamp. M mutes.
   - Build files are named by content hash, so a browser never mixes two builds.

## Quality testing
- **LayoutCheck** (Unity, runs before every WebGL build and fails it): no overlapping structures, nothing on a lane, plots and fountain inside the ring, each lane ending in front of its own tower, every plot's roof line, the plaza and the lanes seen from the camera, nothing under the HUD or out of the view at 16:9, 2.2:1 and 1.2:1.
- **tools/layout/town.py:** the search that places the camera, towers, plaza and plots (rows, 6 m streets, signs inside the ring); **gen_field.py** writes the numbers into Field.cs.
- **Build chain** (tools/blender/chain.py): kit test and export in four Blenders side by side, blocks batched into one mesh per material; about 8 minutes from push to WebGL build.
- **hg_kit_test.py** (Blender): fails when a model part is rotated about a far origin, and runs one real export that must not move the model (the bug that mirrored the ring wall on its first export).
- **Town walk check** (in LayoutCheck): from every spot the wizard can stand, the walk camera sees him past the wall, the gatehouse and the towers.
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
