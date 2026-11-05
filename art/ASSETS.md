# Hearthgate asset list and log

## Asset list
| Id | Type | Where seen | Size at play | Master | States | Status |
| --- | --- | --- | --- | --- | --- | --- |
| city-north-gate | ground painting | middle of the screen: city, north wall, gatehouse, 4 tower platforms | 720x480 | 1536x1024 | restored, ruined, wall layer | shipped (Nov 5) |
| towers fire/frost/storm 1-3 | tier set | tower platforms on the wall | about 112 px tall | Warden element-tiers.png | 3 tiers each | shipped (split by outline) |
| slime, beetle, golem walk | enemy | field | 70 to 150 px | Warden enemy-walk.png | 6 frames each | shipped (split by outline) |
| meadow | ground | field | 720x680 | Warden art/ground/outer/meadow_v1.png | - | shipped |
| forest edge | kit | treeline at the top of the field | see manifest | Warden art/kits/outer | - | shipped (walls and gateposts kit pieces retired Nov 5, still in the asset folder for the asset test) |

## Log
| File | Asset id | Source | Date | Prompt | Processing |
| --- | --- | --- | --- | --- | --- |
| assets/towers/*.webp | towers | Warden of Power element-tiers.png (AI image generation, ChatGPT) | Nov 5 2026 | Warden art/prompts | tools/split_sheets.py: split by outline, padded, one sheet each |
| assets/enemies/*-walk.webp | enemies | Warden of Power enemy-walk.png (AI image generation, ChatGPT) | Nov 5 2026 | Warden art/prompts | tools/split_sheets.py |
| assets/kit/*.webp, assets/ground/meadow.webp | Outer Seal kit | Warden of Power art/kits/outer, art/ground/outer | Nov 5 2026 | Warden art/prompts/outer-seal_v2.md | tools/build_kit.py |
| assets/city/city-*.webp | hub painting (interim) | Warden of Power hub-ruined/restored.jpg | Nov 5 2026 | Warden art/prompts | cropped around the plaza; replaced by city-north-gate |
| art/masters/city-north-gate-restored.png | city-north-gate | AI image generation (ChatGPT, run by Claude in the built-in browser), from the layout guide and Warden's restored hub as style reference | Nov 5 2026 | art/prompts/city-north-gate_v1.md | master as generated; assets/city/city-restored.webp = crop 40,0,1440,960 at half scale |
| art/masters/city-north-gate-ruined.png | city-north-gate | AI image generation (ChatGPT), repainted from the restored image | Nov 5 2026 | art/prompts/city-north-gate_v1.md | aligned to the restored master (OpenCV ECC), grass between buildings healed from the restored master; same crop |
| art/masters/city-wall.png, assets/city/city-wall.webp | city wall layer | cut from the restored master by tools/wall_layer.py | Nov 5 2026 | - | wall, gatehouse and platforms as a foreground layer over enemies outside the wall |
| assets/sfx/*.mp3, assets/music/battle.mp3 | sound | Warden of Power (studio-made) previews/audio-v3 and theme-engine | Nov 5 2026 | - | mono 64 kbps MP3; levels -22 to -28 LUFS, peaks under -9.7 dBFS |
| store/cover-A-*.jpg | cover concept A | built by tools/covers.py from the shipped masters, sprites and Lilita One | Nov 5 2026 | - | ruins left, restored right; no art made for the cover only |
| store/cover-B-800x1200.jpg | cover concept B | real game frame (late-game save, HUD hidden) at 2x, title in Lilita One | Nov 5 2026 | - | numbers on it (wave 41) reachable in the build |
