// Scripted new player: cleared storage, taps whatever the hand points at (~2 s thinking), logs the opening timeline.
// Usage: node tools/qa/new-player-run.cjs <url> <outdir> <w> <h> [seconds]
const { chromium } = require('playwright');
const [url, out, w, h, secs = '75'] = process.argv.slice(2);
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: +w, height: +h }, deviceScaleFactor: 1 });
  const errors = []; p.on('pageerror', e => errors.push(e.message)); p.on('console', m => m.type() === 'error' && errors.push(m.text()));
  const t0 = Date.now(); await p.goto(url); 
  await p.waitForFunction(() => !document.getElementById('boot'), null, { timeout: 15000 });
  const log = { loadToFight: (Date.now() - t0) / 1000, events: [] };
  const mark = (k) => { if (!log.events.find(e => e.k === k)) log.events.push({ k, t: +((Date.now() - t0) / 1000).toFixed(1) }); };
  await p.screenshot({ path: `${out}/0-load.png` });
  let shotSeen = false;
  while ((Date.now() - t0) / 1000 < +secs) {
    const st = await p.evaluate(() => { const { P, S } = Hearthgate.state(); return { parts: P.parts, lvl: P.gateLevel, rest: P.restored.length, combos: P.combos.slice(), pops: S.pops.filter(x => x.text).map(x => x.text), hand: document.getElementById('hand').className, words: document.body.innerText.split(/\s+/).filter(Boolean).length }; });
    if (st.pops.length) mark('firstPayoff');
    if (st.hand.includes('up')) { mark('handUpgrade'); await p.waitForTimeout(2000); await p.click('#up'); mark('firstUpgrade'); await p.screenshot({ path: `${out}/1-upgrade.png` }); }
    else if (st.hand.includes('re')) { mark('handRestore' + st.rest); await p.waitForTimeout(2000); await p.click('#re'); mark('restore' + (st.rest + 1)); await p.waitForTimeout(400); await p.screenshot({ path: `${out}/2-restore${st.rest + 1}.png` }); }
    if (st.combos.includes('shatter') && !shotSeen) { shotSeen = true; mark('firstShatter'); await p.screenshot({ path: `${out}/3-shatter.png` }); }
    log.maxWords = Math.max(log.maxWords || 0, st.words);
    await p.waitForTimeout(250);
  }
  await p.screenshot({ path: `${out}/4-end.png` });
  log.final = await p.evaluate(() => { const { P } = Hearthgate.state(); return { wave: P.wave, parts: P.parts, gate: P.gateLevel, restored: P.restored, combos: P.combos }; });
  log.errors = errors; console.log(JSON.stringify(log, null, 1)); await b.close();
})();
