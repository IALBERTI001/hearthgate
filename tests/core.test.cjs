// Run: node tests/core.test.cjs
const assert = require('assert');
const C = require('../js/core.js');

// Compact numbers: no commas, 1K / 1.04K style, never capped.
assert.strictEqual(C.fmt(0), '0');
assert.strictEqual(C.fmt(999), '999');
assert.strictEqual(C.fmt(1000), '1K');
assert.strictEqual(C.fmt(1040), '1.04K');
assert.strictEqual(C.fmt(12500), '12.5K');
assert.strictEqual(C.fmt(3.2e6), '3.2M');
assert.ok(!/,/.test(C.fmt(123456789)));
assert.ok(C.fmt(1e40).length < 8, 'huge numbers stay compact');

// Sanitize: fresh, sparse, broken and hostile saves all load.
assert.deepStrictEqual(C.sanitize(null), C.defaults());
assert.deepStrictEqual(C.sanitize('not json'), C.defaults());
const sparse = C.sanitize({ parts: 50 });
assert.strictEqual(sparse.parts, 50); assert.deepStrictEqual(sparse.restored, []); assert.strictEqual(sparse.gateLevel, 1);
const bad = C.sanitize({ parts: -5, wave: 'x', gateLevel: Infinity, restored: ['forge', 'forge', 'castle'], combos: ['shatter', 'zzz'], hack: 1 });
assert.strictEqual(bad.parts, 0); assert.strictEqual(bad.wave, 1); assert.strictEqual(bad.gateLevel, 1);
assert.deepStrictEqual(bad.restored, ['forge']); assert.deepStrictEqual(bad.combos, ['shatter']); assert.ok(!('hack' in bad));
assert.ok(C.sanitize({ wave: 40, bestWave: 3 }).bestWave >= 40, 'best wave never below current');

// Approved fix 1: Forge, Frost Spire, Storm Bell come first so SHATTER is reachable in the first minute.
assert.deepStrictEqual(C.BUILDINGS.slice(0, 3), ['forge', 'frostSpire', 'stormBell']);
assert.ok(C.restoreCost(0) + C.restoreCost(1) <= 60, 'first two restores cheap enough for a SHATTER inside 60 s');
for (let i = 1; i < 10; i++) assert.ok(C.restoreCost(i) > C.restoreCost(i - 1));
for (let i = 2; i < 50; i++) assert.ok(C.upgradeCost(i) > C.upgradeCost(i - 1));
// The city's focal point (plaza and city gate) sits on the screen's centre line, under the gate (user, Nov 5 2026).
const [fx] = C.cityToBand(...C.CITY.focus);
assert.ok(Math.abs(fx - 360) <= 12, `city focus at x=${fx.toFixed(0)}, must be within 12 px of the centre line 360`);
const [cx0, cy0, cw, ch] = C.CITY.crop;
assert.ok(cx0 >= 0 && cy0 >= 0 && cx0 + cw <= 1536 && cy0 + ch <= 1024, 'city crop inside the painting');
assert.ok(Math.abs(cw / ch - C.CITY.band[0] / C.CITY.band[1]) < 0.02, 'city crop has the band shape, no stretching');
console.log('core tests passed');
