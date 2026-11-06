// Run: node tests/core.test.cjs
const assert = require('assert');
require('../js/city.js');
const C = require('../js/core.js');

// Compact numbers: no commas, 1K / 1.04K style, never capped.
assert.strictEqual(C.fmt(0), '0'); assert.strictEqual(C.fmt(999), '999'); assert.strictEqual(C.fmt(1000), '1K');
assert.strictEqual(C.fmt(1040), '1.04K'); assert.strictEqual(C.fmt(12500), '12.5K'); assert.strictEqual(C.fmt(3.2e6), '3.2M');
assert.strictEqual(C.fmt(999999), '1M'); assert.ok(!/,/.test(C.fmt(123456789))); assert.ok(C.fmt(1e40).length < 8);

// Sanitize: fresh, sparse, broken, hostile and old (v1) saves all load.
assert.deepStrictEqual(C.sanitize(null), C.defaults());
assert.deepStrictEqual(C.sanitize('not json'), C.defaults());
const bad = C.sanitize({ res: -5, wave: 'x', gateLevel: Infinity, restored: ['forge', 'forge', 'castle'], combos: ['shatter', 'zzz'], forks: { 2: 'nope' }, hack: 1, daily: { day: 'yesterday' } });
assert.strictEqual(bad.res, 0); assert.strictEqual(bad.wave, 1); assert.strictEqual(bad.gateLevel, 1);
assert.deepStrictEqual(bad.restored, ['forge']); assert.deepStrictEqual(bad.combos, ['shatter']); assert.deepStrictEqual(bad.forks, {});
assert.ok(!('hack' in bad)); assert.strictEqual(bad.daily.day, '');
assert.deepStrictEqual(C.sanitize({ restored: ['forge', 'frostSpire', 'stormBell', 'b4', 'b10'] }).restored, ['forge', 'frostSpire', 'stormBell', 'bakery', 'watchtower'], 'v1 building ids migrate');
assert.ok(C.sanitize({ wave: 40, bestWave: 3 }).bestWave >= 40);
assert.deepStrictEqual(C.sanitize({ gateLevel: 3, forks: { 2: 'crews' } }).forks, {}, 'a fork above the gate stage is dropped');

// Approved fix 1: Forge, Frost Spire, Storm Bell first, then a pick of two that does not reroll on reload.
const p = C.defaults();
assert.deepStrictEqual(C.restoreOffer(p), ['forge']);
p.restored = ['forge', 'frostSpire', 'stormBell'];
const offer = C.restoreOffer(p); assert.strictEqual(offer.length, 2); assert.deepStrictEqual(C.restoreOffer(p), offer);
assert.ok(offer.every(id => !p.restored.includes(id)));
p.restored = C.BUILDING_IDS.slice(); assert.deepStrictEqual(C.restoreOffer(p), []); assert.ok(C.canAscend(p));
assert.ok(C.restoreCost(0) + C.restoreCost(1) <= 45 * C.killRes(1, false, C.bonuses(C.defaults())), 'first two restores within about 45 early kills, so SHATTER lands inside 60 s');
for (let i = 1; i < 10; i++) assert.ok(C.restoreCost(i) > C.restoreCost(i - 1));

// Approved fix 2: forks at gate stages 2 and 3, chosen once each.
const g = C.defaults(); g.gateLevel = C.STAGES[1] - 1; assert.strictEqual(C.pendingFork(g), null);
g.gateLevel = C.STAGES[1]; assert.strictEqual(C.pendingFork(g), 2); g.forks[2] = 'crews'; assert.strictEqual(C.pendingFork(g), null);
g.gateLevel = C.STAGES[2]; assert.strictEqual(C.pendingFork(g), 3);
assert.ok(C.fireInterval(C.bonuses(g)) < C.fireInterval(C.bonuses(C.defaults())));

// Approved fix 3: roles arrive one at a time, each with an element that counters it.
assert.deepStrictEqual(C.waveMix(1).map(r => r[0]), ['slime']);
assert.ok(C.waveMix(3).some(r => r[0] === 'runner')); assert.ok(C.waveMix(6).some(r => r[0] === 'armoured'));
assert.ok(C.waveMix(10).some(r => r[0] === 'boss'));
for (const r of ['runner', 'armoured', 'swarm']) assert.ok(['fire', 'frost', 'storm'].includes(C.ROLES[r].weak));

// Buildings: every one does something.
assert.strictEqual(C.BUILDINGS.length, 10);
for (const b of C.BUILDINGS) assert.ok(b.element || b.bonus, b.id + ' has an effect');
const nb = C.defaults(); const before = C.bonuses(nb); nb.restored = ['bakery', 'market']; const after = C.bonuses(nb);
assert.ok(after.res > before.res && C.upgradeCost(5, after) < C.upgradeCost(5, before));

// Idle: offline earnings capped at 8 hours; nothing for under a minute.
const o = C.defaults(); o.lastSeen = 1e12; o.wave = 20;
assert.strictEqual(C.offlineEarnings(o, C.bonuses(o), 1e12 + 30e3).res, 0);
const day = C.offlineEarnings(o, C.bonuses(o), 1e12 + 24 * 3600e3), cap = C.offlineEarnings(o, C.bonuses(o), 1e12 + 8 * 3600e3);
assert.strictEqual(day.res, cap.res, 'capped at 8 h'); assert.ok(cap.res > 0);

// Daily reward: never in the first session, once per local day, a missed day pauses the streak, day 7 pays 5x.
const d = C.defaults(); const now = new Date(2026, 10, 6, 9).getTime();
assert.strictEqual(C.dailyDue(d, now), null, 'not in the first session');
d.firstSession = false; d.daily.day = '2026-11-05'; d.daily.streak = 2;
const due = C.dailyDue(d, now); assert.strictEqual(due.day, 3); assert.strictEqual(due.today, '2026-11-06');
d.daily.day = '2026-11-01'; assert.strictEqual(C.dailyDue(d, now).day, 3, 'a missed day pauses, never resets');
d.daily.day = '2026-11-06'; assert.strictEqual(C.dailyDue(d, now), null, 'never twice a day');
d.daily.day = '2026-11-09'; assert.strictEqual(C.dailyDue(d, now), null, 'a clock moved back never pays');
d.daily.day = '2026-11-05'; d.daily.streak = 6; const big = C.dailyDue(d, now); assert.ok(big.big);
d.daily.streak = 5; assert.ok(big.amount === C.dailyDue(d, now).amount * 5);

// Ascension: city back to ruins, x1.5 for good, best wave and combos kept.
const a = C.defaults(); a.restored = C.BUILDING_IDS.slice(); a.gateLevel = 30; a.wave = 44; a.bestWave = 44; a.combos = ['shatter']; a.res = 5e6; a.levels = { forge: 12 };
const q = C.ascend(a); assert.strictEqual(q.ascensions, 1); assert.deepStrictEqual(q.restored, []); assert.strictEqual(q.wave, 1);
assert.strictEqual(q.bestWave, 44); assert.deepStrictEqual(q.combos, ['shatter']); assert.strictEqual(q.res, 0); assert.deepStrictEqual(q.levels, {}, 'building levels reset with the city');
assert.strictEqual(C.bonuses(q).ascend, 1.5);

// The city: the gate on the screen's centre line, the four tower platforms inside the wall, every building has a spot.
const [gx] = C.cityToBand(...C.CITY.gate); assert.ok(Math.abs(gx - 360) <= 12, `gate at x=${gx.toFixed(0)}`);
const [fx] = C.cityToBand(...C.CITY.focus); assert.ok(Math.abs(fx - 360) <= 12, `plaza at x=${fx.toFixed(0)}`);
const [c0, c1, cw, ch] = C.CITY.crop; assert.ok(c0 >= 0 && c1 >= 0 && c0 + cw <= 1536 && c1 + ch <= 1024);
assert.ok(Math.abs(cw / ch - C.CITY.band[0] / C.CITY.band[1]) < 0.01, 'no stretching');
assert.strictEqual(C.CITY.platforms.length, 4);
for (const [x] of C.CITY.platforms) { const [bx] = C.cityToBand(x, 0); assert.ok(bx > 40 && bx < 680); }
for (const id of C.BUILDING_IDS) assert.ok(C.CITY.spots[id], id + ' has a reveal spot');
// Parts became Resonance (Nov 5 2026): a v2 save keeps its currency.
assert.strictEqual(C.sanitize({ parts: 1234 }).res, 1234, 'v2 Parts carry over as Resonance');
assert.strictEqual(C.sanitize({ res: 50, parts: 9 }).res, 50);
assert.deepStrictEqual(C.sanitize({ restored: ['forge'], levels: { forge: 7, castle: 3, market: 4, frostSpire: -2 } }).levels, { forge: 7 }, 'levels only for restored buildings');

// The city makes Resonance: only restored buildings, more per level, x2 at milestones, later restores make more.
const cy = C.defaults(), cb = C.bonuses(cy);
assert.strictEqual(C.cityRate(cy, cb), 0, 'a ruined city makes nothing');
cy.restored = ['forge', 'frostSpire'];
assert.ok(C.buildingRate(cy, 'frostSpire', cb) > C.buildingRate(cy, 'forge', cb), 'later restores make more');
const r1 = C.buildingRate(cy, 'forge', cb); cy.levels.forge = 2; assert.ok(Math.abs(C.buildingRate(cy, 'forge', cb) - 2 * r1) < 1e-9);
cy.levels.forge = 9; const r9 = C.buildingRate(cy, 'forge', cb); cy.levels.forge = 10;
assert.ok(C.buildingRate(cy, 'forge', cb) > 2 * r9, 'level 10 doubles it');
assert.ok(C.levelCost(cy, 'forge', cb) > C.levelCost(Object.assign(C.defaults(), { restored: ['forge'] }), 'forge', cb), 'levels cost more as they climb');
assert.strictEqual(C.levelCost(cy, 'bakery', cb), Infinity, 'a ruined building cannot level');
const oc = C.defaults(); oc.lastSeen = 1e12; oc.wave = 20; const plain = C.offlineEarnings(oc, C.bonuses(oc), 1e12 + 3600e3).res;
oc.restored = ['forge', 'frostSpire', 'stormBell']; assert.ok(C.offlineEarnings(oc, C.bonuses(oc), 1e12 + 3600e3).res > plain, 'the city earns while away');
// Kills stay the main income: a fresh city at wave 20 makes well under the fight's rate.
assert.ok(C.cityRate(oc, C.bonuses(oc)) < 0.5 * C.fightRate(oc, C.bonuses(oc)));

// Enemy scale (owner's correction, Nov 5 2026): every regular enemy is shorter than a tier-1 tower; only the boss is taller.
for (const r of ['slime', 'runner', 'armoured', 'swarm']) assert.ok(C.ROLES[r].size < C.CITY.towerH, `${r} (${C.ROLES[r].size}) shorter than a tower (${C.CITY.towerH})`);
assert.ok(C.ROLES.boss.size > C.CITY.towerH);
console.log('core tests passed');
