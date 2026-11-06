const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch({ args: ['--autoplay-policy=no-user-gesture-required'] });
  const ctx = await b.newContext({ viewport: { width: 390, height: 844 } });
  await ctx.addInitScript(() => { if (!sessionStorage.s) { sessionStorage.s = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify({ firstSession: false, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0,10), streak: 1 }, restored: ['forge','frostSpire','stormBell'], res: 0, gateLevel: 30, wave: 9, bestWave: 9 })); } });
  const p = await ctx.newPage(); const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => m.type() === 'error' && errs.push(m.text()));
  const reqs = []; p.on('request', r => { if (/music|ambience/.test(r.url())) reqs.push(r.url().split('/').slice(-2).join('/')); });
  await p.goto(process.argv[2]); await p.waitForFunction(() => !document.getElementById('boot'));
  const st = () => p.evaluate(() => JSON.stringify(Object.assign(HGAudio.state(), { wave: Hearthgate.state().P.wave })));
  console.log('before tap', await st());
  await p.mouse.click(200, 400); await p.waitForTimeout(800); console.log('after tap', await st());
  await p.evaluate(() => { const s = Hearthgate.state(); s.S.enemies.forEach(e => e.hp = 0); s.S.spawnQ = []; }); await p.waitForTimeout(1500); console.log('boss wave', await st());
  await p.evaluate(() => { const s = Hearthgate.state(); s.S.enemies.forEach(e => e.hp = 0); s.S.spawnQ = []; }); await p.waitForTimeout(1500); console.log('after boss', await st());
  await p.waitForTimeout(11000); console.log('10 s later', await st());
  console.log('requests', reqs, 'errors', errs); await b.close();
})();
