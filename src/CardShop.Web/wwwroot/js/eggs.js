(function () {
    'use strict';

    var KEY = 'bushu.waterings';
    var GREETED_KEY = 'bushu.greetedDay';

    // Shop-flavored lines for when you water him.
    var MESSAGES = [
        'Slow growth beats panic selling.',
        'A 5% dip is not a trend. Check the chart first.',
        'Roots first, leaves later. Know your cost basis.',
        'Even chase cards have quiet weeks.',
        'I do not set prices. I just look wise about them.',
        'Prune your dead stock before it prunes your margins.',
        'Water me and I bloom. Watch your margins and you will too.',
        'Bushu approves of this inventory.',
        'Patience is the best hedge.',
        'Spreads are like branches: trim the ones that go nowhere.'
    ];

    // Sweet lines for when he pops out on his own.
    var SWEET = [
        'You make this shop feel like home.',
        'I hope today is gentle with you.',
        'Just popping by to say you are doing great.',
        'Every card in here is lucky to be looked after by you.',
        'Drink some water too. We both deserve it.',
        'You have a good eye. I would know, I am mostly leaves.',
        'Small steps still count. Ask any tree.',
        'Thank you for taking care of me. And of everything else.',
        'Whatever today brings, you have got this.',
        'I grew a little today. I think you did too.',
        'Your hard work is showing, even when it feels quiet.',
        'Remember to take a break. The cards will wait.',
        'I saved you a sunny spot.',
        'You are someone\'s favorite person. Probably mine.',
        'Good things grow slowly. You are one of them.',
        'Hi! I missed you.',
        'Proud of you. That is all. Back to hiding.',
        'If nobody told you today: you are appreciated.',
        'You bring the sunshine. I just do the photosynthesis.',
        'Breathe in. Breathe out. Leaves rustle. All is well.',
        'Even on slow days, this place is better with you in it.',
        'I believe in you more than I believe in fertilizer.',
        'You are rarer than any chase card.',
        'A little rest today means more growing tomorrow.',
        'Thank you for being kind. It shows.',
        'I peeked out just to see you.',
        'Be as patient with yourself as you are with me.',
        'You make hard things look easy.',
        'Today is a good day to be proud of how far you have come.',
        'Sending you a tiny leafy hug.'
    ];

    // Where he can hide: behind the bottom of the screen, behind the top menu,
    // or behind the left or right edge.
    var SPOTS = ['bottom', 'top', 'left', 'right'];

    function load() { try { return parseInt(localStorage.getItem(KEY) || '0', 10) || 0; } catch (e) { return 0; } }
    function save(n) { try { localStorage.setItem(KEY, String(n)); } catch (e) { } }
    function getItem(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
    function setItem(k, v) { try { localStorage.setItem(k, v); } catch (e) { } }

    var waterings = load();

    // ---------- Daily pick: same all day, always different from yesterday ----------
    function seeded(seed) {
        return function () {
            seed = (seed + 0x6D2B79F5) | 0;
            var t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
            t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
            return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
        };
    }

    function dayIndex(d) {
        // whole local days since 1 Jan 2026 (UTC math avoids daylight-saving drift)
        return Math.floor((Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()) - Date.UTC(2026, 0, 1)) / 864e5);
    }

    function pickForDay(day) {
        // Walk forward from the epoch so each day is checked against the day before it.
        var prev = null, cur = null;
        for (var i = Math.min(0, day); i <= day; i++) {
            var r = seeded(i * 2654435761);
            cur = { spot: Math.floor(r() * SPOTS.length), offset: 0.1 + r() * 0.75, line: Math.floor(r() * SWEET.length) };
            if (prev) {
                if (cur.spot === prev.spot) cur.spot = (cur.spot + 1) % SPOTS.length;
                if (cur.line === prev.line) cur.line = (cur.line + 1) % SWEET.length;
            }
            prev = cur;
        }
        return cur;
    }

    var todayIdx = dayIndex(new Date());
    var today = pickForDay(todayIdx);

    function rand(min, max) { return min + Math.random() * (max - min); }

    var SVG =
        '<svg viewBox="0 0 120 130" width="64" height="70" aria-hidden="true">' +
        '<g class="b-tree">' +
        '<path d="M60 97 C57 82 71 73 63 59 C57 49 49 47 44 38" fill="none" stroke="#6b4423" stroke-width="7" stroke-linecap="round"/>' +
        '<path d="M63 64 C72 60 81 54 88 47" fill="none" stroke="#6b4423" stroke-width="4" stroke-linecap="round"/>' +
        '<ellipse cx="42" cy="34" rx="21" ry="13" fill="#2f7a3f"/>' +
        '<ellipse cx="88" cy="42" rx="18" ry="11" fill="#3f8f4f"/>' +
        '<ellipse cx="64" cy="22" rx="22" ry="14" fill="#4aa35c"/>' +
        '<ellipse cx="52" cy="46" rx="12" ry="7" fill="#3f8f4f"/>' +
        '<g class="b-blossoms" fill="#f9a8d4">' +
        '<circle cx="34" cy="30" r="3"/><circle cx="48" cy="36" r="2.6"/><circle cx="60" cy="18" r="3"/>' +
        '<circle cx="74" cy="26" r="2.6"/><circle cx="84" cy="38" r="3"/><circle cx="94" cy="44" r="2.4"/>' +
        '<circle cx="46" cy="46" r="2.4"/>' +
        '</g></g>' +
        '<rect x="32" y="95" width="56" height="7" rx="3" fill="#9a3412"/>' +
        '<path d="M36 102 H84 L79 124 H41 Z" fill="#c2410c"/>' +
        '<circle cx="53" cy="111" r="2.2" fill="#1f2937"/><circle cx="67" cy="111" r="2.2" fill="#1f2937"/>' +
        '<path d="M54 116 Q60 120 66 116" fill="none" stroke="#1f2937" stroke-width="1.8" stroke-linecap="round"/>' +
        '<g class="b-shades"><rect x="47" y="107" width="12" height="7" rx="2" fill="#111"/>' +
        '<rect x="61" y="107" width="12" height="7" rx="2" fill="#111"/>' +
        '<path d="M59 109 H61" stroke="#111" stroke-width="1.5"/></g>' +
        '</svg>';

    function init() {
        // ----- Bushu -----
        var peek = document.createElement('div');
        peek.className = 'bushu-peek bottom';

        var bushu = document.createElement('button');
        bushu.type = 'button';
        bushu.className = 'bushu';
        bushu.title = 'Bushu';
        bushu.tabIndex = -1;
        bushu.setAttribute('aria-label', 'Bushu the bonsai');
        bushu.innerHTML = SVG;
        peek.appendChild(bushu);

        var bubble = document.createElement('div');
        bubble.className = 'bushu-bubble';
        bubble.setAttribute('role', 'status');
        bubble.setAttribute('aria-live', 'polite');

        var spot = 'bottom';
        var isOut = false, hovering = false, staying = false;
        var bubbleTimer = null, hideTimer = null, nextTimer = null;
        var STAY = 8000;      // how long he stays out after popping up
        var MAX_OUT = 20000;  // hard limit, even while hovered
        var outSince = 0;

        function placeBubble() {
            var r = bushu.getBoundingClientRect();
            var bw = bubble.offsetWidth, bh = bubble.offsetHeight;
            var x, y;
            if (spot === 'top') { x = r.left; y = r.bottom + 8; }
            else if (spot === 'left') { x = r.right + 8; y = r.top; }
            else if (spot === 'right') { x = r.left - bw - 8; y = r.top; }
            else { x = r.left; y = r.top - bh - 8; }
            x = Math.max(8, Math.min(x, window.innerWidth - bw - 8));
            y = Math.max(8, Math.min(y, window.innerHeight - bh - 8));
            bubble.style.left = x + 'px';
            bubble.style.top = y + 'px';
            bubble.style.bottom = 'auto';
        }

        function say(text) {
            bubble.textContent = text;
            placeBubble();
            bubble.classList.add('show');
            clearTimeout(bubbleTimer);
            bubbleTimer = setTimeout(function () { bubble.classList.remove('show'); }, 6000);
        }

        function applyGrowth() {
            var svg = bushu.querySelector('svg');
            svg.style.setProperty('--grow', (1 + Math.min(waterings, 20) * 0.0125).toFixed(4));
            bushu.classList.toggle('bloom', waterings >= 10);
        }

        function setSpot(name, offset) {
            spot = name;
            peek.classList.add('no-anim');
            peek.className = 'bushu-peek no-anim ' + name;
            peek.style.left = peek.style.right = peek.style.top = peek.style.bottom = '';
            if (name === 'bottom' || name === 'top') {
                peek.style.left = Math.round(offset * Math.max(0, window.innerWidth - 92)) + 'px';
                if (name === 'top') {
                    // tuck him behind the menu bar if it's on screen, else the top edge
                    var header = document.querySelector('header');
                    var hb = header ? header.getBoundingClientRect().bottom : 0;
                    peek.style.top = Math.max(0, Math.round(hb)) + 'px';
                }
            } else {
                peek.style.top = Math.round(offset * Math.max(0, window.innerHeight - 100)) + 'px';
            }
            void peek.offsetWidth; // apply the hidden position without animating
            peek.classList.remove('no-anim');
        }

        function popOut(name, offset, text, stay) {
            clearTimeout(nextTimer);
            clearTimeout(hideTimer);
            staying = !!stay;
            if (!isOut) { setSpot(name, offset); outSince = Date.now(); }
            isOut = true;
            bushu.tabIndex = 0;
            requestAnimationFrame(function () { peek.classList.add('out'); });
            if (text) setTimeout(function () { say(text); }, 650);
            if (!staying) scheduleHide(STAY);
        }

        function scheduleHide(ms) {
            clearTimeout(hideTimer);
            hideTimer = setTimeout(duck, ms);
        }

        function duck() {
            if (staying || document.body.classList.contains('disco')) {
                scheduleHide(3000);
                return;
            }
            // Hovering or keyboard focus buys him a little extra time, but he always
            // hides within MAX_OUT so a resting mouse can't keep him out forever.
            var busy = hovering || bushu.matches(':focus-visible');
            if (busy && Date.now() - outSince < MAX_OUT) {
                scheduleHide(2000);
                return;
            }
            hovering = false;
            bushu.blur();
            isOut = false;
            peek.classList.remove('out');
            bubble.classList.remove('show');
            bushu.tabIndex = -1;
            scheduleNext(rand(60000, 180000));
        }

        function scheduleNext(ms) {
            clearTimeout(nextTimer);
            nextTimer = setTimeout(function () {
                if (document.hidden) { scheduleNext(5000); return; }
                popOut(SPOTS[Math.floor(Math.random() * SPOTS.length)], rand(0.1, 0.85),
                    SWEET[Math.floor(Math.random() * SWEET.length)], false);
            }, ms);
        }

        bushu.addEventListener('mouseenter', function () { hovering = true; });
        bushu.addEventListener('mouseleave', function () { hovering = false; });

        bushu.addEventListener('click', function () {
            var first = waterings === 0;
            waterings++;
            save(waterings);
            applyGrowth();
            if (!staying) scheduleHide(STAY);

            if (first) say("Hi, I'm Bushu. Thanks for the water.");
            else if (waterings === 10) say('Bushu is in bloom!');
            else if (waterings === 25) say('Bushu is now officially a mature tree.');
            else say(MESSAGES[Math.floor(Math.random() * MESSAGES.length)]);
        });

        document.body.appendChild(peek);
        document.body.appendChild(bubble);
        applyGrowth();

        // First peek of this page: today's spot. The first one each day gets today's line.
        setTimeout(function first() {
            if (document.hidden) { setTimeout(first, 5000); return; }
            var dayKey = String(todayIdx);
            var line;
            if (getItem(GREETED_KEY) !== dayKey) {
                line = SWEET[today.line];
                setItem(GREETED_KEY, dayKey);
            } else {
                line = SWEET[Math.floor(Math.random() * SWEET.length)];
            }
            popOut(SPOTS[today.spot], today.offset, line, false);
        }, rand(4000, 18000));

        // ----- Disco -----
        var lights = document.createElement('div');
        lights.id = 'disco-lights';

        var ball = document.createElement('div');
        ball.id = 'disco-ball';
        ball.setAttribute('role', 'button');
        ball.setAttribute('tabindex', '0');
        ball.setAttribute('aria-label', 'Turn off disco mode');
        ball.innerHTML = '<div class="disco-string"></div><div class="disco-sphere"></div>';

        document.body.appendChild(lights);
        document.body.appendChild(ball);

        function discoOn() {
            if (document.body.classList.contains('disco')) return;
            document.body.classList.add('disco');
            popOut(SPOTS[today.spot], today.offset, null, true);
            setTimeout(function () { say('Bushu is vibing. Type "disco" or press Esc to stop.'); }, 650);
        }
        function discoOff() {
            if (!document.body.classList.contains('disco')) return;
            document.body.classList.remove('disco');
            say('Bushu needs a minute to recover.');
            staying = false;
            scheduleHide(5000);
        }
        function toggle() {
            if (document.body.classList.contains('disco')) discoOff(); else discoOn();
        }

        ball.addEventListener('click', discoOff);
        ball.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); discoOff(); }
        });

        var buffer = '';
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { discoOff(); return; }

            var t = e.target;
            var tag = t && t.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (t && t.isContentEditable)) return;
            if (e.ctrlKey || e.metaKey || e.altKey || !e.key || e.key.length !== 1) return;

            buffer = (buffer + e.key.toLowerCase()).slice(-5);
            if (buffer === 'disco') { buffer = ''; toggle(); }
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();

// ---------- The Nido dance ----------
// Every so often, a Nidoking card and a Nidoqueen card waddle in from opposite sides, meet in the
// middle, their artwork pops up out of the cards, they get cuddly, then they head off together.
// Card art loads from TCGplayer's image server, like the Cards page. To see it right away, open the
// browser console (F12) and type:  nidoDance()
(function () {
    'use strict';

    var LAST_KEY = 'nido.lastDance';
    var MIN_GAP_MS = 3 * 60 * 60 * 1000;  // at most once every 3 hours
    var CHANCE = 0.2;                      // and only on about 1 in 5 page views

    var CARDS = {
        king:  { img: 'https://tcgplayer-cdn.tcgplayer.com/product/42351_400w.jpg', name: 'Nidoking' },  // Base Set 11/102
        queen: { img: 'https://tcgplayer-cdn.tcgplayer.com/product/45133_400w.jpg', name: 'Nidoqueen' }  // Jungle 7/64
    };
    var KING_LINES  = ['Best tag team in the binder.', 'Mint condition, just like you.', 'You complete my set.', 'Holo-graphically yours.'];
    var QUEEN_LINES = ['Boyfriends: +20 damage for every Nidoking in play!', 'My favorite pull, ever.', 'Never trading you.', 'Sleeve me up, handsome.'];

    var running = false;
    var reduced = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;

    function pick(a) { return a[Math.floor(Math.random() * a.length)]; }
    function rand(min, max) { return min + Math.random() * (max - min); }
    function after(ms, fn) { return setTimeout(fn, ms); }

    function preload(urls, done) {
        var left = urls.length, failed = false;
        urls.forEach(function (u) {
            var im = new Image();
            im.onload = function () { if (--left === 0 && !failed) done(); };
            im.onerror = function () { failed = true; };
            im.src = u;
        });
    }

    function makeCard(kind) {
        var el = document.createElement('div');
        el.className = 'nido ' + kind;
        el.style.setProperty('--img', 'url("' + CARDS[kind].img + '")');
        el.innerHTML =
            '<div class="nido-body">' +
                '<div class="nido-face"></div>' +
                '<div class="nido-hole"></div>' +
                '<div class="nido-pop"><div class="nido-art"></div></div>' +
                '<div class="nido-say"></div>' +
            '</div>';
        return el;
    }

    function heart(stage, x, y) {
        var h = document.createElement('span');
        h.className = 'nido-heart';
        h.textContent = pick(['💜', '💗', '💕', '✨']);
        h.style.left = (x + rand(-30, 30)) + 'px';
        h.style.bottom = y + 'px';
        h.style.setProperty('--drift', rand(-40, 40).toFixed(0) + 'px');
        h.style.fontSize = rand(16, 26).toFixed(0) + 'px';
        stage.appendChild(h);
        after(2200, function () { h.remove(); });
    }

    function nidoDance() {
        if (running || reduced) return;
        running = true;
        try { localStorage.setItem(LAST_KEY, String(Date.now())); } catch (e) { }

        preload([CARDS.king.img, CARDS.queen.img], function () {
            var W = window.innerWidth;
            var cardW = W < 600 ? 92 : 120;
            var gap = 18;

            var stage = document.createElement('div');
            stage.className = 'nido-stage';
            stage.style.setProperty('--card-w', cardW + 'px');
            stage.setAttribute('aria-hidden', 'true');
            var king = makeCard('king'), queen = makeCard('queen');
            stage.appendChild(king);
            stage.appendChild(queen);
            document.body.appendChild(stage);

            function moveTo(el, x, ms) {
                el.style.transitionDuration = (ms || 0) + 'ms';
                el.style.transform = 'translateX(' + Math.round(x) + 'px)';
            }
            function say(el, text, ms) {
                var b = el.querySelector('.nido-say');
                b.textContent = text;
                b.classList.add('show');
                after(ms, function () { b.classList.remove('show'); });
            }

            // start off-screen
            moveTo(king, -cardW - 40, 0);
            moveTo(queen, W + 40, 0);
            void stage.offsetWidth;

            var meetKing = W / 2 - cardW - gap / 2;
            var meetQueen = W / 2 + gap / 2;
            var heartsX = W / 2;
            var heartsY = cardW * 1.37 + 60;

            // 1. waddle in
            after(60, function () {
                king.classList.add('walking'); queen.classList.add('walking');
                moveTo(king, meetKing, 3200); moveTo(queen, meetQueen, 3200);
            });
            // 2. stop, little hop
            after(3300, function () {
                king.classList.remove('walking'); queen.classList.remove('walking');
                king.classList.add('hop'); queen.classList.add('hop');
            });
            // 3. artwork pops up out of the cards
            after(3900, function () { king.classList.add('popped'); queen.classList.add('popped'); });
            // 4. lean in and get cuddly
            after(4700, function () {
                king.classList.add('lean'); queen.classList.add('lean');
                king.classList.add('nuzzle'); queen.classList.add('nuzzle');
                var n = 0, t = setInterval(function () { heart(stage, heartsX, heartsY); if (++n >= 14) clearInterval(t); }, 220);
            });
            after(5200, function () { say(queen, pick(QUEEN_LINES), 2300); });
            after(7700, function () { say(king, pick(KING_LINES), 2300); });
            // 5. back into their cards
            after(10300, function () {
                king.classList.remove('nuzzle', 'lean'); queen.classList.remove('nuzzle', 'lean');
                after(250, function () { king.classList.remove('popped'); queen.classList.remove('popped'); });
            });
            // 6. leave together, side by side
            after(11300, function () {
                king.classList.remove('hop'); queen.classList.remove('hop');
                king.classList.add('walking'); queen.classList.add('walking');
                moveTo(queen, W + 60 + cardW, 3800);
                moveTo(king, W + 60 - gap, 3800 + 250);
                heart(stage, heartsX + 80, cardW * 1.37 + 30);
            });
            after(15600, function () { stage.remove(); running = false; });
        });

        // if the images can't load (offline), quietly give up
        after(8000, function () { if (!document.querySelector('.nido-stage')) running = false; });
    }

    window.nidoDance = nidoDance;

    function maybeSchedule() {
        if (reduced) return;
        var last = 0;
        try { last = parseInt(localStorage.getItem(LAST_KEY) || '0', 10) || 0; } catch (e) { }
        if (Date.now() - last < MIN_GAP_MS) return;
        if (Math.random() > CHANCE) return;
        after(rand(20000, 90000), function go() {
            if (document.hidden) { after(10000, go); return; }
            nidoDance();
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', maybeSchedule);
    else maybeSchedule();
})();
