// Story capture (Nov 12 2026): the real first minute of a brand-new player, the Magister's lines and all, stepped by hand
// (tick.cjs) so frames are exactly 1/30 s apart. The voice is laid in later by the editor: each line's start frame is logged
// and the typing is paced to the length of its recording, as in the game. Usage: node capture-story.cjs <url?qa> <outdir>
// Writes frame-00000.jpg... and events.json ({frame, kind, name}). A reader takes ~0.7 s after a line finishes, then taps.
const { chromium } = require('playwright');
const { SHIM, tick } = require('./tick.cjs');
const DUR = require('./voice-durations.json');
const [url, out] = process.argv.slice(2), FPS = 30, MAXF = +(process.env.MAXF || 2400);
(async () => {
  const b = await chromium.launch(); const ctx = await b.newContext({ viewport: { width: 450, height: 800 }, deviceScaleFactor: 2 });
  await ctx.addInitScript(() => { window.__storeCapture = true; });
  const p = await ctx.newPage(); await p.addInitScript(SHIM);
  await p.addInitScript(DUR => { const t = setInterval(() => { if (!window.HGAudio) return; clearInterval(t);
    Object.assign(window.HGAudio, { voiceDur: n => DUR[n] || 0, voice: () => [], voiceOn: () => false }); }, 1); }, DUR);
  await p.goto(url);
  for (let i = 0; i < 60 && await p.evaluate(() => !!document.getElementById('boot')); i++) { await p.waitForTimeout(60); await tick(p, 100); }
  await p.addStyleTag({ content: '#toast{display:none!important}' });
  const ev = [], log = (kind, name) => { ev.push({ frame: n, kind, name }); console.log(n, kind, name); };
  let n = 0, lastText = '', doneAt = -1, plan = null, end = -1;
  while (n < MAXF && (end < 0 || n < end)) {
    const st = await p.evaluate(() => { const g = document.getElementById('guide'), H = Hearthgate.state();
      return { in: !!g && g.classList.contains('in'), wait: !!g && g.classList.contains('wait'), press: !!g && g.classList.contains('press'), text: g ? g.querySelector('p').textContent : '',
        line: H && Hearthgate.state().S && (window.__gl || ''), hand: document.getElementById('hand').className, rest: H.P.restored.length, upReady: document.getElementById('up').classList.contains('ready'), reReady: document.getElementById('re').classList.contains('ready') }; });
    if (st.in && st.text && !lastText) log('line', st.text);
    else if (st.in && st.text.length < lastText.length) log('line', st.text);
    lastText = st.in ? st.text : '';
    if (st.wait && doneAt < 0) doneAt = n;
    if (!st.wait) doneAt = -1;
    if (st.wait && !st.press && n - doneAt >= 21) { await p.click('#guide'); log('tap', ''); doneAt = -1; }
    else if (st.wait && st.press && n - doneAt >= 15) {
      const w = st.hand.includes('up') ? 'up' : st.hand.includes('re') ? 're' : null;
      if (w && (w === 'up' ? st.upReady : st.reReady)) { await p.click('#' + w); log('press', w); doneAt = -1; if (w === 're' && st.rest === 0) end = n + FPS * 12; }
    }
    await tick(p, 1000 / FPS);
    await p.screenshot({ path: `${out}/frame-${String(n++).padStart(5, '0')}.jpg`, type: 'jpeg', quality: 92 });
  }
  require('fs').writeFileSync(`${out}/events.json`, JSON.stringify(ev, null, 1)); console.log('frames', n); await b.close();
})();
