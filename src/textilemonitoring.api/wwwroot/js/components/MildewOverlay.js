
(function(global) {
    'use strict';

    const MOLD_COLORS = [
        { r: 107, g: 142, b: 35 },
        { r: 85,  g: 107, b: 47 },
        { r: 128, g: 128, b: 0  },
        { r: 143, g: 188, b: 143 },
        { r: 46,  g: 74,  b: 46 }
    ];

    const HOLE_COLORS = {
        1: { stroke: '#FFB74D', fill: 'rgba(0,0,0,0.7)' },
        2: { stroke: '#F57C00', fill: 'rgba(0,0,0,0.75)' },
        3: { stroke: '#E65100', fill: 'rgba(0,0,0,0.85)' },
        4: { stroke: '#C62828', fill: 'rgba(0,0,0,0.9)' }
    };

    const _offscreenCache = new Map();
    const _resizeTimers = new Map();

    function seededRandom(seed) {
        let s = seed % 2147483647;
        if (s <= 0) s += 2147483646;
        return function() {
            s = (s * 16807) % 2147483647;
            return (s - 1) / 2147483646;
        };
    }

    function rgba(c, a) { return `rgba(${c.r},${c.g},${c.b},${a})`; }

    function hexToRgba(hex, a) {
        const h = hex.replace('#', '');
        const bigint = parseInt(h.length === 3 ? h.split('').map(c => c + c).join('') : h, 16);
        const r = (bigint >> 16) & 255;
        const g = (bigint >> 8) & 255;
        const b = bigint & 255;
        return `rgba(${r},${g},${b},${a})`;
    }

    function getCacheKey(seed, radius, alpha) {
        return `${seed}_${Math.round(radius)}_${(alpha * 100).toFixed(0)}`;
    }

    function getOrCreateMoldOffscreen(seed, radius, alpha) {
        const key = getCacheKey(seed, radius, alpha);
        if (_offscreenCache.has(key)) return _offscreenCache.get(key);

        const size = Math.ceil(radius * 2.4);
        const off = document.createElement('canvas');
        off.width = size; off.height = size;
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

        if (_offscreenCache.size > 50) {
            const firstKey = _offscreenCache.keys().next().value;
            _offscreenCache.delete(firstKey);
        }
        _offscreenCache.set(key, off);
        return off;
    }

    class MildewOverlay {
        constructor(canvasId, baseCanvas) {
            this.canvas = typeof canvasId === 'string'
                ? document.getElementById(canvasId)
                : canvasId;
            if (!this.canvas) throw new Error('Overlay canvas not found');
            this.baseCanvas = baseCanvas;
            this.ctx = this.canvas.getContext('2d');
            this._scale = 1.0;
            this._translation = { x: 0, y: 0 };
            this._data = { holes: [], molds: [], risk: 0 };
            this._seed = 1;
            this._dirty = true;
            this._rafId = null;
        }

        setSeed(seed) { this._seed = seed; this._dirty = true; }
        setScale(scale) { this._scale = scale; this._applyTransform(); }
        setTranslation(x, y) { this._translation = { x, y }; this._applyTransform(); }

        setData(data) {
            if (JSON.stringify(this._data) === JSON.stringify(data)) return;
            this._data = Object.assign({ holes: [], molds: [], risk: 0 }, data);
            this._dirty = true;
            this._scheduleRender();
        }

        addHole(hole) {
            this._data.holes.push(hole);
            this._dirty = true;
            this._scheduleRender();
        }

        addMold(mold) {
            this._data.molds.push(mold);
            this._dirty = true;
            this._scheduleRender();
        }

        clear() {
            this._data = { holes: [], molds: [], risk: 0 };
            this._dirty = true;
            this.render();
        }

        _scheduleRender() {
            if (this._rafId !== null) return;
            this._rafId = requestAnimationFrame(() => {
                this._rafId = null;
                if (this._dirty) this.render();
            });
        }

        render() {
            const W = this.canvas.width = this.baseCanvas?.width || this.canvas.offsetWidth || 900;
            const H = this.canvas.height = this.baseCanvas?.height || this.canvas.offsetHeight || 600;
            this.ctx.clearRect(0, 0, W, H);

            if (!this._dirty) { this._applyTransform(); return; }
            this._dirty = false;

            const { holes, molds, risk } = this._data;

            if (molds && molds.length > 0) {
                molds.forEach((m, i) => {
                    const mx = (m.relativeX / 100 || m.centerX / 100 || 0.2 + Math.random() * 0.6) * W;
                    const my = (m.relativeY / 100 || m.centerY / 100 || 0.2 + Math.random() * 0.6) * H;
                    const mr = Math.max(20, m.radiusMm || m.radius || 40);
                    this._drawMoldCloud(mx, my, mr, this._seed + (m.id || i + 1), 0.85);
                });
            }

            if (holes && holes.length > 0) {
                holes.forEach((h, i) => {
                    const hx = (h.relativeX / 100 || h.positionX / 100 || Math.random()) * W;
                    const hy = (h.relativeY / 100 || h.positionY / 100 || Math.random()) * H;
                    const hr = Math.max(3, h.radiusMm || h.radius || 6);
                    const sev = h.severityLevel || h.severity || (Math.floor(Math.random() * 4) + 1);
                    this._drawHoleMarker(hx, hy, hr, sev);
                });
            }

            if (risk && risk > 0) {
                this._drawRiskFrame(W, H, risk);
            }

            this._applyTransform();
            this._fireEvent('rendered', { holes: holes.length, molds: molds.length, risk });
        }

        _drawMoldCloud(cx, cy, r, seed, alpha) {
            if (r <= 0 || alpha <= 0) return;
            const off = getOrCreateMoldOffscreen(seed, r, alpha);
            const offsetX = cx - off.width / 2;
            const offsetY = cy - off.height / 2;

            this.ctx.save();
            this.ctx.globalAlpha = 1;
            this.ctx.drawImage(off, offsetX, offsetY);
            this.ctx.restore();

            if (alpha >= 0.5) {
                this.ctx.save();
                this.ctx.setLineDash([4, 3]);
                this.ctx.strokeStyle = hexToRgba('#556B2F', alpha * 0.5);
                this.ctx.lineWidth = 1;
                this.ctx.beginPath();
                this.ctx.arc(cx, cy, r * 0.92, 0, Math.PI * 2);
                this.ctx.stroke();
                this.ctx.setLineDash([]);
                this.ctx.restore();
            }
        }

        _drawHoleMarker(cx, cy, r, sev) {
            const col = HOLE_COLORS[sev] || HOLE_COLORS[2];
            const lineW = sev >= 3 ? 2.5 : 1.5;

            this.ctx.beginPath();
            this.ctx.arc(cx, cy, r, 0, Math.PI * 2);
            const holeGrd = this.ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
            holeGrd.addColorStop(0, 'rgba(0,0,0,0.9)');
            holeGrd.addColorStop(0.6, 'rgba(20,10,5,0.85)');
            holeGrd.addColorStop(1, 'rgba(50,30,15,0.7)');
            this.ctx.fillStyle = holeGrd;
            this.ctx.fill();

            this.ctx.beginPath();
            this.ctx.arc(cx, cy, r + 1, 0, Math.PI * 2);
            this.ctx.strokeStyle = col.stroke;
            this.ctx.lineWidth = lineW;
            this.ctx.stroke();

            if (sev >= 3) {
                this.ctx.beginPath();
                this.ctx.arc(cx, cy, r + 4, 0, Math.PI * 2);
                this.ctx.strokeStyle = hexToRgba(col.stroke, 0.4);
                this.ctx.lineWidth = 1;
                this.ctx.setLineDash([3, 2]);
                this.ctx.stroke();
                this.ctx.setLineDash([]);
            }

            this.ctx.strokeStyle = hexToRgba(col.stroke, 0.8);
            this.ctx.lineWidth = 0.8;
            this.ctx.beginPath();
            this.ctx.moveTo(cx - r - 3, cy); this.ctx.lineTo(cx + r + 3, cy);
            this.ctx.moveTo(cx, cy - r - 3); this.ctx.lineTo(cx, cy + r + 3);
            this.ctx.stroke();
        }

        _drawRiskFrame(W, H, risk) {
            const lvl = risk >= 75 ? { color: '#C62828', width: 4 }
                     : risk >= 50 ? { color: '#E65100', width: 3 }
                     : risk >= 25 ? { color: '#F57C00', width: 2 }
                                  : { color: '#2E7D32', width: 1 };

            this.ctx.fillStyle = hexToRgba(lvl.color, 0.12);
            this.ctx.fillRect(0, 0, W, lvl.width);
            this.ctx.fillRect(0, H - lvl.width, W, lvl.width);
            this.ctx.fillRect(0, 0, lvl.width, H);
            this.ctx.fillRect(W - lvl.width, 0, lvl.width, H);
        }

        _applyTransform() {
            const scaleVal = `translate(${this._translation.x}px, ${this._translation.y}px) scale(${this._scale}) translateZ(0)`;
            this.canvas.style.transform = scaleVal;
        }

        _listeners = {};
        on(event, handler) {
            if (!this._listeners[event]) this._listeners[event] = [];
            this._listeners[event].push(handler);
        }
        _fireEvent(event, data) {
            (this._listeners[event] || []).forEach(h => {
                try { h(data); } catch (e) { console.error(e); }
            });
        }

        hitTest(clientX, clientY) {
            const rect = this.canvas.getBoundingClientRect();
            const x = (clientX - rect.left - this._translation.x) / this._scale;
            const y = (clientY - rect.top - this._translation.y) / this._scale;
            const W = this.canvas.width;
            const H = this.canvas.height;

            const holes = this._data.holes || [];
            for (let i = holes.length - 1; i >= 0; i--) {
                const h = holes[i];
                const hx = (h.relativeX / 100 || h.positionX / 100 || 0) * W;
                const hy = (h.relativeY / 100 || h.positionY / 100 || 0) * H;
                const hr = Math.max(3, h.radiusMm || h.radius || 6);
                if (Math.hypot(x - hx, y - hy) <= hr + 5) {
                    return { type: 'hole', index: i, data: h };
                }
            }

            const molds = this._data.molds || [];
            for (let i = molds.length - 1; i >= 0; i--) {
                const m = molds[i];
                const mx = (m.relativeX / 100 || m.centerX / 100 || 0) * W;
                const my = (m.relativeY / 100 || m.centerY / 100 || 0) * H;
                const mr = Math.max(20, m.radiusMm || m.radius || 40);
                if (Math.hypot(x - mx, y - my) <= mr) {
                    return { type: 'mold', index: i, data: m };
                }
            }
            return null;
        }

        scheduleResize(canvasId, delay = 120) {
            if (_resizeTimers.has(canvasId)) clearTimeout(_resizeTimers.get(canvasId));
            _resizeTimers.set(canvasId, setTimeout(() => {
                this.render();
                _resizeTimers.delete(canvasId);
            }, delay));
        }

        static clearCache() { _offscreenCache.clear(); }

        destroy() {
            this._listeners = {};
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        }
    }

    MildewOverlay.MOLD_COLORS = MOLD_COLORS;
    MildewOverlay.HOLE_COLORS = HOLE_COLORS;
    MildewOverlay.clearCache = () => _offscreenCache.clear();

    global.MildewOverlay = MildewOverlay;

})(typeof window !== 'undefined' ? window : this);
