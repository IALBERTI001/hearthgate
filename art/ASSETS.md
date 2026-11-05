# Hearthgate asset list and log

## Asset list
| Id | Type | Where seen | Size at play | Master | States | Status |
| --- | --- | --- | --- | --- | --- | --- |
| city-north-gate | ground painting | middle of the screen: city, north wall, gatehouse, 4 tower platforms | 720x440 | 1536x1024 | restored, ruined | brief (Nov 5) |
| towers fire/frost/storm 1-3 | tier set | tower platforms on the wall | about 112 px tall | Warden element-tiers.png | 3 tiers each | shipped (split by outline) |
| slime, beetle, golem walk | enemy | field | 70 to 150 px | Warden enemy-walk.png | 6 frames each | shipped (split by outline) |
| meadow | ground | field | 720x680 | Warden art/ground/outer/meadow_v1.png | - | shipped |
| forest edge, walls, gatepost | kit | field top, wall line | see manifest | Warden art/kits/outer | - | shipped; walls and gateposts retire when city-north-gate lands |

## Log
| File | Asset id | Source | Date | Prompt | Processing |
| --- | --- | --- | --- | --- | --- |
| assets/towers/*.webp | towers | Warden of Power element-tiers.png (AI image generation, ChatGPT) | Nov 5 2026 | Warden art/prompts | tools/split_sheets.py: split by outline, padded, one sheet each |
| assets/enemies/*-walk.webp | enemies | Warden of Power enemy-walk.png (AI image generation, ChatGPT) | Nov 5 2026 | Warden art/prompts | tools/split_sheets.py |
| assets/kit/*.webp, assets/ground/meadow.webp | Outer Seal kit | Warden of Power art/kits/outer, art/ground/outer | Nov 5 2026 | Warden art/prompts/outer-seal_v2.md | tools/build_kit.py |
| assets/city/city-*.webp | hub painting (interim) | Warden of Power hub-ruined/restored.jpg | Nov 5 2026 | Warden art/prompts | cropped around the plaza; replaced by city-north-gate |
