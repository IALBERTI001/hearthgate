// the save reset is a testing tool for the owner's test page only (owner, Nov 12 2026: "final deployment will not have one")
const fs = require('fs'), path = require('path'), { execSync } = require('child_process');
const fail = [];
const idx = fs.readFileSync(path.join(__dirname, '../index.html'), 'utf8');
if (/HG_TESTING/.test(idx)) fail.push('index.html turns on the test tools');
for (const z of ['hearthgate-crazygames.zip', 'hearthgate-freeplay.zip']) {
  const p = path.join(__dirname, '../dist', z); if (!fs.existsSync(p)) continue;
  const list = execSync(`unzip -l "${p}"`).toString(); if (/artifact\.html/.test(list)) fail.push(z + ' ships the test page');
  const html = execSync(`unzip -p "${p}" index.html`).toString(); if (/HG_TESTING/.test(html)) fail.push(z + ' turns on the test tools');
}
if (fail.length) { console.error('test tools FAIL: ' + fail.join('; ')); process.exit(1); }
console.log('no test tools in portal builds');
