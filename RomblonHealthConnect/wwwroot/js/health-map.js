/* ==========================================================================
   Romblon HealthConnect — Health network map
   MapLibre GL JS view of provincial facilities, driven by RHC.data.facilities.

   Shared unchanged between the provincial dashboard and the referral wizard:
   both pages publish the same contract (data.facilities, getFacility,
   openFacility, closeFacility) and this module owns the map itself.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};

    /* ----------------------------------------------------------------------
       1. Configuration
       ---------------------------------------------------------------------- */

    // Fallback view, used only when no facility data is available. The real
    // framing is computed from the facilities themselves so the province is
    // always fully in frame, even as records change.
    var PROVINCE_CENTER = [122.33, 12.47];
    var PROVINCE_ZOOM = 8.4;
    var FIT_PADDING = 72;

    // Several municipalities host three facilities within ~1.5 km, which is only
    // a few pixels apart at province zoom. Markers closer than this are fanned
    // out so none is hidden underneath another.
    var COLLISION_RADIUS = 30;

    // Centre-to-centre distance that keeps two 26 px markers legible.
    var MIN_SEPARATION = 28;

    var FLOW_SOURCE = 'referral-flows';
    var FLOW_LAYER = 'referral-flow-lines';

    // Light raster basemap that sits quietly beneath the interface chrome.
    var MAP_STYLE = {
        version: 8,
        sources: {
            'carto-light': {
                type: 'raster',
                tiles: [
                    'https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
                    'https://b.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
                    'https://c.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png'
                ],
                tileSize: 256,
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>'
            }
        },
        layers: [
            { id: 'basemap', type: 'raster', source: 'carto-light', minzoom: 0, maxzoom: 20 }
        ]
    };

    var TYPE_ICONS = {
        public: 'fa-hospital',
        district: 'fa-house-medical',
        rhu: 'fa-kit-medical',
        private: 'fa-briefcase-medical'
    };

    // Referral hubs earn a label first when space is tight.
    var TYPE_PRIORITY = { public: 0, district: 1, private: 2, rhu: 3 };

    // Below this zoom only the higher tiers are eligible, so the province view
    // stays readable; above it every facility competes for a label.
    var LABEL_ALL_ZOOM = 9.6;

    // Zoom at which a label can afford its second line of detail.
    var LABEL_DETAIL_ZOOM = 10.5;

    var LABEL_GAP = 3;

    /* ----------------------------------------------------------------------
       2. State
       ---------------------------------------------------------------------- */

    var map = null;
    var markers = {};
    var selectedId = null;
    var flowAnimation = null;

    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    /* ----------------------------------------------------------------------
       3. Markers
       ---------------------------------------------------------------------- */

    /**
     * Second label line: the figures a coordinator scans for before clicking.
     * Anything unknown is left out rather than shown as a zero.
     */
    function describeFacility(facility) {
        var parts = [];

        if (typeof facility.bedsAvailable === 'number' && facility.bedsTotal) {
            parts.push(facility.bedsAvailable + '/' + facility.bedsTotal + ' beds');
        }

        if (facility.doctorsAvailable) {
            parts.push(facility.doctorsAvailable + ' doctors');
        }

        if (facility.emergency) {
            parts.push('ER');
        }

        if (facility.status === 'offline') {
            parts.push('offline');
        } else if (facility.status === 'limited') {
            parts.push('limited');
        }

        return parts.join(' · ');
    }

    function createMarkerElement(facility) {
        var element = document.createElement('button');
        element.type = 'button';
        element.className = 'map-marker map-marker-' + facility.type;
        element.setAttribute('data-facility-id', facility.id);

        // State is announced, not just drawn.
        var stateWords = [];

        if (facility.status === 'offline') {
            element.classList.add('is-offline');
            stateWords.push('offline');
        } else {
            element.classList.add('is-online');
            stateWords.push(facility.status === 'limited' ? 'limited connectivity' : 'online');
        }

        if (facility.emergency) {
            element.classList.add('is-emergency');
            stateWords.push('emergency capable');
        }

        element.setAttribute('aria-label',
            facility.name + ', ' + facility.typeLabel + ', ' + facility.municipality +
            ', ' + stateWords.join(', '));

        // MapLibre owns the transform on `element`, so the visual circle and all
        // of its motion live on this inner shape instead.
        var shape = document.createElement('span');
        shape.className = 'map-marker-shape';

        var icon = document.createElement('i');
        icon.className = 'fa-solid ' + (TYPE_ICONS[facility.type] || 'fa-hospital');
        icon.setAttribute('aria-hidden', 'true');

        shape.appendChild(icon);
        element.appendChild(shape);

        // Label rides inside the marker so it follows any declutter offset.
        // The marker's aria-label already carries this text, so it is decorative.
        var label = document.createElement('span');
        label.className = 'map-marker-label';
        label.setAttribute('aria-hidden', 'true');

        var name = document.createElement('span');
        name.className = 'map-marker-label-name';
        name.textContent = facility.name;
        label.appendChild(name);

        var meta = document.createElement('span');
        meta.className = 'map-marker-label-meta';
        meta.textContent = describeFacility(facility);
        label.appendChild(meta);

        element.appendChild(label);

        element.addEventListener('click', function (event) {
            event.stopPropagation();
            selectFacility(facility.id, true);
        });

        return element;
    }

    function addMarkers() {
        var facilities = (RHC.data && RHC.data.facilities) || [];

        facilities.forEach(function (facility) {
            markers[facility.id] = new window.maplibregl.Marker({ element: createMarkerElement(facility) })
                .setLngLat(facility.coordinates)
                .addTo(map);
        });
    }

    function setSelectedMarker(facilityId) {
        Object.keys(markers).forEach(function (id) {
            markers[id].getElement().classList.toggle('is-selected', id === facilityId);
        });
        selectedId = facilityId;
    }

    /**
     * Fans out markers that would otherwise sit on top of each other.
     *
     * Facilities are grouped by screen distance at the current zoom, then each
     * group is spread evenly around a small circle using pixel offsets. As the
     * user zooms in the groups dissolve and every marker returns to its true
     * position, so accuracy is only traded away where the alternative is an
     * invisible marker.
     */
    function declutterMarkers() {
        if (!map) { return; }

        var facilities = (RHC.data && RHC.data.facilities) || [];
        var points = [];

        facilities.forEach(function (facility) {
            if (markers[facility.id]) {
                points.push({ id: facility.id, at: map.project(facility.coordinates) });
            }
        });

        var claimed = {};
        var placed = [];

        points.forEach(function (point) {
            if (claimed[point.id]) { return; }

            var group = [point];
            claimed[point.id] = true;

            points.forEach(function (other) {
                if (claimed[other.id]) { return; }

                var dx = other.at.x - point.at.x;
                var dy = other.at.y - point.at.y;

                if (Math.sqrt(dx * dx + dy * dy) < COLLISION_RADIUS) {
                    group.push(other);
                    claimed[other.id] = true;
                }
            });

            if (group.length === 1) {
                placed.push({ id: group[0].id, at: point.at, x: point.at.x, y: point.at.y, fanned: false });
                return;
            }

            // Spread around a circle just large enough to clear the markers.
            var radius = COLLISION_RADIUS * 0.62;
            var step = (Math.PI * 2) / group.length;

            group.forEach(function (item, index) {
                var angle = (-Math.PI / 2) + (index * step);

                placed.push({
                    id: item.id,
                    at: item.at,
                    x: item.at.x + Math.cos(angle) * radius,
                    y: item.at.y + Math.sin(angle) * radius,
                    fanned: true
                });
            });
        });

        relax(placed);

        placed.forEach(function (item) {
            markers[item.id].setOffset([item.x - item.at.x, item.y - item.at.y]);
            markers[item.id].getElement().classList.toggle('is-fanned', item.fanned);
        });

        // Offsets are final, so labels can now be allocated against real positions.
        updateLabels(placed);
    }

    /**
     * Grants a label to as many facilities as will fit without overlapping.
     *
     * Candidates are sorted by facility tier, then by how much of the network
     * they serve, and each is accepted only if its box clears every label
     * already granted. This mirrors how MapLibre handles symbol collision, but
     * works on the HTML markers so labels track the declutter offsets.
     *
     * @param {Array} placed - markers with settled screen positions
     */
    function updateLabels(placed) {
        if (!map) { return; }

        var zoom = map.getZoom();
        var shell = map.getContainer().closest('.map-shell');

        if (shell) {
            shell.setAttribute('data-label-detail', zoom >= LABEL_DETAIL_ZOOM ? 'high' : 'low');
        }

        var showEveryTier = zoom >= LABEL_ALL_ZOOM;

        var candidates = placed.map(function (item) {
            var facility = RHC.getFacility ? RHC.getFacility(item.id) : null;
            var priority = facility && TYPE_PRIORITY.hasOwnProperty(facility.type)
                ? TYPE_PRIORITY[facility.type]
                : 9;

            return {
                id: item.id,
                x: item.x,
                y: item.y,
                priority: priority,
                weight: facility ? (facility.bedsTotal || 0) : 0,
                eligible: Boolean(facility) && (showEveryTier || priority <= 1)
            };
        });

        // Highest tier first; larger facilities break ties.
        candidates.sort(function (a, b) {
            return a.priority - b.priority || b.weight - a.weight;
        });

        var granted = [];

        candidates.forEach(function (candidate) {
            var element = markers[candidate.id].getElement();
            var label = element.querySelector('.map-marker-label');

            if (!label || !candidate.eligible) {
                element.classList.remove('has-label');
                return;
            }

            // Measure while visible, otherwise the box is zero-sized.
            element.classList.add('has-label');
            var size = label.getBoundingClientRect();

            var box = {
                left: candidate.x - size.width / 2 - LABEL_GAP,
                right: candidate.x + size.width / 2 + LABEL_GAP,
                top: candidate.y + 16,
                bottom: candidate.y + 16 + size.height + LABEL_GAP
            };

            var collides = granted.some(function (other) {
                return !(box.right < other.left
                    || box.left > other.right
                    || box.bottom < other.top
                    || box.top > other.bottom);
            });

            if (collides) {
                element.classList.remove('has-label');
                return;
            }

            granted.push(box);
        });
    }

    /**
     * Nudges any remaining pairs apart. Fanning one cluster can push a marker
     * into a neighbouring one, so a few relaxation passes settle the layout.
     * @param {Array} placed - markers with mutable x/y screen positions
     */
    function relax(placed) {
        var minimum = MIN_SEPARATION;

        for (var pass = 0; pass < 12; pass++) {
            var moved = false;

            for (var i = 0; i < placed.length; i++) {
                for (var j = i + 1; j < placed.length; j++) {
                    var a = placed[i];
                    var b = placed[j];

                    var dx = b.x - a.x;
                    var dy = b.y - a.y;
                    var distance = Math.sqrt(dx * dx + dy * dy);

                    if (distance >= minimum) { continue; }

                    // Identical positions need an arbitrary direction to separate.
                    if (distance < 0.001) {
                        dx = Math.cos(i);
                        dy = Math.sin(i);
                        distance = 1;
                    }

                    var push = (minimum - distance) / 2;
                    var ux = (dx / distance) * push;
                    var uy = (dy / distance) * push;

                    a.x -= ux; a.y -= uy;
                    b.x += ux; b.y += uy;

                    a.fanned = true;
                    b.fanned = true;
                    moved = true;
                }
            }

            if (!moved) { return; }
        }
    }

    /* ----------------------------------------------------------------------
       4. Referral flow lines
       ---------------------------------------------------------------------- */

    /**
     * Builds a gently curved path between two facilities so overlapping routes
     * stay legible. Uses a quadratic bezier offset perpendicular to the chord.
     */
    function arcBetween(from, to, segments) {
        var midX = (from[0] + to[0]) / 2;
        var midY = (from[1] + to[1]) / 2;

        var dx = to[0] - from[0];
        var dy = to[1] - from[1];

        // Control point pushed to one side, proportional to the span.
        var controlX = midX - dy * 0.18;
        var controlY = midY + dx * 0.18;

        var points = [];
        for (var i = 0; i <= segments; i++) {
            var t = i / segments;
            var inverse = 1 - t;

            points.push([
                inverse * inverse * from[0] + 2 * inverse * t * controlX + t * t * to[0],
                inverse * inverse * from[1] + 2 * inverse * t * controlY + t * t * to[1]
            ]);
        }

        return points;
    }

    function buildFlowData() {
        var referrals = (RHC.data && RHC.data.referrals) || [];
        var active = referrals.filter(function (r) {
            return r.status === 'pending' || r.status === 'accepted' || r.status === 'in-transit';
        });

        var features = [];

        active.forEach(function (referral) {
            var origin = RHC.getFacility ? RHC.getFacility(referral.origin) : null;
            var destination = RHC.getFacility ? RHC.getFacility(referral.destination) : null;

            if (!origin || !destination) { return; }

            features.push({
                type: 'Feature',
                properties: { reference: referral.reference, status: referral.status },
                geometry: {
                    type: 'LineString',
                    coordinates: arcBetween(origin.coordinates, destination.coordinates, 48)
                }
            });
        });

        return { type: 'FeatureCollection', features: features };
    }

    function addFlowLayer() {
        var data = buildFlowData();
        if (data.features.length === 0) { return; }

        map.addSource(FLOW_SOURCE, { type: 'geojson', data: data });

        map.addLayer({
            id: FLOW_LAYER,
            type: 'line',
            source: FLOW_SOURCE,
            layout: {
                'line-cap': 'round',
                'line-join': 'round',
                visibility: 'none'
            },
            paint: {
                'line-color': '#2563EB',
                'line-width': 1.8,
                'line-opacity': 0.75,
                'line-dasharray': [0, 4, 3]
            }
        }, undefined);

        syncFlowVisibility();
    }

    /**
     * Marching-ants dash cycle. Runs only in executive mode and only when the
     * viewer has not asked for reduced motion.
     */
    function startFlowAnimation() {
        if (flowAnimation !== null || !map || !map.getLayer(FLOW_LAYER)) { return; }
        if (prefersReducedMotion()) { return; }

        var sequence = [
            [0, 4, 3], [0.5, 4, 2.5], [1, 4, 2], [1.5, 4, 1.5],
            [2, 4, 1], [2.5, 4, 0.5], [3, 4, 0], [0, 0.5, 3, 3.5],
            [0, 1, 3, 3], [0, 1.5, 3, 2.5], [0, 2, 3, 2],
            [0, 2.5, 3, 1.5], [0, 3, 3, 1], [0, 3.5, 3, 0.5]
        ];

        var step = 0;
        var lastFrame = 0;

        function frame(timestamp) {
            // ~14 steps per second keeps the motion calm.
            if (timestamp - lastFrame > 70) {
                step = (step + 1) % sequence.length;

                if (map.getLayer(FLOW_LAYER)) {
                    map.setPaintProperty(FLOW_LAYER, 'line-dasharray', sequence[step]);
                }

                lastFrame = timestamp;
            }

            flowAnimation = window.requestAnimationFrame(frame);
        }

        flowAnimation = window.requestAnimationFrame(frame);
    }

    function stopFlowAnimation() {
        if (flowAnimation !== null) {
            window.cancelAnimationFrame(flowAnimation);
            flowAnimation = null;
        }
    }

    /** Flow lines belong to the executive narrative, not the operational view. */
    function syncFlowVisibility() {
        if (!map || !map.getLayer(FLOW_LAYER)) { return; }

        var isExecutive = RHC.workspace ? RHC.workspace.isExecutive() : false;

        map.setLayoutProperty(FLOW_LAYER, 'visibility', isExecutive ? 'visible' : 'none');

        if (isExecutive) {
            startFlowAnimation();
        } else {
            stopFlowAnimation();
        }
    }

    /* ----------------------------------------------------------------------
       5. Selection
       ---------------------------------------------------------------------- */

    /**
     * Centres the map on a facility and opens the details drawer.
     * @param {string} facilityId
     * @param {boolean} shouldFly - false when the map is only reacting to an external selection.
     */
    function selectFacility(facilityId, shouldFly) {
        var facility = RHC.getFacility ? RHC.getFacility(facilityId) : null;
        if (!facility) { return; }

        setSelectedMarker(facilityId);

        if (shouldFly && map) {
            map.flyTo({
                center: facility.coordinates,
                zoom: Math.max(map.getZoom(), 11),
                speed: 0.7,
                curve: 1.5,
                essential: true
            });
        }

        if (typeof RHC.openFacility === 'function') {
            RHC.openFacility(facilityId);
        }
    }

    /**
     * Bounding box of every facility, so the province view always frames the
     * whole network rather than a hardcoded centre that drifts as data changes.
     * @returns {maplibregl.LngLatBounds|null}
     */
    function facilityBounds() {
        var facilities = (RHC.data && RHC.data.facilities) || [];
        if (facilities.length === 0) { return null; }

        var bounds = new window.maplibregl.LngLatBounds();
        facilities.forEach(function (facility) {
            bounds.extend(facility.coordinates);
        });

        return bounds;
    }

    /** Frames the whole province. Called on load and by the reset control. */
    function fitProvince(animate) {
        if (!map) { return; }

        var bounds = facilityBounds();

        if (!bounds) {
            map.jumpTo({ center: PROVINCE_CENTER, zoom: PROVINCE_ZOOM });
            return;
        }

        map.fitBounds(bounds, {
            padding: FIT_PADDING,
            bearing: 0,
            pitch: 0,
            duration: animate ? 900 : 0,
            essential: true
        });
    }

    function resetView() {
        setSelectedMarker(null);
        fitProvince(true);
    }

    /* ----------------------------------------------------------------------
       6. Controls and toolbar
       ---------------------------------------------------------------------- */

    function initControls() {
        map.addControl(new window.maplibregl.NavigationControl({
            visualizePitch: true,
            showCompass: true,
            showZoom: true
        }), 'top-right');

        map.addControl(new window.maplibregl.ScaleControl({ maxWidth: 96, unit: 'metric' }), 'bottom-right');
        map.addControl(new window.maplibregl.FullscreenControl(), 'top-right');
    }

    function initToolbar() {
        var resetButton = document.getElementById('mapResetView');
        var legendButton = document.getElementById('mapToggleLegend');
        var legend = document.getElementById('mapLegend');

        if (resetButton) {
            resetButton.addEventListener('click', resetView);
        }

        if (legendButton && legend) {
            legendButton.addEventListener('click', function () {
                var nowHidden = !legend.hasAttribute('hidden');
                legend.toggleAttribute('hidden', nowHidden);
                legendButton.setAttribute('aria-expanded', String(!nowHidden));
            });
        }

        // Ring meanings stay collapsed so the legend keeps its small footprint.
        var legendInfo = document.getElementById('mapLegendInfo');
        var legendNotes = document.getElementById('mapLegendNotes');

        if (legendInfo && legendNotes) {
            legendInfo.addEventListener('click', function () {
                var opening = legendNotes.hasAttribute('hidden');

                legendNotes.toggleAttribute('hidden', !opening);
                legendInfo.setAttribute('aria-expanded', String(opening));
            });
        }

        var labelButton = document.getElementById('mapToggleLabels');
        var shell = map ? map.getContainer().closest('.map-shell') : null;

        if (labelButton && shell) {
            labelButton.addEventListener('click', function () {
                var turningOff = shell.getAttribute('data-labels') !== 'off';

                shell.setAttribute('data-labels', turningOff ? 'off' : 'on');
                labelButton.setAttribute('aria-pressed', String(!turningOff));
                labelButton.setAttribute('aria-label',
                    turningOff ? 'Show facility labels' : 'Hide facility labels');
            });
        }
    }

    /* ----------------------------------------------------------------------
       7. Cross-module wiring
       ---------------------------------------------------------------------- */

    function initEvents() {
        document.addEventListener('rhc:facility-deselected', function () {
            setSelectedMarker(null);
        });

        document.addEventListener('rhc:facility-selected', function (event) {
            if (event.detail && event.detail.id !== selectedId) {
                setSelectedMarker(event.detail.id);
            }
        });

        // Sidebar collapse, mode switch, and wizard steps all resize the canvas.
        document.addEventListener('rhc:layout-changed', function () {
            if (map) { map.resize(); }
        });

        document.addEventListener('rhc:workspace-changed', syncFlowVisibility);

        window.addEventListener('resize', function () {
            if (map) { map.resize(); }
        });

        // Stop the animation loop when the tab is hidden.
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                stopFlowAnimation();
            } else {
                syncFlowVisibility();
            }
        });
    }

    /* ----------------------------------------------------------------------
       8. Fallback
       ---------------------------------------------------------------------- */

    function renderUnavailable(container, message) {
        container.innerHTML = '';

        var wrapper = document.createElement('p');
        wrapper.className = 'map-unavailable';
        wrapper.setAttribute('role', 'status');
        wrapper.textContent = message;

        container.appendChild(wrapper);
    }

    /* ----------------------------------------------------------------------
       9. Bootstrap
       ---------------------------------------------------------------------- */

    function init() {
        var container = document.getElementById('healthMap');
        if (!container) { return; }

        if (!window.maplibregl) {
            renderUnavailable(container, 'Map library could not be loaded. Check the network connection and refresh.');
            return;
        }

        map = new window.maplibregl.Map({
            container: container,
            style: MAP_STYLE,
            center: PROVINCE_CENTER,
            zoom: PROVINCE_ZOOM,
            minZoom: 6,
            maxZoom: 16,
            pitchWithRotate: true,
            dragRotate: true,
            attributionControl: { compact: true }
        });

        map.on('load', async function () {
            // The dashboard loads facilities asynchronously from the database and
            // publishes RHC.dataReady. The referral wizard supplies its data
            // synchronously and sets no promise, so the fallback resolves at once.
            try {
                await (RHC.dataReady || Promise.resolve());
            } catch (error) {
                window.console.warn('[map] Facility data was unavailable.', error);
            }

            addFlowLayer();
            addMarkers();
            initToolbar();

            // Frame the actual network once the facilities are known.
            fitProvince(false);
            declutterMarkers();
        });

        // Groups tighten or dissolve as the scale changes.
        map.on('zoomend', declutterMarkers);
        map.on('resize', declutterMarkers);

        map.on('click', function () {
            if (selectedId && typeof RHC.closeFacility === 'function') {
                RHC.closeFacility();
            }
        });

        initControls();
        initEvents();

        RHC.map = {
            instance: function () { return map; },
            select: selectFacility,
            reset: resetView
        };
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
