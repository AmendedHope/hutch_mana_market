(function () {
    'use strict';

    // ----- Light / dark theme -----
    var root = document.documentElement;
    var toggle = document.getElementById('themeToggle');
    var systemDark = window.matchMedia ? matchMedia('(prefers-color-scheme: dark)') : null;

    function current() { return root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light'; }

    function apply(theme) {
        root.setAttribute('data-theme', theme);
        if (toggle) toggle.setAttribute('aria-label', theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
    }

    apply(current());

    if (toggle) {
        toggle.addEventListener('click', function () {
            var next = current() === 'dark' ? 'light' : 'dark';
            apply(next);
            try { localStorage.setItem('theme', next); } catch (e) { }
        });
    }

    // Follow the system setting until the user picks a theme themselves.
    if (systemDark && systemDark.addEventListener) {
        systemDark.addEventListener('change', function (e) {
            var saved = null;
            try { saved = localStorage.getItem('theme'); } catch (err) { }
            if (saved !== 'light' && saved !== 'dark') apply(e.matches ? 'dark' : 'light');
        });
    }

    // ----- Buttons that start long jobs: show a spinner and prevent double-submits -----
    document.querySelectorAll('form[data-busy]').forEach(function (form) {
        form.addEventListener('submit', function () {
            var btn = form.querySelector('button[type=submit]');
            if (!btn) return;
            btn.classList.add('is-busy');
            var label = btn.querySelector('.label');
            if (label && form.getAttribute('data-busy')) label.textContent = form.getAttribute('data-busy');
            // disable after the submit has been sent
            setTimeout(function () { btn.disabled = true; }, 0);
        });
    });
})();
