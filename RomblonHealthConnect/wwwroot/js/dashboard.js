/* ==========================================================================
   Romblon HealthConnect — Provincial dashboard
   Prototype facility data, executive overview rendering, operational lists,
   and the facility drawer. Publishes window.RHC so health-map.js and the
   command palette share the same records.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};

    /* ----------------------------------------------------------------------
       1. Prototype data — illustrative only, not real health records
       ---------------------------------------------------------------------- */

    // Facilities, the referral feed, the on-duty roster, and the summary figures
    // all come from the database via /Home/NetworkData. That is what makes a
    // facility registered in the Hospitals module appear here — and on the
    // map — with no further wiring.
    //
    // Coordinates are [longitude, latitude] to match MapLibre's ordering.
    var FACILITIES = [];
    var REFERRALS = [];
    var DOCTORS = [];
    var OVERVIEW = {
        lastSyncMinutesAgo: 0,
        activity: { created: 0, accepted: 0, patients: 0 },
        availability: { available: 0, onDuty: 0, unavailable: 0 }
    };

    var NETWORK_ENDPOINT = '/Home/NetworkData';

    /**
     * Loads the live network snapshot. Resolves even on failure so the page
     * still renders (with an empty state) rather than hanging.
     */
    async function loadNetwork() {
        try {
            var response = await fetch(NETWORK_ENDPOINT, {
                credentials: 'same-origin',
                headers: { Accept: 'application/json' }
            });

            if (!response.ok) {
                throw new Error('Network data request failed: ' + response.status);
            }

            var payload = await response.json();

            FACILITIES = payload.facilities || [];
            REFERRALS = payload.referrals || [];
            DOCTORS = payload.doctors || [];
            OVERVIEW = payload.overview || OVERVIEW;

            // health-map.js and the command palette read through RHC.data.
            RHC.data.facilities = FACILITIES;
            RHC.data.referrals = REFERRALS;
            RHC.data.doctors = DOCTORS;
            RHC.data.overview = OVERVIEW;

            return true;
        } catch (error) {
            window.console.error('[dashboard] Could not load network data:', error);
            return false;
        }
    }

    /* ----------------------------------------------------------------------
       2. Display mappings
       ---------------------------------------------------------------------- */

    var STATUS_META = {
        online: { label: 'Online', badge: 'rhc-badge-success', icon: 'fa-circle-check' },
        limited: { label: 'Limited', badge: 'rhc-badge-warning', icon: 'fa-triangle-exclamation' },
        offline: { label: 'Offline', badge: 'rhc-badge-neutral', icon: 'fa-circle-minus' }
    };

    var REFERRAL_STATUS_META = {
        pending: { label: 'Pending', badge: 'rhc-badge-warning' },
        accepted: { label: 'Accepted', badge: 'rhc-badge-success' },
        'in-transit': { label: 'In transit', badge: 'rhc-badge-info' },
        completed: { label: 'Completed', badge: 'rhc-badge-neutral' },
        declined: { label: 'Declined', badge: 'rhc-badge-danger' }
    };

    var DOCTOR_STATUS_META = {
        available: { label: 'Available', badge: 'rhc-badge-success' },
        'on-call': { label: 'On call', badge: 'rhc-badge-info' },
        'in-surgery': { label: 'In surgery', badge: 'rhc-badge-warning' },
        'off-duty': { label: 'Off duty', badge: 'rhc-badge-neutral' }
    };

    var TYPE_ICONS = {
        public: 'fa-hospital',
        district: 'fa-house-medical',
        rhu: 'fa-kit-medical',
        private: 'fa-briefcase-medical'
    };

    /* ----------------------------------------------------------------------
       3. Helpers
       ---------------------------------------------------------------------- */

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function formatRelativeTime(minutes) {
        if (minutes < 1) { return 'just now'; }
        if (minutes === 1) { return '1 minute ago'; }
        if (minutes < 60) { return minutes + ' minutes ago'; }

        var hours = Math.floor(minutes / 60);
        return hours === 1 ? '1 hour ago' : hours + ' hours ago';
    }

    function badge(meta) {
        return meta ? '<span class="rhc-badge ' + meta.badge + '">' + escapeHtml(meta.label) + '</span>' : '';
    }

    function setText(selector, value) {
        var node = document.querySelector(selector);
        if (node) { node.textContent = value; }
    }

    function setHtml(selector, value) {
        var node = document.querySelector(selector);
        if (node) { node.innerHTML = value; }
    }

    function facilityById(id) {
        return FACILITIES.filter(function (f) { return f.id === id; })[0] || null;
    }

    /** Occupancy severity: green above 25% free, amber above 10%, else red. */
    function capacityClass(available, total, prefix) {
        var ratio = total > 0 ? available / total : 0;
        if (ratio > 0.25) { return ''; }
        if (ratio > 0.1) { return prefix + '-warning'; }
        return prefix + '-danger';
    }

    /** Initials for a person avatar, for example "Dr. M. Fabreag" to "MF". */
    function initials(name) {
        var parts = name.replace(/^Dr\.\s*/i, '').split(/\s+/).filter(Boolean);
        var first = parts[0] ? parts[0][0] : '';
        var last = parts.length > 1 ? parts[parts.length - 1][0] : '';
        return (first + last).toUpperCase();
    }

    /* ----------------------------------------------------------------------
       4. Derived provincial figures
       ---------------------------------------------------------------------- */

    var GENERAL_CARE = ['General Practice', 'Maternal Health', 'Immunization', 'Laboratory'];

    function computeNetwork() {
        var reporting = FACILITIES.filter(function (f) { return f.status !== 'offline'; });
        var offline = FACILITIES.length - reporting.length;
        var limited = FACILITIES.filter(function (f) { return f.status === 'limited'; }).length;

        var specialists = {};
        FACILITIES.forEach(function (f) {
            f.specializations.forEach(function (s) {
                if (GENERAL_CARE.indexOf(s) === -1) { specialists[s] = true; }
            });
        });

        var activeReferrals = REFERRALS.filter(function (r) {
            return r.status === 'pending' || r.status === 'accepted' || r.status === 'in-transit';
        }).length;

        return {
            total: FACILITIES.length,
            reporting: reporting.length,
            offline: offline,
            limited: limited,
            emergency: FACILITIES.filter(function (f) { return f.emergency; }).length,
            doctors: FACILITIES.reduce(function (sum, f) { return sum + f.doctorsAvailable; }, 0),
            specialists: Object.keys(specialists).length,
            activeReferrals: activeReferrals,
            pendingReferrals: REFERRALS.filter(function (r) { return r.status === 'pending'; }).length,
            bedsAvailable: FACILITIES.reduce(function (sum, f) { return sum + f.bedsAvailable; }, 0),
            bedsTotal: FACILITIES.reduce(function (sum, f) { return sum + f.bedsTotal; }, 0)
        };
    }

    /* ----------------------------------------------------------------------
       5. Critical status strip
       ---------------------------------------------------------------------- */

    function renderStatusStrip(net) {
        var strip = document.getElementById('statusStrip');
        if (!strip) { return; }

        // Severity escalates only on real degradation, never on routine load.
        var severity = 'normal';
        var headline = 'All systems operational';

        if (net.offline > 0) {
            severity = 'warning';
            headline = net.offline + ' facility' + (net.offline === 1 ? '' : 's') + ' not reporting';
        }

        if (net.offline > 2) {
            severity = 'critical';
        }

        strip.setAttribute('data-severity', severity);

        var dot = strip.querySelector('.live-dot');
        if (dot) {
            dot.className = 'live-dot' +
                (severity === 'critical' ? ' live-dot-danger' : severity === 'warning' ? ' live-dot-warning' : '');
        }

        setText('[data-status-headline]', headline);
        setText('[data-status-reporting]', net.reporting + ' of ' + net.total);
        setText('[data-status-emergency]', String(net.emergency));
        setText('[data-status-updated]', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));
    }

    /* ----------------------------------------------------------------------
       6. Executive KPI cards
       ---------------------------------------------------------------------- */

    function renderKpis(net) {
        var cards = {
            hospitals: {
                value: net.total,
                unit: 'facilities',
                support: net.reporting + ' reporting across 17 municipalities',
                updated: 3,
                trend: { direction: 'flat', label: 'No change' }
            },
            doctors: {
                value: net.doctors,
                support: 'Currently on duty province-wide',
                updated: 5,
                trend: { direction: 'up', label: '+4 today' }
            },
            referrals: {
                value: net.activeReferrals,
                support: net.pendingReferrals + ' awaiting acceptance',
                updated: 2,
                trend: { direction: 'up', label: '+12%' }
            },
            specialists: {
                value: net.specialists,
                support: 'Distinct specialties offered',
                updated: 14,
                trend: { direction: 'down', label: '-2' }
            }
        };

        var icons = { up: 'fa-arrow-trend-up', down: 'fa-arrow-trend-down', flat: 'fa-minus' };

        Object.keys(cards).forEach(function (key) {
            var card = cards[key];

            setText('[data-kpi="' + key + '"]', card.value);
            setText('[data-kpi-support="' + key + '"]', card.support);
            setText('[data-kpi-time="' + key + '"]', formatRelativeTime(card.updated));

            if (card.unit) {
                setText('[data-kpi-unit="' + key + '"]', card.unit);
            }

            var trend = document.querySelector('[data-kpi-trend="' + key + '"]');
            if (trend) {
                trend.className = 'kpi-trend kpi-trend-' + card.trend.direction;
                trend.innerHTML = '<i class="fa-solid ' + icons[card.trend.direction] + '" aria-hidden="true"></i>' +
                    escapeHtml(card.trend.label);
            }
        });
    }

    /* ----------------------------------------------------------------------
       7. Executive summary panel
       ---------------------------------------------------------------------- */

    function renderExecutiveSummary(net) {
        var list = document.querySelector('[data-summary-list]');
        if (!list) { return; }

        function line(text, tone) {
            var toneClass = tone ? ' exec-summary-check-' + tone : '';
            var icon = tone === 'warning' ? 'fa-exclamation' : tone === 'danger' ? 'fa-xmark' : 'fa-check';

            return '<li class="exec-summary-item">' +
                '<span class="exec-summary-check' + toneClass + '" aria-hidden="true">' +
                    '<i class="fa-solid ' + icon + '"></i></span>' +
                '<span>' + text + '</span>' +
            '</li>';
        }

        var items = [
            line('<strong>' + net.reporting + '</strong> of ' + net.total + ' hospitals connected'),
            line('<strong>' + net.doctors + '</strong> doctors available'),
            line('<strong>' + net.activeReferrals + '</strong> active referrals'),
            line('<strong>' + net.emergency + '</strong> emergency-capable facilities')
        ];

        if (net.offline > 0) {
            items.push(line('<strong>' + net.offline + '</strong> facility not reporting', 'warning'));
        } else {
            items.push(line('No critical system alerts'));
        }

        if (net.limited > 0) {
            items.push(line('<strong>' + net.limited + '</strong> on limited connectivity', 'warning'));
        }

        list.innerHTML = items.join('');

        setText('[data-summary-date]', new Date().toLocaleDateString('en-PH', {
            weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
        }));

        var occupied = net.bedsTotal - net.bedsAvailable;
        var occupancyPct = net.bedsTotal > 0 ? Math.round((occupied / net.bedsTotal) * 100) : 0;

        setText('[data-summary-beds]', net.bedsAvailable);
        setText('[data-summary-beds-note]',
            'beds free of ' + net.bedsTotal + ' · ' + occupancyPct + '% occupied');
    }

    /* ----------------------------------------------------------------------
       8. Operational panel
       ---------------------------------------------------------------------- */

    function renderOverview(net) {
        setText('[data-status="reporting"]', net.reporting + ' of ' + net.total);
        setText('[data-status="lastSync"]', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));

        setText('[data-activity="created"]', OVERVIEW.activity.created);
        setText('[data-activity="accepted"]', OVERVIEW.activity.accepted);
        setText('[data-activity="patients"]', OVERVIEW.activity.patients);

        setText('[data-availability="available"]', OVERVIEW.availability.available);
        setText('[data-availability="onDuty"]', OVERVIEW.availability.onDuty);
        setText('[data-availability="unavailable"]', OVERVIEW.availability.unavailable);

        setText('[data-map-count]', net.total);

        // Proportional split of the on-duty roster.
        var bar = document.querySelector('[data-availability-bar]');
        if (bar) {
            var a = OVERVIEW.availability;
            var total = a.available + a.onDuty + a.unavailable;

            if (total > 0) {
                bar.innerHTML =
                    '<span class="share-bar-part share-bar-success" style="width:' +
                        ((a.available / total) * 100) + '%"></span>' +
                    '<span class="share-bar-part share-bar-warning" style="width:' +
                        ((a.onDuty / total) * 100) + '%"></span>' +
                    '<span class="share-bar-part share-bar-neutral" style="width:' +
                        ((a.unavailable / total) * 100) + '%"></span>';
            }
        }
    }

    /* ----------------------------------------------------------------------
       9. Tables
       ---------------------------------------------------------------------- */

    function renderReferrals() {
        var body = document.querySelector('[data-table="referrals"]');
        if (!body) { return; }

        body.innerHTML = REFERRALS.map(function (item) {
            var origin = facilityById(item.origin);
            var destination = facilityById(item.destination);

            return '<tr>' +
                '<td class="cell-mono">' + escapeHtml(item.reference) + '</td>' +
                '<td>' +
                    '<span class="route-cell">' +
                        '<span class="cell-muted">' + escapeHtml(origin ? origin.name : item.origin) + '</span>' +
                        '<i class="fa-solid fa-arrow-right route-arrow" aria-hidden="true"></i>' +
                        '<span class="cell-strong">' +
                            escapeHtml(destination ? destination.name : item.destination) + '</span>' +
                    '</span>' +
                '</td>' +
                '<td>' + badge(REFERRAL_STATUS_META[item.status]) + '</td>' +
                '<td class="cell-muted rhc-numeric">' + escapeHtml(item.time) + '</td>' +
            '</tr>';
        }).join('');
    }

    function renderDoctors() {
        var body = document.querySelector('[data-table="doctors"]');
        if (!body) { return; }

        body.innerHTML = DOCTORS.map(function (doctor) {
            return '<tr>' +
                '<td>' +
                    '<span class="cell-person">' +
                        '<span class="rhc-avatar rhc-avatar-sm rhc-avatar-neutral" aria-hidden="true">' +
                            escapeHtml(initials(doctor.name)) + '</span>' +
                        '<span class="cell-stack">' +
                            '<span class="cell-strong">' + escapeHtml(doctor.name) + '</span>' +
                            '<span class="cell-stack-sub">' + escapeHtml(doctor.specialty) + '</span>' +
                        '</span>' +
                    '</span>' +
                '</td>' +
                '<td class="cell-muted">' + escapeHtml(doctor.hospital) + '</td>' +
                '<td>' + badge(DOCTOR_STATUS_META[doctor.availability]) + '</td>' +
            '</tr>';
        }).join('');
    }

    function renderFacilities() {
        var body = document.querySelector('[data-table="facilities"]');
        if (!body) { return; }

        body.innerHTML = FACILITIES.map(function (facility) {
            var percent = facility.bedsTotal > 0
                ? Math.round((facility.bedsAvailable / facility.bedsTotal) * 100)
                : 0;

            var emergency = facility.emergency
                ? '<span class="rhc-badge rhc-badge-success">24/7</span>'
                : '<span class="rhc-badge rhc-badge-neutral">None</span>';

            return '<tr>' +
                '<td>' +
                    '<span class="cell-stack">' +
                        '<span class="cell-strong">' + escapeHtml(facility.name) + '</span>' +
                        '<span class="cell-stack-sub">' + escapeHtml(facility.municipality) + '</span>' +
                    '</span>' +
                '</td>' +
                '<td>' + badge(STATUS_META[facility.status]) + '</td>' +
                '<td>' + emergency + '</td>' +
                '<td>' +
                    '<span class="bed-meter">' +
                        '<span class="bed-meter-track">' +
                            '<span class="bed-meter-fill ' +
                                capacityClass(facility.bedsAvailable, facility.bedsTotal, 'bed-meter-fill') +
                                '" style="width:' + percent + '%"></span>' +
                        '</span>' +
                        '<span class="bed-meter-text">' +
                            facility.bedsAvailable + '/' + facility.bedsTotal + '</span>' +
                    '</span>' +
                '</td>' +
            '</tr>';
        }).join('');
    }

    /* ----------------------------------------------------------------------
       10. Facility drawer
       ---------------------------------------------------------------------- */

    var drawer = { element: null, backdrop: null, lastFocused: null, currentId: null };

    function referralsForFacility(id) {
        return REFERRALS.filter(function (r) { return r.origin === id || r.destination === id; });
    }

    function fillDrawer(facility) {
        var statusMeta = STATUS_META[facility.status];

        setText('#drawerTitle', facility.name);
        setText('[data-drawer="municipality"]', facility.municipality + ' · ' + facility.typeLabel);
        setText('[data-drawer="facilityType"]', facility.typeLabel);
        setText('[data-drawer="municipalityDetail"]', facility.municipality);
        setText('[data-drawer="address"]', facility.address);
        setText('[data-drawer="contact"]', facility.contact);
        setText('[data-drawer="doctorsAvailable"]', facility.doctorsAvailable);
        setText('[data-drawer="specialistCount"]', facility.specializations.length);
        setText('[data-drawer="lastUpdated"]', formatRelativeTime(facility.updatedMinutesAgo));

        // Type mark
        var mark = document.querySelector('[data-drawer="typeMark"]');
        if (mark) {
            mark.className = 'drawer-type-mark drawer-type-' + facility.type;
            mark.innerHTML = '<i class="fa-solid ' + (TYPE_ICONS[facility.type] || 'fa-hospital') + '"></i>';
        }

        // Badges
        setHtml('[data-drawer="statusBadge"]',
            '<span class="rhc-badge ' + statusMeta.badge + '">' +
            '<i class="fa-solid ' + statusMeta.icon + '" aria-hidden="true"></i> ' +
            escapeHtml(statusMeta.label) + '</span>');

        setHtml('[data-drawer="emergencyBadge"]', facility.emergency
            ? '<span class="rhc-badge rhc-badge-danger">' +
              '<i class="fa-solid fa-truck-medical" aria-hidden="true"></i> Emergency capable</span>'
            : '<span class="rhc-badge rhc-badge-neutral">No emergency service</span>');

        setHtml('[data-drawer="typeBadge"]',
            '<span class="rhc-badge rhc-badge-neutral">' + escapeHtml(facility.typeLabel) + '</span>');

        // Bed occupancy
        var occupied = facility.bedsTotal - facility.bedsAvailable;
        var occupancyPct = facility.bedsTotal > 0 ? Math.round((occupied / facility.bedsTotal) * 100) : 0;
        var freePct = 100 - occupancyPct;

        setText('[data-drawer="bedsAvailable"]', facility.bedsAvailable);
        setText('[data-drawer="bedsTotal"]', 'of ' + facility.bedsTotal + ' beds free');
        setText('[data-drawer="occupancyPct"]', occupancyPct + '% occupied');
        setText('[data-drawer="occupancyNote"]',
            occupied + ' beds in use · updated ' + formatRelativeTime(facility.updatedMinutesAgo));

        var fill = document.querySelector('[data-drawer="occupancyFill"]');
        if (fill) {
            fill.className = 'occupancy-fill ' +
                capacityClass(facility.bedsAvailable, facility.bedsTotal, 'occupancy-fill');
            fill.style.width = freePct + '%';
        }

        // Specializations
        setHtml('[data-drawer="specializations"]', facility.specializations.map(function (spec) {
            return '<span class="rhc-chip">' + escapeHtml(spec) + '</span>';
        }).join(''));

        // Current referrals
        var related = referralsForFacility(facility.id);
        setHtml('[data-drawer="referrals"]', related.length === 0
            ? '<li class="activity-row"><span class="activity-label">No referrals today</span></li>'
            : related.slice(0, 4).map(function (r) {
                var meta = REFERRAL_STATUS_META[r.status];
                var direction = r.destination === facility.id ? 'Incoming' : 'Outgoing';
                var icon = r.destination === facility.id ? 'fa-arrow-down' : 'fa-arrow-up';

                return '<li class="activity-row">' +
                    '<span class="activity-icon" aria-hidden="true"><i class="fa-solid ' + icon + '"></i></span>' +
                    '<span class="cell-stack">' +
                        '<span class="cell-strong">' + escapeHtml(r.reference) + '</span>' +
                        '<span class="cell-stack-sub">' + direction + ' · ' + escapeHtml(r.time) + '</span>' +
                    '</span>' +
                    '<span class="activity-value">' + badge(meta) + '</span>' +
                '</li>';
            }).join(''));

        // Today's activity
        setHtml('[data-drawer="activity"]', [
            { icon: 'fa-user-group', label: 'Patients seen', value: facility.patientsToday },
            { icon: 'fa-bed-pulse', label: 'Admissions', value: facility.admissionsToday },
            { icon: 'fa-arrow-down', label: 'Referrals received', value: facility.incomingReferrals },
            { icon: 'fa-arrow-up', label: 'Referrals sent', value: facility.outgoingReferrals }
        ].map(function (row) {
            return '<li class="activity-row">' +
                '<span class="activity-icon" aria-hidden="true"><i class="fa-solid ' + row.icon + '"></i></span>' +
                '<span class="activity-label">' + row.label + '</span>' +
                '<span class="activity-value">' + row.value + '</span>' +
            '</li>';
        }).join(''));

        // Quick action targets
        var call = document.getElementById('drawerCall');
        if (call) {
            call.href = 'tel:' + facility.contact.replace(/[^0-9+]/g, '');
        }

        var directions = document.getElementById('drawerDirections');
        if (directions) {
            directions.href = 'https://www.openstreetmap.org/?mlat=' + facility.coordinates[1] +
                '&mlon=' + facility.coordinates[0] + '#map=15/' +
                facility.coordinates[1] + '/' + facility.coordinates[0];
        }
    }

    function openDrawer(facilityId) {
        var facility = facilityById(facilityId);
        if (!facility || !drawer.element) { return; }

        drawer.lastFocused = document.activeElement;
        drawer.currentId = facilityId;

        fillDrawer(facility);
        drawer.element.classList.add('is-open');
        drawer.backdrop.classList.add('is-open');
        drawer.element.focus();

        document.dispatchEvent(new CustomEvent('rhc:facility-selected', { detail: { id: facilityId } }));
    }

    function closeDrawer() {
        if (!drawer.element || !drawer.element.classList.contains('is-open')) { return; }

        drawer.element.classList.remove('is-open');
        drawer.backdrop.classList.remove('is-open');
        drawer.currentId = null;

        if (drawer.lastFocused && typeof drawer.lastFocused.focus === 'function') {
            drawer.lastFocused.focus();
        }

        document.dispatchEvent(new CustomEvent('rhc:facility-deselected'));
    }

    /** Keeps Tab cycling inside the drawer while it is open. */
    function trapFocus(event) {
        if (event.key !== 'Tab' || !drawer.element.classList.contains('is-open')) { return; }

        var focusable = drawer.element.querySelectorAll(
            'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (focusable.length === 0) { return; }

        var first = focusable[0];
        var last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    }

    function initDrawer() {
        drawer.element = document.getElementById('hospitalDrawer');
        drawer.backdrop = document.getElementById('drawerBackdrop');
        if (!drawer.element || !drawer.backdrop) { return; }

        var closeButton = document.getElementById('drawerClose');
        if (closeButton) { closeButton.addEventListener('click', closeDrawer); }

        drawer.backdrop.addEventListener('click', closeDrawer);

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') { closeDrawer(); }
            trapFocus(event);
        });

        var startReferral = document.getElementById('drawerStartReferral');
        if (startReferral) {
            startReferral.addEventListener('click', function () {
                window.location.href = '/Referrals/Create';
            });
        }

        var viewDetails = document.getElementById('drawerViewDetails');
        if (viewDetails) {
            viewDetails.addEventListener('click', function () {
                var facility = facilityById(drawer.currentId);
                window.location.href = facility
                    ? '/Referrals?SearchTerm=' + encodeURIComponent(facility.name)
                    : '/Referrals';
            });
        }
    }

    /* ----------------------------------------------------------------------
       11. Shell behaviour
       ---------------------------------------------------------------------- */

    var SIDEBAR_STORAGE_KEY = 'rhc.sidebar';

    function initShell() {
        var shell = document.getElementById('appShell');
        var toggle = document.getElementById('sidebarToggle');
        var mobileToggle = document.getElementById('mobileNavToggle');
        var backdrop = document.getElementById('sidebarBackdrop');
        if (!shell) { return; }

        var stored = null;
        try {
            stored = window.localStorage.getItem(SIDEBAR_STORAGE_KEY);
        } catch (error) {
            stored = null;
        }

        if (stored === 'collapsed') {
            shell.setAttribute('data-sidebar', 'collapsed');
            if (toggle) {
                toggle.setAttribute('aria-expanded', 'false');
                var storedLabel = toggle.querySelector('.sidebar-toggle-label');
                if (storedLabel) { storedLabel.textContent = 'Expand'; }
            }
        }

        if (toggle) {
            toggle.addEventListener('click', function () {
                var collapsed = shell.getAttribute('data-sidebar') === 'collapsed';
                var next = collapsed ? 'expanded' : 'collapsed';

                shell.setAttribute('data-sidebar', next);
                toggle.setAttribute('aria-expanded', String(collapsed));

                var label = toggle.querySelector('.sidebar-toggle-label');
                if (label) { label.textContent = collapsed ? 'Collapse' : 'Expand'; }

                try {
                    window.localStorage.setItem(SIDEBAR_STORAGE_KEY, next);
                } catch (error) {
                    /* Storage unavailable — preference is simply not persisted. */
                }

                window.setTimeout(function () {
                    document.dispatchEvent(new CustomEvent('rhc:layout-changed'));
                }, 260);
            });
        }

        function setMobileNav(open) {
            shell.setAttribute('data-mobile-nav', open ? 'open' : 'closed');
            if (mobileToggle) { mobileToggle.setAttribute('aria-expanded', String(open)); }
        }

        if (mobileToggle) {
            mobileToggle.addEventListener('click', function () {
                setMobileNav(shell.getAttribute('data-mobile-nav') !== 'open');
            });
        }

        if (backdrop) {
            backdrop.addEventListener('click', function () { setMobileNav(false); });
        }

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') { setMobileNav(false); }
        });

        shell.querySelectorAll('.sidebar-link').forEach(function (link) {
            link.addEventListener('click', function () {
                if (window.matchMedia('(max-width: 860px)').matches) { setMobileNav(false); }
            });
        });
    }

    function initHeaderClock() {
        setText('#currentDate', new Date().toLocaleDateString('en-PH', {
            weekday: 'short', year: 'numeric', month: 'short', day: 'numeric'
        }));

        setText('#lastSyncLabel', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));

        // Keeps the "synced N minutes ago" label honest without polling a server.
        window.setInterval(function () {
            OVERVIEW.lastSyncMinutesAgo += 1;
            setText('#lastSyncLabel', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));
            setText('[data-status-updated]', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));
            setText('[data-status="lastSync"]', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));
        }, 60000);
    }

    /* ----------------------------------------------------------------------
       12. Public API
       ---------------------------------------------------------------------- */

    RHC.data = {
        facilities: FACILITIES,
        referrals: REFERRALS,
        doctors: DOCTORS,
        overview: OVERVIEW
    };

    RHC.getFacility = facilityById;
    RHC.openFacility = openDrawer;
    RHC.closeFacility = closeDrawer;
    RHC.formatRelativeTime = formatRelativeTime;
    RHC.escapeHtml = escapeHtml;

    /* ----------------------------------------------------------------------
       13. Bootstrap
       ---------------------------------------------------------------------- */

    /** Paints every data-driven region from the current snapshot. */
    function renderAll() {
        var net = computeNetwork();

        renderStatusStrip(net);
        renderKpis(net);
        renderExecutiveSummary(net);
        renderOverview(net);
        renderReferrals();
        renderDoctors();
        renderFacilities();
    }

    function showEmptyNetwork() {
        var summary = document.querySelector('[data-summary-list]');
        if (summary) {
            summary.innerHTML =
                '<li class="exec-summary-item">' +
                    '<span class="exec-summary-check exec-summary-check-warning" aria-hidden="true">' +
                        '<i class="fa-solid fa-exclamation"></i></span>' +
                    '<span>No facilities registered yet. ' +
                        '<a href="/Hospitals/Create">Add the first facility</a> to populate the map.</span>' +
                '</li>';
        }

        setText('[data-status-headline]', 'No facilities registered');
    }

    function init() {
        // Chrome first, so the shell is usable while the data request is in flight.
        initShell();
        initHeaderClock();
        initDrawer();

        // Other modules await this before touching RHC.data.
        RHC.dataReady = loadNetwork().then(function (loaded) {
            renderAll();

            if (loaded && FACILITIES.length === 0) {
                showEmptyNetwork();
            }

            document.dispatchEvent(new CustomEvent('rhc:ready'));
            return loaded;
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
