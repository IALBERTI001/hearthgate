// Hearthgate core: numbers, the save profile, buildings, upgrades, waves, idle rewards and Ascension.
// Pure logic, no DOM, so Node tests can load it. Every balance number lives here.
(function (root) {
  'use strict';

  // ---------- numbers: 999, 1K, 1.04K, 12.5K, 3.2M ... no commas, never capped ----------
  const UNITS = ['', 'K', 'M', 'B', 'T', 'Qa', 'Qi', 'Sx', 'Sp', 'Oc', 'No', 'Dc'];
  function fmt(n) {
    if (!isFinite(n)) return '0';
    const neg = n < 0; n = Math.abs(n);
    if (n < 1000) return (neg ? '-' : '') + String(Math.floor(n));
    let i = 0; while (n >= 1000 && i < 700) { n /= 1000; i++; }
    if (n >= 999.5) { n /= 1000; i++; }
    const s = n >= 100 ? n.toFixed(0) : n >= 10 ? n.toFixed(1) : n.toFixed(2);
    return (neg ? '-' : '') + s.replace(/\.0+$/, '').replace(/(\.\d*?)0+$/, '$1') + unit(i);
  }
  // Past Dc the suffixes run aa, ab ... zz, so numbers keep growing without a cap.
  function unit(i) {
    if (i < UNITS.length) return UNITS[i];
    const j = i - UNITS.length, a = 'abcdefghijklmnopqrstuvwxyz';
    return a[Math.floor(j / 26) % 26] + a[j % 26];
  }


  // ---------- Daily Omen (Nov 6 2026): one small, always-good twist per day, the same for everyone that day ----------
  const OMENS = [
    { id: 'ember', name: 'Ember Day', line: 'Fire hits 30% harder', bonus: { fire: 0.3 } },
    { id: 'rime', name: 'Rime Day', line: 'Frost hits 30% harder', bonus: { frost: 0.3 } },
    { id: 'thunder', name: 'Thunder Day', line: 'Storm hits 30% harder', bonus: { storm: 0.3 } },
    { id: 'harvest', name: 'Harvest Moon', line: '+25% Resonance from every enemy', bonus: { res: 0.25 } },
    { id: 'hum', name: 'Humming Stones', line: 'The city makes 50% more Resonance', bonus: { city: 0.5 } },
    { id: 'bounty', name: 'Bounty Day', line: 'Bosses pay double Starshards', bonus: { bossShards: 1 } },
    { id: 'confluence', name: 'Confluence', line: 'Combos hit 50% harder', bonus: { combo: 0.5 } },
    { id: 'drums', name: 'War Drums', line: 'Towers fire 15% faster', bonus: { fireRate: 0.15 } }
  ];
  const omenFor = day => OMENS[[...day].reduce((a, c) => (a * 33 + c.charCodeAt(0)) >>> 0, 11) % OMENS.length];

  // ---------- Ascension perks (Nov 6 2026): every Ascension pays perk points, more for going further first ----------
  const PERKS = [
    { id: 'sharp', name: 'Keen Sights', line: 'Towers hit 15% harder', per: { damage: 0.15 }, max: 5 },
    { id: 'hum', name: 'Deep Hum', line: 'The city makes 25% more', per: { city: 0.25 }, max: 4 },
    { id: 'rich', name: 'Rich Veins', line: '+15% Resonance from enemies', per: { res: 0.15 }, max: 4 },
    { id: 'night', name: 'Night Watch', line: 'Offline earnings last 2 hours longer', per: { offlineH: 2 }, max: 4 },
    { id: 'surge', name: 'Quick Surge', line: 'Surge recharges 3 minutes sooner', per: { surgeCd: 3 }, max: 3 },
    { id: 'lucky', name: 'Lucky Stars', line: '+1 Starshard from every boss', per: { bossPlus: 1 }, max: 3 },
    { id: 'start', name: 'Head Start', line: 'Each new city starts 5 waves and 5 gate levels ahead', per: { start: 5 }, max: 3 }
  ];
  const perkCost = (p, id) => (p.perks[id] || 0) + 1;   // the next level costs one more point than the last
  function buyPerk(p, id) {
    const k = PERKS.find(x => x.id === id); if (!k || (p.perks[id] || 0) >= k.max || p.perkPts < perkCost(p, id)) return false;
    p.perkPts -= perkCost(p, id); p.perks[id] = (p.perks[id] || 0) + 1; return true;
  }
  const perkPtsFor = p => 1 + Math.floor(p.wave / 25);   // Ascending at wave 50 pays 3, at wave 100 pays 5
  function perkBonus(p) {
    const b = {}; for (const k of PERKS) { const n = p.perks[k.id] || 0; for (const key in k.per) b[key] = (b[key] || 0) + k.per[key] * n; } return b;
  }

  // ---------- buildings: city layer 1 ----------
  // The first three are restored in this order so SHATTER lands in the first minute (approved fix 1, Nov 5 2026);
  // after them, Restore offers a pick of two. Each building visibly changes the fight or the economy.
  const BUILDINGS = [
    { id: 'forge', name: 'Forge', element: 'fire', line: 'Towers gain Fire. Hits burn.' },
    { id: 'frostSpire', name: 'Frost Spire', element: 'frost', line: 'Towers gain Frost. Hits slow.' },
    { id: 'stormBell', name: 'Storm Bell', element: 'storm', line: 'Towers gain Storm. Hits chain.' },
    { id: 'bakery', name: 'Bakery', bonus: { res: 0.15 }, line: '+15% Resonance from every enemy.' },
    { id: 'market', name: 'Market', bonus: { upgradeDiscount: 0.10 }, line: 'Upgrades cost 10% less.' },
    { id: 'tavern', name: 'Tavern', bonus: { fireRate: 0.10 }, line: 'Towers fire 10% faster.' },
    { id: 'granary', name: 'Granary', bonus: { offline: 0.5 }, line: '+50% Resonance while you are away.' },
    { id: 'library', name: 'Library', bonus: { combo: 0.25 }, line: 'Combos hit 25% harder.' },
    { id: 'cottages', name: 'Cottages', bonus: { gateHp: 0.25 }, line: 'The gate has 25% more health.' },
    { id: 'watchtower', name: 'Watchtower', bonus: { damage: 0.15 }, line: 'Towers hit 15% harder.' }
  ];
  const BUILDING_IDS = BUILDINGS.map(b => b.id);
  const byId = id => BUILDINGS.find(b => b.id === id);
  const FIXED_ORDER = 3;
  const COMBOS = ['shatter', 'frostStorm', 'fireStorm'];

  // Restore choice: the first three in fixed order; after that two unrestored buildings, the same two until one is
  // picked (seeded by how many are restored and the Ascension count, so reloading never rerolls the offer).
  function restoreOffer(p) {
    const left = BUILDING_IDS.filter(id => !p.restored.includes(id));
    if (!left.length) return [];
    if (p.restored.length < FIXED_ORDER) return [BUILDING_IDS[p.restored.length]];
    if (left.length === 1) return left;
    const seed = (p.restored.length * 7 + p.ascensions * 13 + 3) % left.length;
    return [left[seed], left[(seed + 1 + (p.ascensions % (left.length - 1))) % left.length]].filter((v, i, a) => a.indexOf(v) === i);
  }

  // ---------- gate upgrades and forks ----------
  // The gate climbs levels; stages at levels 10 and 25 grow the towers a tier and each offers a fork (approved fix 2).
  const STAGES = [1, 15, 40];
  const FORKS = {
    2: [{ id: 'ballistae', name: 'Heavy Bolts', line: '+40% tower damage', bonus: { damage: 0.40 } },
        { id: 'crews', name: 'Swift Crews', line: '+35% fire rate', bonus: { fireRate: 0.35 } }],
    3: [{ id: 'focus', name: 'Elemental Focus', line: 'Combos hit 50% harder', bonus: { combo: 0.50 } },
        { id: 'ironGate', name: 'Iron Gate', line: 'Double gate health, slow repair', bonus: { gateHp: 1.0, regen: 0.02 } }]
  };
  const stageOf = lvl => lvl >= STAGES[2] ? 3 : lvl >= STAGES[1] ? 2 : 1;
  // A fork is waiting when the gate has reached a stage whose fork has not been chosen yet.
  const pendingFork = p => [2, 3].find(s => stageOf(p.gateLevel) >= s && !p.forks[s]) || null;

  // Every bonus from buildings, forks and Ascension, summed.
  function bonuses(p) {
    const b = { res: 0, upgradeDiscount: 0, fireRate: 0, offline: 0, combo: 0, gateHp: 0, damage: 0, regen: 0, fire: 0, frost: 0, storm: 0, city: 0, bossShards: 0, bossPlus: 0, offlineH: 0, surgeCd: 0, start: 0 };
    const add = o => { for (const k in o) b[k] += o[k]; };
    for (const id of p.restored) { const d = byId(id); if (d && d.bonus) add(d.bonus); }
    for (const s of [2, 3]) { const f = p.forks[s] && FORKS[s].find(x => x.id === p.forks[s]); if (f) add(f.bonus); }
    if (p.perks) add(perkBonus(p));
    if (p.omen) { const o = OMENS.find(x => x.id === p.omen); if (o) add(o.bonus); }   // p.omen is set each day by the game, never saved
    b.ascend = Math.pow(ASCEND_MULT, p.ascensions);
    return b;
  }

  const upgradeCost = (lvl, b) => Math.floor(5 * Math.pow(1.27, lvl - 1) * (1 - (b ? b.upgradeDiscount : 0)));
  const restoreCost = n => n < FIXED_ORDER ? [30, 80, 220][n] : Math.floor(600 * Math.pow(2.7, n - 3));
  const towerDamage = (p, b) => 3 * Math.pow(1.235, p.gateLevel - 1) * (1 + b.damage) * b.ascend;
  const fireInterval = b => 0.55 / (1 + b.fireRate);
  const gateMax = (p, b) => Math.floor(100 * Math.pow(1.11, p.gateLevel - 1) * (1 + b.gateHp));
  const killRes = (wave, boss, b) => Math.ceil(4 * Math.pow(1.12, wave - 1) * (boss ? 25 : 1) * (1 + b.res) * b.ascend);

  // ---------- the city makes Resonance (Nov 5 2026, the owner's idea) ----------
  // Every restored building hums out Resonance each second and can be levelled up by tapping it. Its base output follows
  // the order it was restored in (later restores cost more, so they make more); levels 10, 25, 50 and 100 each double it.
  // Kills stay the main income: the city is tuned so the fight pays more than the town until late in a run (pacing bot).
  const MILESTONES = [10, 25, 50, 100];
  const levelOf = (p, id) => p.restored.includes(id) ? (p.levels[id] || 1) : 0;
  function buildingRate(p, id, b, lvl) {
    const idx = p.restored.indexOf(id); if (idx < 0) return 0;
    const L = lvl || levelOf(p, id), dbl = MILESTONES.filter(m => L >= m).length;
    return CITY_BASE * Math.pow(2.7, idx) * L * Math.pow(2, dbl) * b.ascend * (1 + (b.city || 0));
  }
  const cityRate = (p, b) => p.restored.reduce((s, id) => s + buildingRate(p, id, b), 0);
  const levelCost = (p, id, b) => { const idx = p.restored.indexOf(id); return idx < 0 ? Infinity
    : Math.floor(restoreCost(idx) * LEVEL_COST * Math.pow(1.15, levelOf(p, id) - 1) * (1 - b.upgradeDiscount)); };
  const nextMilestone = L => MILESTONES.find(m => m > L) || null;

  // ---------- waves and enemy roles (approved fix 3) ----------
  // slime: basic. runner (beetle): fast, Frost slows it. armoured (golem): Fire burns through the armour.
  // swarm (small slimes): many and weak, Storm chains through them. A boss every 10 waves.
  // Sizes in stage px: every regular enemy is shorter than a tier-1 tower (82 px); only the boss towers over the wall
  // (owner's correction, Nov 5 2026: "regular mobs towering over a small fence seems a stretch").
  const ROLES = {
    slime: { sheet: 'slime', hp: 1, speed: 46, size: 66, label: 'Slimes' },
    runner: { sheet: 'beetle', hp: 0.7, speed: 84, size: 60, label: 'Runners', weak: 'frost' },
    armoured: { sheet: 'golem', hp: 2.6, speed: 36, size: 76, label: 'Armoured', weak: 'fire', armour: 0.5 },
    swarm: { sheet: 'slime', hp: 0.35, speed: 54, size: 44, label: 'Swarm', weak: 'storm' },
    boss: { sheet: 'golem', hp: 90, speed: 28, size: 190, label: 'Boss', armour: 0.3 }
  };
  const isBoss = w => w % 10 === 0;
  // The wave's make-up as [role, count] pairs. New roles arrive one at a time: runners at wave 3, armoured at 6, swarm at 12.
  function waveMix(w) {
    if (isBoss(w)) return [['boss', 1], ['slime', Math.min(8, 2 + Math.floor(w / 10))]];
    const n = 6 + Math.min(14, w * 2);
    const mix = [];
    const runners = w >= 3 ? Math.floor(n * (w % 3 === 0 ? 0.5 : 0.25)) : 0;
    const armoured = w >= 6 ? Math.max(1, Math.floor(n * (w % 4 === 2 ? 0.3 : 0.12))) : 0;
    const swarm = w >= 12 && w % 5 === 2 ? n : 0;
    const slimes = Math.max(0, n - runners - armoured);
    if (slimes) mix.push(['slime', slimes]);
    if (runners) mix.push(['runner', runners]);
    if (armoured) mix.push(['armoured', armoured]);
    if (swarm) mix.push(['swarm', swarm]);
    return mix;
  }
  // How hard an enemy at the gate hits it, per second; grows with the wave like the gate's health grows with its level.
  const enemyBite = (w, role) => (role === 'boss' ? 16 : role === 'armoured' ? 7 : role === 'swarm' ? 2 : 4) * Math.pow(1.12, w - 1);
  const enemyHp = (w, role) => 10 * Math.pow(1.235, w - 1) * ROLES[role].hp;

  // ---------- idle: offline earnings, daily reward ----------
  const OFFLINE_CAP_H = 8, CITY_BASE = 0.09, LEVEL_COST = 0.4;
  // Resonance per second from the fight: about what the current wave pays when fought live.
  const fightRate = (p, b) => 4 * Math.pow(1.12, p.wave - 1) * 1.2 * (1 + b.res) * b.ascend;
  // While the player is away the gate keeps fighting and the city keeps humming; the Granary adds to both.
  const offlineRate = (p, b) => (fightRate(p, b) + cityRate(p, b)) * (1 + b.offline);
  function offlineEarnings(p, b, now) {
    if (!p.lastSeen || now <= p.lastSeen) return { secs: 0, res: 0, fight: 0, city: 0 };
    const secs = Math.min((OFFLINE_CAP_H + (b.offlineH || 0)) * 3600, (now - p.lastSeen) / 1000);
    if (secs < 60) return { secs, res: 0, fight: 0, city: 0 };
    const fight = Math.floor(secs * fightRate(p, b) * (1 + b.offline)), city = Math.floor(secs * cityRate(p, b) * (1 + b.offline));
    return { secs, res: fight + city, fight, city };
  }
  // Daily reward: one tap on the first screen of the day; a missed day pauses the streak (never resets it);
  // day 7 of each week pays 5x. Days use the player's local midnight; a clock moved back never pays twice.
  const dayKey = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  function dailyDue(p, now) {
    const today = dayKey(new Date(now));
    if (p.firstSession || !p.daily.day) return null;           // the first session never shows it
    if (today <= p.daily.day) return null;                    // already claimed today, or the clock went back
    const day = p.daily.streak + 1, big = day % 7 === 0;
    // worth about 20 minutes of the gate's income at the best wave reached, so it is felt at once
    const amount = Math.floor(4 * Math.pow(1.12, p.bestWave - 1) * 1.2 * 1200 * (big ? 5 : 1) * Math.pow(ASCEND_MULT, p.ascensions));
    return { today, day, big, amount };
  }

  // ---------- Ascension ----------
  // At 10/10 the player can Ascend: the city returns to ruins, the gate and waves restart, and Resonance and damage are
  // multiplied by 1.5 for good, stacking with each Ascension. Combos found and the best wave stay.
  const ASCEND_MULT = 1.5;
  const canAscend = p => p.restored.length >= BUILDINGS.length;
  function ascend(p) {
    const q = defaults();
    q.ascensions = p.ascensions + 1; q.bestWave = p.bestWave; q.combos = p.combos.slice();
    q.daily = Object.assign({}, p.daily); q.firstSession = false; q.lastSeen = p.lastSeen;
    q.settings = Object.assign({}, p.settings); q.grown = p.grown;
    q.shop = JSON.parse(JSON.stringify(p.shop)); q.quests = JSON.parse(JSON.stringify(p.quests)); q.stats = Object.assign({}, p.stats); q.feats = p.feats.slice(); q.surge = Object.assign({}, p.surge); q.shop.shards += SHARDS.ascend;
    q.perks = Object.assign({}, p.perks); q.perkPts = p.perkPts + perkPtsFor(p);
    const st = (q.perks.start || 0) * 5; if (st) { q.wave = 1 + st; q.gateLevel = 1 + st; }   // Head Start   // looks and Starshards stay; Ascending pays
    return q;
  }


  // ---------- the Shop: looks bought with Starshards (Nov 5 2026) ----------
  // Cosmetic only: nothing here changes a number in the fight. Starshards come from milestones (the shop opening, bosses,
  // new combos, daily rewards, Ascension) or from trading Resonance at a price that follows the player's own income,
  // so a trade always costs about a minute of fighting whatever the stage.
  const SHOP = [
    { id: 'fire-classic', cat: 'tower', el: 'fire', name: 'Forge Fire', line: 'The first flame', price: 0 },
    { id: 'fire-obsidian', cat: 'tower', el: 'fire', name: 'Obsidian', line: 'Black glass and magma', price: 20 },
    { id: 'fire-ember', cat: 'tower', el: 'fire', name: 'Ember Gold', line: 'Gilded and white-hot', price: 20 },
    { id: 'frost-classic', cat: 'tower', el: 'frost', name: 'Frost Spire', line: 'The first ice', price: 0 },
    { id: 'frost-night', cat: 'tower', el: 'frost', name: 'Glacier Night', line: 'Deep-sea ice, cold light', price: 20 },
    { id: 'frost-aurora', cat: 'tower', el: 'frost', name: 'Aurora', line: 'Ice that holds the northern lights', price: 20 },
    { id: 'storm-classic', cat: 'tower', el: 'storm', name: 'Storm Bell', line: 'The first thunder', price: 0 },
    { id: 'storm-void', cat: 'tower', el: 'storm', name: 'Voidcaller', line: 'Night-purple and humming', price: 20 },
    { id: 'storm-steel', cat: 'tower', el: 'storm', name: 'Thunder Steel', line: 'Silver iron, white sparks', price: 20 },
    { id: 'trail-classic', cat: 'trail', name: 'Orbs', line: 'A string of glowing beads', price: 0 },
    { id: 'trail-comet', cat: 'trail', name: 'Comet', line: 'One long burning tail', price: 12 },
    { id: 'trail-sparkle', cat: 'trail', name: 'Stardust', line: 'Twinkling sparks behind every shot', price: 12 },
    { id: 'trail-spiral', cat: 'trail', name: 'Twin Spiral', line: 'Two threads wound round the bolt', price: 15 },
    { id: 'trail-runes', cat: 'trail', name: 'Rune Orbit', line: 'Runes circle each shot', price: 15 },
    { id: 'hit-classic', cat: 'hit', name: 'Sparks', line: 'A spray of sparks', price: 0 },
    { id: 'hit-star', cat: 'hit', name: 'Starburst', line: 'An eight-point flash', price: 12 },
    { id: 'hit-ring', cat: 'hit', name: 'Shockwave', line: 'A ring rolls out from every hit', price: 12 },
    { id: 'hit-shards', cat: 'hit', name: 'Crystals', line: 'Glittering splinters of crystal', price: 15 },
    { id: 'wisp-rose', cat: 'wisp', name: 'Rose', line: 'The city\'s own colour', price: 0 },
    { id: 'wisp-gold', cat: 'wisp', name: 'Sunlit Gold', line: 'Wisps like falling coins', price: 8 },
    { id: 'wisp-star', cat: 'wisp', name: 'Starlight', line: 'White wisps with a blue rim', price: 8 },
    { id: 'wisp-ruby', cat: 'wisp', name: 'Ruby', line: 'Deep red and bright', price: 8 },
    { id: 'town-spring', cat: 'town', name: 'Spring Day', line: 'The city as it was painted', price: 0 },
    { id: 'town-golden', cat: 'town', name: 'Golden Hour', line: 'Late sun on every roof', price: 30 },
    { id: 'town-autumn', cat: 'town', name: 'Autumn', line: 'Amber fields and red leaves', price: 30 },
    { id: 'town-winter', cat: 'town', name: 'First Snow', line: 'Snow on the grass, ice-blue light', price: 30 },
    { id: 'town-moon', cat: 'town', name: 'Moonlit', line: 'Night falls, the windows glow', price: 30 }
  ];
  const SLOTS = ['fire', 'frost', 'storm', 'trail', 'hit', 'wisp', 'town'];
  const shopItem = id => SHOP.find(i => i.id === id);
  const slotOf = it => it.cat === 'tower' ? it.el : it.cat;
  const FREE = SHOP.filter(i => !i.price).map(i => i.id);
  const SHARDS = { welcome: 10, boss: 3, combo: 2, daily: 3, daily7: 10, ascend: 15 };
  const SHOP_OPENS = 11;   // the shop opens once the first boss (wave 10) falls
  const exchangeCost = (p, b) => Math.max(30, Math.ceil(fightRate(p, b) * 1.6 * 60));   // about a minute of fighting
  function shopDefaults() {
    const equip = {}; for (const i of SHOP) if (!i.price) equip[slotOf(i)] = i.id;
    return { open: false, shards: 0, owned: FREE.slice(), equip, seen: [] };
  }
  function buy(p, id) {
    const it = shopItem(id); if (!it || p.shop.owned.includes(id) || p.shop.shards < it.price) return false;
    p.shop.shards -= it.price; p.shop.owned.push(id); p.shop.equip[slotOf(it)] = id; return true;
  }
  function equip(p, id) { const it = shopItem(id); if (!it || !p.shop.owned.includes(id)) return false; p.shop.equip[slotOf(it)] = id; return true; }
  function trade(p, b) { const c = exchangeCost(p, b); if (p.res < c) return false; p.res -= c; p.shop.shards += 1; return true; }
  // something new the player can afford: drives the dot on the shop button
  const shopNews = p => p.shop.open && SHOP.some(i => !p.shop.owned.includes(i.id) && i.price <= p.shop.shards && !p.shop.seen.includes(i.id));
  function sanitizeShop(s) {
    const out = shopDefaults(); if (!s || typeof s !== 'object') return out;
    out.open = s.open === true;
    out.shards = Math.floor(num(s.shards, 0, 1e9, 0));
    if (Array.isArray(s.owned)) out.owned = [...new Set(FREE.concat(s.owned.filter(id => shopItem(id))))];
    if (Array.isArray(s.seen)) out.seen = [...new Set(s.seen.filter(id => shopItem(id)))];
    if (s.equip && typeof s.equip === 'object') for (const k of SLOTS) {
      const id = s.equip[k], it = shopItem(id); if (it && slotOf(it) === k && out.owned.includes(id)) out.equip[k] = id;
    }
    return out;
  }



  // ---------- Feats (Nov 5 2026): a long collection, shown as N of total, each paying Starshards once ----------
  // Counted from lifetime stats that survive Ascension. Tier 1 bronze, 2 silver, 3 gold; gold pays more.
  const STATS = ['kills', 'bosses', 'combos', 'shatter', 'upgrades', 'levels', 'surges', 'quests'];
  const FEATS = [];
  const feat = (id, name, tier, text, get, need) => FEATS.push({ id, name, tier, text, get, need });
  [[10, 'Holdfast', 1], [25, 'Stalwart', 1], [50, 'Bulwark', 2], [100, 'Unbroken', 2], [200, 'Everwall', 3]]
    .forEach(([n, nm, t]) => feat('wave' + n, nm, t, `Reach wave ${n}`, p => p.bestWave, n));
  [[100, 'First Blood', 1], [1000, 'Monster Bane', 1], [10000, 'Horde Breaker', 2], [100000, 'Legend of the Gate', 3]]
    .forEach(([n, nm, t]) => feat('kills' + n, nm, t, `Defeat ${fmt(n)} enemies`, p => p.stats.kills, n));
  [[1, 'Giant Slayer', 1], [5, 'Boss Hunter', 2], [25, 'Kingbreaker', 3]]
    .forEach(([n, nm, t]) => feat('boss' + n, nm, t, n === 1 ? 'Defeat a boss' : `Defeat ${n} bosses`, p => p.stats.bosses, n));
  [[10, 'Cracked Ice', 1], [100, 'Shatterer', 2], [1000, 'Glass Storm', 3]]
    .forEach(([n, nm, t]) => feat('shatter' + n, nm, t, `Trigger SHATTER ${fmt(n)} times`, p => p.stats.shatter, n));
  feat('allcombos', 'Elementalist', 2, 'Discover all three combos', p => p.combos.length, 3);
  [[500, 'Chain Reaction', 1], [5000, 'Combo Master', 2]]
    .forEach(([n, nm, t]) => feat('combos' + n, nm, t, `Land ${fmt(n)} combos`, p => p.stats.combos, n));
  [[5, 'Rebuilder', 1], [10, 'Hearth Restored', 2]]
    .forEach(([n, nm, t]) => feat('city' + n, nm, t, `Restore ${n} buildings in one city`, p => p.restored.length, n));
  [[1, 'Reborn', 2], [3, 'Thrice Risen', 2], [10, 'Eternal City', 3]]
    .forEach(([n, nm, t]) => feat('ascend' + n, nm, t, n === 1 ? 'Ascend once' : `Ascend ${n} times`, p => p.ascensions, n));
  [[25, 'Iron Gate', 1], [50, 'Steel Gate', 2], [100, 'Starforged Gate', 3]]
    .forEach(([n, nm, t]) => feat('gate' + n, nm, t, `Upgrade the gate to level ${n}`, p => p.gateLevel, n));
  [[10, 'Humming Halls', 1], [25, 'Resonant', 2], [50, 'Heart of the City', 3]]
    .forEach(([n, nm, t]) => feat('blvl' + n, nm, t, `Get one building to level ${n}`, p => Math.max(0, ...p.restored.map(id => p.levels[id] || 1)), n));
  [[3, 'New Look', 1], [10, 'Collector', 2], [SHOP.length, 'Grand Wardrobe', 3]]
    .forEach(([n, nm, t]) => feat('looks' + n, nm, t, n === SHOP.length ? 'Own every look' : `Own ${n} looks`, p => p.shop.owned.filter(id => shopItem(id).price).length, n));
  [[10, 'Errand Runner', 1], [50, 'Questkeeper', 2]]
    .forEach(([n, nm, t]) => feat('quests' + n, nm, t, `Finish ${n} daily quests`, p => p.stats.quests, n));
  [[5, 'Surge Rider', 1], [25, 'Storm of Light', 2]]
    .forEach(([n, nm, t]) => feat('surges' + n, nm, t, `Start ${n} Surges`, p => p.stats.surges, n));
  feat('streak7', 'Faithful', 2, 'Claim the daily reward 7 days in a row', p => p.daily.streak, 7);
  const FEAT_PAY = [0, 2, 4, 8];
  const featProgress = (p, f) => Math.min(f.need, Math.max(0, f.get(p) || 0));
  // newly earned Feats: marked, paid, and returned so the game can celebrate each one
  function featsEarned(p) {
    const out = [];
    for (const f of FEATS) if (!p.feats.includes(f.id) && featProgress(p, f) >= f.need) { p.feats.push(f.id); p.shop.shards += FEAT_PAY[f.tier]; out.push(f); }
    return out;
  }

  // ---------- daily quests (Nov 5 2026): three goals a day, each pays Starshards, all three pay a bonus ----------
  // Picked from the pool by the day, so a reload never rerolls them; a goal only appears once it can be done.
  const QUESTS = [
    { id: 'kills', text: n => `Defeat ${n} enemies`, need: 150, ok: () => true },
    { id: 'waves', text: n => `Clear ${n} waves`, need: 8, ok: () => true },
    { id: 'upgrade', text: n => `Upgrade the gate ${n} times`, need: 8, ok: () => true },
    { id: 'combo', text: n => `Land ${n} combos`, need: 40, ok: p => p.combos.length > 0 },
    { id: 'shatter', text: n => `Trigger SHATTER ${n} times`, need: 15, ok: p => p.combos.includes('shatter') },
    { id: 'level', text: n => `Level up buildings ${n} times`, need: 6, ok: p => p.restored.length > 0 },
    { id: 'boss', text: () => 'Defeat a boss', need: 1, ok: p => p.bestWave >= 10 },
    { id: 'surge', text: () => 'Start a Resonance Surge', need: 1, ok: () => true }
  ];
  const QUEST_REWARD = 4, QUEST_BONUS = 5;
  function questsFor(p, today) {
    if (p.quests.day === today && p.quests.list.length) return p.quests;
    const pool = QUESTS.filter(q => q.ok(p)), seed = [...today].reduce((a, c) => a * 31 + c.charCodeAt(0) >>> 0, 7), pick = [];
    for (let i = 0; pick.length < 3 && i < pool.length * 3; i++) { const q = pool[(seed + i * 5) % pool.length]; if (!pick.includes(q)) pick.push(q); }
    p.quests = { day: today, list: pick.map(q => ({ id: q.id, n: 0, need: q.need, claimed: false })), bonus: false };
    return p.quests;
  }
  const questText = q => QUESTS.find(x => x.id === q.id).text(q.need);
  function questTick(p, id, amount = 1) { for (const q of p.quests.list) if (q.id === id && q.n < q.need) q.n = Math.min(q.need, q.n + amount); }
  const questClaimable = p => p.quests.list.some(q => q.n >= q.need && !q.claimed) || (!p.quests.bonus && p.quests.list.length && p.quests.list.every(q => q.claimed));
  function questClaim(p, i) {
    const q = p.quests.list[i]; if (!q || q.claimed || q.n < q.need) return 0;
    q.claimed = true; p.stats.quests++; p.shop.shards += QUEST_REWARD; return QUEST_REWARD;
  }
  function questBonus(p) { if (p.quests.bonus || !p.quests.list.length || !p.quests.list.every(q => q.claimed)) return 0; p.quests.bonus = true; p.shop.shards += QUEST_BONUS; return QUEST_BONUS; }

  // ---------- Resonance Surge: double Resonance for 3 minutes ----------
  // Basic Launch (no ads): it recharges for free, 15 minutes after a surge ends. Full Launch: a rewarded ad starts one
  // whenever none is running, up to 8 a day. Real-time stamps, so it runs on across reloads.
  const SURGE_MS = 3 * 60e3, SURGE_COOLDOWN_MS = 15 * 60e3, SURGE_ADS_PER_DAY = 8, SHARD_ADS_PER_DAY = 5, SHARD_AD = 3;
  const surgeOn = (p, now) => now < p.surge.until;
  const surgeCooldown = p => SURGE_COOLDOWN_MS - ((p.perks && p.perks.surge) || 0) * 3 * 60e3;
  const surgeReadyFree = (p, now) => !surgeOn(p, now) && now >= p.surge.until + surgeCooldown(p);
  function startSurge(p, now) { p.surge.until = now + SURGE_MS; }

  // ---------- save profile ----------
  function defaults() {
    return { v: 3, res: 0, wave: 1, bestWave: 1, gateLevel: 1, restored: [], levels: {}, grown: false, shop: shopDefaults(), quests: { day: '', list: [], bonus: false }, surge: { until: 0, adDay: '', ads: 0, shardAds: 0 }, stats: Object.fromEntries(STATS.map(k => [k, 0])), feats: [], perks: {}, perkPts: 0, combos: [], forks: {}, ascensions: 0,
      lastSeen: 0, daily: { day: '', streak: 0, claimedTotal: 0 }, firstSession: true,
      settings: { music: true, sfx: true, reduced: false } };
  }
  const num = (v, lo, hi, d) => (typeof v === 'number' && isFinite(v)) ? Math.min(hi, Math.max(lo, v)) : d;
  const LEGACY = { b4: 'bakery', b5: 'market', b6: 'tavern', b7: 'granary', b8: 'library', b9: 'cottages', b10: 'watchtower' };

  // Fill missing fields, clamp numbers, drop unknown keys and buildings, migrate v1 saves. Runs on every load.
  function sanitize(raw) {
    let p = raw;
    if (typeof p === 'string') { try { p = JSON.parse(p); } catch (e) { p = null; } }
    const out = defaults();
    if (!p || typeof p !== 'object') return out;
    out.res = num(p.res !== undefined ? p.res : p.parts, 0, 1e300, 0);   // v2 saves called it Parts
    out.wave = Math.floor(num(p.wave, 1, 1e9, 1));
    out.bestWave = Math.max(out.wave, Math.floor(num(p.bestWave, 1, 1e9, 1)));
    out.gateLevel = Math.floor(num(p.gateLevel, 1, 1e6, 1));
    out.ascensions = Math.floor(num(p.ascensions, 0, 1e6, 0));
    out.lastSeen = num(p.lastSeen, 0, 1e15, 0);
    out.firstSession = p.firstSession !== false;
    const rest = Array.isArray(p.restored) ? p.restored.map(b => LEGACY[b] || b) : [];
    out.restored = [...new Set(rest.filter(b => BUILDING_IDS.includes(b)))];
    if (p.levels && typeof p.levels === 'object') for (const id of out.restored) {
      const L = Math.floor(num(p.levels[id], 1, 1e5, 1)); if (L > 1) out.levels[id] = L;
    }
    out.grown = p.grown === true;
    out.shop = sanitizeShop(p.shop);
    if (p.quests && typeof p.quests === 'object' && typeof p.quests.day === 'string' && Array.isArray(p.quests.list)) {
      const list = p.quests.list.filter(q => q && QUESTS.some(x => x.id === q.id)).slice(0, 3)
        .map(q => { const need = QUESTS.find(x => x.id === q.id).need; return { id: q.id, need, n: Math.floor(num(q.n, 0, need, 0)), claimed: q.claimed === true }; });
      out.quests = { day: p.quests.day, list, bonus: p.quests.bonus === true };
    }
    if (p.stats && typeof p.stats === 'object') for (const k of STATS) out.stats[k] = Math.floor(num(p.stats[k], 0, 1e15, 0));
    if (p.perks && typeof p.perks === 'object') for (const k of PERKS) { const n = Math.floor(num(p.perks[k.id], 0, k.max, 0)); if (n) out.perks[k.id] = n; }
    out.perkPts = Math.floor(num(p.perkPts, 0, 1e6, 0));
    if (p.perks === undefined && out.ascensions > 0) out.perkPts = out.ascensions * 2;   // saves from before perks: points for the Ascensions already made
    if (Array.isArray(p.feats)) out.feats = [...new Set(p.feats.filter(id => FEATS.some(f => f.id === id)))];
    if (p.surge && typeof p.surge === 'object') out.surge = { until: num(p.surge.until, 0, 1e15, 0), adDay: typeof p.surge.adDay === 'string' ? p.surge.adDay : '',
      ads: Math.floor(num(p.surge.ads, 0, 99, 0)), shardAds: Math.floor(num(p.surge.shardAds, 0, 99, 0)) };
    out.combos = Array.isArray(p.combos) ? [...new Set(p.combos.filter(c => COMBOS.includes(c)))] : [];
    if (p.forks && typeof p.forks === 'object') for (const s of [2, 3]) {
      const f = p.forks[s]; if (typeof f === 'string' && FORKS[s].some(x => x.id === f) && stageOf(out.gateLevel) >= s) out.forks[s] = f;
    }
    if (p.daily && typeof p.daily === 'object') {
      out.daily.day = typeof p.daily.day === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(p.daily.day) ? p.daily.day : '';
      out.daily.streak = Math.floor(num(p.daily.streak, 0, 1e6, 0));
      out.daily.claimedTotal = Math.floor(num(p.daily.claimedTotal, 0, 1e15, 0));
    }
    if (p.settings && typeof p.settings === 'object') for (const k of ['music', 'sfx', 'reduced']) {
      if (typeof p.settings[k] === 'boolean') out.settings[k] = p.settings[k];
    }
    return out;
  }

  // ---------- the city painting ----------
  // Crop of the city painting into the 720x440 band, its focal point (the gatehouse and plaza on the centre line),
  // and each building's spot for its restore reveal, in source pixels. Filled in from the painting by tools/city_layout.py.
  const CITY = root.HG_CITY || { band: [720, 440], crop: [0, 30, 1280, 782], focus: [620, 430], platforms: [], spots: {} };
  const cityToBand = (sx, sy) => [(sx - CITY.crop[0]) * CITY.band[0] / CITY.crop[2], (sy - CITY.crop[1]) * CITY.band[1] / CITY.crop[3]];

  root.HGCore = { fmt, BUILDINGS, BUILDING_IDS, byId, FIXED_ORDER, COMBOS, restoreOffer, STAGES, FORKS, stageOf, pendingFork,
    bonuses, upgradeCost, restoreCost, towerDamage, fireInterval, gateMax, killRes,
    MILESTONES, levelOf, buildingRate, cityRate, levelCost, nextMilestone, fightRate, ROLES, isBoss, waveMix, enemyHp, enemyBite,
    OFFLINE_CAP_H, offlineRate, offlineEarnings, dayKey, dailyDue, ASCEND_MULT, canAscend, ascend, defaults, sanitize,
    CITY, cityToBand, OMENS, omenFor, PERKS, perkCost, buyPerk, perkPtsFor, surgeCooldown, STATS, FEATS, FEAT_PAY, featProgress, featsEarned, QUESTS, QUEST_REWARD, QUEST_BONUS, questsFor, questText, questTick, questClaimable, questClaim, questBonus,
    SURGE_MS, SURGE_COOLDOWN_MS, SURGE_ADS_PER_DAY, SHARD_ADS_PER_DAY, SHARD_AD, surgeOn, surgeReadyFree, startSurge, SHOP, SLOTS, shopItem, slotOf, SHARDS, SHOP_OPENS, exchangeCost, buy, equip, trade, shopNews };
  if (typeof module !== 'undefined') module.exports = root.HGCore;
})(typeof globalThis !== 'undefined' ? globalThis : this);
