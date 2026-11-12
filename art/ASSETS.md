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
| store/cover-B-800x1200.jpg | cover concept B | real game frame (late-game save, HUD and markers hidden) at 2x, tools/cover_b_capture.cjs + tools/cover_b.py, title in Lilita One | Nov 5 2026 (recaptured after the enemy rescale and Resonance) | - | numbers on it (wave 41) reachable in the build |
| js/looks.js (Shop skins) | tower skins, town looks, trails, hits, wisps | made in code from the shipped sheets: colour recipes applied at run time to towers/*.webp and the city, meadow, forest and wall images; trails, hit effects and wisps drawn | Nov 5 2026 | - | no new image files; the one-asset-one-sheet rule is untouched |
| assets/sfx/{buy,feat,surge,claim,boss_horn,level_up}.mp3 | sounds for the Shop, Feats, Surge, quests, boss arrival, building levels | synthesised in code by tools/make_sfx.py (no samples) | Nov 6 2026 | - | mono 64 kbps; -21 to -26 LUFS, matched to the Warden effects |
| store/cover-C-800x1200.jpg | cover concept C (Shop looks) | real game frame (Moonlit city, Shop towers, HUD and medals hidden), tools/cover_c_capture.cjs + tools/cover_b.py, title in Lilita One | Nov 6 2026 | - | the looks on it are all in the build |
| store/trailer-portrait-1080x1620.mp4, store/trailer-landscape-1920x1080.mp4 | trailer | real gameplay stepped at 30 fps with Playwright's clock (tools/trailer/capture.cjs, 0-12.4 s; capture-seg.cjs adds the Meteor Shower and the Wraith King after the 2nd Ascension, Nov 6 build s), assembled by tools/trailer/assemble.sh with the game's own music and Lilita One | Nov 6 2026 | - | 19 s, H.264 High, yuv420p, faststart, 11.8 MB and 7.3 MB |
| assets/music/{hearthfire,lantern-walk,starlit-wall,rebuilding}.mp3, assets/ambience/town.mp3 | calm playlist (4 songs, 2.2 to 2.7 min, 72 to 88 BPM) and the town ambience between songs | synthesised in code by tools/make_music.py (8-bit voices, no samples); WAV masters in art/masters/audio | Nov 6 2026 | - | stereo 128 kbps; songs -23.4 LUFS, battle theme -22.4 (boss slightly louder), ambience -31; peaks under -10 dBFS |
| assets/city/folk-{builder,builderwork,baker,farmer,lamp,child,merchant}.webp | villagers | AI image generation (ChatGPT, run by Claude in the built-in browser, in the "Create Fantasy City Map" chat so the city painting was the style reference) | Nov 6 2026 | art/prompts/villagers_v1.md | magenta keyed and split by outline in the page, 112 px tall WebP; replaced the code-drawn block villagers |
| assets/voice/*.mp3 (18 lines: intro, restore, forge, frostSpire, stormBell, gate, boss, fell, surge, whole, ascend) | the Magister's spoken lines in the first run | recorded by the owner (his own voice), Nov 12 2026; m4a masters in art/masters/audio/voice | Nov 12 2026 | - | tools/make_voice.py: clicks and silence trimmed, rumble cut, gentle compression, -19 LUFS, peaks under -0.5 dBTP, mono 80 kbps; the music dips while he speaks |
| art/masters/keyart.png; store/cover-{1920x1080,1600x1000,1080x1620,800x1200,800x800}.jpg | store covers and the first frame of the preview videos | ChatGPT image generation in the "Create Fantasy City Map" chat (prompts in art/prompts/keyart_v1.md); title set by tools/covers/make_covers.py in Lilita One | Nov 12 2026 | - | crops of one painting, scaled 0.93x to 1.58x with Lanczos, never stretched; title is the only text (CrazyGames rule, Nov 12 2026) |
