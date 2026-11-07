// Trailer segment capture (Nov 6 2026): a later-game moment from a seeded save, same look and frame rules as capture.cjs.
// The save starts one wave early and the field is cleared, so the named wave begins on frame one with its banner.
// Usage: node capture-seg.cjs <url?qa> <outdir> <first frame index> <seconds> <scene: meteor|wraith>
const { chromium } = require('playwright');
const { SHIM, tick } = require('./tick.cjs');
const [url, out, first, secs, scene] = process.argv.slice(2);
const FPS = 30, ALL = ['forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'granary', 'library', 'cottages', 'watchtower'];
const LOOK = { owned: ['fire-obsidian', 'frost-aurora', 'storm-void', 'trail-comet', 'hit-star', 'town-moon', 'town-winter', 'wisp-gold'],
  equip: { fire: 'fire-obsidian', frost: 'frost-aurora', storm: 'storm-void', trail: 'trail-comet', hit: 'hit-star', town: 'town-winter', wisp: 'wisp-gold' }, seen: [] };   // the main capture buys First Snow at 9.6 s, so the later scenes keep it
const SCENES = {
  // the 2nd Ascension's Meteor Shower over the gilded city
  meteor: { preroll: 300, wave: 27, gateLevel: 9, ascensions: 2, seenEvents: ['stampede', 'golden', 'horde', 'pack', 'tide', 'thunder', 'blizzard', 'fog', 'inferno', 'avalanche', 'eclipse', 'harvest', 'starfall', 'frenzy', 'giants', 'rush', 'aurora', 'ironrain', 'gale', 'hail', 'jackpot', 'winter'] },
  // the Wraith King, the 2nd Ascension's boss, fading in and out under Storm
  wraith: { preroll: 2000, wave: 19, gateLevel: 2, ascensions: 2, seenEvents: [] }
};
(async () => {
  const sc = SCENES[scene], b = await chromium.launch();
  const ctx = await b.newContext({ viewport: { width: 450, height: 800 }, deviceScaleFactor: 2 });
  await ctx.addInitScript(sv => { window.__storeCapture = true; if (!sessionStorage.seeded) { sessionStorage.seeded = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify(sv)); } },
    Object.assign({ firstSession: false, grown: true, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0, 10), streak: 2 }, restored: ALL,
      levels: { forge: 30, frostSpire: 26, stormBell: 22, bakery: 18 }, combos: ['shatter', 'frostStorm', 'fireStorm'], forks: { 2: 'ballistae', 3: 'focus' }, res: 4e8,
      bestWave: 60, runWave: sc.wave, shop: Object.assign({ open: true, shards: 60 }, LOOK), seenRoles: ['slime', 'runner', 'armoured', 'swarm', 'elite', 'shielded', 'splitter', 'mender', 'ironhide', 'sprinter', 'bulwark', 'boss', 'queen', 'lord', 'phantom', 'thief'],
      quests: { day: new Date().toISOString().slice(0, 10), list: [], bonus: false } }, sc));
  const p = await ctx.newPage(); await p.addInitScript(SHIM); await p.goto(url);
  for (let i = 0; i < 60 && await p.evaluate(() => !!document.getElementById('boot')); i++) { await p.waitForTimeout(60); await tick(p, 100); }
  await p.addStyleTag({ content: '#toast{display:none!important} #hand{display:none!important} #guide{display:none!important}' });
  await p.evaluate(() => { const H = Hearthgate; for (let k = 0; k < 4 && H.card(); k++) H.act('ok'); if (H.card()) H.act('close'); const { S } = H.state(); S.omenTold = true; S.enemies = []; S.spawnQ = []; });
  await tick(p, sc.preroll);   // the new wave has just begun: its banner holds most of the shot
  let n = +first;
  for (let f = 0; f < FPS * +secs; f++) { await p.evaluate(() => { const { P, S, B } = Hearthgate.state(); S.gateHp = HGCore.gateMax(P, B); }); await tick(p, 1000 / FPS);   // the gate holds for the shot: a weak gate keeps the fight long, a full bar keeps the wave from falling back
    await p.screenshot({ path: `${out}/frame-${String(n++).padStart(5, '0')}.jpg`, type: 'jpeg', quality: 92 }); }
  console.log(scene, 'frames', n - +first, await p.evaluate(() => { const { S } = Hearthgate.state(); return [S.event && S.event.id, S.enemies.map(e => e.role).join(',')]; })); await b.close();
})();
