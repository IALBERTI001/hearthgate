// Every screen of a mid-game city in the desktop landscape layout at the reviewer's frame (824x464, devicePixelRatio 1):
// a screenshot of each, the smallest visible text on each, and anything that leaves the stage. Usage: node wide-tour.cjs <out> [w h]
const { chromium } = require('playwright');
const KINDS = [['perks'], ['quests'], ['shop'], ['daily', { day: 3, big: false, amount: 1234 }], ['offline', { secs: 9000, res: 5e5, fight: 4e5, city: 1e5 }],
  ['ascend'], ['building', 'forge'], ['settings'], ['pick', ['watchtower', 'granary']], ['pick', ['library', 'tavern']], ['fork', 2]];
(async () => { const [out, w = 824, h = 464] = process.argv.slice(2);
  const b = await chromium.launch(); const ctx = await b.newContext({ viewport: { width: +w, height: +h }, deviceScaleFactor: 1 });
  await ctx.addInitScript(() => { if (!sessionStorage.s) { sessionStorage.s = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify({ firstSession: false, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0, 10), streak: 1 }, restored: ['forge', 'frostSpire', 'stormBell', 'bakery', 'market'], combos: ['shatter'], res: 1e5, gateLevel: 20, wave: 30, bestWave: 30, ascensions: 4, perkPts: 6, shop: { open: true, shards: 50 } })); } });
  const p = await ctx.newPage(); const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.goto('http://localhost:8765/index.html?qa'); await p.waitForFunction(() => !document.getElementById('boot')); await p.waitForTimeout(1500);
  const small = () => p.evaluate(() => { let m = [99, '']; const st = document.getElementById('stage').getBoundingClientRect(), off = [];
    for (const e of document.querySelectorAll('#stage *')) { if (!e.offsetParent) continue; const r = e.getBoundingClientRect(); if (!r.width) continue;
      const t = [...e.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent.trim()).join(' '); const cs = getComputedStyle(e);
      if (cs.visibility === 'hidden' || +cs.opacity === 0 || e.closest('#guide:not(.in)')) continue;
      if (e.closest('#card') && !document.getElementById('card').classList.contains('on')) continue;
      if (t) { const px = parseFloat(cs.fontSize) * r.height / (e.offsetHeight || 1); if (px < m[0]) m = [+px.toFixed(1), t.slice(0, 30)]; }
      if (e.matches('button') && (r.left < st.left - 1 || r.right > st.right + 1 || r.bottom > st.bottom + 1 || r.top < st.top - 1) && !e.closest('#card .body')) off.push(e.id || e.className); }
    return { m, off }; });
  const rows = []; let r = await small(); await p.screenshot({ path: `${out}/wt-hud.png` }); rows.push(['hud', r.m, r.off]);
  for (const [kind, data] of KINDS) {
    await p.evaluate(([kind, data]) => Hearthgate.act('card', { kind, data }), [kind, data]); await p.waitForTimeout(450);
    r = await small(); await p.screenshot({ path: `${out}/wt-${kind}.png` });
    // a card that is not a long list fits whole: no scrolling, nothing clipped (owner, Nov 13 2026: "text clipping ... no reason to make this so big that the text does not fit into one menu")
    const over = await p.evaluate(() => { const b = document.querySelector('#card .body'); return b ? b.scrollHeight - b.clientHeight : 0; });
    const wide = await p.evaluate(() => [...document.querySelectorAll('#card .body *')].filter(e => e.offsetParent && e.scrollWidth > e.clientWidth + 2 && getComputedStyle(e).overflowX !== 'visible').length
      + [...document.querySelectorAll('#card .body > *')].filter(e => e.offsetParent && e.getBoundingClientRect().right > document.querySelector('#card .body').getBoundingClientRect().right + 1).length);
    if (wide) r.off.push(`${wide} part(s) wider than the card`);
    if (over > 2 && !['shop', 'quests', 'perks'].includes(kind)) r.off.push(`card scrolls by ${over}px`);
    rows.push([kind, r.m, r.off]);
    await p.evaluate(() => { const s = Hearthgate.state().S; s.card = null; document.getElementById('card').className = ''; });
  }
  let bad = 0; for (const [k, m, off] of rows) { const ok = m[0] >= 10 && !off.length; if (!ok) bad++; console.log(`${ok ? 'PASS' : 'FAIL'}  ${k}: smallest ${m[0]} px (${m[1]})${off.length ? ' off-stage: ' + off.join(',') : ''}`); }
  if (errs.length) { bad++; console.log('FAIL page errors: ' + errs.join(' | ')); }
  await b.close(); process.exit(bad ? 1 : 0);
})();
