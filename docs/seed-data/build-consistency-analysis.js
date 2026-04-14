const fs = require('fs');
const XLSX = require('xlsx');

function excelDateToISO(serial) {
  if (!serial || serial === '' || serial === 'NULL' || typeof serial !== 'number') return null;
  const epoch = new Date(1899, 11, 30);
  return new Date(epoch.getTime() + serial * 86400000).toISOString();
}

const q3wb = XLSX.readFile('q3 logs.xlsx');
function parseQ3Sheet(sheetName) {
  const ws = q3wb.Sheets[sheetName];
  const data = XLSX.utils.sheet_to_json(ws, {header:1, defval:''});
  const headers = data[0];
  const idx = {};
  headers.forEach((h, i) => idx[String(h).trim()] = i);
  const entries = [];
  for (let i = 1; i < data.length; i++) {
    const r = data[i];
    if (!r[idx['reportMonth']] && !r[idx['Location']]) continue;
    entries.push({
      reportMonth: String(r[idx['reportMonth']]),
      location: r[idx['Location']],
      scriptName: r[idx['ScriptName']],
      stepName: r[idx['stepName']],
      stateName: r[idx['StateName']],
      iteration: r[idx['iteration']] || 1,
      startedAt: excelDateToISO(r[idx['StartedAtDisplay']]),
      endedAt: excelDateToISO(r[idx['EndedAtDisplay']]),
    });
  }
  return entries;
}

const oldLogs = JSON.parse(fs.readFileSync('log-entries.json','utf8')).map(e => ({
  reportMonth: String(e.reportMonth), location: e.location, scriptName: e.scriptName,
  stepName: e.stepName, stateName: e.stateName, iteration: e.iteration,
  startedAt: e.startedAt, endedAt: e.endedAt,
}));

const allLogs = {
  '2512': parseQ3Sheet('2512'),
  '2601': oldLogs,
  '2602': parseQ3Sheet('2602'),
  '2603': parseQ3Sheet('2603'),
};
const allMonths = ['2512','2601','2602','2603'];
const quarterEndMonths = new Set(['2512','2603']);
const midQuarterMonths = new Set(['2601','2602']);

// ===== ADHOC detection =====
const adhocPrefixes = ['ICDB_MCP','ICDB R','ICDB MCP','Copy_dbwruns','Copydbworuns','Copy_variables','CSRBB_Sideload','LAD_SF'];
function isAdhocScript(scriptName, location, steps) {
  for (const p of adhocPrefixes) { if (scriptName.startsWith(p)) return true; }
  if ((location === 'NLBTR' || location === 'BEGT' || location === 'DEGT') &&
      (scriptName.toUpperCase().includes('_RAS') || scriptName.toUpperCase().includes('RAS)') ||
       scriptName.toLowerCase().includes('ras run') || scriptName.includes(' to RAS') ||
       scriptName.toUpperCase().endsWith('_RAS'))) return true;
  if (location === 'AUDB' && scriptName.startsWith('TRISS_')) return true;
  if (location === 'AUDB' && (scriptName.includes('CSRBB_Export') || scriptName.includes('_CSRBB_Export'))) return true;
  if (steps.length === 1 && steps[0] === 'CSRBB exports' && !scriptName.includes('_Export_updated')) return true;
  return false;
}

function classifyStepByPrefix(stepName) {
  const s = stepName.toLowerCase();
  if (s.startsWith('load fv') || s.startsWith('fv spreads') || s.startsWith('fv valuation') || s === 'fv valuation olap export') return 'Fair Value';
  if (s.startsWith('pdce') || s === 'quarterly valuation') return 'PDCE';
  return null;
}

const scriptToSubprocess = [
  ['Delete_Portfolio',       'Delete Portfolio',            'Data Ingestion'],
  ['Load_compare_MP',       'Model Parameters',            'Data Ingestion'],
  ['Load_MP',               'Model Parameters',            'Data Ingestion'],
  ['Load_compare_Spreads',  'Spreads',                     'Data Ingestion'],
  ['Load_Spreads',          'Spreads',                     'Data Ingestion'],
  ['LOAD_POS_NLBTR_MCP_RAS','Load Position (RAS)',         'Processing'],
  ['Load_POS_',             'Load Position',               'Data Ingestion'],
  ['LOAD_POS_',             'Load Position',               'Data Ingestion'],
  ['Load_ELP',              'Load ELP and Mortgages',      'Data Ingestion'],
  ['Load_Index',            'Load Index History',          'Data Ingestion'],
  ['Copy_Portfolio_SLM',    'SLM',                         'Processing'],
  ['Copy_Portfolio_DEGT',   'Load Position (RAS)',          'Processing'],
  ['Copy_Portfolio_NLBTR',  'Load Position (RAS)',          'Processing'],
  ['Copy_Portfolio',        'Load Position',               'Data Ingestion'],
  ['POS_MCP_Export',        'Load Position',               'Data Ingestion'],
  ['Import_Strategies_Risk','Import Strategies',            'Data Ingestion'],
  ['Import_Strategies_IG',  'Import Strategies',            'Data Ingestion'],
  ['Import_Strategies',     'Import Strategies',            'Data Ingestion'],
  ['Valuation_AIC',         'AIC',                         'Processing'],
  ['Valuation_NLBTR_MCP_RAS','Valuation (RAS)',            'Processing'],
  ['Valuation_MCP_Export',  'Valuation',                   'Processing'],
  ['Valuation_MCP',         'Valuation',                   'Processing'],
  ['Valuation_',            'Valuation',                   'Processing'],
  ['CSRBB_Valuation_',      'CSRBB (Val)',                'Processing'],
  ['CSRBB_Planning_',       'CSRBB (Plan)',               'Processing'],
  ['EC_NPV_Export',          'EC',                         'Processing'],
  ['EC_MCP',                 'EC',                         'Processing'],
  ['EC_',                    'EC',                         'Processing'],
  ['KR_BPV_Export',          'KR',                         'Processing'],
  ['KR_MCP',                 'KR',                         'Processing'],
  ['KR_',                    'KR',                         'Processing'],
  ['GAP_ITS',               'GAP ITS Planning',            'Processing'],
  ['GAP_',                   'Gap + Liquidity IR',         'Processing'],
  ['Planning_NLBTR_MCP_RAS', 'NII / Planning risk (RAS)', 'Processing'],
  ['Planning_DEGT_MCP_RAS',  'NII / Planning risk (RAS)', 'Processing'],
  ['Combined_Planning',      'NII / Planning risk',        'Processing'],
  ['Planning_MCP_Export',    'NII / Planning risk',         'Processing'],
  ['Planning_MCP',           'NII / Planning risk',        'Processing'],
  ['Planning_',              'NII / Planning risk',        'Processing'],
  ['ITS_',                   'ITS Planning',               'Processing'],
  ['FTP_EVE',                'FTP EVE SOT',                'Processing'],
  ['SOT_Create',             'SOT',                        'Processing'],
  ['SOT_',                   'SOT',                        'Processing'],
  ['Finance_Planning',       'Planning Finance',           'Processing'],
  ['Finance_Strategies',     'Planning Finance',           'Processing'],
  ['Cost_Pricing',           'Cost Pricing',               'Processing'],
  ['SOLO_',                  'SOLO',                       'Processing'],
  ['SLM_',                   'SLM',                        'Processing'],
  ['EVE_Monthly',            'EVE (Monthly)',              'Processing'],
  ['EVE_',                   'EVE Optionality',             'Processing'],
  ['TRISS_',                 'TRISS export',               'Reporting'],
  ['Fair_Value_',            'Fair Value',                  'Processing'],
  ['CSRBB_Export_updated',   'CSRBB Export',                'Reporting'],
];

function mapToSubprocess(scriptName) {
  if (scriptName.toUpperCase().includes('PDCE')) return { subprocess: 'PDCE', phase: 'Processing' };
  if (scriptName.includes('CSRBB_Export_updated') || scriptName === 'CSRBB_Export' || scriptName.endsWith('_CSRBB_Export_updated')) {
    return { subprocess: 'CSRBB Export', phase: 'Reporting' };
  }
  for (const [prefix, subprocess, phase] of scriptToSubprocess) {
    if (scriptName.startsWith(prefix)) return { subprocess, phase };
  }
  return { subprocess: '** UNMAPPED **', phase: '?' };
}

// ===== Process entries =====
function processEntries(month) {
  const entries = allLogs[month].map(e => {
    const base = mapToSubprocess(e.scriptName);
    if (base.subprocess !== '** UNMAPPED **') return { ...e, _subprocess: base.subprocess, _phase: base.phase };
    const stepClass = classifyStepByPrefix(e.stepName);
    if (stepClass === 'Fair Value') return { ...e, _subprocess: 'Fair Value', _phase: 'Processing' };
    if (stepClass === 'PDCE') return { ...e, _subprocess: 'PDCE', _phase: 'Processing' };
    return { ...e, _subprocess: '** UNMAPPED **', _phase: '?' };
  });
  // Tag adhoc
  const byScript = {};
  for (const e of entries) {
    const key = e.location + '|' + e.scriptName;
    if (!byScript[key]) byScript[key] = [];
    byScript[key].push(e);
  }
  const adhocKeys = new Set();
  for (const [key, es] of Object.entries(byScript)) {
    const steps = [...new Set(es.map(e => e.stepName))];
    if (isAdhocScript(es[0].scriptName, es[0].location, steps)) adhocKeys.add(key);
  }
  for (const e of entries) {
    if (adhocKeys.has(e.location + '|' + e.scriptName)) {
      e._subprocess = 'ADHOC - REMOVE';
      e._phase = 'Adhoc';
    }
  }
  return entries;
}

const processed = {};
for (const m of allMonths) processed[m] = processEntries(m);

// ===== Build per-month step list per Location+Subprocess =====
// stepsByKey[location|subprocess][month] = [step1, step2, ...]  (in chronological order)
const stepsByKey = {};
for (const month of allMonths) {
  const groups = {};
  for (const e of processed[month]) {
    if (e._subprocess === 'ADHOC - REMOVE' || e._subprocess === '** UNMAPPED **') continue;
    const key = e.location + '|' + e._subprocess;
    if (!groups[key]) groups[key] = [];
    groups[key].push(e);
  }
  for (const [key, entries] of Object.entries(groups)) {
    entries.sort((a,b) => (a.startedAt||'').localeCompare(b.startedAt||''));
    const seen = new Set();
    const steps = [];
    for (const e of entries) {
      if (!seen.has(e.stepName)) { seen.add(e.stepName); steps.push(e.stepName); }
    }
    if (!stepsByKey[key]) stepsByKey[key] = {};
    stepsByKey[key][month] = steps;
  }
}

// ===== Quarterly detection =====
const quarterlySubprocesses = new Set();
const subprocessMonths = {};
for (const [key, monthSteps] of Object.entries(stepsByKey)) {
  const [, subprocess] = key.split('|');
  if (!subprocessMonths[subprocess]) subprocessMonths[subprocess] = new Set();
  for (const month of Object.keys(monthSteps)) subprocessMonths[subprocess].add(month);
}
for (const [sub, months] of Object.entries(subprocessMonths)) {
  const hasMid = [...months].some(m => midQuarterMonths.has(m));
  const hasEnd = [...months].some(m => quarterEndMonths.has(m));
  if (hasEnd && !hasMid) quarterlySubprocesses.add(sub);
}

// ===== Consistency analysis =====
// For each Location+Subprocess:
//   - monthsPresent = months where this combo appeared
//   - allSteps = union of steps across those months
//   - For each step, count in how many of monthsPresent it appeared
//   - If count == monthsPresent.length → CONSISTENT
//   - Else → INCONSISTENT (note which months have/miss it)

const analysis = [];
for (const [key, monthSteps] of Object.entries(stepsByKey)) {
  const [location, subprocess] = key.split('|');
  const monthsPresent = Object.keys(monthSteps).sort();

  const allSteps = new Set();
  for (const steps of Object.values(monthSteps)) steps.forEach(s => allSteps.add(s));

  const stepDetails = [];
  for (const step of allSteps) {
    const presentIn = monthsPresent.filter(m => monthSteps[m].includes(step));
    const absentIn = monthsPresent.filter(m => !monthSteps[m].includes(step));
    stepDetails.push({
      step,
      presentIn, absentIn,
      consistent: absentIn.length === 0,
    });
  }

  const consistentSteps = stepDetails.filter(s => s.consistent).map(s => s.step);
  const inconsistentSteps = stepDetails.filter(s => !s.consistent);

  analysis.push({
    location, subprocess,
    monthsPresent,
    totalMonths: monthsPresent.length,
    allSteps: [...allSteps],
    consistentSteps,
    inconsistentSteps,
    quarterly: quarterlySubprocesses.has(subprocess),
  });
}

analysis.sort((a,b) => a.location.localeCompare(b.location) || a.subprocess.localeCompare(b.subprocess));

// ===== SHEET 1: Consistency Summary per Location+Subprocess =====
const sheet1 = [['Location', 'Subprocess', 'Monthly/Quarterly', 'Months Present', 'Total Steps', 'Consistent Steps', 'Inconsistent Steps', '% Consistent', 'Status']];
for (const a of analysis) {
  const pct = a.allSteps.length > 0 ? (a.consistentSteps.length / a.allSteps.length * 100).toFixed(0) : 0;
  const status = a.allSteps.length === 0 ? 'EMPTY' :
    a.inconsistentSteps.length === 0 ? 'ALL CONSISTENT' :
    (a.monthsPresent.length === 1 ? 'ONLY 1 MONTH - CANNOT COMPARE' : 'HAS INCONSISTENCIES');
  sheet1.push([
    a.location, a.subprocess,
    a.quarterly ? 'Quarterly' : 'Monthly',
    a.monthsPresent.join(', '),
    a.allSteps.length,
    a.consistentSteps.length,
    a.inconsistentSteps.length,
    pct + '%',
    status
  ]);
}

// ===== SHEET 2: Consistent Steps Only (the reliable golden source) =====
const sheet2 = [['Location', 'Subprocess', 'Monthly/Quarterly', 'Months Compared', 'Consistent Steps (present in ALL months)']];
for (const a of analysis) {
  if (a.monthsPresent.length < 2) continue; // can't establish consistency with 1 month
  sheet2.push([
    a.location, a.subprocess, a.quarterly ? 'Quarterly' : 'Monthly',
    a.monthsPresent.join(', '),
    a.consistentSteps.join(' -> ') || '(none)'
  ]);
}

// ===== SHEET 3: Inconsistencies — steps missing in some months =====
const sheet3 = [['Location', 'Subprocess', 'Monthly/Quarterly', 'Step Name', 'Months Present In', 'Months MISSING From', 'Appears in 2512?', 'Appears in 2601?', 'Appears in 2602?', 'Appears in 2603?']];
for (const a of analysis) {
  if (a.monthsPresent.length < 2) continue;
  for (const s of a.inconsistentSteps) {
    sheet3.push([
      a.location, a.subprocess, a.quarterly ? 'Quarterly' : 'Monthly',
      s.step,
      s.presentIn.join(', '),
      s.absentIn.join(', '),
      s.presentIn.includes('2512') ? 'Y' : '',
      s.presentIn.includes('2601') ? 'Y' : '',
      s.presentIn.includes('2602') ? 'Y' : '',
      s.presentIn.includes('2603') ? 'Y' : '',
    ]);
  }
}

// ===== SHEET 4: All months side-by-side =====
const sheet4 = [['Location', 'Subprocess', 'Monthly/Quarterly', '2512', '2601', '2602', '2603']];
for (const a of analysis) {
  const ms = stepsByKey[a.location + '|' + a.subprocess];
  sheet4.push([
    a.location, a.subprocess, a.quarterly ? 'Quarterly' : 'Monthly',
    ms['2512']?.join(' -> ') || '—',
    ms['2601']?.join(' -> ') || '—',
    ms['2602']?.join(' -> ') || '—',
    ms['2603']?.join(' -> ') || '—',
  ]);
}

// ===== SHEET 5: Inconsistency summary by Subprocess =====
const bySub = {};
for (const a of analysis) {
  if (!bySub[a.subprocess]) bySub[a.subprocess] = { subprocess: a.subprocess, quarterly: a.quarterly, total: 0, inconsistent: 0, locations: [] };
  bySub[a.subprocess].total++;
  if (a.inconsistentSteps.length > 0 && a.monthsPresent.length >= 2) {
    bySub[a.subprocess].inconsistent++;
    bySub[a.subprocess].locations.push(a.location);
  }
}

const sheet5 = [['Subprocess', 'Monthly/Quarterly', 'Total Locations', 'Locations w/ Inconsistencies', '% Inconsistent', 'Inconsistent Locations']];
for (const s of Object.values(bySub).sort((a,b) => b.inconsistent - a.inconsistent || a.subprocess.localeCompare(b.subprocess))) {
  const pct = s.total > 0 ? (s.inconsistent / s.total * 100).toFixed(0) : 0;
  sheet5.push([s.subprocess, s.quarterly ? 'Quarterly' : 'Monthly', s.total, s.inconsistent, pct + '%', s.locations.join(', ')]);
}

// ===== WRITE =====
const wb = XLSX.utils.book_new();
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(sheet1), 'Consistency Summary');
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(sheet2), 'Consistent Steps (Golden)');
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(sheet3), 'Inconsistent Steps');
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(sheet4), 'All Months Side-by-Side');
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(sheet5), 'By Subprocess');

for (const name of wb.SheetNames) {
  const ws = wb.Sheets[name];
  const range = XLSX.utils.decode_range(ws['!ref']);
  ws['!autofilter'] = { ref: XLSX.utils.encode_range(range) };
  ws['!cols'] = [12,25,12,25,10,15,15,12,30,80,80,80,80].map(w => ({ wch: w }));
}
XLSX.writeFile(wb, 'consistency-analysis.xlsx');

// Summary
console.log('Created: consistency-analysis.xlsx');
console.log('  Consistency Summary: ' + (sheet1.length-1));
console.log('  Consistent Steps (Golden): ' + (sheet2.length-1));
console.log('  Inconsistent Steps: ' + (sheet3.length-1));
console.log('  All Months Side-by-Side: ' + (sheet4.length-1));
console.log('  By Subprocess: ' + (sheet5.length-1));

let totalInconsistent = 0, totalConsistent = 0, totalSingleMonth = 0;
for (const a of analysis) {
  if (a.monthsPresent.length === 1) totalSingleMonth++;
  else if (a.inconsistentSteps.length === 0) totalConsistent++;
  else totalInconsistent++;
}
console.log();
console.log('Location+Subprocess breakdown:');
console.log('  Fully consistent: ' + totalConsistent);
console.log('  Has inconsistencies: ' + totalInconsistent);
console.log('  Only 1 month (cannot compare): ' + totalSingleMonth);

// Top inconsistent subprocesses
console.log('\nTop subprocesses with most inconsistent locations:');
Object.values(bySub)
  .filter(s => s.inconsistent > 0)
  .sort((a,b) => b.inconsistent - a.inconsistent)
  .slice(0, 10)
  .forEach(s => console.log('  ' + s.subprocess + ': ' + s.inconsistent + '/' + s.total + ' locations inconsistent'));
