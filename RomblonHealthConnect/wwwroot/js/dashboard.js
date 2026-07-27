/* ==========================================================================
   Romblon HealthConnect — Dashboard
   Prototype mock data, view rendering, shell behaviour, and the facility drawer.
   Exposes window.RHC so health-map.js can share the same facility records.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};

    /* ----------------------------------------------------------------------
       1. Mock data — illustrative only, not real health records
       ---------------------------------------------------------------------- */

    // Coordinates are [longitude, latitude] to match MapLibre's ordering.
    var FACILITIES = [
        {
            id: 'rph-romblon',
            name: 'Romblon Provincial Hospital',
            type: 'public',
            typeLabel: 'Provincial Hospital',
            municipality: 'Romblon',
            address: 'Barangay Capaclan, Romblon, Romblon',
            contact: '(042) 567 1234',
            coordinates: [122.2708, 12.5764],
            status: 'online',
            emergency: true,
            doctorsAvailable: 14,
            bedsAvailable: 32,
            bedsTotal: 120,
            specializations: ['Internal Medicine', 'Surgery', 'Pediatrics', 'Obstetrics', 'Anesthesiology', 'Radiology'],
            updatedMinutesAgo: 3
        },
        {
            id: 'tidh-odiongan',
            name: 'Tablas Island District Hospital',
            type: 'district',
            typeLabel: 'District Hospital',
            municipality: 'Odiongan',
            address: 'Barangay Dapawan, Odiongan, Romblon',
            contact: '(042) 567 5580',
            coordinates: [121.9889, 12.4003],
            status: 'online',
            emergency: true,
            doctorsAvailable: 9,
            bedsAvailable: 18,
            bedsTotal: 75,
            specializations: ['Internal Medicine', 'Surgery', 'Pediatrics', 'Obstetrics'],
            updatedMinutesAgo: 6
        },
        {
            id: 'rdh-romblon',
            name: 'Romblon District Hospital',
            type: 'district',
            typeLabel: 'District Hospital',
            municipality: 'Romblon',
            address: 'Barangay Bagacay, Romblon, Romblon',
            contact: '(042) 567 2210',
            coordinates: [122.2609, 12.5698],
            status: 'online',
            emergency: true,
            doctorsAvailable: 6,
            bedsAvailable: 11,
            bedsTotal: 50,
            specializations: ['Internal Medicine', 'Pediatrics', 'General Practice'],
            updatedMinutesAgo: 11
        },
        {
            id: 'adh-alcantara',
            name: 'Alcantara District Hospital',
            type: 'district',
            typeLabel: 'District Hospital',
            municipality: 'Alcantara',
            address: 'Poblacion, Alcantara, Romblon',
            contact: '(042) 567 3341',
            coordinates: [122.0667, 12.2333],
            status: 'online',
            emergency: true,
            doctorsAvailable: 5,
            bedsAvailable: 4,
            bedsTotal: 40,
            specializations: ['Internal Medicine', 'General Practice', 'Obstetrics'],
            updatedMinutesAgo: 8
        },
        {
            id: 'cdh-cajidiocan',
            name: 'Cajidiocan District Hospital',
            type: 'district',
            typeLabel: 'District Hospital',
            municipality: 'Cajidiocan',
            address: 'Poblacion, Cajidiocan, Sibuyan Island',
            contact: '(042) 567 4419',
            coordinates: [122.5308, 12.4394],
            status: 'limited',
            emergency: true,
            doctorsAvailable: 3,
            bedsAvailable: 7,
            bedsTotal: 35,
            specializations: ['General Practice', 'Pediatrics'],
            updatedMinutesAgo: 24
        },
        {
            id: 'sfdh-sanfernando',
            name: 'San Fernando District Hospital',
            type: 'district',
            typeLabel: 'District Hospital',
            municipality: 'San Fernando',
            address: 'Poblacion, San Fernando, Sibuyan Island',
            contact: '(042) 567 4802',
            coordinates: [122.5461, 12.3175],
            status: 'online',
            emergency: true,
            doctorsAvailable: 4,
            bedsAvailable: 9,
            bedsTotal: 30,
            specializations: ['General Practice', 'Internal Medicine'],
            updatedMinutesAgo: 15
        },
        {
            id: 'rhu-sanagustin',
            name: 'San Agustin Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'San Agustin',
            address: 'Poblacion, San Agustin, Tablas Island',
            contact: '(042) 567 6120',
            coordinates: [122.1333, 12.6167],
            status: 'online',
            emergency: false,
            doctorsAvailable: 2,
            bedsAvailable: 6,
            bedsTotal: 12,
            specializations: ['General Practice', 'Maternal Health'],
            updatedMinutesAgo: 19
        },
        {
            id: 'rhu-sanandres',
            name: 'San Andres Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'San Andres',
            address: 'Poblacion, San Andres, Tablas Island',
            contact: '(042) 567 6255',
            coordinates: [122.0333, 12.5167],
            status: 'online',
            emergency: false,
            doctorsAvailable: 2,
            bedsAvailable: 5,
            bedsTotal: 10,
            specializations: ['General Practice', 'Immunization'],
            updatedMinutesAgo: 27
        },
        {
            id: 'rhu-odiongan',
            name: 'Odiongan Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'Odiongan',
            address: 'Poblacion, Odiongan, Tablas Island',
            contact: '(042) 567 5612',
            coordinates: [121.9975, 12.4118],
            status: 'online',
            emergency: false,
            doctorsAvailable: 3,
            bedsAvailable: 8,
            bedsTotal: 15,
            specializations: ['General Practice', 'Maternal Health', 'Immunization'],
            updatedMinutesAgo: 5
        },
        {
            id: 'rhu-magdiwang',
            name: 'Magdiwang Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'Magdiwang',
            address: 'Poblacion, Magdiwang, Sibuyan Island',
            contact: '(042) 567 4703',
            coordinates: [122.5217, 12.4972],
            status: 'online',
            emergency: false,
            doctorsAvailable: 2,
            bedsAvailable: 4,
            bedsTotal: 10,
            specializations: ['General Practice', 'Maternal Health'],
            updatedMinutesAgo: 31
        },
        {
            id: 'rhu-looc',
            name: 'Looc Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'Looc',
            address: 'Poblacion, Looc, Tablas Island',
            contact: '(042) 567 5934',
            coordinates: [121.9944, 12.2611],
            status: 'online',
            emergency: false,
            doctorsAvailable: 2,
            bedsAvailable: 3,
            bedsTotal: 10,
            specializations: ['General Practice'],
            updatedMinutesAgo: 22
        },
        {
            id: 'rhu-santafe',
            name: 'Santa Fe Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'Santa Fe',
            address: 'Poblacion, Santa Fe, Tablas Island',
            contact: '(042) 567 5177',
            coordinates: [122.0333, 12.1500],
            status: 'offline',
            emergency: false,
            doctorsAvailable: 0,
            bedsAvailable: 2,
            bedsTotal: 8,
            specializations: ['General Practice'],
            updatedMinutesAgo: 96
        },
        {
            id: 'rhu-corcuera',
            name: 'Corcuera Rural Health Unit',
            type: 'rhu',
            typeLabel: 'Rural Health Unit',
            municipality: 'Corcuera',
            address: 'Poblacion, Corcuera, Simara Island',
            contact: '(042) 567 6488',
            coordinates: [122.1667, 12.6333],
            status: 'limited',
            emergency: false,
            doctorsAvailable: 1,
            bedsAvailable: 3,
            bedsTotal: 8,
            specializations: ['General Practice'],
            updatedMinutesAgo: 44
        },
        {
            id: 'tmc-odiongan',
            name: 'Tablas Medical Center',
            type: 'private',
            typeLabel: 'Private Hospital',
            municipality: 'Odiongan',
            address: 'Barangay Liwayway, Odiongan, Romblon',
            contact: '(042) 567 5900',
            coordinates: [121.9820, 12.3946],
            status: 'online',
            emergency: true,
            doctorsAvailable: 7,
            bedsAvailable: 14,
            bedsTotal: 45,
            specializations: ['Internal Medicine', 'Surgery', 'Cardiology', 'Radiology'],
            updatedMinutesAgo: 9
        },
        {
            id: 'shmc-romblon',
            name: 'Sacred Heart Medical Clinic',
            type: 'private',
            typeLabel: 'Private Clinic',
            municipality: 'Romblon',
            address: 'Barangay Ilauran, Romblon, Romblon',
            contact: '(042) 567 2077',
            coordinates: [122.2782, 12.5821],
            status: 'online',
            emergency: false,
            doctorsAvailable: 3,
            bedsAvailable: 5,
            bedsTotal: 12,
            specializations: ['General Practice', 'Dermatology', 'Laboratory'],
            updatedMinutesAgo: 13
        }
    ];

    var REFERRALS = [
        { reference: 'RF-2026-0418', origin: 'Looc RHU', destination: 'Tablas Island District Hospital', status: 'accepted', time: '09:42' },
        { reference: 'RF-2026-0417', origin: 'Magdiwang RHU', destination: 'Cajidiocan District Hospital', status: 'in-transit', time: '09:15' },
        { reference: 'RF-2026-0416', origin: 'Alcantara District Hospital', destination: 'Romblon Provincial Hospital', status: 'pending', time: '08:57' },
        { reference: 'RF-2026-0415', origin: 'San Andres RHU', destination: 'Tablas Island District Hospital', status: 'accepted', time: '08:30' },
        { reference: 'RF-2026-0414', origin: 'Corcuera RHU', destination: 'Romblon Provincial Hospital', status: 'in-transit', time: '08:04' },
        { reference: 'RF-2026-0413', origin: 'San Fernando District Hospital', destination: 'Romblon Provincial Hospital', status: 'completed', time: '07:38' },
        { reference: 'RF-2026-0412', origin: 'Odiongan RHU', destination: 'Tablas Medical Center', status: 'completed', time: '07:12' },
        { reference: 'RF-2026-0411', origin: 'San Agustin RHU', destination: 'Romblon District Hospital', status: 'declined', time: '06:55' }
    ];

    var DOCTORS = [
        { name: 'Dr. M. Fabreag', specialty: 'Internal Medicine', hospital: 'Romblon Provincial Hospital', availability: 'available' },
        { name: 'Dr. R. Fadri', specialty: 'General Surgery', hospital: 'Romblon Provincial Hospital', availability: 'in-surgery' },
        { name: 'Dr. L. Mindoro', specialty: 'Pediatrics', hospital: 'Tablas Island District Hospital', availability: 'available' },
        { name: 'Dr. A. Gaa', specialty: 'Obstetrics', hospital: 'Tablas Island District Hospital', availability: 'available' },
        { name: 'Dr. C. Rioflorido', specialty: 'Cardiology', hospital: 'Tablas Medical Center', availability: 'on-call' },
        { name: 'Dr. P. Musico', specialty: 'General Practice', hospital: 'Alcantara District Hospital', availability: 'available' },
        { name: 'Dr. E. Solidum', specialty: 'Anesthesiology', hospital: 'Romblon Provincial Hospital', availability: 'on-call' },
        { name: 'Dr. V. Faigao', specialty: 'General Practice', hospital: 'San Fernando District Hospital', availability: 'off-duty' }
    ];

    var OVERVIEW = {
        lastSyncMinutesAgo: 2,
        activity: { created: 18, accepted: 14, patients: 63 },
        availability: { available: 26, onDuty: 41, unavailable: 12 }
    };

    /* ----------------------------------------------------------------------
       2. Labels and badge mappings
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

    /* ----------------------------------------------------------------------
       3. Helpers
       ---------------------------------------------------------------------- */

    function escapeHtml(value) {
        return String(value)
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
        if (!meta) { return ''; }
        return '<span class="rhc-badge ' + meta.badge + '">' + escapeHtml(meta.label) + '</span>';
    }

    function setText(selector, value) {
        var node = document.querySelector(selector);
        if (node) { node.textContent = value; }
    }

    /** Bed occupancy drives the meter colour: green > 25%, amber > 10%, else red. */
    function bedMeterClass(available, total) {
        var ratio = total > 0 ? available / total : 0;
        if (ratio > 0.25) { return ''; }
        if (ratio > 0.1) { return 'bed-meter-fill-warning'; }
        return 'bed-meter-fill-danger';
    }

    /* ----------------------------------------------------------------------
       4. Rendering
       ---------------------------------------------------------------------- */

    function renderMetrics() {
        var totalDoctors = FACILITIES.reduce(function (sum, f) { return sum + f.doctorsAvailable; }, 0);
        var activeReferrals = REFERRALS.filter(function (r) {
            return r.status === 'pending' || r.status === 'accepted' || r.status === 'in-transit';
        }).length;

        // Count distinct specialisations that are not general/primary care.
        var generalCare = ['General Practice', 'Maternal Health', 'Immunization', 'Laboratory'];
        var specialists = {};
        FACILITIES.forEach(function (f) {
            f.specializations.forEach(function (s) {
                if (generalCare.indexOf(s) === -1) { specialists[s] = true; }
            });
        });

        var metrics = {
            totalHospitals: {
                value: FACILITIES.length,
                caption: 'Registered across the province',
                trend: { direction: 'flat', label: 'No change' }
            },
            availableDoctors: {
                value: totalDoctors,
                caption: 'On duty across all facilities',
                trend: { direction: 'up', label: '+4' }
            },
            activeReferrals: {
                value: activeReferrals,
                caption: 'Awaiting or in progress',
                trend: { direction: 'up', label: '+12%' }
            },
            specialists: {
                value: Object.keys(specialists).length,
                caption: 'Distinct specialties offered',
                trend: { direction: 'down', label: '-2' }
            }
        };

        Object.keys(metrics).forEach(function (key) {
            var metric = metrics[key];
            setText('[data-metric="' + key + '"]', metric.value);
            setText('[data-metric-caption="' + key + '"]', metric.caption);

            var trendNode = document.querySelector('[data-metric-trend="' + key + '"]');
            if (!trendNode) { return; }

            var icons = { up: 'fa-arrow-trend-up', down: 'fa-arrow-trend-down', flat: 'fa-minus' };
            trendNode.className = 'metric-trend metric-trend-' + metric.trend.direction;
            trendNode.innerHTML = '<i class="fa-solid ' + icons[metric.trend.direction] + '" aria-hidden="true"></i>' +
                escapeHtml(metric.trend.label);
        });
    }

    function renderOverview() {
        var reporting = FACILITIES.filter(function (f) { return f.status !== 'offline'; }).length;

        setText('[data-status="reporting"]', reporting + ' of ' + FACILITIES.length);
        setText('[data-status="lastSync"]', formatRelativeTime(OVERVIEW.lastSyncMinutesAgo));

        setText('[data-activity="created"]', OVERVIEW.activity.created);
        setText('[data-activity="accepted"]', OVERVIEW.activity.accepted);
        setText('[data-activity="patients"]', OVERVIEW.activity.patients);

        setText('[data-availability="available"]', OVERVIEW.availability.available);
        setText('[data-availability="onDuty"]', OVERVIEW.availability.onDuty);
        setText('[data-availability="unavailable"]', OVERVIEW.availability.unavailable);

        setText('[data-map-count]', FACILITIES.length);
    }

    function renderReferrals() {
        var body = document.querySelector('[data-table="referrals"]');
        if (!body) { return; }

        body.innerHTML = REFERRALS.map(function (item) {
            return '<tr>' +
                '<td class="cell-mono">' + escapeHtml(item.reference) + '</td>' +
                '<td>' +
                    '<span class="route-cell">' +
                        '<span class="cell-muted">' + escapeHtml(item.origin) + '</span>' +
                        '<i class="fa-solid fa-arrow-right route-arrow" aria-hidden="true"></i>' +
                        '<span class="cell-strong">' + escapeHtml(item.destination) + '</span>' +
                    '</span>' +
                '</td>' +
                '<td>' + badge(REFERRAL_STATUS_META[item.status]) + '</td>' +
                '<td class="cell-muted">' + escapeHtml(item.time) + '</td>' +
            '</tr>';
        }).join('');
    }

    function renderDoctors() {
        var body = document.querySelector('[data-table="doctors"]');
        if (!body) { return; }

        body.innerHTML = DOCTORS.map(function (doctor) {
            return '<tr>' +
                '<td>' +
                    '<span class="cell-stack">' +
                        '<span class="cell-strong">' + escapeHtml(doctor.name) + '</span>' +
                        '<span class="cell-stack-sub">' + escapeHtml(doctor.specialty) + '</span>' +
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
                            '<span class="bed-meter-fill ' + bedMeterClass(facility.bedsAvailable, facility.bedsTotal) + '" ' +
                                  'style="width:' + percent + '%"></span>' +
                        '</span>' +
                        '<span class="bed-meter-text">' + facility.bedsAvailable + '/' + facility.bedsTotal + '</span>' +
                    '</span>' +
                '</td>' +
            '</tr>';
        }).join('');
    }

    /* ----------------------------------------------------------------------
       5. Facility details drawer
       ---------------------------------------------------------------------- */

    var drawer = {
        element: null,
        backdrop: null,
        lastFocused: null,
        currentId: null
    };

    function fillDrawer(facility) {
        var statusMeta = STATUS_META[facility.status];

        setText('#drawerTitle', facility.name);
        setText('[data-drawer="municipality"]', facility.municipality + ' · ' + facility.typeLabel);
        setText('[data-drawer="facilityType"]', facility.typeLabel);
        setText('[data-drawer="municipalityDetail"]', facility.municipality);
        setText('[data-drawer="address"]', facility.address);
        setText('[data-drawer="contact"]', facility.contact);
        setText('[data-drawer="doctorsAvailable"]', facility.doctorsAvailable);
        setText('[data-drawer="bedsAvailable"]', facility.bedsAvailable + ' / ' + facility.bedsTotal);
        setText('[data-drawer="lastUpdated"]', formatRelativeTime(facility.updatedMinutesAgo));

        var statusNode = document.querySelector('[data-drawer="statusBadge"]');
        if (statusNode) {
            statusNode.innerHTML = '<span class="rhc-badge ' + statusMeta.badge + '">' +
                '<i class="fa-solid ' + statusMeta.icon + '" aria-hidden="true"></i> ' +
                escapeHtml(statusMeta.label) + '</span>';
        }

        var emergencyNode = document.querySelector('[data-drawer="emergencyBadge"]');
        if (emergencyNode) {
            emergencyNode.innerHTML = facility.emergency
                ? '<span class="rhc-badge rhc-badge-info"><i class="fa-solid fa-truck-medical" aria-hidden="true"></i> Emergency capable</span>'
                : '<span class="rhc-badge rhc-badge-neutral">No emergency service</span>';
        }

        var specNode = document.querySelector('[data-drawer="specializations"]');
        if (specNode) {
            specNode.innerHTML = facility.specializations.map(function (spec) {
                return '<span class="rhc-chip">' + escapeHtml(spec) + '</span>';
            }).join('');
        }
    }

    function openDrawer(facilityId) {
        var facility = RHC.getFacility(facilityId);
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

        var focusable = drawer.element.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
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

        // Prototype actions — wired to real routes in a later phase.
        var viewDetails = document.getElementById('drawerViewDetails');
        var startReferral = document.getElementById('drawerStartReferral');

        if (viewDetails) {
            viewDetails.addEventListener('click', function () {
                window.console.info('[prototype] View details for facility:', drawer.currentId);
            });
        }
        if (startReferral) {
            startReferral.addEventListener('click', function () {
                window.console.info('[prototype] Start referral from facility:', drawer.currentId);
            });
        }
    }

    /* ----------------------------------------------------------------------
       6. Shell behaviour
       ---------------------------------------------------------------------- */

    var SIDEBAR_STORAGE_KEY = 'rhc.sidebar';

    function initShell() {
        var shell = document.getElementById('appShell');
        var toggle = document.getElementById('sidebarToggle');
        var mobileToggle = document.getElementById('mobileNavToggle');
        var backdrop = document.getElementById('sidebarBackdrop');
        if (!shell) { return; }

        // Restore the collapsed preference from the previous visit.
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
                    /* Storage unavailable (private mode) — preference is simply not persisted. */
                }

                // The map needs to re-measure once the rail finishes animating.
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

        // Close the mobile drawer after navigating.
        shell.querySelectorAll('.sidebar-link').forEach(function (link) {
            link.addEventListener('click', function () {
                if (window.matchMedia('(max-width: 860px)').matches) { setMobileNav(false); }
            });
        });
    }

    function initDate() {
        var node = document.getElementById('currentDate');
        if (!node) { return; }

        node.textContent = new Date().toLocaleDateString('en-PH', {
            weekday: 'short', year: 'numeric', month: 'short', day: 'numeric'
        });
    }

    /* ----------------------------------------------------------------------
       7. Search — filters the visible table rows
       ---------------------------------------------------------------------- */

    function initSearch() {
        var input = document.getElementById('globalSearch');
        if (!input) { return; }

        input.addEventListener('input', function () {
            var term = input.value.trim().toLowerCase();

            document.querySelectorAll('.rhc-table tbody tr').forEach(function (row) {
                var matches = term === '' || row.textContent.toLowerCase().indexOf(term) !== -1;
                row.style.display = matches ? '' : 'none';
            });

            document.dispatchEvent(new CustomEvent('rhc:search', { detail: { term: term } }));
        });
    }

    /* ----------------------------------------------------------------------
       8. Public API
       ---------------------------------------------------------------------- */

    RHC.data = {
        facilities: FACILITIES,
        referrals: REFERRALS,
        doctors: DOCTORS,
        overview: OVERVIEW
    };

    RHC.getFacility = function (id) {
        return FACILITIES.filter(function (facility) { return facility.id === id; })[0] || null;
    };

    RHC.openFacility = openDrawer;
    RHC.closeFacility = closeDrawer;
    RHC.formatRelativeTime = formatRelativeTime;
    RHC.escapeHtml = escapeHtml;

    /* ----------------------------------------------------------------------
       9. Bootstrap
       ---------------------------------------------------------------------- */

    function init() {
        initShell();
        initDate();
        initDrawer();
        initSearch();

        renderMetrics();
        renderOverview();
        renderReferrals();
        renderDoctors();
        renderFacilities();

        document.dispatchEvent(new CustomEvent('rhc:ready'));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
