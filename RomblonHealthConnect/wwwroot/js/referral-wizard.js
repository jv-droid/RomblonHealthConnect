/* ==========================================================================
   Romblon HealthConnect — Create Referral wizard
   Seven-step flow: patient, origin, destination, doctor, attachments, review, submit.

   Reuses the Phase 2 GIS map unchanged: this module publishes the small
   RHC contract health-map.js expects (data.facilities, getFacility,
   openFacility, closeFacility) and lets that file own the map itself.
   ========================================================================== */

(function (window, document) {
    'use strict';

    var RHC = window.RHC = window.RHC || {};
    var Referrals = RHC.referrals = RHC.referrals || {};

    var TOTAL_STEPS = 7;

    var state = {
        step: 1,
        patient: null,
        originHospitalId: null,
        destinationHospitalId: null,
        specializationId: null,
        doctorId: null,
        capability: null,
        files: []
    };

    var config = {
        hospitals: [],
        patients: [],
        specializations: [],
        maxFileSize: 10 * 1024 * 1024,
        allowedExtensions: []
    };

    /* ----------------------------------------------------------------------
       1. Map contract consumed by health-map.js
       ---------------------------------------------------------------------- */

    /**
     * Publishes hospitals in the shape health-map.js already understands.
     * The facility id is the hospital Code so it matches the Phase 2 dataset.
     */
    function publishMapData() {
        RHC.data = RHC.data || {};
        RHC.data.facilities = config.hospitals.map(function (hospital) {
            return {
                id: hospital.code,
                hospitalId: hospital.id,
                name: hospital.name,
                type: hospital.typeKey,
                typeLabel: hospital.typeLabel,
                municipality: hospital.municipality,
                address: hospital.address,
                contact: '',
                coordinates: [hospital.longitude, hospital.latitude],
                status: hospital.statusLabel.toLowerCase(),
                emergency: hospital.hasEmergency,
                doctorsAvailable: 0,
                bedsAvailable: hospital.availableBeds,
                bedsTotal: hospital.totalBeds,
                specializations: [],
                updatedMinutesAgo: 0
            };
        });

        RHC.getFacility = function (code) {
            return RHC.data.facilities.filter(function (f) { return f.id === code; })[0] || null;
        };

        // A marker click selects that hospital as the destination.
        RHC.openFacility = function (code) {
            var facility = RHC.getFacility(code);
            if (facility) {
                selectDestination(facility.hospitalId, { fly: false });
            }
        };

        RHC.closeFacility = function () {
            /* The wizard keeps the capability card visible; nothing to dismiss. */
        };
    }

    /* ----------------------------------------------------------------------
       2. Step navigation
       ---------------------------------------------------------------------- */

    function stepIsValid(step) {
        switch (step) {
            case 1: return state.patient !== null;
            case 2: return state.originHospitalId !== null;
            case 3: return state.destinationHospitalId !== null && state.specializationId !== null;
            case 4: return true;  // A preferred doctor is optional.
            case 5: return true;  // Attachments are optional.
            case 6: return document.getElementById('ReasonForReferral').value.trim().length >= 5;
            default: return true;
        }
    }

    function validationMessage(step) {
        switch (step) {
            case 1: return 'Select a patient before continuing.';
            case 2: return 'Select the referring facility.';
            case 3: return 'Select a destination facility and the specialty required.';
            case 6: return 'Describe the reason for this referral (at least 5 characters).';
            default: return 'Complete this step before continuing.';
        }
    }

    function goToStep(step) {
        if (step < 1 || step > TOTAL_STEPS) { return; }

        // Block forward movement past an incomplete step.
        if (step > state.step) {
            for (var i = state.step; i < step; i++) {
                if (!stepIsValid(i)) {
                    showStepError(i, validationMessage(i));
                    return;
                }
            }
        }

        state.step = step;
        render();
    }

    function showStepError(step, message) {
        goToStepImmediate(step);

        var banner = document.getElementById('wizardError');
        if (banner) {
            banner.textContent = message;
            banner.hidden = false;
            window.setTimeout(function () { banner.hidden = true; }, 5000);
        }
    }

    function goToStepImmediate(step) {
        state.step = step;
        render();
    }

    function render() {
        document.querySelectorAll('.wizard-panel').forEach(function (panel) {
            var step = Number(panel.getAttribute('data-step'));
            panel.classList.toggle('is-active', step === state.step);
        });

        document.querySelectorAll('.wizard-step').forEach(function (button) {
            var step = Number(button.getAttribute('data-step'));

            button.classList.toggle('is-active', step === state.step);
            button.classList.toggle('is-complete', step < state.step && stepIsValid(step));
            button.setAttribute('aria-current', step === state.step ? 'step' : 'false');
        });

        var back = document.getElementById('wizardBack');
        var next = document.getElementById('wizardNext');
        var submitGroup = document.getElementById('wizardSubmitGroup');
        var progress = document.getElementById('wizardProgress');

        if (back) { back.disabled = state.step === 1; }
        if (next) { next.hidden = state.step === TOTAL_STEPS; }
        if (submitGroup) { submitGroup.hidden = state.step !== TOTAL_STEPS; }
        if (progress) { progress.textContent = 'Step ' + state.step + ' of ' + TOTAL_STEPS; }

        if (state.step === TOTAL_STEPS) {
            renderReview();
        }

        // The map only measures correctly once its panel is visible.
        if (state.step === 3) {
            window.setTimeout(function () {
                document.dispatchEvent(new CustomEvent('rhc:layout-changed'));
            }, 60);
        }
    }

    /* ----------------------------------------------------------------------
       3. Step 1 — Patient
       ---------------------------------------------------------------------- */

    function renderPatients(patients) {
        var list = document.getElementById('patientList');
        if (!list) { return; }

        if (patients.length === 0) {
            list.innerHTML = '<li class="empty-state"><span class="empty-state-title">No patients found</span></li>';
            return;
        }

        list.innerHTML = patients.map(function (patient) {
            var selected = state.patient && state.patient.id === patient.id;

            return '<li>' +
                '<button type="button" class="option-card' + (selected ? ' is-selected' : '') + '" ' +
                        'data-patient-id="' + patient.id + '">' +
                    '<span class="option-card-body">' +
                        '<span class="option-card-title">' + Referrals.escapeHtml(patient.fullName) + '</span>' +
                        '<span class="option-card-meta">' +
                            Referrals.escapeHtml(patient.patientNumber) + ' · ' +
                            patient.age + ' yrs · ' + Referrals.escapeHtml(patient.sex) + ' · ' +
                            Referrals.escapeHtml(patient.municipality) +
                        '</span>' +
                    '</span>' +
                    '<span class="option-card-trailing">' +
                        (patient.bloodType
                            ? '<span class="rhc-badge rhc-badge-neutral">' +
                                Referrals.escapeHtml(patient.bloodType) + '</span>'
                            : '') +
                    '</span>' +
                '</button>' +
            '</li>';
        }).join('');

        list.querySelectorAll('[data-patient-id]').forEach(function (button) {
            button.addEventListener('click', function () {
                var id = Number(button.getAttribute('data-patient-id'));
                state.patient = patients.filter(function (p) { return p.id === id; })[0] || null;

                document.getElementById('PatientId').value = id;
                renderPatients(patients);
                renderPatientSummary();
            });
        });
    }

    function renderPatientSummary() {
        var summary = document.getElementById('patientSummary');
        if (!summary) { return; }

        if (!state.patient) {
            summary.hidden = true;
            return;
        }

        summary.hidden = false;
        summary.innerHTML =
            '<div class="review-block-title"><i class="fa-solid fa-user" aria-hidden="true"></i> Selected patient</div>' +
            '<div class="info-row"><span class="info-row-label">Name</span>' +
                '<span class="info-row-value">' + Referrals.escapeHtml(state.patient.fullName) + '</span></div>' +
            '<div class="info-row"><span class="info-row-label">Patient number</span>' +
                '<span class="info-row-value">' + Referrals.escapeHtml(state.patient.patientNumber) + '</span></div>' +
            '<div class="info-row"><span class="info-row-label">Age / Sex</span>' +
                '<span class="info-row-value">' + state.patient.age + ' · ' +
                Referrals.escapeHtml(state.patient.sex) + '</span></div>' +
            '<div class="info-row"><span class="info-row-label">Municipality</span>' +
                '<span class="info-row-value">' + Referrals.escapeHtml(state.patient.municipality) + '</span></div>';
    }

    function initPatientStep() {
        renderPatients(config.patients);

        var search = document.getElementById('patientSearch');
        if (!search) { return; }

        var timer = null;

        search.addEventListener('input', function () {
            window.clearTimeout(timer);

            // Debounce so typing does not fire a request per keystroke.
            timer = window.setTimeout(async function () {
                try {
                    var results = await Referrals.getJson(
                        '/Referrals/SearchPatients?term=' + encodeURIComponent(search.value.trim()));
                    renderPatients(results);
                } catch (error) {
                    window.console.warn('[wizard] Patient search failed:', error);
                }
            }, 250);
        });
    }

    /* ----------------------------------------------------------------------
       4. Steps 2 and 3 — Facilities
       ---------------------------------------------------------------------- */

    function hospitalCardMarkup(hospital, selected) {
        return '<button type="button" class="option-card' + (selected ? ' is-selected' : '') + '" ' +
                    'data-hospital-id="' + hospital.id + '">' +
                '<span class="facility-dot facility-dot-' + hospital.typeKey + '" aria-hidden="true"></span>' +
                '<span class="option-card-body">' +
                    '<span class="option-card-title">' + Referrals.escapeHtml(hospital.name) + '</span>' +
                    '<span class="option-card-meta">' +
                        Referrals.escapeHtml(hospital.typeLabel) + ' · ' +
                        Referrals.escapeHtml(hospital.municipality) +
                    '</span>' +
                '</span>' +
                '<span class="option-card-trailing">' +
                    (hospital.hasEmergency
                        ? '<span class="rhc-badge rhc-badge-info">ER</span>'
                        : '') +
                    '<span class="rhc-badge ' + hospital.statusBadgeClass + '">' +
                        Referrals.escapeHtml(hospital.statusLabel) + '</span>' +
                '</span>' +
            '</button>';
    }

    function renderOriginList() {
        var list = document.getElementById('originList');
        if (!list) { return; }

        list.innerHTML = config.hospitals.map(function (hospital) {
            return '<li>' + hospitalCardMarkup(hospital, hospital.id === state.originHospitalId) + '</li>';
        }).join('');

        list.querySelectorAll('[data-hospital-id]').forEach(function (button) {
            button.addEventListener('click', function () {
                state.originHospitalId = Number(button.getAttribute('data-hospital-id'));
                document.getElementById('OriginHospitalId').value = state.originHospitalId;

                // The origin can never also be the destination.
                if (state.destinationHospitalId === state.originHospitalId) {
                    state.destinationHospitalId = null;
                    document.getElementById('DestinationHospitalId').value = '';
                }

                renderOriginList();
                renderDestinationList();
                loadReferringDoctors();
            });
        });
    }

    function renderDestinationList() {
        var list = document.getElementById('destinationList');
        if (!list) { return; }

        var candidates = config.hospitals.filter(function (h) { return h.id !== state.originHospitalId; });

        list.innerHTML = candidates.map(function (hospital) {
            return '<li>' + hospitalCardMarkup(hospital, hospital.id === state.destinationHospitalId) + '</li>';
        }).join('');

        list.querySelectorAll('[data-hospital-id]').forEach(function (button) {
            button.addEventListener('click', function () {
                selectDestination(Number(button.getAttribute('data-hospital-id')), { fly: true });
            });
        });
    }

    /**
     * Selects the destination, loads its capability snapshot, and moves the map.
     * @param {number} hospitalId
     * @param {{fly: boolean}} options
     */
    async function selectDestination(hospitalId, options) {
        if (hospitalId === state.originHospitalId) { return; }

        // health-map.js calls back into RHC.openFacility after a selection, which
        // routes here again. Returning early on an unchanged id breaks that cycle.
        if (hospitalId === state.destinationHospitalId) { return; }

        state.destinationHospitalId = hospitalId;
        document.getElementById('DestinationHospitalId').value = hospitalId;

        renderDestinationList();
        await loadCapability();

        var hospital = config.hospitals.filter(function (h) { return h.id === hospitalId; })[0];

        // Fly to and highlight the facility using the Phase 2 map module.
        if (hospital && RHC.map && typeof RHC.map.select === 'function') {
            RHC.map.select(hospital.code, options && options.fly);
        }
    }

    async function loadCapability() {
        var panel = document.getElementById('capabilityCard');
        if (!panel || !state.destinationHospitalId) { return; }

        var url = '/Referrals/HospitalCapability?hospitalId=' + state.destinationHospitalId +
            (state.specializationId ? '&specializationId=' + state.specializationId : '');

        try {
            state.capability = await Referrals.getJson(url);
            renderCapability(state.capability);
            renderDoctorList(state.capability.doctors);
        } catch (error) {
            window.console.warn('[wizard] Capability lookup failed:', error);
            panel.innerHTML = '<div class="empty-state">' +
                '<span class="empty-state-title">Facility details unavailable</span></div>';
        }
    }

    function renderCapability(capability) {
        var panel = document.getElementById('capabilityCard');
        if (!panel) { return; }

        var services = capability.services.length
            ? capability.services.map(function (s) {
                return '<span class="rhc-chip">' + Referrals.escapeHtml(s) + '</span>';
            }).join('')
            : '<span class="cell-muted">Not recorded</span>';

        var specialties = capability.specializations.length
            ? capability.specializations.map(function (s) {
                return '<span class="rhc-chip">' + Referrals.escapeHtml(s) + '</span>';
            }).join('')
            : '<span class="cell-muted">None staffed</span>';

        panel.innerHTML =
            '<div class="rhc-card-header">' +
                '<div class="rhc-card-heading">' +
                    '<h3 class="rhc-card-title">' + Referrals.escapeHtml(capability.name) + '</h3>' +
                    '<p class="rhc-card-subtitle">' +
                        Referrals.escapeHtml(capability.typeLabel) + ' · ' +
                        Referrals.escapeHtml(capability.municipality) + '</p>' +
                '</div>' +
                '<div class="rhc-card-tools">' +
                    '<span class="rhc-badge ' + capability.statusBadgeClass + '">' +
                        Referrals.escapeHtml(capability.statusLabel) + '</span>' +
                '</div>' +
            '</div>' +
            '<div class="rhc-card-body">' +
                '<div class="capability-stats">' +
                    '<div class="capability-stat">' +
                        '<div class="capability-stat-value">' + capability.availableDoctorCount + '</div>' +
                        '<div class="capability-stat-label">Doctors available</div>' +
                    '</div>' +
                    '<div class="capability-stat">' +
                        '<div class="capability-stat-value">' + capability.availableBeds + '</div>' +
                        '<div class="capability-stat-label">Beds free of ' + capability.totalBeds + '</div>' +
                    '</div>' +
                    '<div class="capability-stat">' +
                        '<div class="capability-stat-value">' +
                            (capability.hasEmergency
                                ? '<i class="fa-solid fa-truck-medical" aria-hidden="true"></i>'
                                : '<i class="fa-solid fa-minus" aria-hidden="true"></i>') +
                        '</div>' +
                        '<div class="capability-stat-label">' +
                            (capability.hasEmergency ? 'Emergency capable' : 'No emergency') + '</div>' +
                    '</div>' +
                '</div>' +
                '<div class="capability-section">' +
                    '<p class="capability-section-label">Address</p>' +
                    '<p>' + Referrals.escapeHtml(capability.address) + '</p>' +
                '</div>' +
                '<div class="capability-section">' +
                    '<p class="capability-section-label">Services</p>' +
                    '<div class="chip-group">' + services + '</div>' +
                '</div>' +
                '<div class="capability-section">' +
                    '<p class="capability-section-label">Available specialists</p>' +
                    '<div class="chip-group">' + specialties + '</div>' +
                '</div>' +
            '</div>';
    }

    function initSpecializationPicker() {
        var select = document.getElementById('RequestedSpecializationId');
        if (!select) { return; }

        select.addEventListener('change', async function () {
            state.specializationId = select.value ? Number(select.value) : null;

            // Re-query so the doctor list narrows to the requested specialty.
            if (state.destinationHospitalId) {
                await loadCapability();
            }
        });
    }

    /* ----------------------------------------------------------------------
       5. Step 4 — Doctor
       ---------------------------------------------------------------------- */

    function renderDoctorList(doctors) {
        var list = document.getElementById('doctorList');
        if (!list) { return; }

        if (!doctors || doctors.length === 0) {
            list.innerHTML = '<li class="empty-state">' +
                '<i class="fa-solid fa-user-doctor empty-state-icon" aria-hidden="true"></i>' +
                '<span class="empty-state-title">No matching doctors</span>' +
                '<span>Choose a different specialty or leave this step blank.</span></li>';
            return;
        }

        list.innerHTML = doctors.map(function (doctor) {
            var selected = state.doctorId === doctor.id;

            return '<li>' +
                '<button type="button" class="option-card' + (selected ? ' is-selected' : '') + '" ' +
                        'data-doctor-id="' + doctor.id + '"' + (doctor.isAccepting ? '' : ' disabled') + '>' +
                    '<span class="option-card-body">' +
                        '<span class="option-card-title">' + Referrals.escapeHtml(doctor.fullName) + '</span>' +
                        '<span class="option-card-meta">' +
                            Referrals.escapeHtml(doctor.specialization) + '</span>' +
                    '</span>' +
                    '<span class="option-card-trailing">' +
                        '<span class="rhc-badge ' + doctor.availabilityBadgeClass + '">' +
                            Referrals.escapeHtml(doctor.availabilityLabel) + '</span>' +
                    '</span>' +
                '</button>' +
            '</li>';
        }).join('');

        list.querySelectorAll('[data-doctor-id]').forEach(function (button) {
            button.addEventListener('click', function () {
                var id = Number(button.getAttribute('data-doctor-id'));

                // Clicking the selected doctor clears the preference.
                state.doctorId = state.doctorId === id ? null : id;
                document.getElementById('AssignedDoctorId').value = state.doctorId || '';

                renderDoctorList(doctors);
            });
        });
    }

    async function loadReferringDoctors() {
        var select = document.getElementById('ReferringDoctorId');
        if (!select || !state.originHospitalId) { return; }

        try {
            var doctors = await Referrals.getJson(
                '/Referrals/AvailableDoctors?hospitalId=' + state.originHospitalId);

            select.innerHTML = '<option value="">Not specified</option>' +
                doctors.map(function (d) {
                    return '<option value="' + d.id + '">' +
                        Referrals.escapeHtml(d.name + ' — ' + d.specialization) + '</option>';
                }).join('');
        } catch (error) {
            window.console.warn('[wizard] Referring doctor lookup failed:', error);
        }
    }

    /* ----------------------------------------------------------------------
       6. Step 5 — Attachments
       ---------------------------------------------------------------------- */

    function fileIsAllowed(file) {
        var dot = file.name.lastIndexOf('.');
        if (dot < 0) { return false; }

        var extension = file.name.slice(dot).toLowerCase();
        return config.allowedExtensions.indexOf(extension) !== -1 && file.size <= config.maxFileSize;
    }

    function renderAttachments() {
        var list = document.getElementById('attachmentList');
        if (!list) { return; }

        list.innerHTML = state.files.map(function (entry, index) {
            var thumb = entry.previewUrl
                ? '<span class="attachment-thumb"><img src="' + entry.previewUrl + '" alt=""></span>'
                : '<span class="attachment-thumb">' +
                    Referrals.escapeHtml(entry.file.name.split('.').pop().toUpperCase()) + '</span>';

            var sizeKb = entry.file.size < 1024 * 1024
                ? (entry.file.size / 1024).toFixed(1) + ' KB'
                : (entry.file.size / (1024 * 1024)).toFixed(1) + ' MB';

            return '<div class="attachment-card">' +
                thumb +
                '<span class="attachment-body">' +
                    '<span class="attachment-name">' + Referrals.escapeHtml(entry.file.name) + '</span>' +
                    '<span class="attachment-meta">' + sizeKb + ' · ' +
                        Referrals.escapeHtml(entry.categoryLabel) + '</span>' +
                '</span>' +
                '<button type="button" class="rhc-icon-btn" data-remove-file="' + index + '" ' +
                        'aria-label="Remove ' + Referrals.escapeHtml(entry.file.name) + '">' +
                    '<i class="fa-solid fa-xmark" aria-hidden="true"></i>' +
                '</button>' +
            '</div>';
        }).join('');

        list.querySelectorAll('[data-remove-file]').forEach(function (button) {
            button.addEventListener('click', function () {
                var index = Number(button.getAttribute('data-remove-file'));

                if (state.files[index].previewUrl) {
                    URL.revokeObjectURL(state.files[index].previewUrl);
                }

                state.files.splice(index, 1);
                syncFileInput();
                renderAttachments();
            });
        });
    }

    /**
     * Rebuilds the real file input and the parallel category inputs so the
     * standard multipart post carries exactly what the user kept.
     */
    function syncFileInput() {
        var input = document.getElementById('attachmentInput');
        var categoryHost = document.getElementById('attachmentCategories');
        if (!input || !categoryHost) { return; }

        var transfer = new DataTransfer();
        state.files.forEach(function (entry) { transfer.items.add(entry.file); });
        input.files = transfer.files;

        categoryHost.innerHTML = state.files.map(function (entry) {
            return '<input type="hidden" name="AttachmentCategories" value="' + entry.category + '">';
        }).join('');
    }

    function addFiles(fileList) {
        var rejected = [];

        Array.prototype.forEach.call(fileList, function (file) {
            if (!fileIsAllowed(file)) {
                rejected.push(file.name);
                return;
            }

            var categorySelect = document.getElementById('attachmentCategory');
            var category = categorySelect ? categorySelect.value : 'Document';
            var categoryLabel = categorySelect
                ? categorySelect.options[categorySelect.selectedIndex].text
                : 'Document';

            state.files.push({
                file: file,
                category: category,
                categoryLabel: categoryLabel,
                previewUrl: file.type.indexOf('image/') === 0 ? URL.createObjectURL(file) : null
            });
        });

        if (rejected.length > 0) {
            Referrals.showToast(
                'Some files were skipped',
                rejected.join(', ') + ' — only PDF, JPEG, PNG, and DOCX up to 10 MB are accepted.',
                'fa-triangle-exclamation');
        }

        syncFileInput();
        renderAttachments();
    }

    function initAttachments() {
        var dropzone = document.getElementById('attachmentDropzone');
        var input = document.getElementById('attachmentInput');
        if (!dropzone || !input) { return; }

        dropzone.addEventListener('click', function () { input.click(); });

        dropzone.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                input.click();
            }
        });

        // The input is the source of truth; picking files funnels through addFiles.
        input.addEventListener('change', function () {
            var picked = Array.prototype.slice.call(input.files);
            input.value = '';
            addFiles(picked);
        });

        ['dragenter', 'dragover'].forEach(function (name) {
            dropzone.addEventListener(name, function (event) {
                event.preventDefault();
                dropzone.classList.add('is-dragover');
            });
        });

        ['dragleave', 'drop'].forEach(function (name) {
            dropzone.addEventListener(name, function (event) {
                event.preventDefault();
                dropzone.classList.remove('is-dragover');
            });
        });

        dropzone.addEventListener('drop', function (event) {
            if (event.dataTransfer && event.dataTransfer.files) {
                addFiles(event.dataTransfer.files);
            }
        });
    }

    /* ----------------------------------------------------------------------
       7. Step 7 — Review
       ---------------------------------------------------------------------- */

    function renderReview() {
        var container = document.getElementById('reviewContent');
        if (!container) { return; }

        var origin = config.hospitals.filter(function (h) { return h.id === state.originHospitalId; })[0];
        var destination = config.hospitals.filter(function (h) { return h.id === state.destinationHospitalId; })[0];

        var specialtySelect = document.getElementById('RequestedSpecializationId');
        var specialty = specialtySelect && specialtySelect.selectedIndex > 0
            ? specialtySelect.options[specialtySelect.selectedIndex].text
            : 'Not selected';

        var doctor = 'No preference';
        if (state.doctorId && state.capability) {
            var match = state.capability.doctors.filter(function (d) { return d.id === state.doctorId; })[0];
            if (match) { doctor = match.fullName; }
        }

        var prioritySelect = document.getElementById('Priority');
        var priority = prioritySelect
            ? prioritySelect.options[prioritySelect.selectedIndex].text
            : 'Routine';

        function block(title, icon, rows) {
            return '<div class="review-block">' +
                '<div class="review-block-title"><i class="fa-solid ' + icon + '" aria-hidden="true"></i> ' +
                    title + '</div>' + rows + '</div>';
        }

        function row(label, value) {
            return '<div class="info-row">' +
                '<span class="info-row-label">' + label + '</span>' +
                '<span class="info-row-value">' + Referrals.escapeHtml(value || 'Not provided') + '</span>' +
            '</div>';
        }

        container.innerHTML =
            block('Patient', 'fa-user',
                row('Name', state.patient ? state.patient.fullName : '') +
                row('Patient number', state.patient ? state.patient.patientNumber : '') +
                row('Age / Sex', state.patient ? state.patient.age + ' · ' + state.patient.sex : '')) +

            block('Transfer', 'fa-right-left',
                row('From', origin ? origin.name : '') +
                row('To', destination ? destination.name : '') +
                row('Specialty requested', specialty) +
                row('Preferred doctor', doctor) +
                row('Priority', priority)) +

            block('Clinical detail', 'fa-notes-medical',
                row('Reason', document.getElementById('ReasonForReferral').value) +
                row('Diagnosis', document.getElementById('Diagnosis').value) +
                row('Notes', document.getElementById('ClinicalNotes').value)) +

            block('Attachments', 'fa-paperclip',
                state.files.length === 0
                    ? '<p class="cell-muted">No files attached.</p>'
                    : state.files.map(function (entry) {
                        return row(entry.categoryLabel, entry.file.name);
                    }).join(''));
    }

    /* ----------------------------------------------------------------------
       8. Bootstrap
       ---------------------------------------------------------------------- */

    function readConfig() {
        var node = document.getElementById('wizardData');
        if (!node) { return false; }

        try {
            var parsed = JSON.parse(node.textContent);

            config.hospitals = parsed.hospitals || [];
            config.patients = parsed.patients || [];
            config.specializations = parsed.specializations || [];
            config.maxFileSize = parsed.maxFileSizeBytes || config.maxFileSize;
            config.allowedExtensions = parsed.allowedExtensions || [];

            state.originHospitalId = parsed.defaultOriginHospitalId || null;
            return true;
        } catch (error) {
            window.console.error('[wizard] Could not parse wizard data:', error);
            return false;
        }
    }

    function initNavigation() {
        var back = document.getElementById('wizardBack');
        var next = document.getElementById('wizardNext');

        if (back) {
            back.addEventListener('click', function () { goToStep(state.step - 1); });
        }

        if (next) {
            next.addEventListener('click', function () { goToStep(state.step + 1); });
        }

        document.querySelectorAll('.wizard-step').forEach(function (button) {
            button.addEventListener('click', function () {
                goToStep(Number(button.getAttribute('data-step')));
            });
        });

        // Guard the final post in case a step was bypassed.
        var form = document.getElementById('createReferralForm');
        if (form) {
            form.addEventListener('submit', function (event) {
                for (var i = 1; i <= 6; i++) {
                    if (!stepIsValid(i)) {
                        event.preventDefault();
                        showStepError(i, validationMessage(i));
                        return;
                    }
                }
            });
        }

        var draftButton = document.getElementById('saveDraftButton');
        if (draftButton) {
            draftButton.addEventListener('click', function () {
                document.getElementById('SaveAsDraft').value = 'true';
            });
        }
    }

    function init() {
        if (!readConfig()) { return; }

        publishMapData();

        initNavigation();
        initPatientStep();
        renderOriginList();
        renderDestinationList();
        initSpecializationPicker();
        initAttachments();
        loadReferringDoctors();

        render();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})(window, document);
