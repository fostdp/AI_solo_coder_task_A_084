
(function(global) {
    'use strict';

    const DYNASTY_PALETTES = {
        '明': { primary: '#8B2500', secondary: '#CD5C5C', accent: '#F4A460', gold: '#DAA520' },
        '清': { primary: '#4A235A', secondary: '#9370DB', accent: '#DDA0DD', gold: '#DAA520' },
        '宋': { primary: '#1E3A5F', secondary: '#4682B4', accent: '#B0C4DE', gold: '#FFD700' },
        '元': { primary: '#2E4A2E', secondary: '#6B8E23', accent: '#BDB76B', gold: '#FFD700' },
        '汉': { primary: '#5C4033', secondary: '#A0522D', accent: '#DEB887', gold: '#DAA520' }
    };

    const MOTIF_TYPES = ['flower', 'cloud', 'dragon', 'phoenix', 'lotus', 'peony', 'geometric', 'star'];

    function seededRandom(seed) {
        let s = seed % 2147483647;
        if (s <= 0) s += 2147483646;
        return function() {
            s = (s * 16807) % 2147483647;
            return (s - 1) / 2147483646;
        };
    }

    function hexToRgba(hex, a) {
        const h = hex.replace('#', '');
        const bigint = parseInt(h.length === 3 ? h.split('').map(c => c + c).join('') : h, 16);
        const r = (bigint >> 16) & 255;
        const g = (bigint >> 8) & 255;
        const b = bigint & 255;
        return `rgba(${r},${g},${b},${a})`;
    }

    class EmbroideryImage {
        constructor(canvasId, options = {}) {
            this.canvas = typeof canvasId === 'string'
                ? document.getElementById(canvasId)
                : canvasId;
            if (!this.canvas) throw new Error('Canvas element not found');

            this.ctx = this.canvas.getContext('2d');
            this.options = Object.assign({
                width: 900,
                height: 600,
                resolution: 1,
                showGrid: true,
                showBorder: true,
                backgroundColor: null
            }, options);

            this.textile = null;
            this._scale = 1.0;
            this._translation = { x: 0, y: 0 };
            this._seed = 1;
            this._rand = seededRandom(1);
        }

        setTextile(textile) {
            this.textile = textile;
            this._seed = (textile?.id || 1) * 1000 + ((textile?.dynasty?.charCodeAt(0) || 77) % 100);
            this._rand = seededRandom(this._seed);
            this.render();
        }

        setScale(scale, centerX, centerY) {
            const prev = this._scale;
            this._scale = Math.max(0.4, Math.min(3.0, scale));
            const ratio = this._scale / prev;

            if (centerX !== undefined && centerY !== undefined) {
                const rect = this.canvas.getBoundingClientRect();
                const cx = centerX - rect.left;
                const cy = centerY - rect.top;
                this._translation.x = cx - (cx - this._translation.x) * ratio;
                this._translation.y = cy - (cy - this._translation.y) * ratio;
            }
            this.render();
        }

        resetView() {
            this._scale = 1.0;
            this._translation = { x: 0, y: 0 };
            this.render();
        }

        getPalette() {
            const d = this.textile?.dynasty || '明';
            return DYNASTY_PALETTES[d] || DYNASTY_PALETTES['明'];
        }

        render() {
            const { width, height } = this.options;
            this.canvas.width = width;
            this.canvas.height = height;

            const ctx = this.ctx;
            ctx.save();
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.clearRect(0, 0, width, height);

            ctx.translate(this._translation.x, this._translation.y);
            ctx.scale(this._scale, this._scale);

            this._drawBackground();
            if (this.options.showGrid) this._drawWeaveGrid();
            this._drawMotifs();
            if (this.options.showBorder) this._drawBorder();

            ctx.restore();
            this._fireEvent('rendered', { scale: this._scale });
        }

        _drawBackground() {
            const { width, height } = this.options;
            const palette = this.getPalette();
            const rand = this._rand;

            const bg = this.ctx.createLinearGradient(0, 0, width, height);
            if (this.options.backgroundColor) {
                bg.addColorStop(0, this.options.backgroundColor);
                bg.addColorStop(1, this.options.backgroundColor);
            } else {
                bg.addColorStop(0, palette.primary);
                bg.addColorStop(0.33, palette.secondary);
                bg.addColorStop(0.66, palette.accent);
                bg.addColorStop(1, palette.primary);
            }
            this.ctx.fillStyle = bg;
            this.ctx.fillRect(0, 0, width, height);

            this.ctx.globalAlpha = 0.25;
            for (let i = 0; i < 15; i++) {
                const cx = rand() * width;
                const cy = rand() * height;
                const r = 15 + rand() * 50;
                const grd = this.ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
                grd.addColorStop(0, hexToRgba('#FFF8DC', 0.5));
                grd.addColorStop(1, hexToRgba('#FFF8DC', 0));
                this.ctx.fillStyle = grd;
                this.ctx.beginPath();
                this.ctx.arc(cx, cy, r, 0, Math.PI * 2);
                this.ctx.fill();
            }
            this.ctx.globalAlpha = 1;
        }

        _drawWeaveGrid() {
            const { width, height } = this.options;
            const palette = this.getPalette();

            this.ctx.globalAlpha = 0.12;
            this.ctx.strokeStyle = '#FFF8DC';
            this.ctx.lineWidth = 0.5;
            this.ctx.beginPath();
            for (let i = 0; i < width; i += 6) {
                this.ctx.moveTo(i, 0);
                this.ctx.lineTo(i, height);
            }
            for (let j = 0; j < height; j += 6) {
                this.ctx.moveTo(0, j);
                this.ctx.lineTo(width, j);
            }
            this.ctx.stroke();
            this.ctx.globalAlpha = 1;
        }

        _drawMotifs() {
            const { width, height } = this.options;
            const palette = this.getPalette();
            const rand = this._rand;
            const motifs = 5 + Math.floor(rand() * 5);
            const types = this.textile?.material?.includes('云锦')
                ? ['cloud', 'dragon', 'phoenix', 'peony']
                : MOTIF_TYPES;

            for (let i = 0; i < motifs; i++) {
                const mx = 60 + rand() * (width - 120);
                const my = 60 + rand() * (height - 120);
                const mr = 25 + rand() * 55;
                const type = types[Math.floor(rand() * types.length)];
                this._drawMotif(type, mx, my, mr, palette, this._seed + i);
            }
        }

        _drawMotif(type, cx, cy, r, palette, seed) {
            const rand = seededRandom(seed);
            const ctx = this.ctx;

            ctx.save();
            ctx.translate(cx, cy);
            ctx.globalAlpha = 0.32;
            ctx.fillStyle = '#FFF8DC';
            ctx.strokeStyle = palette.gold;
            ctx.lineWidth = 1.5;

            switch (type) {
                case 'flower':
                case 'peony':
                    const petals = 8 + Math.floor(rand() * 4);
                    for (let p = 0; p < petals; p++) {
                        ctx.rotate(Math.PI * 2 / petals);
                        ctx.beginPath();
                        ctx.ellipse(0, -r * 0.6, r * 0.26, r * 0.5, 0, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.stroke();
                    }
                    ctx.beginPath();
                    ctx.arc(0, 0, r * 0.26, 0, Math.PI * 2);
                    ctx.fillStyle = palette.gold;
                    ctx.fill();
                    ctx.stroke();
                    break;

                case 'cloud':
                    ctx.beginPath();
                    for (let i = 0; i < 36; i++) {
                        const a = (i / 36) * Math.PI * 2;
                        const rr = i % 2 === 0 ? r : r * 0.5;
                        const px = Math.cos(a) * rr;
                        const py = Math.sin(a) * rr;
                        if (i === 0) ctx.moveTo(px, py);
                        else ctx.lineTo(px, py);
                    }
                    ctx.closePath();
                    ctx.fill();
                    ctx.stroke();
                    break;

                case 'dragon':
                case 'phoenix':
                    for (let d = 0; d < 3; d++) {
                        ctx.beginPath();
                        const rr = r * (1 - d * 0.3);
                        ctx.arc(0, 0, rr, 0, Math.PI * 2);
                        ctx.globalAlpha = 0.12 + d * 0.1;
                        ctx.fill();
                        ctx.globalAlpha = 0.38;
                        ctx.stroke();
                    }
                    break;

                case 'star':
                case 'geometric':
                default:
                    const pts = type === 'star' ? 5 : 8;
                    ctx.beginPath();
                    for (let i = 0; i < pts * 2; i++) {
                        const a = (i / (pts * 2)) * Math.PI * 2 - Math.PI / 2;
                        const rr = i % 2 === 0 ? r : r * 0.45;
                        const px = Math.cos(a) * rr;
                        const py = Math.sin(a) * rr;
                        if (i === 0) ctx.moveTo(px, py);
                        else ctx.lineTo(px, py);
                    }
                    ctx.closePath();
                    ctx.fill();
                    ctx.stroke();
                    break;
            }

            ctx.restore();
        }

        _drawBorder() {
            const { width, height } = this.options;
            const palette = this.getPalette();

            this.ctx.strokeStyle = hexToRgba(palette.gold, 0.6);
            this.ctx.lineWidth = 3;
            this.ctx.strokeRect(10, 10, width - 20, height - 20);
            this.ctx.lineWidth = 1;
            this.ctx.strokeRect(20, 20, width - 40, height - 40);
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

        getImageData() {
            return this.canvas.toDataURL('image/png');
        }

        getCoordinatesAt(clientX, clientY) {
            const rect = this.canvas.getBoundingClientRect();
            const x = (clientX - rect.left - this._translation.x) / this._scale;
            const y = (clientY - rect.top - this._translation.y) / this._scale;
            return {
                x: Math.max(0, Math.min(100, (x / this.options.width) * 100)),
                y: Math.max(0, Math.min(100, (y / this.options.height) * 100)),
                px: x, py: y
            };
        }

        destroy() {
            this._listeners = {};
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        }
    }

    EmbroideryImage.PALETTES = DYNASTY_PALETTES;

    global.EmbroideryImage = EmbroideryImage;

})(typeof window !== 'undefined' ? window : this);
