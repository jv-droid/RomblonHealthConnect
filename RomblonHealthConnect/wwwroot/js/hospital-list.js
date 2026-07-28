/* ==========================================================================
   Romblon HealthConnect — Facility registry list
   Applies bed-meter widths from data attributes, keeping percentages out of
   inline style attributes in the Razor view.
   ========================================================================== */

(function (window, document) {
    'use strict';

    function init() {
        document.querySelectorAll('[data-bed-fill]').forEach(function (fill) {
            var percent = Number(fill.getAttribute('data-bed-fill'));
            if (isNaN(percent)) { return; }

            fill.style.width = Math.max(0, Math.min(100, percent)) + '%';

            // Same thresholds the dashboard uses: amber under 25%, red under 10%.
            if (percent <= 10) {
                fill.classList.add('bed-meter-fill-danger');
            } else if (percent <= 25) {
                fill.classList.add('bed-meter-fill-warning');
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
