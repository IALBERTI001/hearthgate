# Hearthgate lessons

Every correction becomes a rule in the skill that should have caught it, plus a test that fails before the fix.

| Date | Correction (user's words) | Root cause | Rule and where it lives | Test |
| --- | --- | --- | --- | --- |
| Nov 5 2026 | "I can see that other assets bleed into simple assets ... the bug walking and I see a fraction of a slime and other parts like pieces of a different tower showing. EVERY asset created should HAVE its own sheet so that NO other asset can mistakenly be placed into it." | The first build cut Warden's shared enemy and tower sheets on an even grid. Those sheets are not grid-true (slime undersides and golem heads cross into the beetle row; Frost and Storm tier 3 touch), and the game drew from one shared enemy sheet. No check looked at what was inside a frame. | One asset, one sheet: every subject in its own file, split from shared sources by outline and mask, never by grid; listed in assets/manifest.json. Skill: sprite-and-growth-art, section 0 (proposed Nov 5). | tests/assets_test.py (fails 18 of 18 old grid-cut frames; passes all 12 new sheets) |
