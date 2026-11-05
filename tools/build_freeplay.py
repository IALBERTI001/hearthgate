"""Build the freeplays.site upload: the CrazyGames build with the Freeplay SDK (/sdk/v1.js) and js/fp-bridge.js in place
of js/sdk.js, no ads. Usage: python3 tools/build_freeplay.py  ->  dist/hearthgate-freeplay.zip
"""
import os, re, zipfile, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_crazygames import ROOT, files

def main():
    os.makedirs(os.path.join(ROOT, 'dist'), exist_ok=True)
    out = os.path.join(ROOT, 'dist', 'hearthgate-freeplay.zip')
    total, n = 0, 0
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        for rel in files():
            rel = rel.replace(os.sep, '/')
            if rel == 'js/sdk.js':
                continue
            src = os.path.join(ROOT, rel)
            if rel == 'index.html':
                s = open(src, encoding='utf-8').read()
                s, k = re.subn(r'<script src="js/sdk\.js[^"]*"></script>', '<script src="/sdk/v1.js"></script><script src="js/fp-bridge.js?v=__V__"></script>', s)
                assert k == 1, k
                v = re.search(r'core\.js\?v=([^"]+)', s).group(1)
                s = s.replace('__V__', v).replace("script-src 'self' https:", "script-src 'self'")
                z.writestr(rel, s); total += len(s.encode()); n += 1
                continue
            z.write(src, rel); total += os.path.getsize(src); n += 1
    print(f'{out}: {n} files, {total / 1e6:.2f} MB unpacked, {os.path.getsize(out) / 1e6:.2f} MB zipped')
    if total > 50e6 or n > 1500:
        sys.exit('FAIL: over the Freeplay budget')

if __name__ == '__main__':
    main()
