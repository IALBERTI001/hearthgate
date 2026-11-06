// QA step 1 (crazygames-qa): scripted sessions on the UPLOAD PACKAGE with a stand-in CrazyGames SDK that logs calls.
// Usage: node tools/qa/technical-gate.cjs <url of the served package> <outdir>
const { chromium } = require('playwright');
const fs = require('fs');
const [B, OUT] = process.argv.slice(2); fs.mkdirSync(OUT, { recursive: true });
const fake = () => { window.__log = []; const st = {}; window.CrazyGames = { SDK: { environment: 'crazygames', init: async () => {},
  game: { gameplayStart: () => __log.push('start'), gameplayStop: () => __log.push('stop'), loadingStart: () => __log.push('loadStart'), loadingStop: () => __log.push('loadStop'), happytime: () => __log.push('happy') },
  ad: { requestAd: (t, cb) => { __log.push('ad:' + t); setTimeout(() => cb.adError && cb.adError('test'), 50); } },
  data: { getItem: k => st[k] ?? null, setItem: (k, v) => { st[k] = v; __log.push('data'); }, removeItem: k => delete st[k] } } }; };
const sizes = [[390, 844], [800, 450], [821, 462], [910, 512], [1080, 607], [1280, 720], [1920, 1080]];
(async () => {
  const b = await chromium.launch(); const out = []; const hosts = new Set(); const bad = [];
  for (const [w, h] of sizes) {
    const ctx = await b.newContext({ viewport: { width: w, height: h } }); await ctx.addInitScript(fake); const pg = await ctx.newPage();
    let bytes = 0, started = false;
    pg.on('pageerror', e => bad.push(`${w}x${h} JS ${e.message}`));
    pg.on('console', m => { if (m.type() === 'error') bad.push(`${w}x${h} console ${m.text().slice(0, 160)}`); });
    pg.on('request', r => hosts.add(new URL(r.url()).host));
    pg.on('response', async r => { if (r.status() >= 400) bad.push(`${w}x${h} ${r.status()} ${r.url()}`); if (!started) { try { bytes += (await r.body()).length; } catch (e) {} } });
    const t0 = Date.now(); await pg.goto(B); let tStart = null;
    for (let i = 0; i < 80; i++) { await pg.waitForTimeout(125); if (await pg.evaluate(() => window.__log && __log.includes('start'))) { tStart = Date.now() - t0; started = true; break; } }
    await pg.waitForTimeout(3500); await pg.screenshot({ path: `${OUT}/${w}x${h}-play.png` });
    // smallest rendered text: every visible text element's font size times the stage scale
    const small = await pg.evaluate(() => { const st = document.getElementById('stage'); const f = st.getBoundingClientRect().width / 720; const r = [];
      for (const e of st.querySelectorAll('*')) { if (!e.offsetParent && e.id !== 'stage') continue; const t = [...e.childNodes].filter(n => n.nodeType === 3 && n.textContent.trim()); if (!t.length) continue; const px = parseFloat(getComputedStyle(e).fontSize) * f; if (px < 10) r.push(`${e.id || e.className}:${px.toFixed(1)}px`); } return r; });
    // pause screen (settings) then resume: gameplayStop then gameplayStart
    await pg.click('#pause'); await pg.waitForTimeout(400); await pg.screenshot({ path: `${OUT}/${w}x${h}-pause.png` });
    const stamp = await pg.evaluate(() => document.querySelector('#card').innerText.includes('build'));
    await pg.click('#c-ok'); await pg.waitForTimeout(300);
    const log = await pg.evaluate(() => __log.join(' '));
    const canvas = await pg.evaluate(() => { const c = document.getElementById('field'); const r = c.getBoundingClientRect(); return { backing: c.width, shown: Math.round(r.width * devicePixelRatio) }; });
    // a returning player with progress: daily reward and offline cards, then a restore pick and a fork
    const ctx2 = await b.newContext({ viewport: { width: w, height: h } }); await ctx2.addInitScript(fake);
    await ctx2.addInitScript(() => { if (sessionStorage.getItem('seeded')) return; sessionStorage.setItem('seeded', '1');
      localStorage.setItem('hearthgate.profile.v1', JSON.stringify({ firstSession: false, lastSeen: Date.now() - 5 * 3600e3, daily: { day: '2026-01-01', streak: 3, claimedTotal: 0 }, restored: ['forge', 'frostSpire', 'stormBell'], res: 9e5, gateLevel: 15, wave: 12, bestWave: 12 })); });
    const pg1 = pg; await ctx.close(); const pg2 = await ctx2.newPage(); pg2.on('pageerror', e => bad.push(`${w}x${h} JS ${e.message}`));
    await pg2.goto(B); await pg2.waitForTimeout(1500); await pg2.screenshot({ path: `${OUT}/${w}x${h}-daily.png` });
    await pg2.click('#c-ok'); await pg2.waitForTimeout(400); await pg2.screenshot({ path: `${OUT}/${w}x${h}-offline.png` });
    await pg2.click('#c-ok'); await pg2.waitForTimeout(300); await pg2.click('#re'); await pg2.waitForTimeout(400); await pg2.screenshot({ path: `${OUT}/${w}x${h}-pick.png` });
    await pg2.click('#card .opt'); await pg2.waitForTimeout(300); await pg2.click('#up'); await pg2.waitForTimeout(400); await pg2.screenshot({ path: `${OUT}/${w}x${h}-fork.png` });
    await pg2.click('#card .opt'); await pg2.waitForTimeout(1500);
    const log2 = await pg2.evaluate(() => __log.join(' '));
    out.push(`${w}x${h}: gameplayStart ${tStart} ms, initial download ${(bytes / 1e6).toFixed(2)} MB, small text [${small.join(', ')}], build stamp ${stamp}, canvas backing ${canvas.backing} for ${canvas.shown} shown px, sdk: ${log.slice(0, 90)} | returning: ${log2.slice(0, 60)}`);
    await ctx2.close();
  }
  // ad blocker: SDK script blocked, no stand-in: the game must still play
  const ctx = await b.newContext({ viewport: { width: 390, height: 844 } }); const pg = await ctx.newPage();
  await pg.route('**/sdk.crazygames.com/**', r => r.abort()); const errs = []; pg.on('pageerror', e => errs.push(e.message));
  await pg.goto(B + '?useLocalSdk=true'); await pg.waitForTimeout(5000);
  const kills = await pg.evaluate(() => window.Hearthgate ? Hearthgate.state().P.res : -1);
  out.push(`ad blocker: playable ${kills > 0} (Resonance ${kills}), errors ${errs.length}`);
  await ctx.close(); await b.close();
  console.log(out.join('\n')); console.log('hosts:', [...hosts].join(' ')); console.log('problems:', bad.length ? '\n' + bad.join('\n') : 'none');
})();
