// A trailing // comment placed before more code on the same line silently disables that code. It happened twice on
// Nov 5-6 2026 (the boot sequence, then the perks chip's visibility). Flag comment text that looks like code.
const fs = require('fs'), path = require('path');
const dir = path.join(__dirname, '..', 'js'), bad = [];
const CODE = /(\bel\(['"]|\b[SP]\.\w+\s*=[^=]|\bcx\.\w+\(|\)\s*;\s*[A-Za-z_$][\w$]*(\.[\w$]+)+\s*[=(])/;
for (const f of fs.readdirSync(dir).filter(f => f.endsWith('.js'))) {
  fs.readFileSync(path.join(dir, f), 'utf8').split('\n').forEach((line, i) => {
    const m = line.match(/(^|[^:'"`/\\])\/\/(?!\/)(.*)$/); if (m && CODE.test(m[2])) bad.push(`${f}:${i + 1}: ${m[2].trim().slice(0, 90)}`);
  });
}
if (bad.length) { console.error('code after a // comment:\n' + bad.join('\n')); process.exit(1); }
console.log('comments test passed');
