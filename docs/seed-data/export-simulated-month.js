// Exports the simulated month's data (matrix + ProcessLogEntry rows + operator overrides)
// into an Excel workbook similar in shape to the production `q3 logs.xlsx`.
//
// Run AFTER the simulator has finished (or mid-run for partial data).
// Usage: node export-simulated-month.js [reportMonth]   e.g. 2606

const XLSX = require('xlsx');
const http = require('http');

const reportMonth = process.argv[2] || '2606';
const API = 'http://localhost:5176';

function fetch(path) {
  return new Promise((resolve, reject) => {
    http.get(API + path, res => {
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => {
        const body = Buffer.concat(chunks).toString();
        try { resolve(JSON.parse(body)); }
        catch (e) { reject(new Error('Parse failed for ' + path + ': ' + body.slice(0, 200))); }
      });
    }).on('error', reject);
  });
}

// Excel serial-date helpers to match prod log format
function toExcelSerial(iso) {
  if (!iso) return null;
  const d = new Date(iso);
  const epoch = new Date(Date.UTC(1899, 11, 30));
  return (d - epoch) / 86400000;
}
function toDisplayDate(iso) {
  if (!iso) return null;
  return new Date(iso).toISOString().replace('T', ' ').slice(0, 19);
}

async function getAllLogEntries(rm) {
  // Fetch per-location since the /logs endpoint requires a location filter for efficiency
  const locations = await fetch('/api/locations');
  const all = [];
  for (const loc of locations) {
    const rows = await fetch('/api/mcp-runs/' + rm + '/logs?location=' + loc.code);
    for (const r of rows) all.push(r);
  }
  return all;
}

async function main() {
  console.log('Exporting reportMonth=' + reportMonth + '...');

  const run = await fetch('/api/mcp-runs/current');
  const matrix = await fetch('/api/mcp-runs/' + reportMonth + '/matrix');
  const overrides = await fetch('/api/mcp-runs/' + reportMonth + '/overrides');
  const simStatus = await fetch('/api/simulator/status');
  const logEntries = await getAllLogEntries(reportMonth);

  console.log('  run:', JSON.stringify(run).slice(0, 180));
  console.log('  matrix rows:', matrix.length);
  console.log('  log entries:', logEntries.length);
  console.log('  overrides:', overrides.length);

  const wb = XLSX.utils.book_new();

  // Sheet 1: Run Summary
  const completedCount = matrix.filter(m => m.status === 'Completed').length;
  const inScope = matrix.filter(m => m.status !== 'Not in Scope').length;
  const overrideCount = matrix.filter(m => m.hasOverrides).length;
  const s1 = [
    ['MCP Run Summary — Simulated Month ' + reportMonth],
    [],
    ['Report Month', reportMonth],
    ['Year / Month', (run.year || '') + ' / ' + (run.month || '')],
    ['Run Status', run.status || ''],
    ['Start Date', run.startDate ? toDisplayDate(run.startDate) : ''],
    ['End Date', run.endDate ? toDisplayDate(run.endDate) : ''],
    ['Total Subprocess cells', matrix.length],
    ['In Scope', inScope],
    ['Completed', completedCount],
    ['Completed via Override', overrideCount],
    ['Process log entries (step rows)', logEntries.length],
    ['Operator Overrides', overrides.length],
    ['Simulator speed at export time', simStatus.speedMultiplier + 'x'],
    ['Simulator was running?', simStatus.isRunning ? 'Yes (partial data)' : 'No (complete)'],
    ['Quarter-end?', simStatus.isQuarterEnd ? 'Yes' : 'No'],
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(s1), 'Summary');

  // Sheet 2: Matrix (location × subprocess) — the grid view
  const s2 = [['Location', 'Subprocess', 'Phase', 'Scope', 'Quarterly', 'Status', 'Has Overrides', 'Steps Completed', 'Steps Required', 'Started At', 'Completed At', 'Elapsed Minutes']];
  for (const m of matrix) {
    s2.push([
      m.location, m.subprocess, m.phase, m.scope || '', m.isQuarterly ? 'Yes' : '',
      m.status, m.hasOverrides ? 'Yes' : '',
      m.completedSteps, m.totalRequiredSteps,
      toDisplayDate(m.startedAt), toDisplayDate(m.completedAt),
      m.elapsedMinutes ? parseFloat(m.elapsedMinutes.toFixed(2)) : null,
    ]);
  }
  const ws2 = XLSX.utils.aoa_to_sheet(s2);
  ws2['!autofilter'] = { ref: ws2['!ref'] };
  ws2['!cols'] = [{wch:12},{wch:28},{wch:14},{wch:18},{wch:10},{wch:12},{wch:14},{wch:12},{wch:12},{wch:22},{wch:22},{wch:14}];
  XLSX.utils.book_append_sheet(wb, ws2, 'Matrix');

  // Sheet 3: Process Log Entries — prod-like shape
  const s3 = [[
    'reportMonth', 'Location', 'process', 'StateName', 'ScriptName', 'iteration', 'stepName', 'ErrorMessage',
    'TotalQ3RuntimeHours', 'FailedRuntimeHours', 'EfficientRuntimeHours', 'opportunityCostHours',
    'InefficientRuntimeHours', 'E2ERuntimeHours',
    'StartedAtDisplay', 'EndedAtDisplay'
  ]];
  for (const e of logEntries) {
    s3.push([
      reportMonth,
      e.location,
      'MCP',
      e.stateName,
      e.scriptName,
      e.iteration,
      e.stepName,
      e.errorMessage || 'NULL',
      parseFloat((e.totalRuntimeHours || 0).toFixed(3)),
      parseFloat((e.failedRuntimeHours || 0).toFixed(3)),
      parseFloat((e.efficientRuntimeHours || 0).toFixed(3)),
      parseFloat((e.opportunityCostHours || 0).toFixed(3)),
      parseFloat(((e.failedRuntimeHours || 0) + (e.opportunityCostHours || 0)).toFixed(3)),
      parseFloat((e.e2ERuntimeHours || 0).toFixed(3)),
      toDisplayDate(e.startedAt),
      toDisplayDate(e.endedAt),
    ]);
  }
  const ws3 = XLSX.utils.aoa_to_sheet(s3);
  ws3['!autofilter'] = { ref: ws3['!ref'] };
  ws3['!cols'] = [{wch:10},{wch:10},{wch:8},{wch:12},{wch:35},{wch:8},{wch:30},{wch:45},{wch:14},{wch:14},{wch:14},{wch:14},{wch:14},{wch:14},{wch:22},{wch:22}];
  XLSX.utils.book_append_sheet(wb, ws3, 'Process Log Entries');

  // Sheet 4: Operator Overrides
  const s4 = [['Location', 'Subprocess', 'Step', 'Action', 'Reason', 'Ticket Ref', 'Operator', 'Created At']];
  for (const o of overrides) {
    s4.push([
      o.location, o.subprocess, o.stepName, o.action, o.reason,
      o.ticketRef || '', o.operator, toDisplayDate(o.createdAt),
    ]);
  }
  const ws4 = XLSX.utils.aoa_to_sheet(s4);
  ws4['!autofilter'] = { ref: ws4['!ref'] };
  ws4['!cols'] = [{wch:12},{wch:28},{wch:28},{wch:10},{wch:60},{wch:14},{wch:22},{wch:22}];
  XLSX.utils.book_append_sheet(wb, ws4, 'Operator Overrides');

  // Sheet 5: Runtime aggregation per (location × subprocess)
  const agg = {};
  for (const e of logEntries) {
    const k = e.location + '|' + (e.scriptName || '').split('_')[0];
    if (!agg[k]) agg[k] = { location: e.location, count: 0, total: 0, efficient: 0, failed: 0, oc: 0, e2e: 0, iters: new Set() };
    agg[k].count++;
    agg[k].total += e.totalRuntimeHours || 0;
    agg[k].efficient += e.efficientRuntimeHours || 0;
    agg[k].failed += e.failedRuntimeHours || 0;
    agg[k].oc += e.opportunityCostHours || 0;
    agg[k].e2e += e.e2ERuntimeHours || 0;
    agg[k].iters.add(e.iteration);
  }
  const s5 = [['Location', 'Subprocess (from ScriptName prefix)', 'Log Rows', 'Iterations', 'TotalQ3 (h)', 'Efficient (h)', 'Failed (h)', 'OppCost (h)', 'E2E (h)']];
  for (const m of matrix.filter(m => m.status !== 'Not in Scope')) {
    const matchLoc = m.location;
    const relatedRows = logEntries.filter(e => e.location === matchLoc && (
      (e.scriptName || '').replace(/ rr\d*.*/i, '').replace(/_(MCP|RAS|SOT).*/i, '').toLowerCase().includes(m.subprocess.replace(/[^a-z0-9]/gi, '').toLowerCase().slice(0,6))
    ));
    const sums = (f) => relatedRows.reduce((s, e) => s + (e[f] || 0), 0);
    s5.push([
      m.location, m.subprocess,
      relatedRows.length,
      [...new Set(relatedRows.map(e => e.iteration))].sort((a, b) => a - b).join(', '),
      parseFloat(sums('totalRuntimeHours').toFixed(3)),
      parseFloat(sums('efficientRuntimeHours').toFixed(3)),
      parseFloat(sums('failedRuntimeHours').toFixed(3)),
      parseFloat(sums('opportunityCostHours').toFixed(3)),
      parseFloat(sums('e2ERuntimeHours').toFixed(3)),
    ]);
  }
  const ws5 = XLSX.utils.aoa_to_sheet(s5);
  ws5['!autofilter'] = { ref: ws5['!ref'] };
  ws5['!cols'] = [{wch:10},{wch:35},{wch:10},{wch:14},{wch:12},{wch:12},{wch:12},{wch:12},{wch:12}];
  XLSX.utils.book_append_sheet(wb, ws5, 'Runtime Per Loc+Sub');

  const filename = 'simulated-' + reportMonth + '-extract.xlsx';
  XLSX.writeFile(wb, filename);
  console.log('\nWrote: ' + filename);
  console.log('  Sheet Summary     : run-level stats');
  console.log('  Sheet Matrix      : ' + (s2.length - 1) + ' location × subprocess rows');
  console.log('  Sheet Log Entries : ' + (s3.length - 1) + ' step-level rows');
  console.log('  Sheet Overrides   : ' + (s4.length - 1) + ' operator override rows');
  console.log('  Sheet Runtime Agg : ' + (s5.length - 1) + ' per (loc × sub) summary rows');
}

main().catch(e => { console.error(e); process.exit(1); });
