/* ==========================================================================
   Romblon HealthConnect — Workspace mode
   One platform, two presentations: Executive (leadership, presentation) and
   Operations (day-to-day work). Only information hierarchy changes; the data,
   components, and routes are identical.

   The mode is a data attribute on .app-shell. CSS does the hiding, so
   switching never reloads the page or rebuilds the DOM.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};

    var STORAGE_KEY = 'rhc.workspace';
    var MODES = ['executive', 'operations'];
    var DEFAULT_MODE = 'operations';

    var shell = null;

    function readStoredMode() {
        try {
            var stored = window.localStorage.getItem(STORAGE_KEY);
            return MODES.indexOf(stored) !== -1 ? stored : null;
        } catch (error) {
            return null;
        }
    }

    function persistMode(mode) {
        try {
            window.localStorage.setItem(STORAGE_KEY, mode);
        } catch (error) {
            /* Private browsing — the choice simply does not persist. */
        }
    }

    function currentMode() {
        return shell ? shell.getAttribute('data-workspace') : DEFAULT_MODE;
    }

    function syncButtons(mode) {
        document.querySelectorAll('.mode-switch-option').forEach(function (button) {
            button.setAttribute('aria-pressed', String(button.getAttribute('data-mode') === mode));
        });
    }

    /**
     * Applies a mode.
     * @param {string} mode - "executive" or "operations"
     * @param {boolean} animate - cross-fade the content region
     */
    function applyMode(mode, animate) {
        if (!shell || MODES.indexOf(mode) === -1 || mode === currentMode()) { return; }

        var commit = function () {
            shell.setAttribute('data-workspace', mode);
            syncButtons(mode);
            persistMode(mode);

            // Panels change size, so anything measuring itself must re-measure.
            document.dispatchEvent(new CustomEvent('rhc:layout-changed'));
            document.dispatchEvent(new CustomEvent('rhc:workspace-changed', { detail: { mode: mode } }));
        };

        var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        if (!animate || reduceMotion) {
            commit();
            return;
        }

        shell.classList.add('is-switching-mode');

        window.setTimeout(function () {
            commit();
            // Next frame, so the fade-in starts from the committed layout.
            window.requestAnimationFrame(function () {
                shell.classList.remove('is-switching-mode');
            });
        }, 160);
    }

    function init() {
        shell = document.getElementById('appShell');
        if (!shell) { return; }

        // Restore the previous choice before first paint of the content region.
        var stored = readStoredMode();
        if (stored) {
            shell.setAttribute('data-workspace', stored);
        }
        syncButtons(currentMode());

        document.querySelectorAll('.mode-switch-option').forEach(function (button) {
            button.addEventListener('click', function () {
                applyMode(button.getAttribute('data-mode'), true);
            });
        });

        RHC.workspace = {
            get: currentMode,
            set: function (mode) { applyMode(mode, true); },
            isExecutive: function () { return currentMode() === 'executive'; }
        };

        document.dispatchEvent(new CustomEvent('rhc:workspace-ready', { detail: { mode: currentMode() } }));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
