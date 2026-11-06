// Hearthgate looks: the Shop's skins, all made in code from the shipped art (no new sheets).
// Tower and town skins are colour recipes applied once per image when equipped; trails, hits and wisps are drawn.
(function (root) {
  'use strict';

  // ---------- colour helpers ----------
  function rgb2hsl(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    const mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2; let h = 0, s = 0;
    if (mx !== mn) {
      const d = mx - mn; s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
      h = mx === r ? (g - b) / d + (g < b ? 6 : 0) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4; h *= 60;
    }
    return [h, s, l];
  }
  function hue2(p, q, t) { if (t < 0) t += 1; if (t > 1) t -= 1; return t < 1 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p; }
  function hsl2rgb(h, s, l) {
    h = ((h % 360) + 360) % 360 / 360; s = Math.max(0, Math.min(1, s)); l = Math.max(0, Math.min(1, l));
    if (!s) { const v = l * 255; return [v, v, v]; }
    const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
    return [hue2(p, q, h + 1 / 3) * 255, hue2(p, q, h) * 255, hue2(p, q, h - 1 / 3) * 255];
  }
  const near = (h, c, w) => Math.abs(((h - c + 540) % 360) - 180) < w;   // hue h within w degrees of c

  // Apply fn(h, s, l, y01) -> [h, s, l] (or null to keep the pixel) to every visible pixel; returns a canvas.
  function grade(im, fn, rgbFn) {
    const c = document.createElement('canvas'); c.width = im.width; c.height = im.height;
    const g = c.getContext('2d'); g.drawImage(im, 0, 0);
    if (!fn && !rgbFn) return c;
    let d; try { d = g.getImageData(0, 0, c.width, c.height); } catch (e) { return c; }   // a tainted image: show it as it is
    const a = d.data, W = c.width, H = c.height;
    for (let i = 0; i < a.length; i += 4) {
      if (a[i + 3] < 8) continue;
      const y01 = Math.floor(i / 4 / W) / H;
      if (rgbFn) { const o = rgbFn(a[i], a[i + 1], a[i + 2], y01); if (o) { a[i] = o[0]; a[i + 1] = o[1]; a[i + 2] = o[2]; } continue; }
      const [h, s, l] = rgb2hsl(a[i], a[i + 1], a[i + 2]), o = fn(h, s, l, y01);
      if (!o) continue; const [r, gg, b] = hsl2rgb(o[0], o[1], o[2]); a[i] = r; a[i + 1] = gg; a[i + 2] = b;
    }
    g.putImageData(d, 0, 0); return c;
  }

  // ---------- tower skins: stone, metal and the element's light, recoloured within the element's family ----------
  // glow: the crest light; each keeps the element readable (fire warm, frost cold, storm violet-white).
  const TOWER = {
    'fire-classic': { glow: 'rgba(255,150,60,' },
    // Obsidian: the copper body turns to black volcanic glass; only the brightest flame stays, as magma
    'fire-obsidian': { glow: 'rgba(255,90,40,', fn: (h, s, l) => (near(h, 35, 32) && l > 0.46 && s > 0.55) ? [h - 12, 1, Math.min(0.7, l * 1.1)] : near(h, 25, 50) && s > 0.2 ? [272, 0.2, l * 0.55 + 0.05] : s < 0.32 ? [265, 0.14, l * 0.6] : [8, s, l * 0.8] },
    'fire-ember': { glow: 'rgba(255,220,110,', fn: (h, s, l) => s < 0.32 ? [42, 0.45 + s * 0.4, Math.min(0.85, l * 1.12 + 0.04)] : near(h, 25, 40) ? [h + 14, s, l + (1 - l) * 0.3] : [38, s * 0.85, l * 1.05] },
    'frost-classic': { glow: 'rgba(160,230,255,' },
    'frost-night': { glow: 'rgba(90,200,255,', fn: (h, s, l) => s < 0.25 ? [228, 0.35 + s, l * 0.5] : near(h, 200, 45) ? [192, Math.min(1, s * 1.2), Math.min(0.85, l * 1.05)] : [228, s * 0.7, l * 0.6] },
    'frost-aurora': { glow: 'rgba(140,255,200,', fn: (h, s, l, y) => s < 0.25 ? [190, 0.18 + s * 0.5, l * 0.78] : near(h, 200, 50) ? [300 - y * 170, Math.min(1, s * 1.1 + 0.1), l] : [175, s, l] },
    'storm-classic': { glow: 'rgba(200,140,255,' },
    'storm-void': { glow: 'rgba(255,90,220,', fn: (h, s, l) => s < 0.28 ? [272, 0.3 + s * 0.5, l * 0.38] : near(h, 275, 50) ? [312, Math.min(1, s * 1.2), Math.min(0.8, l * 1.12)] : [290, s * 0.8, l * 0.6] },
    'storm-steel': { glow: 'rgba(120,230,255,', fn: (h, s, l) => s < 0.28 ? [212, 0.22, l * 0.78] : near(h, 275, 50) ? [192, Math.min(1, s * 0.9), Math.min(0.82, l * 1.15)] : [205, s * 0.5, l * 0.9] }
  };

  // ---------- town skins: the whole ground (meadow, forest, city, wall) graded together ----------
  const greenish = (h, s) => near(h, 105, 55) && s > 0.15;
  const TOWN = {
    'town-spring': { backdrop: '' },
    // a time of day, not a filter: warm light on the bright parts, cool violet in the shadows, a little less saturated
    'town-golden': { backdrop: 'sepia(.3) saturate(1.1)', rgb: (r, g, b) => { const w = (r + g + b) / 765, m = (r + g + b) / 3;
      r = r * 0.88 + m * 0.12; g = g * 0.88 + m * 0.12; b = b * 0.88 + m * 0.12;
      return [Math.min(255, r * (0.92 + 0.22 * w) + 10 * w), Math.min(255, g * (0.9 + 0.1 * w)), Math.min(255, b * (0.7 + 0.1 * w) + 26 * (1 - w))]; } },
    // greens turn to a spread of leaf colours, red-orange through gold, kept rich rather than olive
    'town-autumn': { backdrop: 'hue-rotate(-30deg) saturate(1.05)', fn: (h, s, l) => greenish(h, s) ? [20 + ((h * 37) % 34), Math.min(1, s * 0.9 + 0.12), l * 0.9] : null },
    'town-winter': { backdrop: 'saturate(.35) brightness(1.15)', fn: (h, s, l) => greenish(h, s) ? [205, 0.18 * s, 0.7 + l * 0.27] : [h, s * 0.7, Math.min(1, l * 1.04)] },
    'town-moon': { backdrop: 'brightness(.45) hue-rotate(185deg) saturate(.8)',
      // night: everything sinks into blue, except warm bright light (windows, forges, lanterns), which keeps glowing
      rgb: (r, g, b) => (r > 215 && g > 120 && b < 140 && r > g + 22) ? [r, g * 0.92, b * 0.75] : [r * 0.3 + 8, g * 0.36 + 14, b * 0.55 + 42] }   // warm light only: never the yellow-green meadow flowers
  };
  function townGrade(id, im) { const t = TOWN[id] || TOWN['town-spring']; return grade(im, t.fn, t.rgb); }
  function towerGrade(id, im) { const t = TOWER[id] || {}; return grade(im, t.fn); }

  // ---------- wisps ----------
  const WISP = {
    'wisp-rose': { col: '#ff8ae6', glow: 'rgba(255,110,220,', core: '#fff4fc', rim: '#a11d7f' },
    'wisp-gold': { col: '#ffd56b', glow: 'rgba(255,200,80,', core: '#fffbe8', rim: '#8a5a1b' },
    'wisp-star': { col: '#bfe4ff', glow: 'rgba(150,200,255,', core: '#ffffff', rim: '#2f5fa8' },
    'wisp-ruby': { col: '#ff4a5e', glow: 'rgba(255,60,80,', core: '#ffe8ea', rim: '#7a1020' }
  };

  // ---------- shot trails: ctx, the shot ({x, y, trail}), its colour and glow, a glow painter and the clock ----------
  function drawTrail(ctx, style, s, col, glowRGBA, glow, t) {
    const tr = s.trail, n = tr.length;
    const dir = n ? Math.atan2(s.y - tr[n - 1][1], s.x - tr[n - 1][0]) : -Math.PI / 2, nx = -Math.sin(dir), ny = Math.cos(dir);
    if (style === 'trail-comet') {
      const pts = tr.concat([[s.x, s.y]]);
      for (let i = 1; i < pts.length; i++) {
        ctx.globalAlpha = i / pts.length * 0.9; ctx.strokeStyle = col; ctx.lineCap = 'round'; ctx.lineWidth = 2 + i * 1.6;
        ctx.beginPath(); ctx.moveTo(pts[i - 1][0], pts[i - 1][1]); ctx.lineTo(pts[i][0], pts[i][1]); ctx.stroke();
      }
      ctx.globalAlpha = 0.9; ctx.strokeStyle = '#fff'; ctx.lineWidth = 3; ctx.beginPath();
      const m = pts[Math.max(0, pts.length - 4)]; ctx.moveTo(m[0], m[1]); ctx.lineTo(s.x, s.y); ctx.stroke();
    } else if (style === 'trail-sparkle') {
      tr.forEach(([x, y], i) => {
        const a = (i + 1) / n, tw = 0.5 + 0.5 * Math.sin(t * 30 + i * 2.1), r = (2 + i * 0.9) * (0.6 + tw * 0.6);
        ctx.globalAlpha = a * 0.9; ctx.fillStyle = i % 2 ? '#fff' : col;
        const ox = nx * Math.sin(i * 2.7) * 7, oy = ny * Math.sin(i * 2.7) * 7;
        star(ctx, x + ox, y + oy, r * 1.6, r * 0.45, 4);
      });
    } else if (style === 'trail-spiral') {
      tr.forEach(([x, y], i) => {
        const ph = i * 1.25 + t * 26, off = Math.sin(ph) * 8, a = (i + 1) / n;
        ctx.globalAlpha = a * 0.85; ctx.fillStyle = col; ctx.beginPath(); ctx.arc(x + nx * off, y + ny * off, 2.5 + i * 0.5, 0, 7); ctx.fill();
        ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(x - nx * off, y - ny * off, 2 + i * 0.4, 0, 7); ctx.fill();
      });
    } else if (style === 'trail-runes') {
      tr.forEach(([x, y], i) => { ctx.globalAlpha = (i + 1) / n * 0.5; ctx.fillStyle = col; ctx.beginPath(); ctx.arc(x, y, 2 + i * 0.5, 0, 7); ctx.fill(); });
      ctx.globalAlpha = 0.85; ctx.strokeStyle = col; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(s.x, s.y, 13, 0, 7); ctx.stroke();
      for (let k = 0; k < 3; k++) {
        const a = t * 7 + k * 2.094, rx = s.x + Math.cos(a) * 13, ry = s.y + Math.sin(a) * 13;
        ctx.save(); ctx.translate(rx, ry); ctx.rotate(a); ctx.fillStyle = '#fff'; ctx.strokeStyle = col; ctx.lineWidth = 1.5;
        ctx.fillRect(-3.5, -3.5, 7, 7); ctx.strokeRect(-3.5, -3.5, 7, 7); ctx.restore();
      }
    } else {
      tr.forEach(([x, y], i) => { ctx.globalAlpha = (i + 1) / n * 0.75; ctx.fillStyle = col; ctx.beginPath(); ctx.arc(x, y, 3 + i * 0.8, 0, 7); ctx.fill(); });
    }
    ctx.globalAlpha = 1; glow(s.x, s.y, 18, glowRGBA, 0.8, ctx);
    ctx.fillStyle = '#fff'; ctx.strokeStyle = 'rgba(42,26,34,.8)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(s.x, s.y, 5, 0, 7); ctx.fill(); ctx.stroke();
  }
  function star(ctx, x, y, R, r, pts, rot = 0) {
    ctx.beginPath();
    for (let i = 0; i < pts * 2; i++) { const a = rot + i * Math.PI / pts - Math.PI / 2, q = i % 2 ? r : R; ctx.lineTo(x + Math.cos(a) * q, y + Math.sin(a) * q); }
    ctx.closePath(); ctx.fill();
  }

  // ---------- hit effects: drawn from a small record {x, y, t, col, style, seed} that lives HIT_LIFE seconds ----------
  const HIT_LIFE = 0.32;
  function drawHit(ctx, h) {
    const u = h.t / HIT_LIFE, a = 1 - u;
    ctx.save(); ctx.globalAlpha = a;
    if (h.style === 'hit-star') {
      ctx.fillStyle = '#fff'; star(ctx, h.x, h.y, 10 + u * 26, 3 + u * 4, 4, h.seed);
      ctx.globalAlpha = a * 0.8; ctx.fillStyle = h.col; star(ctx, h.x, h.y, 8 + u * 20, 3, 4, h.seed + Math.PI / 4);
    } else if (h.style === 'hit-ring') {
      ctx.strokeStyle = h.col; ctx.lineWidth = 5 * a + 1.5; ctx.beginPath(); ctx.arc(h.x, h.y, 6 + u * 34, 0, 7); ctx.stroke();
      ctx.strokeStyle = '#fff'; ctx.lineWidth = 2 * a; ctx.beginPath(); ctx.arc(h.x, h.y, 3 + u * 22, 0, 7); ctx.stroke();
    } else if (h.style === 'hit-shards') {
      for (let k = 0; k < 7; k++) {
        const ang = h.seed + k * 0.9, d = 6 + u * 30 * (0.7 + (k % 3) * 0.2);
        ctx.save(); ctx.translate(h.x + Math.cos(ang) * d, h.y + Math.sin(ang) * d - u * 10); ctx.rotate(ang + u * 4);
        ctx.fillStyle = k % 2 ? '#fff' : h.col; ctx.strokeStyle = 'rgba(42,26,34,.6)'; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(0, -6); ctx.lineTo(3, 0); ctx.lineTo(0, 6); ctx.lineTo(-3, 0); ctx.closePath(); ctx.fill(); ctx.stroke(); ctx.restore();
      }
    }
    ctx.restore();
  }

  root.HGLooks = { grade, TOWER, TOWN, WISP, townGrade, towerGrade, drawTrail, drawHit, HIT_LIFE, star };
})(typeof globalThis !== 'undefined' ? globalThis : this);
