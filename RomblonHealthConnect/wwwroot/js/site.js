// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

/* Account menu toggle in the application shell. */
(function (window, document) {
    'use strict';

    function init() {
        var button = document.getElementById('profileButton');
        var menu = document.getElementById('accountMenu');
        if (!button || !menu) { return; }

        button.addEventListener('click', function (event) {
            event.stopPropagation();
            var open = menu.classList.toggle('is-open');
            button.setAttribute('aria-expanded', String(open));
        });

        document.addEventListener('click', function (event) {
            if (!menu.contains(event.target) && event.target !== button) {
                menu.classList.remove('is-open');
                button.setAttribute('aria-expanded', 'false');
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                menu.classList.remove('is-open');
                button.setAttribute('aria-expanded', 'false');
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(window, document);
