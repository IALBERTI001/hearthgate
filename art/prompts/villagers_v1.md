# villagers v1 (Nov 6 2026)

Brief: townsfolk who walk the city's paths, carry things between buildings and build, so the city feels alive
(owner, Nov 6 2026: "An animation of villagers around moving and building things would not hurt"). They replace the
three code-drawn block figures that mend the gate after a wave.

- Seen: on the paved paths of the city painting, between buildings; about 34 px tall on the 720 px stage
  (a building is about 110 px). Master: each figure about 300 px tall on a 1536x1024 sheet.
- Read in half a second: a tiny cheerful villager with a job (builder, baker, farmer, lamplighter, child, merchant).
- Poses: one standing three-quarter pose facing lower-right per villager (the game mirrors it for left, and
  animates walking with a hop and lean, so no frame cycle is asked of the image tool), plus a second pose for the
  builder: hammer raised.
- Proportions: chunky, about 2.5 heads tall, big hands and feet, readable silhouette when filled black at 34 px.
- One asset, one sheet: generated together so they match, then split by outline into assets/city/folk-<id>.webp.
- Background: flat #FF00FF, no shadow on the background, a gap of at least 120 px between figures.

## Prompt
```
Painted cartoon fantasy, three-quarter top-down camera, chunky readable shapes with dark coloured outlines, rich elemental colours, warm golden light from the upper left, clean flat play areas with detail kept to the borders.
A sprite sheet of seven tiny cheerful medieval fantasy villagers for a city-building game, in the exact painted style of the attached city image (same outlines, colours and detail). Each figure is chunky and cute, about 2.5 heads tall, big hands and feet, simple readable shapes, seen from a three-quarter top-down camera, standing in a mid-step walking pose turned toward the lower right.
Two rows on a 1536x1024 landscape canvas, every figure the same height (about 300 px), centred in its own cell with large empty gaps between figures:
Row 1: a builder with a hard leather cap, tool belt and a hammer at his side carrying a wooden plank on his shoulder; the same builder kneeling with the hammer raised above his head; a baker in a white apron and cap carrying a basket of bread loaves; a farmer in a straw hat carrying a sack of grain over her shoulder.
Row 2: a lamplighter in a blue coat holding a long lighting pole; a small child with a red scarf running happily; a merchant in a purple tunic pushing nothing, holding a little wooden crate of fruit.
Colours: warm earth tones with one bright accent each (red, blue, green, purple, gold), matching the city.
Background: one solid flat pure magenta #FF00FF everywhere behind the figures, no ground, no shadows on the background, no scenery.
Every figure entirely visible, nothing cropped, at least 6% from every image edge.
Do not include: text, letters, numbers, logos, watermarks, frames or borders, ground or floor, cast shadows, photorealism, extra objects.
```
Attach: art/masters/city-north-gate-restored.png (style reference).
