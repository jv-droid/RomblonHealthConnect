/* ==========================================================================
   Romblon HealthConnect — Health network map
   MapLibre GL JS view of provincial facilities, driven by RHC.data.facilities.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};

    /* ----------------------------------------------------------------------
       1. Configuration
       ---------------------------------------------------------------------- */

    var PROVINCE_CENTER = [122.20, 12.45];
    var PROVINCE_ZOOM = 8.4;

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
            {
                id: 'basemap',
                type: 'raster',
                source: 'carto-light',
                minzoom: 0,
                maxzoom: 20
            }
        ]
    };

    var TYPE_ICONS = {
        public: 'fa-hospital',
        district: 'fa-house-medical',
        rhu: 'fa-kit-medical',
        private: 'fa-briefcase-medical'
    };

    /* ----------------------------------------------------------------------
       2. State
       ---------------------------------------------------------------------- */

    var map = null;
    var markers = {};
    var selectedId = null;

    /* ----------------------------------------------------------------------
       3. Markers
       ---------------------------------------------------------------------- */

    function createMarkerElement(facility) {
        var element = document.createElement('button');
        element.type = 'button';
        element.className = 'map-marker map-marker-' + facility.type;
        element.setAttribute('aria-label', facility.name + ', ' + facility.typeLabel + ', ' + facility.municipality);
        element.setAttribute('data-facility-id', facility.id);

        var icon = document.createElement('i');
        icon.className = 'fa-solid ' + (TYPE_ICONS[facility.type] || 'fa-hospital');
        icon.setAttribute('aria-hidden', 'true');
        element.appendChild(icon);

        element.addEventListener('click', function (event) {
            event.stopPropagation();
            selectFacility(facility.id, true);
        });

        return element;
    }

    function addMarkers() {
        var facilities = (RHC.data && RHC.data.facilities) || [];

        facilities.forEach(function (facility) {
            var element = createMarkerElement(facility);

            markers[facility.id] = new window.maplibregl.Marker({ element: element })
                .setLngLat(facility.coordinates)
                .addTo(map);
        });
    }

    function setSelectedMarker(facilityId) {
        Object.keys(markers).forEach(function (id) {
            var element = markers[id].getElement();
            element.classList.toggle('is-selected', id === facilityId);
        });
        selectedId = facilityId;
    }

    /* ----------------------------------------------------------------------
       4. Selection
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
                speed: 0.8,
                curve: 1.4,
                essential: true
            });
        }

        if (typeof RHC.openFacility === 'function') {
            RHC.openFacility(facilityId);
        }
    }

    function resetView() {
        if (!map) { return; }

        setSelectedMarker(null);
        map.flyTo({
            center: PROVINCE_CENTER,
            zoom: PROVINCE_ZOOM,
            bearing: 0,
            pitch: 0,
            speed: 0.9,
            essential: true
        });
    }

    /* ----------------------------------------------------------------------
       5. Map controls
       ---------------------------------------------------------------------- */

    function initControls() {
        // Rotation and pitch are exposed through the navigation compass.
        map.addControl(new window.maplibregl.NavigationControl({
            visualizePitch: true,
            showCompass: true,
            showZoom: true
        }), 'top-right');

        map.addControl(new window.maplibregl.ScaleControl({
            maxWidth: 96,
            unit: 'metric'
        }), 'bottom-right');

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
    }

    /* ----------------------------------------------------------------------
       6. Cross-module wiring
       ---------------------------------------------------------------------- */

    function initEvents() {
        // Clearing the drawer clears the map selection.
        document.addEventListener('rhc:facility-deselected', function () {
            setSelectedMarker(null);
        });

        // A selection made elsewhere (e.g. future table click) recentres the map.
        document.addEventListener('rhc:facility-selected', function (event) {
            if (event.detail && event.detail.id !== selectedId) {
                setSelectedMarker(event.detail.id);
            }
        });

        // The sidebar animation changes the canvas width.
        document.addEventListener('rhc:layout-changed', function () {
            if (map) { map.resize(); }
        });

        window.addEventListener('resize', function () {
            if (map) { map.resize(); }
        });
    }

    /* ----------------------------------------------------------------------
       7. Fallback
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
       8. Bootstrap
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

        map.on('load', function () {
            addMarkers();
            initToolbar();
        });

        // Clicking empty water or land dismisses the current selection.
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
