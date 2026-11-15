# Hearthgate 3D: HUD and UI design (game-hud-ui-design, Nov 14 2026)

Desktop first (CrazyGames). Reference frame 1280 x 720; the player's HUD size (80 to 125%) scales the whole canvas.

## 1. Information map

| Information or action | When it is needed | Where it lives | Presses to reach |
|---|---|---|---|
| Resonance (the spent currency, the primary number) | All the time | Top left, the largest number | 0 |
| Wave, city income, the named wave | All the time | Top left, around Resonance | 0 |
| Starshards, perk points | When spending | Top left row; a click explains each | 0 |
| Ascension, progress to the next | All the time (the lasting progress) | Top left, gold row; hourglasses circle the fountain | 0 |
| Upgrade defenses (U) | Every few seconds | The gatehouse's ring (click the gatehouse); the U key anywhere | 1 (key) / 2 |
| Defenses level | All the time | A plaque over the gate's arch | 0 |
| Ascend | Once a run | The gatehouse's ring | 2 |
| Rebuild the next building (R) / raise a building | A few times a run | The building's ring (click it); the R key | 1 (key) / 2 |
| A tower's element and designs | Now and then | The tower's ring (click it) | 2 |
| Walk the town / back to the walls (T) | Often | Top right | 1 |
| Walls' health | All the time in a wave | Bottom centre bar | 0 |
| Boss health | During a boss | Bottom centre, over the walls bar | 0 |
| Time Stop (F) and later skills | In danger, on the walk | Bottom centre, over the walls bar, walk only | 1 |
| Raise any building | Often | Tobin's ledger at the Market hub; or the building's own board on the walk | 2 (walk, talk) |
| Perks, Shop, quests, training | Between waves | Keepers: Orrin, Mira, Elder Quill, Aurelian (! and ? over them) | 2 |
| Settings, HUD size and opacity | Rarely | Bottom right | 1 |
| Controls | Once, then rarely | Settings > Controls | 2 |
| The walls fallen: rewind choices | On a loss | A card | 0 |

## 2. Phase table

| Element | Walls view | Town walk | Boss | Walls fallen | A card open |
|---|---|---|---|---|---|
| Top-left readouts | shown | shown | shown | shown | dimmed under the card |
| Walk the town button | shown | shown | shown | hidden | dimmed |
| A ring (gatehouse, tower, building) | on a click | closes | on a click | closes | closes |
| Walls bar | shown | shown | shown | "Walls broken" | dimmed |
| Boss bar | - | - | shown (raised over the skill row on the walk) | hidden | dimmed |
| Skill row (Time Stop) | hidden | shown | shown on the walk | hidden | hidden |
| Building boards | - | the two nearest | as the walk | hidden | hidden |
| Keeper ? (an offer) and ! (a reward) | over the roofs | over the heads | as before | hidden | - |
| Combat words | shown | only those on screen | shown | - | - |
| Guide line, watch's call | bottom left | bottom left | bottom left | - | - |

## 3. Position rules

- Resources and progress: top left, in the body face (bold); titles in the display face.
- Actions on a thing: a ring of kit buttons around the cursor where the thing was pressed, its title in a hub at the centre, kept whole on screen. Press and hold, lean toward an option (it lights), let go to choose; a quick click leaves the ring open to click. On the town walk a long press anywhere opens the wizard's ring (Upgrade defenses, Stop time, Back to the walls), and Upgrade defenses (U) sits beside the walk button. The gold marker bobs over the gatehouse when the guide points there.
- Other actions (Walk the town): top right, kit buttons, keycap in the corner.
- Health: bottom centre (walls; a boss over it); the wizard's skills sit over the walls bar, on the walk.
- Settings: bottom right, a kit button with its word.
- Words from the guide and the watch: bottom left. Banners: under the top row, centred.
- Things in the world (the Surge board, building boards, keeper signs, the hourglasses) sit on the thing they describe.

## 4. Motion table

| Element type | Motion | Goal |
|---|---|---|
| Every card (Settings, keepers, ledger, shop, perks) | Rises 26 px into place and fades in, 0.16 s | Consistency |
| A ring | Its buttons slide out from the thing's centre and fade in, 0.14 s | Awareness |
| Buttons | A small squeeze when pressed, a click; unavailable ones dim under a veil | Responsiveness |
| The one button to press next | Its gold rim breathes; only it, while the guide points | Intention |
| Resonance | Counts toward the new value | Responsiveness |
| Combat and reward words | Pop up from where they happen and drift up | Awareness |
| Time Stop dial, rewind | The clock lurches, rises, shrinks; buildings rise back in reverse | Physical intuition |

## 5. Player settings

HUD size (80 to 125%) and HUD opacity (40 to 100%, readouts and buttons only; cards stay solid), saved; Default HUD restores both. Controls card one press from Settings.

## Open

- Touch layout (rule 6): not yet; the game is desktop first. Needed before claiming mobile.
- Leader study (rule 2) and the icon-naming test (rule 4) with a fresh reviewer: to do.
- Awareness motion still to add: a card growing out of the button that opened it, Resonance flying to its counter.
