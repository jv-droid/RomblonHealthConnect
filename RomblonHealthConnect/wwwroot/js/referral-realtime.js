/* ==========================================================================
   Romblon HealthConnect — Real-time referral updates
   Subscribes to the SignalR hub and refreshes dashboard counts, queue rows,
   and the notification centre without a page reload.
   Degrades silently to the server-rendered page when the hub is unreachable.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};
    var Referrals = RHC.referrals = RHC.referrals || {};

    var HUB_URL = '/hubs/referrals';

    var connection = null;
    var currentHospitalId = null;

    /* ----------------------------------------------------------------------
       1. View updates
       ---------------------------------------------------------------------- */

    /** Repaints the status cell of a visible row and flashes it. */
    function applyStatusToRow(payload) {
        var row = document.querySelector('tr[data-referral-id="' + payload.id + '"]');
        if (!row) { return; }

        var cell = row.querySelector('[data-cell="status"]');
        if (cell) {
            cell.innerHTML = '<span class="rhc-badge ' + payload.statusBadgeClass + '">' +
                Referrals.escapeHtml(payload.statusLabel) + '</span>';
        }

        row.classList.remove('is-updated');
        // Restart the animation on a repeat update.
        void row.offsetWidth;
        row.classList.add('is-updated');
    }

    /** Pulls fresh metric values so the cards match the database. */
    async function refreshCounts() {
        var container = document.querySelector('[data-referral-metrics]');
        if (!container) { return; }

        try {
            var response = await fetch(window.location.href, {
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) { return; }

            var html = await response.text();
            var parsed = new DOMParser().parseFromString(html, 'text/html');
            var fresh = parsed.querySelector('[data-referral-metrics]');

            if (fresh) {
                container.innerHTML = fresh.innerHTML;
            }
        } catch (error) {
            window.console.warn('[realtime] Count refresh failed:', error);
        }
    }

    /* ----------------------------------------------------------------------
       2. Hub wiring
       ---------------------------------------------------------------------- */

    function registerHandlers() {
        connection.on('referralCreated', function (payload) {
            // Only the receiving facility should be interrupted by a new referral.
            if (payload.destinationHospitalId === currentHospitalId) {
                Referrals.showToast(
                    'New referral received',
                    payload.referralNumber + ' from ' + (payload.patientName || 'a patient'),
                    'fa-inbox');
            }

            refreshCounts();
        });

        connection.on('referralStatusChanged', function (payload) {
            applyStatusToRow(payload);

            Referrals.showToast(
                'Referral ' + payload.statusLabel.toLowerCase(),
                payload.referralNumber + ' is now ' + payload.statusLabel.toLowerCase() + '.',
                'fa-arrows-rotate');

            refreshCounts();
        });

        connection.on('notificationReceived', function (payload) {
            Referrals.showToast(payload.title, payload.message, payload.icon);
            Referrals.reloadNotifications();
        });

        connection.on('countsChanged', function () {
            refreshCounts();
        });
    }

    async function connect() {
        try {
            await connection.start();

            if (currentHospitalId) {
                await connection.invoke('JoinHospitalGroup', currentHospitalId);
            }

            window.console.info('[realtime] Connected to the referral hub.');
        } catch (error) {
            window.console.warn('[realtime] Hub connection failed; falling back to page refresh.', error);
        }
    }

    /* ----------------------------------------------------------------------
       3. Bootstrap
       ---------------------------------------------------------------------- */

    async function init() {
        if (!window.signalR) {
            window.console.info('[realtime] SignalR client unavailable; live updates are disabled.');
            return;
        }

        try {
            var facility = await Referrals.getJson('/Referrals/CurrentFacility');
            currentHospitalId = facility.id;
        } catch (error) {
            window.console.warn('[realtime] Could not resolve the acting facility.', error);
            return;
        }

        connection = new window.signalR.HubConnectionBuilder()
            .withUrl(HUB_URL)
            .withAutomaticReconnect()
            .configureLogging(window.signalR.LogLevel.Warning)
            .build();

        registerHandlers();

        // Re-join the facility group after an automatic reconnect.
        connection.onreconnected(function () {
            if (currentHospitalId) {
                connection.invoke('JoinHospitalGroup', currentHospitalId);
            }
        });

        await connect();

        RHC.realtime = {
            connection: function () { return connection; },
            hospitalId: function () { return currentHospitalId; }
        };
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
