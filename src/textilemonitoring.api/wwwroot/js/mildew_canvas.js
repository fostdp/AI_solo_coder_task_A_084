
(function(global) {
    'use strict';

    const MildewCanvas = {};

    const MOLD_COLORS = [
        { r: 107, g: 142, b: 35 },
        { r: 85,  g: 107, b: 47 },
        { r: 128, g: 128, b: 0  },
        { r: 143, g: 188, b: 143 },
        { r: 46,  g: 74,  b: 46 }
    ];

    const offscreenCache = new Map();

    function seededRandom(seed) {
        let s = seed % 2147483647;
        if (s <= 0) s += 2147483646;
        return function() {
            s = (s * 16807) % 2147483647;
            return (s - 1) / 2147483646;
        };
    }

    function rgba(c, a) {
        return `rgba(${c.r},${c.g},${c.b},${a})`;
    }

    function hexToRgba(hex, a) {
        const h = hex.replace('#', '');
        const bigint = parseInt(h.length === 3 ? h.split('').map(c => c + c).join('') : h, 16);
        const r = (bigint >> 16) & 255;
        const g = (bigint >> 8) & 255;
        const b = bigint & 255;
        return `rgba(${r},${g},${b},${a})`;
    }

    function getCacheKey(id, radius, alpha) {
        return `${id}_${Math.round(radius)}_${(alpha * 100).toFixed(0)}`;
    }

    function getOrCreateOffscreen(key, radius, seed, alpha) {
        if (offscreenCache.has(key)) {
            return offscreenCache.get(key);
        }

        const size = Math.ceil(radius * 2.4);
        const off = document.createElement('canvas');
        off.width = size;
        off.height = size;
        const octx = off.getContext('2d');

        const rand = seededRandom(seed * 31 + 7);
        const cx = size / 2;
        const cy = size / 2;
        const baseR = radius * 0.9;

        const layerConfigs = [
            { rScale: 0.55, a: 0.12, colorIdx: 0 },
            { rScale: 0.70, a: 0.18, colorIdx: 1 },
            { rScale: 0.85, a: 0.14, colorIdx: 2 },
            { rScale: 0.95, a: 0.08, colorIdx: 3 }
        ];

        for (let l = 0; l < layerConfigs.length; l++) {
            const cfg = layerConfigs[l];
            const lr = baseR * cfg.rScale;
            const col = MOLD_COLORS[cfg.colorIdx % MOLD_COLORS.length];

            const blobs = 6 + Math.floor(rand() * 4);
            for (let b = 0; b < blobs; b++) {
                const ang = rand() * Math.PI * 2;
                const dist = rand() * lr * 0.7;
                const bx = cx + Math.cos(ang) * dist;
                const by = cy + Math.sin(ang) * dist;
                const br = lr * (0.35 + rand() * 0.45);
                const strech = 0.7 + rand() * 0.6;
                const rot = rand() * Math.PI;

                octx.save();
                octx.translate(bx, by);
                octx.rotate(rot);
                octx.scale(strech, 1 / strech);

                const grd = octx.createRadialGradient(0, 0, 0, 0, 0, br);
                grd.addColorStop(0, rgba(col, alpha * cfg.a * 1.5));
                grd.addColorStop(0.5, rgba(col, alpha * cfg.a));
                grd.addColorStop(1, rgba(col, 0));

                octx.fillStyle = grd;
                octx.beginPath();
                octx.arc(0, 0, br, 0, Math.PI * 2);
                octx.fill();
                octx.restore();
            }
        }

        const innerGrd = octx.createRadialGradient(cx, cy, 0, cx, cy, baseR);
        innerGrd.addColorStop(0, rgba(MOLD_COLORS[0], alpha * 0.06));
        innerGrd.addColorStop(0.6, rgba(MOLD_COLORS[1], alpha * 0.08));
        innerGrd.addColorStop(1, rgba(MOLD_COLORS[4], 0));

        octx.fillStyle = innerGrd;
        octx.beginPath();
        octx.arc(cx, cy, baseR, 0, Math.PI * 2);
        octx.fill();

        const dots = 8 + Math.floor(rand() * 8);
        const dotCol = MOLD_COLORS[4];
        for (let d = 0; d < dots; d++) {
            const ang = rand() * Math.PI * 2;
            const dist = rand() * baseR * 0.85;
            const dx = cx + Math.cos(ang) * dist;
            const dy = cy + Math.sin(ang) * dist;
            const dr = 0.8 + rand() * 2.2;

            octx.beginPath();
            octx.arc(dx, dy, dr, 0, Math.PI * 2);
            octx.fillStyle = rgba(dotCol, alpha * 0.7);
            octx.fill();
        }

        if (offscreenCache.size > 50) {
            const firstKey = offscreenCache.keys().next().value;
            offscreenCache.delete(firstKey);
        }
        offscreenCache.set(key, off);

        return off;
    }

    MildewCanvas.drawMoldCloud = function(ctx, cx, cy, r, seed, alpha) {
        if (r <= 0 || alpha <= 0) return;

        const key = getCacheKey(seed, r, alpha);
        const off = getOrCreateOffscreen(key, r, seed, alpha);

        const offsetX = cx - off.width / 2;
        const offsetY = cy - off.height / 2;

        ctx.save();
        ctx.globalAlpha = 1;
        ctx.drawImage(off, offsetX, offsetY);
        ctx.restore();

        if (alpha >= 0.5) {
            ctx.save();
            ctx.setLineDash([4, 3]);
            ctx.strokeStyle = hexToRgba('#556B2F', alpha * 0.5);
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.arc(cx, cy, r * 0.92, 0, Math.PI * 2);
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.restore();
        }
    };

    MildewCanvas.clearCache = function() {
        offscreenCache.clear();
    };

    const _resizeTimers = new Map();

    MildewCanvas.scheduleRedraw = function(canvasId, drawFn, delay = 120) {
        if (_resizeTimers.has(canvasId)) {
            clearTimeout(_resizeTimers.get(canvasId));
        }
        _resizeTimers.set(canvasId, setTimeout(() => {
            drawFn();
            _resizeTimers.delete(canvasId);
        }, delay));
    };

    MildewCanvas.drawMiniPattern = function(canvasId, id, data) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const W = canvas.width = canvas.offsetWidth || 280;
        const H = canvas.height = canvas.offsetHeight || 180;
        const seed = (id || 1) * 1000 + ((data?.id) || 1);
        const rand = seededRandom(seed);

        const baseColors = [
            ['#8B2500', '#CD5C5C', '#F4A460'],
            ['#1E3A5F', '#4682B4', '#B0C4DE'],
            ['#2E4A2E', '#6B8E23', '#BDB76B'],
            ['#4A235A', '#9370DB', '#DDA0DD'],
            ['#5C4033', '#A0522D', '#DEB887']
        ];
        const cols = baseColors[Math.floor(rand() * baseColors.length)];

        const bg = ctx.createLinearGradient(0, 0, W, H);
        bg.addColorStop(0, cols[0]);
        bg.addColorStop(0.5, cols[1]);
        bg.addColorStop(1, cols[2]);
        ctx.fillStyle = bg;
        ctx.fillRect(0, 0, W, H);

        ctx.globalAlpha = 0.12;
        ctx.strokeStyle = '#FFF8DC';
        ctx.lineWidth = 0.5;
        ctx.beginPath();
        for (let i = 0; i < W; i += 6) {
            ctx.moveTo(i, 0); ctx.lineTo(i, H);
        }
        for (let j = 0; j < H; j += 6) {
            ctx.moveTo(0, j); ctx.lineTo(W, j);
        }
        ctx.stroke();
        ctx.globalAlpha = 1;

        ctx.globalAlpha = 0.2;
        for (let k = 0; k < 8; k++) {
            const bx = rand() * W, by = rand() * H, br = 12 + rand() * 22;
            const grd = ctx.createRadialGradient(bx, by, 0, bx, by, br);
            grd.addColorStop(0, 'rgba(255,248,220,0.5)');
            grd.addColorStop(1, 'rgba(255,248,220,0)');
            ctx.fillStyle = grd;
            ctx.beginPath();
            ctx.arc(bx, by, br, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.globalAlpha = 1;

        const holes = data?.holeMarkers || [];
        const holeColors = { 1: '#FFB74D', 2: '#F57C00', 3: '#E65100', 4: '#C62828' };
        holes.slice(0, 8).forEach(h => {
            const hx = (h.positionX / 100 || rand()) * W;
            const hy = (h.positionY / 100 || rand()) * H;
            const hr = Math.max(2, (h.radiusMm || h.radius || 3) * W / 600);
            const sev = h.severity || h.severityLevel || 1;
            const col = holeColors[sev] || holeColors[2];

            ctx.beginPath();
            ctx.arc(hx, hy, hr, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(0,0,0,0.7)';
            ctx.fill();
            ctx.strokeStyle = col;
            ctx.lineWidth = 1.5;
            ctx.stroke();
        });

        const molds = data?.moldRegions || [];
        molds.slice(0, 4).forEach(m => {
            const mx = (m.centerX / 100 || m.relativeX || 0.2 + rand() * 0.6) * W;
            const my = (m.centerY / 100 || m.relativeY || 0.2 + rand() * 0.6) * H;
            const mr = Math.max(8, (m.radiusMm || m.radius || 20) * W / 500);
            MildewCanvas.drawMoldCloud(ctx, mx, my, mr, seed + (m.id || 1), 0.5);
        });

        const risk = data?.synergyRisk || 0;
        if (risk > 0) {
            const lvl = risk >= 75 ? { color: '#C62828' } :
                       risk >= 50 ? { color: '#E65100' } :
                       risk >= 25 ? { color: '#F57C00' } :
                                    { color: '#2E7D32' };
            ctx.fillStyle = lvl.color;
            ctx.globalAlpha = 0.15;
            ctx.fillRect(0, 0, W, 4);
            ctx.fillRect(0, H - 4, W, 4);
            ctx.globalAlpha = 1;
        }
    };

    MildewCanvas.drawFullTextile = function(textile, showHoles, showMold, canvasScaleRef) {
        const canvas = document.getElementById('textileCanvas');
        const overlay = document.getElementById('overlayCanvas');
        if (!canvas) return;
        const W = 900, H = 600;
        canvas.width = W; canvas.height = H;
        overlay.width = W; overlay.height = H;
        const ctx = canvas.getContext('2d');
        const octx = overlay.getContext('2d');

        const id = textile?.id || 1;
        const seed = id * 1000;
        const rand = seededRandom(seed);

        const dynasties = {
            '明': ['#8B2500', '#CD5C5C', '#F4A460', '#DAA520'],
            '清': ['#4A235A', '#9370DB', '#DDA0DD', '#DAA520']
        };
        const d = textile?.dynasty || '明';
        const cols = dynasties[d] || dynasties['明'];

        const bg = ctx.createLinearGradient(0, 0, W, H);
        bg.addColorStop(0, cols[0]);
        bg.addColorStop(0.33, cols[1]);
        bg.addColorStop(0.66, cols[2]);
        bg.addColorStop(1, cols[0]);
        ctx.fillStyle = bg;
        ctx.fillRect(0, 0, W, H);

        ctx.globalAlpha = 0.1;
        ctx.strokeStyle = '#FFF8DC';
        ctx.lineWidth = 0.5;
        ctx.beginPath();
        for (let i = 0; i < W; i += 6) { ctx.moveTo(i, 0); ctx.lineTo(i, H); }
        for (let j = 0; j < H; j += 6) { ctx.moveTo(0, j); ctx.lineTo(W, j); }
        ctx.stroke();
        ctx.globalAlpha = 1;

        const motifCount = 5 + Math.floor(rand() * 4);
        for (let m = 0; m < motifCount; m++) {
            const mx = 60 + rand() * (W - 120);
            const my = 60 + rand() * (H - 120);
            const mr = 28 + rand() * 45;
            drawMotifFast(ctx, mx, my, mr, cols[3], seed + m);
        }

        ctx.strokeStyle = hexToRgba(cols[3], 0.5);
        ctx.lineWidth = 3;
        ctx.strokeRect(10, 10, W - 20, H - 20);
        ctx.lineWidth = 1;
        ctx.strokeRect(20, 20, W - 40, H - 40);

        octx.clearRect(0, 0, W, H);

        if (showMold !== false) {
            const molds = textile?.moldRegions || generateMockMolds(id);
            molds.forEach((m, i) => {
                const mx = (m.centerX / 100 || m.relativeX || 0.2 + rand() * 0.6) * W;
                const my = (m.centerY / 100 || m.relativeY || 0.2 + rand() * 0.6) * H;
                const mr = Math.max(20, (m.radiusMm || m.radius || 40));
                MildewCanvas.drawMoldCloud(octx, mx, my, mr, seed + (m.id || i + 1), 0.85);
            });
        }

        if (showHoles !== false) {
            const holes = textile?.holeMarkers || generateMockHoles(id);
            holes.forEach((h, i) => {
                const hx = (h.positionX / 100 || h.relativeX || rand()) * W;
                const hy = (h.positionY / 100 || h.relativeY || rand()) * H;
                const hr = Math.max(3, (h.radiusMm || h.radius || 6));
                const sev = h.severity || h.severityLevel || (Math.floor(rand() * 4) + 1);
                drawHoleFast(octx, hx, hy, hr, sev);
            });
        }

        applyScale(canvasScaleRef);
    };

    function drawMotifFast(ctx, cx, cy, r, col, seed) {
        const rand = seededRandom(seed);
        const motifType = Math.floor(rand() * 4);

        ctx.save();
        ctx.translate(cx, cy);
        ctx.globalAlpha = 0.32;
        ctx.fillStyle = '#FFF8DC';
        ctx.strokeStyle = col;
        ctx.lineWidth = 1.5;

        if (motifType === 0) {
            const petals = 8;
            for (let p = 0; p < petals; p++) {
                ctx.rotate(Math.PI * 2 / petals);
                ctx.beginPath();
                ctx.ellipse(0, -r * 0.58, r * 0.24, r * 0.48, 0, 0, Math.PI * 2);
                ctx.fill(); ctx.stroke();
            }
            ctx.beginPath();
            ctx.arc(0, 0, r * 0.24, 0, Math.PI * 2);
            ctx.fillStyle = col; ctx.fill(); ctx.stroke();
        } else if (motifType === 1) {
            ctx.beginPath();
            for (let i = 0; i < 36; i++) {
                const a = (i / 36) * Math.PI * 2;
                const rr = i % 2 === 0 ? r : r * 0.48;
                const px = Math.cos(a) * rr, py = Math.sin(a) * rr;
                if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
            }
            ctx.closePath(); ctx.fill(); ctx.stroke();
        } else if (motifType === 2) {
            for (let d = 0; d < 3; d++) {
                ctx.beginPath();
                const rr = r * (1 - d * 0.3);
                ctx.arc(0, 0, rr, 0, Math.PI * 2);
                ctx.globalAlpha = 0.12 + d * 0.1;
                ctx.fill();
                ctx.globalAlpha = 0.38;
                ctx.stroke();
            }
        } else {
            const pts = 5;
            ctx.beginPath();
            for (let i = 0; i < pts * 2; i++) {
                const a = (i / (pts * 2)) * Math.PI * 2 - Math.PI / 2;
                const rr = i % 2 === 0 ? r : r * 0.44;
                const px = Math.cos(a) * rr, py = Math.sin(a) * rr;
                if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
            }
            ctx.closePath(); ctx.fill(); ctx.stroke();
        }
        ctx.restore();
    }

    function drawHoleFast(ctx, cx, cy, r, sev) {
        const cols = { 1: '#FFB74D', 2: '#F57C00', 3: '#E65100', 4: '#C62828' };
        const col = cols[sev] || cols[2];
        const lineW = sev >= 3 ? 2.5 : 1.5;

        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        const holeGrd = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
        holeGrd.addColorStop(0, 'rgba(0,0,0,0.9)');
        holeGrd.addColorStop(0.6, 'rgba(20,10,5,0.85)');
        holeGrd.addColorStop(1, 'rgba(50,30,15,0.7)');
        ctx.fillStyle = holeGrd;
        ctx.fill();

        ctx.beginPath();
        ctx.arc(cx, cy, r + 1, 0, Math.PI * 2);
        ctx.strokeStyle = col;
        ctx.lineWidth = lineW;
        ctx.stroke();

        if (sev >= 3) {
            ctx.beginPath();
            ctx.arc(cx, cy, r + 4, 0, Math.PI * 2);
            ctx.strokeStyle = hexToRgba(col, 0.4);
            ctx.lineWidth = 1;
            ctx.setLineDash([3, 2]);
            ctx.stroke();
            ctx.setLineDash([]);
        }

        ctx.strokeStyle = hexToRgba(col, 0.8);
        ctx.lineWidth = 0.8;
        ctx.beginPath();
        ctx.moveTo(cx - r - 3, cy); ctx.lineTo(cx + r + 3, cy);
        ctx.moveTo(cx, cy - r - 3); ctx.lineTo(cx, cy + r + 3);
        ctx.stroke();
    }

    function generateMockHoles(id) {
        const rand = seededRandom(id * 17 + 3);
        const n = 2 + Math.floor(rand() * 6);
        const arr = [];
        for (let i = 0; i < n; i++) {
            arr.push({
                id: i + 1,
                positionX: 10 + rand() * 80,
                positionY: 10 + rand() * 80,
                radiusMm: 3 + rand() * 10,
                severity: 1 + Math.floor(rand() * 4)
            });
        }
        return arr;
    }

    function generateMockMolds(id) {
        const rand = seededRandom(id * 23 + 7);
        const n = 1 + Math.floor(rand() * 3);
        const arr = [];
        for (let i = 0; i < n; i++) {
            arr.push({
                id: i + 1,
                centerX: 15 + rand() * 70,
                centerY: 15 + rand() * 70,
                radiusMm: 25 + rand() * 50
            });
        }
        return arr;
    }

    function applyScale(scale) {
        const overlay = document.getElementById('overlayCanvas');
        const canvas = document.getElementById('textileCanvas');
        const scaleVal = `scale(${scale}) translateZ(0)`;
        [canvas, overlay].forEach(el => {
            if (el) el.style.transform = scaleVal;
        });
    }

    global.MildewCanvas = MildewCanvas;

})(typeof window !== 'undefined' ? window : this);
