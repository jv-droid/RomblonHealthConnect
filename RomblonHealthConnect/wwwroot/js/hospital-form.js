/* ==========================================================================
   Romblon HealthConnect — Facility location picker
   Click or drag on the map to set a facility's coordinates. The map and the
   latitude/longitude inputs stay in sync in both directions, so the pin is
   always exactly what gets saved and drawn on the provincial dashboard.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var PROVINCE_CENTER = [122.33, 12.47];
    var PROVINCE_ZOOM = 8.4;
    var PLACED_ZOOM = 13;

    // Matches RomblonGeography on the server; keeps an obviously wrong pin out.
    var BOUNDS = { minLat: 11.85, maxLat: 13.20, minLon: 121.60, maxLon: 122.95 };

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
        layers: [{ id: 'basemap', type: 'raster', source: 'carto-light', minzoom: 0, maxzoom: 20 }]
    };

    var map = null;
    var marker = null;
    var latInput = null;
    var lonInput = null;
    var hint = null;
    var centres = {};

    /* ----------------------------------------------------------------------
       1. Helpers
       ---------------------------------------------------------------------- */

    function readCentres() {
        var node = document.getElementById('municipalityCentres');
        if (!node) { return {}; }

        try {
            return JSON.parse(node.textContent);
        } catch (error) {
            window.console.warn('[picker] Could not parse municipality centres:', error);
            return {};
        }
    }

    function currentCoordinates() {
        var lat = parseFloat(latInput.value);
        var lon = parseFloat(lonInput.value);

        return (isNaN(lat) || isNaN(lon)) ? null : [lon, lat];
    }

    function withinProvince(lat, lon) {
        return lat >= BOUNDS.minLat && lat <= BOUNDS.maxLat
            && lon >= BOUNDS.minLon && lon <= BOUNDS.maxLon;
    }

    function setHint(message, isWarning) {
        if (!hint) { return; }

        hint.textContent = message;
        hint.classList.toggle('rhc-field-error', Boolean(isWarning));
        hint.classList.toggle('rhc-caption', !isWarning);
    }

    /* ----------------------------------------------------------------------
       2. Pin
       ---------------------------------------------------------------------- */

    function createMarkerElement() {
        var element = document.createElement('div');
        element.className = 'map-marker map-marker-public is-online';

        var shape = document.createElement('span');
        shape.className = 'map-marker-shape';
        shape.innerHTML = '<i class="fa-solid fa-location-dot" aria-hidden="true"></i>';

        element.appendChild(shape);
        return element;
    }

    /** Moves the pin and writes the value back into the form fields. */
    function placePin(lngLat, updateInputs) {
        if (!marker) {
            marker = new window.maplibregl.Marker({
                element: createMarkerElement(),
                draggable: true
            }).setLngLat(lngLat).addTo(map);

            marker.on('dragend', function () {
                var position = marker.getLngLat();
                writeInputs(position.lat, position.lng);
                validatePin(position.lat, position.lng);
            });
        } else {
            marker.setLngLat(lngLat);
        }

        if (updateInputs) {
            writeInputs(lngLat[1], lngLat[0]);
        }

        validatePin(lngLat[1], lngLat[0]);
    }

    function writeInputs(lat, lon) {
        latInput.value = lat.toFixed(6);
        lonInput.value = lon.toFixed(6);
    }

    function validatePin(lat, lon) {
        if (!withinProvince(lat, lon)) {
            setHint('That position is outside Romblon province. Move the pin back inside the province.', true);
            return false;
        }

        setHint('Pin placed at ' + lat.toFixed(5) + ', ' + lon.toFixed(5) +
            '. Drag it to fine-tune, or type exact coordinates below.', false);
        return true;
    }

    /* ----------------------------------------------------------------------
       3. Wiring
       ---------------------------------------------------------------------- */

    function centreOnMunicipality(fly) {
        var select = document.getElementById('Municipality');
        if (!select || !select.value) { return; }

        var centre = centres[select.value];
        if (!centre || !map) { return; }

        var target = [centre.lon, centre.lat];

        if (fly) {
            map.flyTo({ center: target, zoom: 12, speed: 1.1, essential: true });
        } else {
            map.jumpTo({ center: target, zoom: 12 });
        }

        // Only auto-place the pin when the facility has no position yet.
        if (!currentCoordinates()) {
            placePin(target, true);
            setHint('Pin placed at the centre of ' + select.value +
                '. Click or drag to move it to the exact site.', false);
        }
    }

    function initInputs() {
        function syncFromInputs() {
            var coordinates = currentCoordinates();
            if (!coordinates || !map) { return; }

            placePin(coordinates, false);
            map.easeTo({ center: coordinates, duration: 400 });
        }

        latInput.addEventListener('change', syncFromInputs);
        lonInput.addEventListener('change', syncFromInputs);

        var select = document.getElementById('Municipality');
        if (select) {
            select.addEventListener('change', function () { centreOnMunicipality(true); });
        }

        var centreButton = document.getElementById('centreOnMunicipality');
        if (centreButton) {
            centreButton.addEventListener('click', function () { centreOnMunicipality(true); });
        }
    }

    /* ----------------------------------------------------------------------
       4. Bootstrap
       ---------------------------------------------------------------------- */

    function init() {
        var container = document.getElementById('locationPicker');
        if (!container) { return; }

        latInput = document.getElementById('Latitude');
        lonInput = document.getElementById('Longitude');
        hint = document.getElementById('pickerHint');
        if (!latInput || !lonInput) { return; }

        centres = readCentres();

        if (!window.maplibregl) {
            container.innerHTML = '';
            var fallback = document.createElement('p');
            fallback.className = 'map-unavailable';
            fallback.textContent = 'Map unavailable. Enter the latitude and longitude manually below.';
            container.appendChild(fallback);
            return;
        }

        var existing = currentCoordinates();

        map = new window.maplibregl.Map({
            container: container,
            style: MAP_STYLE,
            center: existing || PROVINCE_CENTER,
            zoom: existing ? PLACED_ZOOM : PROVINCE_ZOOM,
            minZoom: 6,
            maxZoom: 18,
            attributionControl: { compact: true }
        });

        map.addControl(new window.maplibregl.NavigationControl({ showCompass: false }), 'top-right');

        map.on('load', function () {
            if (existing) {
                placePin(existing, false);
            } else {
                centreOnMunicipality(false);
            }
        });

        // Clicking anywhere sets the location.
        map.on('click', function (event) {
            placePin([event.lngLat.lng, event.lngLat.lat], true);
        });

        initInputs();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
