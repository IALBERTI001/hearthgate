// CrazyGames review, Nov 13 2026: the guide held the fight 25.5 of the first 75 s with 71 words and no skip; accepted games
// pause for none. Pins: the fight runs while he talks, a caption is one short line, a line moves on by itself, Skip is
// there and silences him for good, and the Rebuild button names the building. Usage: node guide-hold.cjs <url?qa> [w h]
const { chromium } = require('playwright');
(async () => { const [url, w = 390, h = 844] = process.argv.slice(2);
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: +w, height: +h } });
  const fail = []; p.on('pageerror', e => fail.push('page error ' + e.message));
  await p.goto(url); await p.waitForFunction(() => !document.getElementById('boot'));
  await p.waitForFunction(() => { const g = document.getElementById('guide'); return g && g.classList.contains('in'); }, null, { timeout: 15000 });
  const read = () => p.evaluate(() => ({ t: Hearthgate.state().S.t, text: document.querySelector('#guide p').textContent, re: document.querySelector('#re .label').textContent, skip: !!document.querySelector('#guide .gskip') }));
  const a = await read(); await p.waitForTimeout(3000); const b2 = await read();
  if (!(b2.t > a.t + 2)) fail.push(`the fight stopped while he talked (${a.t} -> ${b2.t})`);
  if (a.text.split(/\s+/).length > 8) fail.push(`caption longer than 8 words: "${a.text}"`);
  if (!a.skip) fail.push('no Skip on his panel');
  if (!/^Rebuild \w/.test(a.re)) fail.push(`Rebuild button does not name the building: "${a.re}"`);
  await p.waitForTimeout(6000); const c = await read();
  if (c.text === a.text && !/Tap /.test(a.text)) fail.push('a line did not move on by itself');
  await p.click('#guide .gskip'); await p.waitForTimeout(400);
  const gone = await p.evaluate(() => !document.getElementById('guide').classList.contains('in') && Hearthgate.state().P.settings.noGuide === true);
  if (!gone) fail.push('Skip did not silence him');
  console.log(fail.length ? 'GUIDE FAIL: ' + fail.join('; ') : 'guide: fight never held, short captions, moves on by itself, Skip works, Rebuild names the building'); await b.close(); process.exit(fail.length ? 1 : 0);
})();
