// ============================
// SHARED HELPERS
// ============================

function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/[&<>"']/g, c => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    }[c]));
}

// ============================
// FLOW DIAGRAM FUNCTIONS
// ============================

function updateFlowStep(step, status, color, message) {
    const colors = { success: '#28a745', danger: '#dc3545', warning: '#ffc107', secondary: '#6c757d', info: '#17a2b8' };
    const statusTexts = { success: '✅ Pass', danger: '❌ Fail', warning: '⏳ Run', secondary: '⏸️', info: '🔍 Test' };

    const fillColor = colors[color] || '#6c757d';
    const strokeColor = color === 'danger' ? '#721c24' : color === 'success' ? '#155724' : '#343a40';

    const circle = document.getElementById(`flowStep${step}`);
    if (circle) {
        circle.setAttribute('fill', fillColor);
        circle.setAttribute('stroke', strokeColor);
    }

    const statusEl = document.getElementById(`flowStep${step}Status`);
    if (statusEl) {
        statusEl.textContent = statusTexts[status] || status;
        statusEl.setAttribute('fill', fillColor);
    }

    if (step < 4) {
        const arrow = document.getElementById(`flowArrow${step}`);
        if (arrow) {
            const marker = color === 'success' ? 'url(#arrowhead-success)' :
                color === 'danger' ? 'url(#arrowhead-error)' : 'url(#arrowhead)';
            arrow.setAttribute('stroke', fillColor);
            arrow.setAttribute('marker-end', marker);
        }
    }
}

function resetFlowDiagram() {
    for (let i = 1; i <= 4; i++) updateFlowStep(i, 'secondary', 'secondary', '');
    for (let i = 1; i <= 3; i++) {
        const arrow = document.getElementById(`flowArrow${i}`);
        if (arrow) {
            arrow.setAttribute('stroke', '#6c757d');
            arrow.setAttribute('marker-end', 'url(#arrowhead)');
        }
    }
    const statusEl = document.getElementById('flowStatus');
    if (statusEl) { statusEl.textContent = '⏳ Waiting...'; statusEl.className = 'badge bg-secondary ms-2'; }
}

function updateFlowStepTesting(step, message) {
    const circle = document.getElementById(`flowStep${step}`);
    if (circle) {
        circle.setAttribute('fill', '#17a2b8');
        circle.setAttribute('stroke', '#0c5460');
    }
    const statusEl = document.getElementById(`flowStep${step}Status`);
    if (statusEl) {
        statusEl.textContent = message || '🔍 Test';
        statusEl.setAttribute('fill', '#17a2b8');
    }
}

// ============================
// GLOBALS
// ============================

let testCancelled = false;
let detailedTestCancelled = false;
let detailedLogLines = [];
let currentInspectedDevice = null;

// ============================
// DETAILED LOG HELPER
// ============================

function detailedLog(message, level = 'info') {
    const ts = new Date().toLocaleTimeString('en-GB', { hour12: false }) + '.' +
        String(new Date().getMilliseconds()).padStart(3, '0');
    const colorClass = {
        info: 'text-info', success: 'text-success', warn: 'text-warning',
        error: 'text-danger', muted: 'text-secondary'
    }[level] || 'text-light';

    const line = `[${ts}] ${message}`;
    detailedLogLines.push(line);

    const logEl = document.getElementById('detailedTestLog');
    if (logEl) {
        const row = document.createElement('div');
        row.className = colorClass;
        row.textContent = line;
        logEl.appendChild(row);
        logEl.scrollTop = logEl.scrollHeight;
    }
}

// ============================
// STANDARD TEST
// ============================

function initStandardTest() {
    $('.test-btn').click(async function () {
        const btn = $(this);
        const modal = $('#testModal');
        const modalBody = $('#testModalBody');
        const cancelBtn = $('#cancelTestBtn');
        const deviceName = btn.data('name');
        const deviceTypeName = btn.closest('tr').find('td:nth-child(2)').text().trim();

        const ip = btn.data('ip');
        const port = parseInt(btn.data('port'));
        const slaveId = parseInt(btn.data('slave'));
        const timeout = parseInt(btn.data('timeout')) || 5000;

        testCancelled = false;
        cancelBtn.removeClass('d-none');

        modalBody.html(`<div class="text-center"><i class="fas fa-spinner fa-spin fa-2x text-primary"></i><p class="mt-2">Getting mappings for <strong>${esc(deviceName)}</strong>...</p></div>`);
        modal.modal('show');

        try {
            let mappings = [];
            try {
                const typeResponse = await fetch(`/api/devicetypes/${encodeURIComponent(deviceTypeName)}`);
                if (typeResponse.ok) {
                    const deviceTypeInfo = await typeResponse.json();
                    mappings = deviceTypeInfo.mappings || [];
                } else {
                    const mappingResponse = await fetch(`/api/devicetypes/${encodeURIComponent(deviceTypeName)}/mappings`);
                    if (mappingResponse.ok) mappings = await mappingResponse.json();
                }
            } catch (error) { console.warn('Failed to get mappings:', error); }

            if (testCancelled) return;

            if (!mappings || mappings.length === 0) {
                cancelBtn.addClass('d-none');
                modalBody.html(`<div class="alert alert-warning"><h5><i class="fas fa-exclamation-triangle"></i> No Mappings Found</h5><p>No register mappings found for device type: <strong>${esc(deviceTypeName)}</strong></p></div>`);
                return;
            }

            modalBody.html(`<div class="text-center"><i class="fas fa-spinner fa-spin fa-2x text-primary"></i><p class="mt-2">Testing <strong>${esc(deviceName)}</strong>...</p><p class="text-muted small">${esc(ip)}:${esc(port)} (Slave ${esc(slaveId)}) - Testing ${mappings.length} registers</p><div class="progress mt-3" style="height:25px;"><div id="testProgress" class="progress-bar progress-bar-striped progress-bar-animated bg-info" style="width:0%;" role="progressbar">0%</div></div></div>`);

            const results = []; let passedCount = 0; let failedCount = 0; const diagnosticsByReason = new Map();

            for (let i = 0; i < mappings.length; i++) {
                if (testCancelled) break;
                const mapping = mappings[i];
                const registerAddress = mapping.startAddress ?? mapping.StartAddress;
                const mappingName = mapping.name || mapping.Name || `Register ${registerAddress}`;
                const displayName = mapping.displayName || mapping.DisplayName || mappingName;
                const dataType = mapping.dataType || mapping.DataType || 'Unknown';
                const unit = mapping.unit || mapping.Unit || '';
                const registerType = mapping.registerType || mapping.RegisterType || null;
                const count = mapping.count ?? mapping.Count ?? null;
                const byteOrder = mapping.byteOrder || mapping.ByteOrder || null;
                const factor = mapping.factor ?? mapping.Factor ?? null;

                const progress = Math.round(((i + 1) / mappings.length) * 100);
                $('#testProgress').css('width', progress + '%').text(`${progress}% - Testing ${displayName}`);

                try {
                    const testData = { ipAddress: ip, port, slaveId, testRegister: registerAddress, dataType, registerType, count, byteOrder, factor, timeoutMs: timeout };
                    const response = await fetch('/api/modbus/tcp/test', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' }, body: JSON.stringify(testData) });

                    let success = false, value = null, error = null, suggestion = null, diagnostics = [];
                    if (response.ok) {
                        const result = await response.json();
                        success = result.isSuccessful; value = result.testValue; diagnostics = result.diagnostics || [];
                        if (!success) {
                            const failing = diagnostics.find(d => !d.isPassed);
                            if (failing) {
                                error = failing.message; suggestion = failing.suggestion || null;
                                const key = `${failing.name}:${failing.message}`;
                                if (!diagnosticsByReason.has(key)) diagnosticsByReason.set(key, { diagnostics, registers: [mappingName] });
                                else diagnosticsByReason.get(key).registers.push(mappingName);
                            } else error = result.errorDetails || result.message || 'Test failed';
                        }
                    } else error = `HTTP ${response.status}`;

                    results.push({ mapping, registerAddress, mappingName, displayName, dataType, unit, success, value, error, suggestion });
                    success ? passedCount++ : failedCount++;
                } catch (error) {
                    results.push({ mapping, registerAddress, mappingName, displayName, dataType, unit, success: false, value: null, error: error.message });
                    failedCount++;
                }
            }

            cancelBtn.addClass('d-none');
            const allPassed = failedCount === 0 && results.length > 0;

            let html = `<div class="alert ${allPassed ? 'alert-success' : 'alert-warning'}"><h5><i class="fas ${allPassed ? 'fa-check-circle' : 'fa-exclamation-triangle'}"></i> ${allPassed ? 'All registers passed!' : `${failedCount} of ${results.length} tested registers failed`}</h5><div class="row mt-2"><div class="col-md-4"><small class="text-muted">Device</small><br /><strong>${esc(deviceName)}</strong></div><div class="col-md-4"><small class="text-muted">Total Registers</small><br /><strong>${mappings.length}</strong></div><div class="col-md-4"><small class="text-muted">Passed</small><br /><strong>${passedCount} / ${results.length}</strong></div></div></div>`;

            if (!allPassed && diagnosticsByReason.size > 0) {
                html += `<div class="mt-3"><h6><i class="fas fa-list-check"></i> Diagnostic Details</h6><table class="table table-sm table-striped table-bordered"><thead class="table-light"><tr><th>Step</th><th>Status</th><th>Message</th><th>Suggestion</th><th>Affected Registers</th></tr></thead><tbody>`;
                for (const [, group] of diagnosticsByReason) {
                    const failing = group.diagnostics.find(d => !d.isPassed);
                    html += `<tr class="${failing?.isPassed ? 'table-success' : 'table-danger'}"><td><strong>${esc(failing?.name || 'Unknown')}</strong></td><td>${failing?.isPassed ? '✅' : '❌'}</td><td>${esc(failing?.message || '')}</td><td>${esc(failing?.suggestion || '-')}</td><td>${group.registers.map(esc).join(', ')}</td></tr>`;
                }
                html += `</tbody></table></div>`;
            }

            html += `<div class="mt-3"><h6><i class="fas fa-list"></i> Register Test Results (${results.length})</h6><div style="max-height:400px;overflow-y:auto;"><table class="table table-sm table-striped table-bordered"><thead class="table-light"><tr><th>#</th><th>Name</th><th>Address</th><th>Type</th><th>Status</th><th>Value</th></tr></thead><tbody>`;
            html += results.map((r, index) => `<tr class="${r.success ? 'table-success' : 'table-danger'}"><td>${index + 1}</td><td><strong>${esc(r.mappingName)}</strong></td><td><span class="badge bg-secondary">${esc(r.registerAddress)}</span></td><td><span class="badge bg-info">${esc(r.dataType)}</span></td><td>${r.success ? '<span class="badge bg-success"><i class="fas fa-check"></i> Pass</span>' : '<span class="badge bg-danger"><i class="fas fa-times"></i> Fail</span>'}</td><td>${r.success ? `${esc(r.value)} ${esc(r.unit || '')}` : `<span class="text-danger">${esc(r.error || 'Unknown error')}</span>${r.suggestion ? `<br /><small class="text-muted"><i class="fas fa-lightbulb"></i> ${esc(r.suggestion)}</small>` : ''}`}</td></tr>`).join('');
            html += `</tbody></table></div></div>`;

            modalBody.html(html);

        } catch (error) {
            console.error('Test error:', error);
            cancelBtn.addClass('d-none');
            modalBody.html(`<div class="alert alert-danger"><h5><i class="fas fa-exclamation-triangle"></i> Test Error</h5><p><strong>${esc(error.message)}</strong></p><hr /><small class="text-muted">Make sure the API is running: /api/modbus/tcp/test</small></div>`);
        }
    });

    $('#cancelTestBtn').click(function () { testCancelled = true; $(this).addClass('d-none'); });
    $('#testModal').on('hidden.bs.modal', function () { testCancelled = true; });
}

// ============================
// DETAILED TEST
// ============================

function resetDetailedTestUI(deviceName) {
    detailedTestCancelled = false; detailedLogLines = [];
    $('#detailedTestDeviceName').text(deviceName);
    $('#detailedTestLog').empty();
    $('#detailedResultsBody').empty();
    $('#detailedTestRootCause').html('<div class="text-muted text-center py-4">Root cause analysis will appear here once the test completes.</div>');
    $('#statPassed, #statFailed').text('0');
    $('#statAvgMs, #statMinMs, #statMaxMs, #statTotalMs').text('–');
    $('#rootCauseBadge').addClass('d-none').text('0');
    $('#detailedTestProgressBar').css('width', '0%').text('0%');
    $('#detailedTestCounter').text('0 / 0');
    $('#detailedTestStatusLabel').text('Preparing test...');
    $('#cancelDetailedTestBtn').removeClass('d-none');
    $('#exportLogBtn').addClass('d-none');
    resetFlowDiagram();
    const logTabTrigger = document.querySelector('#detailedTestTabs button[data-bs-target="#tabLiveLog"]');
    if (logTabTrigger) bootstrap.Tab.getOrCreateInstance(logTabTrigger).show();
}

function initDetailedTest() {
    $('.detailed-test-btn').click(async function () {
        const btn = $(this);
        const modal = $('#detailedTestModal');
        const deviceName = btn.data('name');
        const deviceTypeName = btn.closest('tr').find('td:nth-child(2)').text().trim();
        const ip = btn.data('ip');
        const port = parseInt(btn.data('port'));
        const slaveId = parseInt(btn.data('slave'));
        const timeout = parseInt(btn.data('timeout')) || 5000;

        resetDetailedTestUI(deviceName);
        modal.modal('show');

        detailedLog(`Starting detailed test for "${deviceName}"`, 'info');
        detailedLog(`Target: ${ip}:${port} | Slave ID: ${slaveId} | Timeout: ${timeout}ms`, 'muted');

        updateFlowStep(1, 'warning', 'warning', 'Loading...');
        document.getElementById('flowStatus').textContent = '⏳ Loading mappings...';
        document.getElementById('flowStatus').className = 'badge bg-warning ms-2';

        const overallStart = performance.now();

        try {
            detailedLog(`Fetching register mappings for device type "${deviceTypeName}"...`, 'info');
            let mappings = [];

            try {
                const typeResponse = await fetch(`/api/devicetypes/${encodeURIComponent(deviceTypeName)}`);
                if (typeResponse.ok) {
                    const deviceTypeInfo = await typeResponse.json();
                    mappings = deviceTypeInfo.mappings || [];
                    detailedLog(`Loaded ${mappings.length} mapping(s).`, 'success');
                    updateFlowStep(1, 'success', 'success', 'Loaded');
                } else {
                    detailedLog(`Primary endpoint returned HTTP ${typeResponse.status}, trying fallback...`, 'warn');
                    const mappingResponse = await fetch(`/api/devicetypes/${encodeURIComponent(deviceTypeName)}/mappings`);
                    if (mappingResponse.ok) {
                        mappings = await mappingResponse.json();
                        detailedLog(`Loaded ${mappings.length} mapping(s) via fallback.`, 'success');
                        updateFlowStep(1, 'success', 'success', 'Loaded');
                    }
                }
            } catch (err) {
                detailedLog(`Failed to fetch mappings: ${err.message}`, 'error');
                updateFlowStep(1, 'danger', 'danger', 'Failed');
            }

            if (!mappings || mappings.length === 0) {
                detailedLog('No mappings found — aborting test.', 'error');
                $('#detailedTestStatusLabel').text('No mappings found');
                $('#cancelDetailedTestBtn').addClass('d-none');
                $('#exportLogBtn').removeClass('d-none');
                document.getElementById('flowStatus').textContent = '🔴 No mappings found';
                document.getElementById('flowStatus').className = 'badge bg-danger ms-2';
                return;
            }

            $('#detailedTestStatusLabel').text(`Testing ${mappings.length} register(s)...`);
            $('#detailedTestCounter').text(`0 / ${mappings.length}`);

            updateFlowStep(2, 'warning', 'warning', 'Testing...');
            document.getElementById('flowStatus').textContent = '⏳ Testing network...';
            document.getElementById('flowStatus').className = 'badge bg-warning ms-2';

            const results = []; const durations = []; let passedCount = 0; let failedCount = 0;
            const diagnosticsByReason = new Map();

            for (let i = 0; i < mappings.length; i++) {
                if (detailedTestCancelled) { detailedLog('Test cancelled by user.', 'warn'); break; }

                const mapping = mappings[i];
                const registerAddress = mapping.startAddress ?? mapping.StartAddress;
                const mappingName = mapping.name || mapping.Name || `Register ${registerAddress}`;
                const dataType = mapping.dataType || mapping.DataType || 'Unknown';
                const unit = mapping.unit || mapping.Unit || '';
                const registerType = mapping.registerType || mapping.RegisterType || null;
                const count = mapping.count ?? mapping.Count ?? null;
                const byteOrder = mapping.byteOrder || mapping.ByteOrder || null;
                const factor = mapping.factor ?? mapping.Factor ?? null;

                const progress = Math.round(((i + 1) / mappings.length) * 100);
                $('#detailedTestProgressBar').css('width', progress + '%').text(progress + '%');
                $('#detailedTestCounter').text(`${i + 1} / ${mappings.length}`);
                updateFlowStepTesting(4, `${mappingName}`);

                detailedLog(`→ Testing "${mappingName}" (address ${registerAddress}, type ${dataType})...`, 'info');
                const regStart = performance.now();

                let success = false, value = null, error = null, suggestion = null, diagnostics = [];

                try {
                    const response = await fetch('/api/modbus/tcp/test', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                        body: JSON.stringify({ ipAddress: ip, port, slaveId, testRegister: registerAddress, timeoutMs: timeout, dataType, registerType, count, byteOrder, factor })
                    });

                    const durationMs = Math.round(performance.now() - regStart);
                    durations.push(durationMs);

                    if (response.ok) {
                        const result = await response.json();
                        success = result.isSuccessful; value = result.testValue; diagnostics = result.diagnostics || [];

                        if (i === 0 && diagnostics.length > 0) {
                            let stepIdx = 1;
                            diagnostics.forEach(d => { if (stepIdx <= 4) { if (d.isPassed) updateFlowStep(stepIdx, 'success', 'success', d.name); else updateFlowStep(stepIdx, 'danger', 'danger', d.name); stepIdx++; } });
                        }

                        if (success) {
                            detailedLog(`  ✓ Pass — value ${value} ${unit || ''} (${durationMs}ms)`, 'success');
                        } else {
                            const failing = diagnostics.find(d => !d.isPassed);
                            if (failing) {
                                error = failing.message; suggestion = failing.suggestion || null;
                                detailedLog(`  ✗ Fail — ${failing.name}: ${failing.message} (${durationMs}ms)`, 'error');
                                const key = `${failing.name}:${failing.message}`;
                                if (!diagnosticsByReason.has(key)) diagnosticsByReason.set(key, { diagnostics, registers: [mappingName] });
                                else diagnosticsByReason.get(key).registers.push(mappingName);
                            } else { error = result.errorDetails || result.message || 'Test failed'; detailedLog(`  ✗ Fail — ${error} (${durationMs}ms)`, 'error'); }
                        }
                    } else { error = `HTTP ${response.status}`; detailedLog(`  ✗ Fail — ${error} (${durationMs}ms)`, 'error'); }

                    results.push({ mapping, registerAddress, mappingName, dataType, unit, success, value, error, suggestion, durationMs });
                } catch (err) {
                    const durationMs = Math.round(performance.now() - regStart);
                    durations.push(durationMs);
                    error = err.message;
                    detailedLog(`  ✗ Exception — ${err.message} (${durationMs}ms)`, 'error');
                    results.push({ mapping, registerAddress, mappingName, dataType, unit, success: false, value: null, error, suggestion: null, durationMs });
                }

                success ? passedCount++ : failedCount++;

                $('#statPassed').text(passedCount); $('#statFailed').text(failedCount);
                if (durations.length > 0) {
                    const avg = Math.round(durations.reduce((a, b) => a + b, 0) / durations.length);
                    $('#statAvgMs').text(avg); $('#statMinMs').text(Math.min(...durations)); $('#statMaxMs').text(Math.max(...durations));
                }

                const r = results[results.length - 1];
                $('#detailedResultsBody').append(`<tr class="${r.success ? 'table-success' : 'table-danger'}"><td>${results.length}</td><td><strong>${esc(r.mappingName)}</strong></td><td><span class="badge bg-secondary">${esc(r.registerAddress)}</span></td><td><span class="badge bg-info">${esc(r.dataType)}</span></td><td>${r.success ? '<span class="badge bg-success"><i class="fas fa-check"></i> Pass</span>' : '<span class="badge bg-danger"><i class="fas fa-times"></i> Fail</span>'}</td><td>${r.success ? `${esc(r.value)} ${esc(r.unit || '')}` : `<span class="text-danger">${esc(r.error || 'Unknown error')}</span>${r.suggestion ? `<br /><small class="text-muted"><i class="fas fa-lightbulb"></i> ${esc(r.suggestion)}</small>` : ''}`}</td><td class="${r.durationMs > timeout * 0.8 ? 'text-warning fw-bold' : ''}">${r.durationMs}</td></tr>`);
            }

            const overallMs = Math.round(performance.now() - overallStart);
            $('#statTotalMs').text((overallMs / 1000).toFixed(1) + 's');

            detailedLog('─────────────────────────────', 'muted');
            if (detailedTestCancelled) {
                detailedLog(`Test stopped early: ${results.length} of ${mappings.length} registers tested.`, 'warn');
                $('#detailedTestStatusLabel').text(`Cancelled — ${results.length}/${mappings.length} tested`);
            } else {
                detailedLog(`Test complete: ${passedCount} passed, ${failedCount} failed, total time ${(overallMs / 1000).toFixed(1)}s.`, passedCount === results.length ? 'success' : 'warn');
                $('#detailedTestStatusLabel').text(failedCount === 0 ? 'All registers passed' : `${failedCount} of ${results.length} registers failed`);
            }

            document.getElementById('flowStatus').textContent = failedCount === 0 ? '✅ All tests passed' : '⚠️ Some tests failed';
            document.getElementById('flowStatus').className = failedCount === 0 ? 'badge bg-success ms-2' : 'badge bg-danger ms-2';

            if (durations.length > 1) {
                const avg = durations.reduce((a, b) => a + b, 0) / durations.length;
                const slow = results.filter(r => r.durationMs > avg * 2 && r.durationMs > 200).sort((a, b) => b.durationMs - a.durationMs).slice(0, 5);
                if (slow.length > 0) {
                    detailedLog(`Bottleneck check: ${slow.length} register(s) responded 2x+ slower than average:`, 'warn');
                    slow.forEach(s => detailedLog(`   • ${s.mappingName} — ${s.durationMs}ms`, 'warn'));
                }
            }

            $('#cancelDetailedTestBtn').addClass('d-none');
            $('#exportLogBtn').removeClass('d-none');

            if (diagnosticsByReason.size > 0) {
                $('#rootCauseBadge').removeClass('d-none').text(diagnosticsByReason.size);
                let rcHtml = '';
                for (const [, group] of diagnosticsByReason) {
                    const failing = group.diagnostics.find(d => !d.isPassed);
                    const affected = group.registers;
                    rcHtml += `<div class="card mb-3"><div class="card-header bg-danger text-white d-flex justify-content-between"><span><i class="fas fa-triangle-exclamation"></i> ${esc(failing?.name || 'Unknown')}: ${esc(failing?.message || '')}</span><span class="badge bg-light text-dark">${affected.length} register(s)</span></div><div class="card-body"><p class="mb-2"><i class="fas fa-lightbulb text-warning"></i> <strong>Suggestion:</strong> ${esc(failing?.suggestion || 'No suggestion available.')}</p><p class="mb-0 small text-muted"><strong>Affected registers:</strong> ${affected.map(esc).join(', ')}</p></div></div>`;
                }
                $('#detailedTestRootCause').html(rcHtml);
            } else {
                $('#detailedTestRootCause').html('<div class="alert alert-success text-center"><i class="fas fa-circle-check"></i> No failures — nothing to analyze.</div>');
            }

        } catch (error) {
            detailedLog(`Unexpected error: ${error.message}`, 'error');
            $('#detailedTestStatusLabel').text('Test error');
            $('#cancelDetailedTestBtn').addClass('d-none');
            $('#exportLogBtn').removeClass('d-none');
            document.getElementById('flowStatus').textContent = '🔴 Test error';
            document.getElementById('flowStatus').className = 'badge bg-danger ms-2';
        }
    });

    $('#cancelDetailedTestBtn').click(function () { detailedTestCancelled = true; $(this).addClass('d-none'); });
    $('#detailedTestModal').on('hidden.bs.modal', function () { detailedTestCancelled = true; });
    $('#exportLogBtn').click(function () {
        const blob = new Blob([detailedLogLines.join('\n')], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        const stamp = new Date().toISOString().replace(/[:.]/g, '-');
        a.href = url;
        a.download = `modbus-test-log-${stamp}.txt`;
        document.body.appendChild(a); a.click(); document.body.removeChild(a); URL.revokeObjectURL(url);
    });
}

// ============================
// INSPECT BUTTON
// ============================

function initInspect() {
    $('.inspect-btn').click(async function () {
        const btn = $(this);
        const modal = $('#inspectModal');
        const modalBody = $('#inspectModalBody');
        const deviceName = btn.data('name');

        modalBody.html(`<div class="text-center"><i class="fas fa-spinner fa-spin fa-2x text-info"></i><p class="mt-2">Loading configuration for <strong>${esc(deviceName)}</strong>...</p></div>`);
        modal.modal('show');

        try {
            const deviceData = btn.data('device');
            currentInspectedDevice = deviceData;

            let mappings = []; let deviceTypeInfo = null;
            try {
                const fullTypeResponse = await fetch(`/api/devicetypes/${encodeURIComponent(deviceData.deviceTypeName)}`);
                if (fullTypeResponse.ok) { deviceTypeInfo = await fullTypeResponse.json(); mappings = deviceTypeInfo.mappings || []; }
                else {
                    const mappingResponse = await fetch(`/api/devicetypes/${encodeURIComponent(deviceData.deviceTypeName)}/mappings`);
                    if (mappingResponse.ok) mappings = await mappingResponse.json();
                }
            } catch (error) { console.error('Error getting device type info:', error); }

            let html = `<div class="row"><div class="col-md-6"><div class="card mb-3"><div class="card-header bg-primary text-white"><i class="fas fa-server"></i> Device Settings</div><div class="card-body"><table class="table table-sm table-striped mb-0"><tbody>
                <tr><td><strong>ID</strong></td><td>${esc(deviceData.id)}</td></tr>
                <tr><td><strong>Name</strong></td><td>${esc(deviceData.name)}</td></tr>
                <tr><td><strong>Device Type</strong></td><td>${esc(deviceData.deviceTypeName)}</td></tr>
                <tr><td><strong>Protocol</strong></td><td><span class="badge bg-primary">${deviceData.protocol === 1 ? 'TCP' : 'RTU'}</span></td></tr>
                <tr><td><strong>IP Address</strong></td><td>${esc(deviceData.modbusTcpParameters?.ipAddress || 'N/A')}</td></tr>
                <tr><td><strong>Port</strong></td><td>${esc(deviceData.modbusTcpParameters?.port || 'N/A')}</td></tr>
                <tr><td><strong>Slave ID</strong></td><td>${esc(deviceData.modbusTcpParameters?.slaveId || deviceData.modbusSerialParameters?.slaveId || 'N/A')}</td></tr>
                <tr><td><strong>Connection Timeout</strong></td><td>${esc(deviceData.modbusTcpParameters?.connectionTimeout || 'N/A')} ms</td></tr>
                <tr><td><strong>Read Timeout</strong></td><td>${esc(deviceData.modbusTcpParameters?.readTimeout || deviceData.modbusSerialParameters?.readTimeout || 'N/A')} ms</td></tr>
                <tr><td><strong>Write Timeout</strong></td><td>${esc(deviceData.modbusTcpParameters?.writeTimeout || deviceData.modbusSerialParameters?.writeTimeout || 'N/A')} ms</td></tr>
            </tbody></table></div></div></div><div class="col-md-6"><div class="card mb-3"><div class="card-header bg-success text-white"><i class="fas fa-list"></i> Device Type Info</div><div class="card-body">
            ${deviceTypeInfo ? `<table class="table table-sm table-striped mb-0"><tbody><tr><td><strong>Name</strong></td><td>${esc(deviceTypeInfo.name || deviceData.deviceTypeName)}</td></tr><tr><td><strong>Description</strong></td><td>${esc(deviceTypeInfo.description || '-')}</td></tr><tr><td><strong>Max Registers Per Read</strong></td><td>${esc(deviceTypeInfo.maxRegistersPerRead || 'N/A')}</td></tr><tr><td><strong>Total Mappings</strong></td><td><span class="badge bg-info">${mappings.length}</span></td></tr></tbody></table>` : `<div class="alert alert-warning"><i class="fas fa-exclamation-triangle"></i> No device type info found<br /><small>Device Type: ${esc(deviceData.deviceTypeName)}</small></div>`}
            </div></div></div></div>`;

            if (mappings && mappings.length > 0) {
                html += `<div class="card"><div class="card-header bg-warning text-dark"><i class="fas fa-map"></i> Register Mappings (${mappings.length})</div><div class="card-body p-0"><div style="max-height:300px;overflow-y:auto;"><table class="table table-sm table-striped mb-0"><thead class="table-light"><tr><th>#</th><th>Name</th><th>Start Address</th><th>DataType</th><th>Unit</th><th>Access</th></tr></thead><tbody>
                ${mappings.map((m, index) => `<tr><td>${index + 1}</td><td><strong>${esc(m.name || m.Name)}</strong></td><td><span class="badge bg-secondary">${esc(m.startAddress ?? m.StartAddress)}</span></td><td><span class="badge bg-info">${esc(m.dataType || m.DataType)}</span></td><td>${esc(m.unit || m.Unit || '-')}</td><td><span class="badge ${m.accessRight === 'Read' || m.AccessRight === 'Read' ? 'bg-success' : 'bg-warning'}">${esc(m.accessRight || m.AccessRight || 'Read')}</span></td></tr>`).join('')}
            </tbody></table></div></div></div>`;
            }

            modalBody.html(html);

        } catch (error) {
            console.error('Error loading device config:', error);
            modalBody.html(`<div class="alert alert-danger"><h5><i class="fas fa-exclamation-triangle"></i> Error Loading Configuration</h5><p><strong>${esc(error.message)}</strong></p><hr /><small class="text-muted">Make sure the API endpoints are working: /api/devicetypes/${esc(btn.data('device')?.deviceTypeName || 'unknown')}</small></div>`);
        }
    });
}

// ============================
// COPY JSON BUTTON
// ============================

function initCopyJson() {
    $('#copyJsonBtn').click(function () {
        if (!currentInspectedDevice) { toastr.warning('No device data to copy'); return; }

        const json = JSON.stringify(currentInspectedDevice, null, 2);
        const btn = $(this);
        const originalHtml = btn.html();

        const onCopied = () => {
            btn.html('<i class="fas fa-check"></i> Copied!').removeClass('btn-primary').addClass('btn-success');
            setTimeout(() => { btn.html(originalHtml).removeClass('btn-success').addClass('btn-primary'); }, 1500);
        };

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(json).then(onCopied).catch(() => fallbackCopy(json, onCopied));
        } else {
            fallbackCopy(json, onCopied);
        }
    });

    function fallbackCopy(text, onSuccess) {
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        try { document.execCommand('copy'); onSuccess(); } catch (err) { console.error('Copy failed:', err); }
        finally { document.body.removeChild(textarea); }
    }
}

// ============================
// INITIALIZE EVERYTHING
// ============================

$(document).ready(function () {
    initStandardTest();
    initDetailedTest();
    initInspect();
    initCopyJson();
});