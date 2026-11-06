// Trailer capture: real gameplay of a mid-game save (Moonlit city, Shop towers), stepped with Playwright's clock so
// every frame is exactly 1/30 s apart; scripted taps at fixed times. Usage: node capture.cjs <url?qa> <outdir>
// Output: frame-00000.jpg ... at 2x of a 450x800 phone (900x1600).
const { chromium } = require('playwright');
const [url, out] = process.argv.slice(2);
const FPS = 30, SECONDS = 19;
(async () => {
  const b = await chromium.launch();
  const ctx = await b.newContext({ viewport: { width: 450, height: 800 }, deviceScaleFactor: 2 });
  await ctx.addInitScript(() => { window.__storeCapture = true; if (!sessionStorage.seeded) { sessionStorage.seeded = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify({
    firstSession: false, grown: true, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0, 10), streak: 2 },
    restored: ['forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern'], levels: { forge: 14, frostSpire: 11, stormBell: 9 },
    combos: ['shatter', 'frostStorm', 'fireStorm'], forks: { 2: 'ballistae' }, res: 2.4e6, gateLevel: 30, wave: 28, bestWave: 28,
    shop: { open: true, shards: 60, owned: ['fire-obsidian', 'frost-aurora', 'storm-void', 'trail-comet', 'hit-star', 'town-moon', 'wisp-gold'],
      equip: { fire: 'fire-obsidian', frost: 'frost-aurora', storm: 'storm-void', trail: 'trail-comet', hit: 'hit-star', town: 'town-moon', wisp: 'wisp-gold' }, seen: [] },
    quests: { day: new Date().toISOString().slice(0, 10), list: [], bonus: false } })); } });
  const p = await ctx.newPage();
  await p.clock.install();
  await p.goto(url);
  for (let i = 0; i < 60 && await p.evaluate(() => !!document.getElementById('boot')); i++) await p.clock.runFor(100);
  await p.addStyleTag({ content: '#toast{display:none!important} #hand{display:none!important}' });
  await p.clock.runFor(2500);   // the wave is under way when the video starts
  const script = { 2.2: 'up', 3.6: 'up', 5.0: 're', 5.4: 'pick', 8.6: 'shop', 9.0: 'tabtown', 9.6: 'buywinter', 10.0: 'close', 10.4: 'boss' };   // the Shop holds the screen about 1.4 s
  let n = 0;
  for (let f = 0; f < FPS * SECONDS; f++) {
    const t = +(f / FPS).toFixed(2);
    for (const [k, a] of Object.entries(script)) if (Math.abs(t - k) < 0.5 / FPS) await p.evaluate(a => {
      const H = Hearthgate;
      if (a === 'up') H.act('up'); else if (a === 're') H.act('re'); else if (a === 'pick') H.act('pick');
      else if (a === 'shop') H.act('shop'); else if (a === 'tabtown') H.act('tab', 'town'); else if (a === 'buywinter') H.act('buy', 'town-winter');
      else if (a === 'close') H.act('close');
      else if (a === 'boss') { const s = H.state(); s.P.wave = 29; s.S.enemies.forEach(e => (e.hp = 0)); s.S.spawnQ = []; }
    }, a);
    await p.clock.runFor(1000 / FPS);
    await p.screenshot({ path: `${out}/frame-${String(n++).padStart(5, '0')}.jpg`, type: 'jpeg', quality: 92 });
  }
  console.log('frames', n); await b.close();
})();
