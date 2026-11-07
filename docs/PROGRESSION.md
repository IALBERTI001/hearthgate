# Hearthgate: progression ladder

Companion to claude/HEARTHGATE_VISION.md: how the game keeps bringing something new, as built and measured.

The "30-second rule" (something new at least every 30 s in the first 5 minutes and every 60 s after, a payoff every 30 to 45 s) shaped this ladder. It is measured with `tools/qa/novelty-timeline.cjs`.

- **First run, about 19 to 29 minutes.**
  - 10 buildings, each with a visible job.
  - 7 enemy variants, from wave 12 to wave 37.
  - Rotating bosses: Golem King, Slime Queen and Swarmlord.
  - 10 wave events, from wave 22 to wave 57.
  - Tower stages 2 to 5, each with a fork.
  - The Golden Wisp, Tavern Cheer, quests, Feats and the Shop.
- **Ascension.** You need the whole city and wave 30 + 5 per Ascension (capped at 90). Each Ascension gives ×1.5 and a perk point, plus 1 point for every 25 waves.
- **Each later run brings its own content:**

| Ascension | What it brings |
|---|---|
| 1 | Ascended buildings, each with a second ability; Phantom; 7 events; the Anvil constellation |
| 2 | Challenge Trials; gilded city; Thief, Bomber, Wraith King; 9 events |
| 3 | Relics; Totem, Totem Titan; 10 events |
| 4 | Earth and QUAKE; Thief Tyrant; 7 events |
| 5 and on | Boss ranks; 9 new twin events each run |
| 6 | The Steward: levels the cheapest building by itself (On/Off in the Watchtower) |
| 7 to 9 | Leeches, Knights and Hives; Blackout, Royal Hunt and Cataclysm |
| 10 | The Eternal City: a finale card, a lasting light over the fountain; the game goes on |
| 1 to 8 | One constellation of 5 stars each, 40 stars in all, every star a lasting boon |

- **Measured.** The typical wait for something new is about 20 s in the first two runs and 30 to 60 s in runs 3 to 6. Ascensions come every 11 to 18 minutes.
- **Collections:** Feats (41), the Bestiary (21 kinds of enemy, unmet ones as silhouettes), constellation stars (40), combos.
- **Ascension rule:** the whole city and wave 30 + 5 per Ascension (capped at 90).
- **Measured (200-minute bot):** the Eternal City after about 2 h 47 min of steady play.
