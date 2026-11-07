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
p.restored = C.BUILDING_IDS.slice(); assert.deepStrictEqual(C.restoreOffer(p), []); assert.ok(C.cityWhole(p) && !C.canAscend(p), 'a whole city at wave 1 cannot Ascend yet');
p.runWave = 30; assert.ok(C.canAscend(p)); p.ascensions = 1; assert.ok(!C.canAscend(p), 'the 2nd Ascension asks for wave 35'); p.runWave = 35; assert.ok(C.canAscend(p)); p.ascensions = 0;
assert.strictEqual(C.sanitize({ wave: 37, restored: [] }).runWave, 37, 'old saves count their current wave');
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
// The Shop (Nov 5 2026): cosmetic looks bought with Starshards.
const sp = C.defaults();
assert.strictEqual(sp.shop.open, false); assert.strictEqual(sp.shop.equip.town, 'town-spring');
assert.ok(C.SHOP.every(i => C.SLOTS.includes(C.slotOf(i))), 'every item fits a slot');
assert.ok(C.SLOTS.every(k => C.SHOP.some(i => C.slotOf(i) === k && !i.price)), 'every slot has a free default');
assert.strictEqual(C.buy(sp, 'town-moon'), false, 'no Starshards, no purchase');
sp.shop.shards = 35; assert.ok(C.buy(sp, 'town-moon')); assert.strictEqual(sp.shop.shards, 5); assert.strictEqual(sp.shop.equip.town, 'town-moon', 'a purchase is worn at once');
assert.strictEqual(C.buy(sp, 'town-moon'), false, 'never bought twice');
assert.strictEqual(C.equip(sp, 'fire-obsidian'), false, 'cannot wear what is not owned');
assert.ok(C.equip(sp, 'town-spring')); assert.ok(C.equip(sp, 'town-moon'));
sp.shop.open = true; assert.ok(!C.shopNews(sp), '5 Starshards buys nothing new'); sp.shop.shards = 8; assert.ok(C.shopNews(sp), 'a wisp at 8 is news');
const bad2 = C.sanitize({ shop: { open: true, shards: -4, owned: ['town-moon', 'nope'], equip: { town: 'town-moon', fire: 'fire-ember', trail: 'hit-star' } } }).shop;
assert.strictEqual(bad2.shards, 0); assert.ok(!bad2.owned.includes('nope')); assert.strictEqual(bad2.equip.town, 'town-moon');
assert.strictEqual(bad2.equip.fire, 'fire-classic', 'an unowned look falls back to the default'); assert.strictEqual(bad2.equip.trail, 'trail-classic', 'a look in the wrong slot is dropped');
assert.deepStrictEqual(C.sanitize({}).shop.equip, C.defaults().shop.equip, 'old saves get the default looks');
const asc = C.ascend(Object.assign(C.defaults(), { restored: C.BUILDING_IDS.slice(), shop: { open: true, shards: 4, owned: C.defaults().shop.owned.concat('wisp-gold'), equip: Object.assign(C.defaults().shop.equip, { wisp: 'wisp-gold' }), seen: [] } }));
assert.strictEqual(asc.shop.shards, 4 + C.SHARDS.ascend, 'Ascending pays Starshards'); assert.strictEqual(asc.shop.equip.wisp, 'wisp-gold', 'looks survive Ascension');
const tp = C.defaults(); tp.wave = 30; const tb = C.bonuses(tp), tc = C.exchangeCost(tp, tb);
tp.wave = 5; assert.ok(C.exchangeCost(tp, C.bonuses(tp)) < tc, 'the trade price follows income'); tp.wave = 30;
tp.res = tc - 1; assert.strictEqual(C.trade(tp, tb), false); tp.res = tc; assert.ok(C.trade(tp, tb)); assert.strictEqual(tp.res, 0); assert.strictEqual(tp.shop.shards, 1);
// Daily quests and the Surge (Nov 5 2026).
const qp = C.defaults(); qp.shop.open = true;
const qa = C.questsFor(qp, '2026-11-05');
assert.strictEqual(qa.list.length, 3); assert.strictEqual(new Set(qa.list.map(q => q.id)).size, 3, 'three different quests');
assert.ok(!qa.list.some(q => q.id === 'shatter' || q.id === 'boss' || q.id === 'level'), 'no quest the player cannot do yet');
assert.strictEqual(C.questsFor(qp, '2026-11-05'), qa, 'the same day keeps the same quests');
const q0 = qa.list[0]; assert.strictEqual(C.questClaim(qp, 0), 0, 'an unfinished quest pays nothing');
C.questTick(qp, q0.id, 1e6); assert.strictEqual(q0.n, q0.need, 'progress stops at the goal');
assert.ok(C.questClaimable(qp)); assert.strictEqual(C.questClaim(qp, 0), C.QUEST_REWARD); assert.strictEqual(C.questClaim(qp, 0), 0, 'paid once');
assert.strictEqual(C.questBonus(qp), 0, 'the bonus needs all three');
for (let i = 1; i < 3; i++) { C.questTick(qp, qa.list[i].id, 1e6); C.questClaim(qp, i); }
assert.strictEqual(C.questBonus(qp), C.QUEST_BONUS); assert.strictEqual(qp.shop.shards, 3 * C.QUEST_REWARD + C.QUEST_BONUS);
assert.notDeepStrictEqual(C.questsFor(qp, '2026-11-06').list.map(q => q.n), [q0.need, 0, 0].slice(0, 0), 'a new day');
assert.ok(C.questsFor(qp, '2026-11-06').list.every(q => q.n === 0 && !q.claimed), 'a new day starts fresh');
assert.deepStrictEqual(C.sanitize({ quests: { day: '2026-11-05', list: [{ id: 'kills', n: 9e9 }, { id: 'nope' }] } }).quests.list, [{ id: 'kills', need: 150, n: 150, claimed: false }]);
const sg = C.defaults(), t0 = 1e12;
assert.ok(C.surgeReadyFree(sg, t0), 'the first Surge is free'); C.startSurge(sg, t0);
assert.ok(C.surgeOn(sg, t0 + C.SURGE_MS - 1)); assert.ok(!C.surgeOn(sg, t0 + C.SURGE_MS));
assert.ok(!C.surgeReadyFree(sg, t0 + C.SURGE_MS + 1), 'it charges after it ends'); assert.ok(C.surgeReadyFree(sg, t0 + C.SURGE_MS + C.SURGE_COOLDOWN_MS));
// Feats (Nov 5 2026): earned once, paid once, kept through Ascension.
assert.strictEqual(new Set(C.FEATS.map(f => f.id)).size, C.FEATS.length, 'feat ids are unique');
const fp = C.defaults(); assert.deepStrictEqual(C.featsEarned(fp), [], 'a fresh save has none');
fp.bestWave = 26; fp.stats.kills = 100; const got = C.featsEarned(fp).map(f => f.id);
assert.deepStrictEqual(got.sort(), ['kills100', 'wave10', 'wave25']); assert.strictEqual(fp.shop.shards, 3 * C.FEAT_PAY[1]);
assert.deepStrictEqual(C.featsEarned(fp), [], 'never paid twice');
const fa = C.ascend(Object.assign(fp, { restored: C.BUILDING_IDS.slice() }));
assert.ok(fa.feats.includes('wave25') && fa.stats.kills === 100, 'Feats and lifetime stats survive Ascension');
assert.deepStrictEqual(C.sanitize({ feats: ['wave10', 'nope', 'wave10'], stats: { kills: -3, bosses: 2.7 } }).feats, ['wave10']);
assert.strictEqual(C.sanitize({ stats: { kills: -3 } }).stats.kills, 0);
const qq = C.defaults(); qq.shop.open = true; C.questsFor(qq, 'd'); C.questTick(qq, qq.quests.list[0].id, 1e6); C.questClaim(qq, 0); assert.strictEqual(qq.stats.quests, 1, 'claimed quests count toward Feats');
// Daily Omen and Ascension perks (Nov 6 2026).
assert.strictEqual(C.omenFor('2026-11-06'), C.omenFor('2026-11-06'), 'one omen per day');
assert.ok(new Set(['2026-11-01', '2026-11-02', '2026-11-03', '2026-11-04', '2026-11-05', '2026-11-06', '2026-11-07'].map(d => C.omenFor(d).id)).size >= 4, 'a week brings variety');
const op = C.defaults(); op.omen = 'drums'; assert.ok(C.fireInterval(C.bonuses(op)) < C.fireInterval(C.bonuses(C.defaults())), 'an omen changes the fight');
assert.strictEqual(C.sanitize(JSON.stringify(op)).omen, undefined, 'the omen is never saved');
const pk = C.defaults(); assert.strictEqual(C.buyPerk(pk, 'sharp'), false, 'no points, no perk');
pk.perkPts = 3; assert.ok(C.buyPerk(pk, 'sharp')); assert.strictEqual(C.perkCost(pk, 'sharp'), 2); assert.ok(C.buyPerk(pk, 'sharp')); assert.strictEqual(pk.perkPts, 0);
assert.ok(Math.abs(C.bonuses(pk).damage - 0.3) < 1e-9);
pk.perks.sharp = 5; pk.perkPts = 99; assert.strictEqual(C.buyPerk(pk, 'sharp'), false, 'capped at max');
const pa = Object.assign(C.defaults(), { restored: C.BUILDING_IDS.slice(), wave: 55, perks: { start: 2 }, perkPts: 1 });
const pq = C.ascend(pa); assert.strictEqual(pq.perkPts, 1 + 3, 'wave 55 pays 3 points'); assert.strictEqual(pq.wave, 11); assert.strictEqual(pq.gateLevel, 11); assert.deepStrictEqual(pq.perks, { start: 2 });
assert.strictEqual(C.sanitize({ ascensions: 3 }).perkPts, 6, 'older saves get points for past Ascensions');
assert.strictEqual(C.sanitize({ ascensions: 3, perks: {}, perkPts: 1 }).perkPts, 1);
const ow = C.defaults(); ow.lastSeen = 1e12; ow.wave = 10; const ob = C.bonuses(ow);
const cap0 = C.offlineEarnings(ow, ob, 1e12 + 24 * 3600e3).secs; ow.perks.night = 2; assert.strictEqual(C.offlineEarnings(ow, C.bonuses(ow), 1e12 + 24 * 3600e3).secs, cap0 + 4 * 3600, 'Night Watch extends the cap');
const sp2 = C.defaults(); sp2.perks.surge = 2; assert.strictEqual(C.surgeCooldown(sp2), C.SURGE_COOLDOWN_MS - 6 * 60e3);
// Every Feat can be earned (owner's report, Nov 6 2026: "Own every look" needed 27 but only 20 looks can be bought).
const all = C.defaults(); all.shop.open = true; all.shop.shards = 1e6;
for (const it of C.SHOP) if (it.price) C.buy(all, it.id);
const wardrobe = C.FEATS.find(f => f.id.startsWith('looks') && f.tier === 3);
assert.ok(C.featProgress(all, wardrobe) >= wardrobe.need, `owning every look completes ${wardrobe.name}`);
// The 30-second rule's schedule (Nov 6 2026): variants, bosses, events and Ascension unlocks.
assert.ok(C.waveMix(12).some(r => r[0] === 'elite'), 'elites from wave 12');
assert.ok(C.waveMix(14).some(r => r[0] === 'shielded'));
assert.ok(C.waveMix(16).some(r => r[0] === 'splitter')); assert.ok(C.waveMix(19).some(r => r[0] === 'mender'));
assert.deepStrictEqual([10, 20, 30, 40].map(w => C.bossOf(w)), ['boss', 'queen', 'lord', 'boss'], 'bosses rotate');
for (const w of [12, 20, 33, 37, 60]) assert.ok(C.waveMix(w).every(([, c]) => c > 0), 'no empty groups at wave ' + w);
const ev = C.defaults(); assert.strictEqual(C.eventFor(ev, 21), null); assert.strictEqual(C.eventFor(ev, 22).id, 'stampede', 'first event at wave 22');
ev.seenEvents.push('stampede'); assert.strictEqual(C.eventFor(ev, 23), null); assert.strictEqual(C.eventFor(ev, 25).id, 'golden');
ev.ascensions = 1; ev.seenEvents.push('golden'); assert.strictEqual(C.eventFor(ev, 7).id, 'eclipse', 'an Ascension adds new events, early in the run');
assert.ok(C.STAGES.length === 5 && C.stageOf(85) === 5 && C.stageOf(60) === 4);
const fk = C.defaults(); fk.gateLevel = 61; fk.forks = { 2: 'crews', 3: 'focus' }; assert.strictEqual(C.pendingFork(fk), 4);
assert.ok(!C.unlocked(C.defaults(), 'trials')); const a2 = C.defaults(); a2.ascensions = 2; assert.ok(C.isTrial(a2, 15) && !C.isTrial(a2, 20));
const rl = C.defaults(); rl.ascensions = 3; C.equipRelic(rl, 'ember'); C.equipRelic(rl, 'rime'); C.equipRelic(rl, 'crown'); assert.deepStrictEqual(rl.relics, ['rime', 'crown'], 'two relics at most');
assert.ok(C.bonuses(rl).bossDmg > 0);
console.log('core tests passed');
// After-Ascension novelty (Nov 6 2026): every building has an Ascended ability, later loops meet new enemies and events
for (const b of C.BUILDINGS) assert.ok(C.RANK2[b.id] && C.RANK2[b.id].name && C.RANK2[b.id].line, 'Ascended ability for ' + b.id);
{ const p = C.defaults ? C.defaults() : null; if (p) { p.restored = ['forge', 'bakery']; const b0 = C.bonuses(p); p.ascensions = 1; const b1 = C.bonuses(p);
  assert.ok(b1.molten === 1 && b0.molten === 0, 'Molten Forge only after an Ascension'); assert.ok(b1.wispX === b0.wispX + 1, 'Golden Ovens'); } }
assert.ok(!C.waveMix(15, 0).some(r => r[0] === 'phantom'), 'no Phantoms before the 1st Ascension');

assert.ok(C.waveMix(15, 1).some(r => r[0] === 'phantom') && C.waveMix(8, 2).some(r => r[0] === 'thief') && C.waveMix(26, 2).some(r => r[0] === 'bomber') && C.waveMix(16, 3).some(r => r[0] === 'totem'));
assert.strictEqual(C.waveMix(20, 2)[0][0], 'wraith', 'the newest Ascension boss takes wave 20'); assert.strictEqual(C.waveMix(20, 4)[0][0], 'tyrant'); assert.strictEqual(C.waveMix(20, 0)[0][0], 'queen');
assert.ok(!C.waveMix(10, 3).some(r => C.ASC_ROLES[r[0]]), 'boss waves stay boss waves');
for (const r in C.ASC_ROLES) assert.ok(C.ROLES[r] && C.ROLES[r].tip, 'Ascension enemy has a tip: ' + r);
console.log('after-Ascension tests passed');
// Constellations: 40 distinct stars, five per Ascension run, lit before that run's Ascend wave
assert.strictEqual(new Set(C.STARS.map(s => s.id)).size, 50); assert.strictEqual(new Set(C.STARS.map(s => s.name)).size, 50);
{ const p = C.defaults(); p.wave = 50; assert.strictEqual(C.starsDue(p).length, 0, 'no stars before the 1st Ascension');
  p.ascensions = 1; p.wave = 20; p.runWave = 20; assert.strictEqual(C.starsDue(p).length, 3);
  for (let k = 0; k < 5; k++) assert.ok(C.starWave(p, k) < C.ascendWave(p), 'every star comes before the Ascend wave');
  p.stars = C.starsDue(p).map(s => s.id); assert.ok(C.bonuses(p).damage > C.bonuses(C.defaults()).damage); assert.deepStrictEqual(C.sanitize(p).stars, p.stars);
  p.restored = C.BUILDING_IDS.slice(); p.runWave = 40; assert.deepStrictEqual(C.ascend(p).stars, p.stars, 'stars stay through Ascension'); }
console.log('constellation tests passed');
{ const p = C.defaults(); p.wave = 47; p.runWave = 50; assert.strictEqual(C.perkPtsFor(p), 3, 'perk points count the best wave of the run'); }
console.log('review fix tests passed');
// the Eternal City: the 10th Ascension, and every Ascend wave stays reachable (capped)
assert.strictEqual(C.ETERNAL_AT, 10);
{ const p = C.defaults(); for (let a = 0; a <= 40; a++) { p.ascensions = a; assert.ok(C.ascendWave(p) <= 90, 'Ascend wave capped'); } }
console.log('eternal tests passed');
{ const p = C.defaults(); p.ascensions = 5; assert.ok(!C.unlocked(p, 'steward')); p.ascensions = 6; assert.ok(C.unlocked(p, 'steward'), 'the Steward at the 6th Ascension');
  p.settings.steward = false; assert.strictEqual(C.sanitize(p).settings.steward, false, 'the Steward switch survives a reload'); }
console.log('steward tests passed');
{ const p = C.defaults(); p.told = ['gold', 'tomorrow']; assert.deepStrictEqual(C.sanitize(JSON.parse(JSON.stringify(p))).told, ['gold', 'tomorrow'], 'one-time tips stay told after a reload');
  p.restored = C.BUILDING_IDS.slice(); p.runWave = 40; assert.deepStrictEqual(C.ascend(p).told, ['gold', 'tomorrow']); }
console.log('told tests passed');
// the 7th to 9th Ascensions each bring an enemy and an event of their own
assert.ok(C.waveMix(12, 7).some(r => r[0] === 'leech') && !C.waveMix(12, 6).some(r => r[0] === 'leech'));
assert.ok(C.waveMix(12, 8).some(r => r[0] === 'knight') && C.waveMix(12, 9).some(r => r[0] === 'hive'));
for (const id of ['blackout', 'royalhunt', 'cataclysm']) assert.ok(C.EVENTS.some(e => e.id === id));
console.log('late Ascension tests passed');
