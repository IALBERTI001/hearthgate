// The pass gate at the CrazyGames reviewer's frame (Nov 13 2026, after the "overall quality" rejection). A brand-new player
// at 824x464 (the desktop embed) and devicePixelRatio 1 plays 75 s, tapping the pointed button about every 2 s. Measured
// against what accepted games did in the same frame: frame use, smallest text (page and canvas), size of enemies and
// towers, time the fight is held, caption words in the first 60 s, Skip on screen. Usage: node reviewer-frame.cjs <url> [out]
const { chromium } = require('playwright');
const BAR = { frame: 0.75,   // owner, Nov 13 2026: the whole working area in view beats filling the frame (was 0.8)
 text: 10, button: 14, subject: 36, held: 3, words: 15, skipBy: 3 };
(async () => { const [url, out] = process.argv.slice(2), W = 824, H = 464;
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
  const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.addInitScript(() => {   // every canvas text drawn on the field: its size on screen
    window.__ct = []; const f = CanvasRenderingContext2D.prototype.fillText;
    CanvasRenderingContext2D.prototype.fillText = function (t, x, y) {
      if (this.canvas.id === 'field' && this.globalAlpha > 0.5 && String(t).trim()) { const m = /(\d+(?:\.\d+)?)px/.exec(this.font), r = this.canvas.getBoundingClientRect();
        if (m) window.__ct.push([String(t).slice(0, 30), +m[1] * this.getTransform().d * r.height / this.canvas.height]); }
      return f.apply(this, arguments); };
  });
  const t0 = Date.now(), T = () => (Date.now() - t0) / 1000;
  await p.goto(url); await p.waitForFunction(() => !document.getElementById('boot'), null, { timeout: 20000 });
  let offView = 0, held = 0, lastT = null, words = new Set(), skipAt = null, minDom = [99, ''], minBtn = [99, ''], subj = 99, tower = 0, shot = 3;
  while (T() < 75) {
    const st = await p.evaluate(() => {
      const g = document.getElementById('guide'), cv = document.getElementById('field'), r = cv.getBoundingClientRect(), s = Hearthgate.state();
      const css = r.width / 720;   // screen px per world unit
      const txt = []; for (const e of document.querySelectorAll('#stage *')) { if (!e.offsetParent) continue; const t = [...e.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent.trim()).join(' ');
        if (!t) continue; const cs = getComputedStyle(e); if (cs.visibility === 'hidden' || +cs.opacity === 0) continue; const k = e.getBoundingClientRect().height / (e.offsetHeight || 1);
        txt.push([t.slice(0, 30), +(parseFloat(cs.fontSize) * k).toFixed(1), !!e.closest('button')]); }
      const en = s.S.enemies.filter(e => !e.boss && !e.dying).map(e => e.size * css), top = Hearthgate.view().top;
      const hidden = s.S.enemies.filter(e => !e.dying && e.y - e.size * 0.5 < top).length;
      return { t: s.S.t, gin: !!g && g.classList.contains('in'), cap: g ? g.querySelector('p').textContent : '', skip: !!(g && g.classList.contains('in') && g.querySelector('.gskip')),
        txt, hidden, en: en.length ? Math.min(...en) : null, tower: 82 * css, hand: document.getElementById('hand').className };
    });
    if (lastT !== null && st.t - lastT < 0.05) held += 0.25; lastT = st.t;
    if (st.gin && T() < 60) st.cap.split(/\s+/).filter(Boolean).forEach(w => words.add(st.cap + '|' + w));
    if (st.skip && skipAt === null) skipAt = T();
    for (const [t, px, btn] of st.txt) { if (px < minDom[0]) minDom = [px, t]; if (btn && px < minBtn[0]) minBtn = [px, t]; }
    if (st.hidden) offView += 0.25;
    if (st.en) subj = Math.min(subj, st.en); tower = st.tower;
    if (out && T() >= shot) { await p.screenshot({ path: `${out}/rf-${String(Math.round(T())).padStart(2, '0')}.png` }); shot += 15; }
    if (/\bup\b/.test(st.hand)) await p.click('#up').catch(() => {}); else if (/\bre\b/.test(st.hand)) await p.click('#re').catch(() => {});
    await p.waitForTimeout(250);
  }
  const ct = await p.evaluate(() => window.__ct); let minCv = [99, '']; for (const [t, px] of ct) if (px < minCv[0]) minCv = [+px.toFixed(1), t];
  const fr = await p.evaluate(() => { const r = document.getElementById('stage').getBoundingClientRect(); return Math.min(r.width, innerWidth) * Math.min(r.height, innerHeight) / (innerWidth * innerHeight); });
  const capWords = new Set([...words].map(w => w.split('|')[0])); const nWords = [...capWords].reduce((n, c) => n + c.split(/\s+/).filter(Boolean).length, 0);
  const rows = [
    ['Frame use', +fr.toFixed(2), fr >= BAR.frame],
    ['Smallest page text px', minDom[0] + ` (${minDom[1]})`, minDom[0] >= BAR.text],
    ['Smallest button text px', minBtn[0] + ` (${minBtn[1]})`, minBtn[0] >= BAR.button],
    ['Smallest canvas text px', minCv[0] + ` (${minCv[1]})`, minCv[0] >= BAR.text],
    ['Smallest enemy px', +subj.toFixed(1), subj >= BAR.subject],
    ['Tower px (tier 1)', +tower.toFixed(1), tower >= BAR.subject],
    ['Fight held s (first 75 s)', held, held <= BAR.held],
    ['Monster bodies above the view, s', offView, offView <= 0.5],
    ['Caption words (first 60 s)', nWords, nWords <= BAR.words],
    ['Skip on screen by s', skipAt === null ? 'never' : +skipAt.toFixed(1), skipAt !== null && skipAt <= BAR.skipBy],
    ['Page errors', errs.length, errs.length === 0]];
  rows.forEach(r => console.log(`${r[2] ? 'PASS' : 'FAIL'}  ${r[0]}: ${r[1]}`));
  await b.close(); process.exit(rows.every(r => r[2]) ? 0 : 1);
})();
