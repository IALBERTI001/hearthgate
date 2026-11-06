// Store cover C (Shop looks: Moonlit city, Voidcaller, Aurora, Obsidian, comet trails, gold wisps), same steps as cover B.
// Store cover B, step 1: real frames of a late-game save (wave 41, most of the city restored), HUD hidden, at 2x.
// Usage: node tools/cover_b_capture.cjs <url> <outdir>   ->  frame-0.png ... frame-11.png (pick one for tools/cover_b.py)
const { chromium } = require('playwright');
const [url, out] = process.argv.slice(2);
(async () => {
  const b = await chromium.launch();
  const ctx = await b.newContext({ viewport: { width: 450, height: 800 }, deviceScaleFactor: 2 });
  await ctx.addInitScript(() => { window.__storeCapture = true; if (!sessionStorage.seeded) { sessionStorage.seeded = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify({
    firstSession: false, grown: true, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0, 10), streak: 4, claimedTotal: 0 },
    restored: ['forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'library', 'granary'], levels: { forge: 25, frostSpire: 18, stormBell: 12, bakery: 10 },
    combos: ['shatter', 'frostStorm', 'fireStorm'], forks: { 2: 'ballistae', 3: 'focus' }, shop: { open: true, shards: 0, owned: ['fire-obsidian','frost-aurora','storm-void','trail-comet','hit-star','town-moon','wisp-gold'], equip: { fire: 'fire-obsidian', frost: 'frost-aurora', storm: 'storm-void', trail: 'trail-comet', hit: 'hit-star', town: 'town-moon', wisp: 'wisp-gold' } }, res: 0, gateLevel: 27, wave: 41, bestWave: 41 })); } });
  const p = await ctx.newPage();
  await p.goto(url); await p.waitForFunction(() => !document.getElementById('boot'));
  await p.addStyleTag({ content: '#hud,#counters,#bar,#toast,#hand,#card{display:none!important}' });
  await p.waitForTimeout(4200);
  for (let i = 0; i < 12; i++) { await p.screenshot({ path: `${out}/frame-${i}.png` }); await p.waitForTimeout(260); }
  await b.close();
})();
