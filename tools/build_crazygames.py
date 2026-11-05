"""Build the CrazyGames upload zip: index.html at the root, dev-only files stripped, size reported.
Usage: python3 tools/build_crazygames.py  ->  dist/hearthgate-crazygames.zip
"""
import os, zipfile, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INCLUDE = ['index.html', 'style.css', 'js', 'assets']
SKIP_EXT = {'.wav', '.psd', '.md', '.blend'}
SKIP_PARTS = ('candidate', 'reference', '.DS_Store')

def files():
    for top in INCLUDE:
        p = os.path.join(ROOT, top)
        if os.path.isfile(p):
            yield top
            continue
        for d, _, fs in os.walk(p):
            for f in fs:
                rel = os.path.relpath(os.path.join(d, f), ROOT)
                if os.path.splitext(f)[1].lower() in SKIP_EXT or any(s in rel for s in SKIP_PARTS):
                    continue
                yield rel

CG_SKIP = {'js/fp-bridge.js'}

def main():
    os.makedirs(os.path.join(ROOT, 'dist'), exist_ok=True)
    out = os.path.join(ROOT, 'dist', 'hearthgate-crazygames.zip')
    total, n = 0, 0
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        for rel in files():
            if rel.replace(os.sep, '/') in CG_SKIP:
                continue
            z.write(os.path.join(ROOT, rel), rel.replace(os.sep, '/'))
            total += os.path.getsize(os.path.join(ROOT, rel)); n += 1
    print(f'{out}: {n} files, {total / 1e6:.2f} MB unpacked, {os.path.getsize(out) / 1e6:.2f} MB zipped')
    if total > 20e6 or n > 1500:
        sys.exit('FAIL: over the 20 MB initial-download or 1500-file budget')

if __name__ == '__main__':
    main()
