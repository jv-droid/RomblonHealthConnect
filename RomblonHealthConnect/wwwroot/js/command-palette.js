/* ==========================================================================
   Romblon HealthConnect — Global command palette
   Ctrl/Cmd + K opens a centred search across navigation, facilities, staff,
   patients, referrals, municipalities, and specializations.

   Static navigation entries are built in; live records are fetched from the
   referral module's existing JSON endpoints and from whatever facility data
   the current page has already published on window.RHC.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};

    var MAX_PER_GROUP = 5;
    var SEARCH_DEBOUNCE = 180;

    var palette = {
        root: null,
        backdrop: null,
        input: null,
        results: null,
        open: false,
        activeIndex: 0,
        items: [],
        lastFocused: null,
        searchTimer: null
    };

    /* ----------------------------------------------------------------------
       1. Static commands
       ---------------------------------------------------------------------- */

    var NAVIGATION = [
        { title: 'Provincial Dashboard', meta: 'Map and network overview', icon: 'fa-map', url: '/' },
        { title: 'Referral Overview', meta: 'Referral metrics and table', icon: 'fa-chart-bar', url: '/Referrals' },
        { title: 'Create Referral', meta: 'Start a new patient transfer', icon: 'fa-plus', url: '/Referrals/Create' },
        { title: 'Incoming Referrals', meta: 'Awaiting this facility', icon: 'fa-inbox', url: '/Referrals/Incoming' },
        { title: 'Outgoing Referrals', meta: 'Sent by this facility', icon: 'fa-paper-plane', url: '/Referrals/Outgoing' },
        { title: 'Pending Referrals', meta: 'Still in progress', icon: 'fa-hourglass-half', url: '/Referrals/Pending' },
        { title: 'Completed Referrals', meta: 'Closed transfers', icon: 'fa-circle-check', url: '/Referrals/Completed' },
        { title: 'Referral Archive', meta: 'Historical records', icon: 'fa-folder-open', url: '/Referrals/Archive' }
    ];

    var ACTIONS = [
        {
            title: 'Switch to Executive view',
            meta: 'Presentation layout for leadership',
            icon: 'fa-chart-pie',
            run: function () { if (RHC.workspace) { RHC.workspace.set('executive'); } }
        },
        {
            title: 'Switch to Operations view',
            meta: 'Detailed layout for daily work',
            icon: 'fa-list-check',
            run: function () { if (RHC.workspace) { RHC.workspace.set('operations'); } }
        }
    ];

    /* ----------------------------------------------------------------------
       2. Helpers
       ---------------------------------------------------------------------- */

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    /** Highlights the matched substring inside an already-escaped title. */
    function highlight(text, term) {
        var safe = escapeHtml(text);
        if (!term) { return safe; }

        var index = text.toLowerCase().indexOf(term.toLowerCase());
        if (index === -1) { return safe; }

        return escapeHtml(text.slice(0, index)) +
            '<mark class="palette-match">' + escapeHtml(text.slice(index, index + term.length)) + '</mark>' +
            escapeHtml(text.slice(index + term.length));
    }

    function matches(text, term) {
        return String(text || '').toLowerCase().indexOf(term) !== -1;
    }

    async function getJson(url) {
        var response = await fetch(url, {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        });

        if (!response.ok) { throw new Error('Request failed: ' + response.status); }
        return response.json();
    }

    /* ----------------------------------------------------------------------
       3. Result sources
       ---------------------------------------------------------------------- */

    /** Facilities published by the current page (dashboard or wizard). */
    function facilityResults(term) {
        var facilities = (RHC.data && RHC.data.facilities) || [];

        return facilities
            .filter(function (f) {
                return matches(f.name, term) || matches(f.municipality, term) || matches(f.typeLabel, term);
            })
            .slice(0, MAX_PER_GROUP)
            .map(function (f) {
                return {
                    title: f.name,
                    meta: (f.typeLabel || 'Facility') + ' · ' + f.municipality,
                    icon: 'fa-hospital',
                    run: function () {
                        // On the dashboard this flies the map and opens the drawer.
                        if (RHC.map && typeof RHC.map.select === 'function') {
                            RHC.map.select(f.id, true);
                        } else if (typeof RHC.openFacility === 'function') {
                            RHC.openFacility(f.id);
                        } else {
                            window.location.href = '/';
                        }
                    }
                };
            });
    }

    /** Distinct municipalities drawn from the same facility data. */
    function municipalityResults(term) {
        var facilities = (RHC.data && RHC.data.facilities) || [];
        var seen = {};
        var out = [];

        facilities.forEach(function (f) {
            if (!f.municipality || seen[f.municipality] || !matches(f.municipality, term)) { return; }

            seen[f.municipality] = true;
            out.push({
                title: f.municipality,
                meta: 'Municipality',
                icon: 'fa-location-dot',
                run: function () {
                    if (RHC.map && typeof RHC.map.select === 'function') {
                        RHC.map.select(f.id, true);
                    }
                }
            });
        });

        return out.slice(0, MAX_PER_GROUP);
    }

    function staticResults(list, term) {
        return list
            .filter(function (item) { return matches(item.title, term) || matches(item.meta, term); })
            .slice(0, MAX_PER_GROUP);
    }

    async function patientResults(term) {
        try {
            var patients = await getJson('/Referrals/SearchPatients?term=' + encodeURIComponent(term));

            return patients.slice(0, MAX_PER_GROUP).map(function (p) {
                return {
                    title: p.fullName,
                    meta: p.patientNumber + ' · ' + p.age + ' yrs · ' + p.municipality,
                    icon: 'fa-user',
                    url: '/Referrals?SearchTerm=' + encodeURIComponent(p.patientNumber)
                };
            });
        } catch (error) {
            return [];
        }
    }

    /* ----------------------------------------------------------------------
       4. Rendering
       ---------------------------------------------------------------------- */

    function renderGroups(groups, term) {
        palette.items = [];

        var html = '';

        groups.forEach(function (group) {
            if (group.items.length === 0) { return; }

            html += '<li class="palette-group-label" role="presentation">' + escapeHtml(group.label) + '</li>';

            group.items.forEach(function (item) {
                var index = palette.items.length;
                palette.items.push(item);

                html += '<li role="presentation">' +
                    '<button type="button" class="palette-item" role="option" aria-selected="false" ' +
                            'data-index="' + index + '" id="palette-option-' + index + '">' +
                        '<span class="palette-item-icon" aria-hidden="true">' +
                            '<i class="fa-solid ' + escapeHtml(item.icon) + '"></i></span>' +
                        '<span class="palette-item-body">' +
                            '<span class="palette-item-title">' + highlight(item.title, term) + '</span>' +
                            '<span class="palette-item-meta">' + escapeHtml(item.meta) + '</span>' +
                        '</span>' +
                    '</button>' +
                '</li>';
            });
        });

        if (palette.items.length === 0) {
            html = '<li class="palette-empty">' +
                '<i class="fa-regular fa-face-frown palette-empty-icon" aria-hidden="true"></i>' +
                '<span>No results for &ldquo;' + escapeHtml(term) + '&rdquo;</span></li>';
        }

        palette.results.innerHTML = html;

        palette.results.querySelectorAll('.palette-item').forEach(function (button) {
            button.addEventListener('click', function () {
                runItem(Number(button.getAttribute('data-index')));
            });

            button.addEventListener('mousemove', function () {
                setActive(Number(button.getAttribute('data-index')));
            });
        });

        setActive(0);
    }

    function setActive(index) {
        if (palette.items.length === 0) { return; }

        palette.activeIndex = Math.max(0, Math.min(index, palette.items.length - 1));

        palette.results.querySelectorAll('.palette-item').forEach(function (button) {
            var isActive = Number(button.getAttribute('data-index')) === palette.activeIndex;

            button.classList.toggle('is-active', isActive);
            button.setAttribute('aria-selected', String(isActive));

            if (isActive) {
                button.scrollIntoView({ block: 'nearest' });
                palette.input.setAttribute('aria-activedescendant', button.id);
            }
        });
    }

    function runItem(index) {
        var item = palette.items[index];
        if (!item) { return; }

        close();

        if (typeof item.run === 'function') {
            item.run();
        } else if (item.url) {
            window.location.href = item.url;
        }
    }

    /* ----------------------------------------------------------------------
       5. Search
       ---------------------------------------------------------------------- */

    function renderDefault() {
        renderGroups([
            { label: 'Go to', items: NAVIGATION.slice(0, 6) },
            { label: 'Actions', items: ACTIONS }
        ], '');
    }

    async function search(rawTerm) {
        var term = rawTerm.trim().toLowerCase();

        if (term === '') {
            renderDefault();
            return;
        }

        // Local sources render immediately so typing always feels instant.
        var groups = [
            { label: 'Facilities', items: facilityResults(term) },
            { label: 'Municipalities', items: municipalityResults(term) },
            { label: 'Go to', items: staticResults(NAVIGATION, term) },
            { label: 'Actions', items: staticResults(ACTIONS, term) }
        ];

        renderGroups(groups, rawTerm.trim());

        // Then fold in server-side records once they arrive.
        if (term.length >= 2) {
            var patients = await patientResults(term);

            // Ignore a late response if the query has moved on.
            if (palette.input.value.trim().toLowerCase() !== term) { return; }

            if (patients.length > 0) {
                groups.splice(2, 0, { label: 'Patients', items: patients });
                renderGroups(groups, rawTerm.trim());
            }
        }
    }

    /* ----------------------------------------------------------------------
       6. Open / close
       ---------------------------------------------------------------------- */

    function open() {
        if (palette.open) { return; }

        palette.open = true;
        palette.lastFocused = document.activeElement;

        palette.root.classList.add('is-open');
        palette.backdrop.classList.add('is-open');

        palette.input.value = '';
        renderDefault();
        palette.input.focus();
    }

    function close() {
        if (!palette.open) { return; }

        palette.open = false;
        palette.root.classList.remove('is-open');
        palette.backdrop.classList.remove('is-open');
        palette.input.removeAttribute('aria-activedescendant');

        if (palette.lastFocused && typeof palette.lastFocused.focus === 'function') {
            palette.lastFocused.focus();
        }
    }

    function onKeydown(event) {
        var isShortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k';

        if (isShortcut) {
            event.preventDefault();
            palette.open ? close() : open();
            return;
        }

        if (!palette.open) { return; }

        switch (event.key) {
            case 'Escape':
                event.preventDefault();
                close();
                break;
            case 'ArrowDown':
                event.preventDefault();
                setActive(palette.activeIndex + 1);
                break;
            case 'ArrowUp':
                event.preventDefault();
                setActive(palette.activeIndex - 1);
                break;
            case 'Home':
                event.preventDefault();
                setActive(0);
                break;
            case 'End':
                event.preventDefault();
                setActive(palette.items.length - 1);
                break;
            case 'Enter':
                event.preventDefault();
                runItem(palette.activeIndex);
                break;
            case 'Tab':
                // Keep focus inside the dialog.
                event.preventDefault();
                break;
        }
    }

    /* ----------------------------------------------------------------------
       7. Bootstrap
       ---------------------------------------------------------------------- */

    function init() {
        palette.root = document.getElementById('commandPalette');
        palette.backdrop = document.getElementById('paletteBackdrop');
        palette.input = document.getElementById('paletteInput');
        palette.results = document.getElementById('paletteResults');

        if (!palette.root || !palette.input || !palette.results) { return; }

        var trigger = document.getElementById('paletteTrigger');
        if (trigger) {
            trigger.addEventListener('click', open);
        }

        palette.backdrop.addEventListener('click', close);
        document.addEventListener('keydown', onKeydown);

        palette.input.addEventListener('input', function () {
            window.clearTimeout(palette.searchTimer);
            var value = palette.input.value;

            palette.searchTimer = window.setTimeout(function () {
                search(value);
            }, SEARCH_DEBOUNCE);
        });

        RHC.palette = { open: open, close: close };
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
