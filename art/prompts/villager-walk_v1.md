# villager walk cycles v1 (Nov 13 2026)

Brief: owner, Nov 13 2026: "villagers still do not have a walk animation. They just move with the same image ... When they
turn around it's like a paper being flipped. I want immersion in the game." Each villager needs a real walk cycle. Until
the sheets arrive, the game builds a stepping motion from the one painted pose (feet warp in and out, body rises at the
passing moment, turns as pivots), which is a stopgap, not a walk.

- Seen: on the city paths, about 34-44 px tall at the reviewer's frame, 40-90 px on a large desktop. Master: each frame
  about 300 px tall.
- Read in half a second: the same villager as today (match the attached current sprite exactly: face, clothes, the thing
  carried), clearly walking.
- Frames: a 6-frame walk cycle turned toward the lower right (the game mirrors it for lower left): contact (right foot
  forward), down, passing, up, contact (left foot forward), down... ending where frame 1 begins. Arms and the carried
  thing move with the step; the head stays level; same scale and same ground line in every frame.
- One villager per sheet, 6 frames in one row, so frames match. Six sheets: builder, baker, farmer, lamplighter, child,
  merchant. Then a 2-frame turn (three-quarter right to facing the camera) per villager, so a turn passes through the
  front view instead of a mirror flip.
- Processing (sprite-and-growth-art): cut by outline onto a shared baseline, one atlas per villager
  (assets/city/folk-<id>-walk.webp, 6 cells), art-detail-standard on every frame at 100% and 200%.

## Prompt (one per villager; this is the baker)
```
Painted cartoon fantasy, three-quarter top-down camera, chunky readable shapes with dark coloured outlines, rich elemental colours, warm golden light from the upper left, clean flat play areas with detail kept to the borders.
A walk-cycle sprite sheet of ONE character: the attached baker (white apron and cap, red scarf, carrying a basket of bread loaves), exactly the same character, colours and painted style as the attached sprite. Six frames in one row on a 1536x512 canvas, every frame the same size and on the same ground line, the character turned toward the lower right, walking: 1 right foot forward heel down, 2 weight down on the right foot, 3 left leg passing, body highest, 4 left foot reaching forward, 5 left foot forward heel down, 6 weight down on the left foot. Arms and the basket swing a little with each step; the head stays level; same height in every frame (about 300 px).
Background: one solid flat pure magenta #FF00FF, no ground, no shadows on the background. Every frame entirely visible, nothing cropped, large empty gaps between frames.
Do not include: text, letters, numbers, logos, watermarks, frames or borders, ground, cast shadows, photorealism, extra objects.
```
Attach: assets/city/folk-baker.webp (the character), art/masters/city-north-gate-restored.png (style). Repeat with
builder (plank on shoulder, tool belt), farmer (straw hat, grain sack), lamplighter (blue coat, lantern pole), child (red
scarf, running), merchant (purple tunic, fruit crate).
