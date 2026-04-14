const fs = require('fs');
const XLSX = require('xlsx');

function excelDateToISO(serial) {
  if (!serial || serial === '' || serial === 'NULL' || typeof serial !== 'number') return null;
  const epoch = new Date(1899, 11, 30);
  return new Date(epoch.getTime() + serial * 86400000).toISOString();
}

// ===== Read all 4 months =====
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
      process: r[idx['process']],
      scriptName: r[idx['ScriptName']],
      stepName: r[idx['stepName']],
      stateName: r[idx['StateName']],
      iteration: r[idx['iteration']] || 1,
      startedAt: excelDateToISO(r[idx['StartedAtDisplay']]),
      endedAt: excelDateToISO(r[idx['EndedAtDisplay']]),
      errorMessage: r[idx['ErrorMessage']] === 'NULL' ? null : r[idx['ErrorMessage']] || null,
    });
  }
  return entries;
}

const oldLogs = JSON.parse(fs.readFileSync('log-entries.json','utf8'));

const allLogs = {
  '2512': parseQ3Sheet('2512'),
  '2601': oldLogs,
  '2602': parseQ3Sheet('2602'),
  '2603': parseQ3Sheet('2603'),
};
const allMonths = ['2512','2601','2602','2603'];
const quarterEndMonths = new Set(['2512','2603']); // end of quarters in our data
const midQuarterMonths = new Set(['2601','2602']);

// ===== ADHOC detection =====
const adhocPrefixes = [
  'ICDB_MCP',
  'ICDB R',
  'ICDB MCP',
  'Copy_dbwruns',
  'Copydbworuns',
  'Copy_variables',
  'CSRBB_Sideload',
  'LAD_SF',
];

function isAdhocScript(scriptName, location, steps) {
  // 1. Always-adhoc prefixes
  for (const p of adhocPrefixes) {
    if (scriptName.startsWith(p)) return true;
  }
  // 2. RAS scripts at NLBTR, BEGT, DEGT → all adhoc per Rose
  if ((location === 'NLBTR' || location === 'BEGT' || location === 'DEGT') &&
      (scriptName.toUpperCase().includes('_RAS') || scriptName.toUpperCase().includes('RAS)') || scriptName.toLowerCase().includes('ras run') ||
       scriptName.includes(' to RAS') || scriptName.toUpperCase().endsWith('_RAS'))) {
    return true;
  }
  // 3. AUDB CSRBB Export and TRISS are adhoc per Rose
  if (location === 'AUDB' && scriptName.startsWith('TRISS_')) return true;
  if (location === 'AUDB' && (scriptName.includes('CSRBB_Export') || scriptName.includes('_CSRBB_Export'))) return true;
  // 4. Short "CSRBB exports" standalone (generic 1-step scripts with that name)
  if (steps.length === 1 && steps[0] === 'CSRBB exports' && !scriptName.includes('_Export_updated')) return true;
  return false;
}

// ===== Step-level classifier for split scripts (FV + PDCE combined) =====
function classifyStepByPrefix(stepName) {
  const s = stepName.toLowerCase();
  if (s.startsWith('load fv') || s.startsWith('fv spreads') || s.startsWith('fv valuation') || s === 'fv valuation olap export') {
    return 'Fair Value';
  }
  if (s.startsWith('pdce') || s === 'quarterly valuation') {
    return 'PDCE';
  }
  return null;
}

// ===== Script prefix → Subprocess + Phase mapping =====
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
  // PDCE by name
  if (scriptName.toUpperCase().includes('PDCE')) {
    return { subprocess: 'PDCE', phase: 'Processing' };
  }
  // CSRBB Export variants
  if (scriptName.includes('CSRBB_Export_updated') || scriptName === 'CSRBB_Export' || scriptName.endsWith('_CSRBB_Export_updated')) {
    return { subprocess: 'CSRBB Export', phase: 'Reporting' };
  }
  for (const [prefix, subprocess, phase] of scriptToSubprocess) {
    if (scriptName.startsWith(prefix)) return { subprocess, phase };
  }
  return { subprocess: '** UNMAPPED **', phase: '?' };
}

// ===== Process each entry — map, split if needed, tag adhoc =====
function processAllEntries() {
  const result = {};
  for (const month of allMonths) {
    result[month] = [];

    // First pass: assign subprocess
    const entries = allLogs[month].map(e => {
      const base = mapToSubprocess(e.scriptName);
      if (base.subprocess !== '** UNMAPPED **') {
        return { ...e, _subprocess: base.subprocess, _phase: base.phase };
      }
      const stepClass = classifyStepByPrefix(e.stepName);
      if (stepClass === 'Fair Value') return { ...e, _subprocess: 'Fair Value', _phase: 'Processing' };
      if (stepClass === 'PDCE') return { ...e, _subprocess: 'PDCE', _phase: 'Processing' };
      return { ...e, _subprocess: '** UNMAPPED **', _phase: '?' };
    });

    // Second pass: tag adhoc
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
      // Remove ICDB location entirely
      if (e.location === 'ICDB') {
        e._subprocess = 'ADHOC - REMOVE';
        e._phase = 'Adhoc';
      }
      // Remove GAP ITS Planning subprocess entirely
      if (e._subprocess === 'GAP ITS Planning') {
        e._subprocess = 'ADHOC - REMOVE';
        e._phase = 'Adhoc';
      }
      // Remove Load Position (RAS) — adhoc run
      if (e._subprocess === 'Load Position (RAS)') {
        e._subprocess = 'ADHOC - REMOVE';
        e._phase = 'Adhoc';
      }
    }
    result[month] = entries;
  }
  return result;
}

const processedByMonth = processAllEntries();

// ===== Build Location+Subprocess mapping per month =====
function buildMonthMapping(entries) {
  const logIndex = new Map();
  entries.forEach((e,i) => logIndex.set(e, i));
  const groups = {};
  for (const e of entries) {
    const key = e.location + '|' + e._subprocess;
    if (!groups[key]) groups[key] = { location: e.location, subprocess: e._subprocess, phase: e._phase, entries: [], scripts: new Set() };
    groups[key].entries.push(e);
    groups[key].scripts.add(e.scriptName);
  }
  const result = [];
  for (const data of Object.values(groups)) {
    const sorted = [...data.entries].sort((a,b) => {
      const cmp = (a.startedAt||'').localeCompare(b.startedAt||'');
      if (cmp !== 0) return cmp;
      return (logIndex.get(a)||0) - (logIndex.get(b)||0);
    });
    const finalSteps = [];
    const finalSet = new Set();
    for (const e of sorted) {
      if (!finalSet.has(e.stepName)) { finalSet.add(e.stepName); finalSteps.push(e.stepName); }
    }
    result.push({
      location: data.location, subprocess: data.subprocess, phase: data.phase,
      steps: finalSteps, scripts: [...data.scripts].sort(),
      earliestStart: sorted[0]?.startedAt || null,
    });
  }
  return result.sort((a,b) => a.location.localeCompare(b.location) || (a.earliestStart||'').localeCompare(b.earliestStart||''));
}

const mappingByMonth = {};
for (const m of allMonths) mappingByMonth[m] = buildMonthMapping(processedByMonth[m]);

// ===== Compute which subprocess is QUARTERLY based on data pattern =====
// Rule: appears only in quarter-end months (2512, 2603) and NOT in mid-quarter (2601, 2602)
const subprocessMonths = {}; // subprocess -> set of months
for (const month of allMonths) {
  for (const m of mappingByMonth[month]) {
    if (m.subprocess === 'ADHOC - REMOVE' || m.subprocess === '** UNMAPPED **') continue;
    if (!subprocessMonths[m.subprocess]) subprocessMonths[m.subprocess] = new Set();
    subprocessMonths[m.subprocess].add(month);
  }
}

const quarterlySubprocesses = new Set();
for (const [sub, months] of Object.entries(subprocessMonths)) {
  const hasMid = [...months].some(m => midQuarterMonths.has(m));
  const hasEnd = [...months].some(m => quarterEndMonths.has(m));
  if (hasEnd && !hasMid) quarterlySubprocesses.add(sub);
}

// ===== Compute Main Process vs Location Specific =====
// Threshold: MORE THAN HALF of locations = Main Process
// Total = 15 locations (ICDB removed per Rose)
const TOTAL_LOCATIONS = 15;
const MAIN_THRESHOLD = Math.ceil(TOTAL_LOCATIONS / 2); // 8

const subprocessLocations = {}; // subprocess -> set of locations
for (const month of allMonths) {
  for (const m of mappingByMonth[month]) {
    if (m.subprocess === 'ADHOC - REMOVE' || m.subprocess === '** UNMAPPED **') continue;
    if (m.location === 'ICDB') continue;
    if (!subprocessLocations[m.subprocess]) subprocessLocations[m.subprocess] = new Set();
    subprocessLocations[m.subprocess].add(m.location);
  }
}

function scopeTag(subprocess) {
  const count = subprocessLocations[subprocess]?.size || 0;
  if (count >= MAIN_THRESHOLD) return 'Main Process';
  return 'Location Specific';
}

// ===== Compute AVERAGE RANK =====
const subprocessRanks = {};
for (const month of allMonths) {
  const byLoc = {};
  for (const m of mappingByMonth[month]) {
    if (m.subprocess === 'ADHOC - REMOVE' || m.subprocess === '** UNMAPPED **') continue;
    if (!byLoc[m.location]) byLoc[m.location] = [];
    byLoc[m.location].push(m);
  }
  for (const locEntries of Object.values(byLoc)) {
    const sorted = [...locEntries].sort((a,b) => (a.earliestStart||'').localeCompare(b.earliestStart||''));
    sorted.forEach((m, i) => {
      if (!subprocessRanks[m.subprocess]) subprocessRanks[m.subprocess] = { ranks: [] };
      subprocessRanks[m.subprocess].ranks.push(i + 1);
    });
  }
}

const avgRanks = Object.entries(subprocessRanks).map(([sub, data]) => ({
  subprocess: sub,
  avgRank: data.ranks.reduce((a,b) => a+b, 0) / data.ranks.length,
  samples: data.ranks.length,
  quarterly: quarterlySubprocesses.has(sub),
  scope: scopeTag(sub),
  locCount: subprocessLocations[sub]?.size || 0,
}));

avgRanks.sort((a,b) => {
  if (a.quarterly !== b.quarterly) return a.quarterly ? 1 : -1;
  return a.avgRank - b.avgRank;
});

const rankMap = {};
avgRanks.forEach((r, i) => rankMap[r.subprocess] = { globalSeq: i+1, avgRank: r.avgRank, quarterly: r.quarterly, scope: r.scope });

// ===== Combine entries across months =====
const combined = {};
for (const month of allMonths) {
  for (const m of mappingByMonth[month]) {
    const key = m.location + '|' + m.subprocess;
    if (!combined[key]) combined[key] = {
      location: m.location, subprocess: m.subprocess, phase: m.phase,
      byMonth: {}, scripts: new Set()
    };
    combined[key].byMonth[month] = m.steps;
    m.scripts.forEach(s => combined[key].scripts.add(s));
  }
}

// ===== SHEET 1: Final Mapping =====
const sheet1 = [['Global Seq #', 'Location', 'Subprocess', 'Phase', 'Scope', 'Monthly/Quarterly', 'Months Seen', 'Final Steps (union across months)', 'Step Count', 'Avg Rank', 'Script Variants']];
const byLocation = {};
for (const c of Object.values(combined)) {
  if (c.subprocess === 'ADHOC - REMOVE' || c.subprocess === '** UNMAPPED **') continue;
  if (!byLocation[c.location]) byLocation[c.location] = [];
  byLocation[c.location].push(c);
}
// Helper: union of steps across all months (chronological order of first appearance)
function unionStepsAcrossMonths(c) {
  const seen = new Set();
  const union = [];
  // Go through months in chronological order (2512, 2601, 2602, 2603)
  for (const month of allMonths) {
    const steps = c.byMonth[month] || [];
    for (const s of steps) {
      if (!seen.has(s)) { seen.add(s); union.push(s); }
    }
  }
  return union;
}

for (const loc of Object.keys(byLocation).sort()) {
  const entries = byLocation[loc].sort((a,b) => (rankMap[a.subprocess]?.globalSeq||999) - (rankMap[b.subprocess]?.globalSeq||999));
  for (const c of entries) {
    // Use UNION of steps across all months (handles partial/incomplete months)
    const finalSteps = unionStepsAcrossMonths(c);
    // Track which months contributed
    const monthsWithData = allMonths.filter(m => c.byMonth[m]);
    const rm = rankMap[c.subprocess] || {};
    sheet1.push([
      rm.globalSeq || '?', c.location, c.subprocess, c.phase,
      rm.scope || '?',
      rm.quarterly ? 'Quarterly' : 'Monthly',
      monthsWithData.join(', '),
      finalSteps.join(' -> '), finalSteps.length,
      rm.avgRank?.toFixed(2) || '',
      [...c.scripts].join(', ')
    ]);
  }
}

// ===== SHEET 2: Subprocess Global Order =====
const sheet2 = [['Global Seq #', 'Subprocess', 'Phase', 'Scope', 'Monthly/Quarterly', 'Locations Running It', 'Location Count', 'Avg Rank', 'Samples']];
avgRanks.forEach((r, i) => {
  const phase = Object.values(combined).find(c => c.subprocess === r.subprocess)?.phase || '?';
  sheet2.push([
    i+1, r.subprocess, phase, r.scope, r.quarterly ? 'Quarterly' : 'Monthly',
    [...subprocessLocations[r.subprocess]].sort().join(', '),
    r.locCount, r.avgRank.toFixed(2), r.samples
  ]);
});

// ===== SHEET 3: Step Evolution =====
const sheet3 = [['Location', 'Subprocess', 'Phase', 'Scope', 'Monthly/Quarterly', '2512 Steps', '2601 Steps', '2602 Steps', '2603 Steps', 'Removed over time', 'Added over time']];
for (const c of Object.values(combined)) {
  if (c.subprocess === 'ADHOC - REMOVE' || c.subprocess === '** UNMAPPED **') continue;
  const s = m => (c.byMonth[m] || []);
  const all = new Set();
  for (const m of allMonths) s(m).forEach(x => all.add(x));
  const latest = c.byMonth['2603'] ? '2603' : (c.byMonth['2602'] ? '2602' : (c.byMonth['2601'] ? '2601' : '2512'));
  const earliest = c.byMonth['2512'] ? '2512' : (c.byMonth['2601'] ? '2601' : (c.byMonth['2602'] ? '2602' : '2603'));
  const removed = [...all].filter(st => s(earliest).includes(st) && !s(latest).includes(st));
  const added = [...all].filter(st => !s(earliest).includes(st) && s(latest).includes(st));
  const rm = rankMap[c.subprocess] || {};
  sheet3.push([
    c.location, c.subprocess, c.phase, rm.scope || '?', rm.quarterly ? 'Quarterly' : 'Monthly',
    s('2512').join(' -> ') || '—', s('2601').join(' -> ') || '—',
    s('2602').join(' -> ') || '—', s('2603').join(' -> ') || '—',
    removed.join(', ') || '', added.join(', ') || ''
  ]);
}

// ===== SHEET 4: Matrix — Location × Subprocess =====
const monthlyMainSubs = avgRanks.filter(r => !r.quarterly && r.scope === 'Main Process').map(r => r.subprocess);
const monthlyLocSpecific = avgRanks.filter(r => !r.quarterly && r.scope === 'Location Specific').map(r => r.subprocess);
const quarterlySubs = avgRanks.filter(r => r.quarterly).map(r => r.subprocess);

const subColumns = [...monthlyMainSubs, ...monthlyLocSpecific, ...quarterlySubs];
const allLocations = [...new Set(Object.values(combined).map(c => c.location).filter(l => l))].sort();

const sheet4 = [['Location', ...subColumns]];
// header row for scope tags
const scopeHeader = [''];
for (const sub of subColumns) {
  if (quarterlySubprocesses.has(sub)) scopeHeader.push('Quarterly');
  else if (subprocessLocations[sub]?.size >= MAIN_THRESHOLD) scopeHeader.push('Main Process');
  else scopeHeader.push('Location Specific');
}
sheet4.push(scopeHeader);

for (const loc of allLocations) {
  const row = [loc];
  for (const sub of subColumns) {
    const key = loc + '|' + sub;
    row.push(combined[key] ? 'Y' : '');
  }
  sheet4.push(row);
}

// ===== SHEET 5: ADHOC scripts (removed) =====
const sheet5 = [['Location', 'Script Name', 'Reason', 'Months Seen', 'Step Count', 'Sample Steps']];
const adhocEntries = {};
for (const month of allMonths) {
  for (const e of processedByMonth[month]) {
    if (e._subprocess !== 'ADHOC - REMOVE') continue;
    const key = e.location + '|' + e.scriptName;
    if (!adhocEntries[key]) adhocEntries[key] = { location: e.location, script: e.scriptName, months: new Set(), steps: new Set() };
    adhocEntries[key].months.add(month);
    adhocEntries[key].steps.add(e.stepName);
  }
}
function adhocReason(location, script) {
  for (const p of adhocPrefixes) if (script.startsWith(p)) return 'Adhoc prefix: ' + p;
  if ((location === 'NLBTR' || location === 'BEGT' || location === 'DEGT') &&
      (script.toUpperCase().includes('RAS') || script.includes(' to RAS'))) return 'RAS at ' + location;
  if (location === 'AUDB' && script.startsWith('TRISS_')) return 'AUDB TRISS adhoc';
  if (location === 'AUDB' && script.includes('CSRBB_Export')) return 'AUDB CSRBB Export adhoc';
  return 'Single-step adhoc';
}
for (const a of Object.values(adhocEntries).sort((a,b) => a.location.localeCompare(b.location) || a.script.localeCompare(b.script))) {
  sheet5.push([a.location, a.script, adhocReason(a.location, a.script), [...a.months].sort().join(', '), a.steps.size, [...a.steps].slice(0,5).join(' | ') + (a.steps.size > 5 ? '...' : '')]);
}

// ===== SHEET 6: Still unmapped =====
const sheet6 = [['Location', 'Script Name', 'Months Seen', 'Step Count', 'Steps']];
const unmapped = {};
for (const month of allMonths) {
  for (const e of processedByMonth[month]) {
    if (e._subprocess !== '** UNMAPPED **') continue;
    const key = e.location + '|' + e.scriptName;
    if (!unmapped[key]) unmapped[key] = { location: e.location, script: e.scriptName, months: new Set(), steps: [] };
    unmapped[key].months.add(month);
    if (!unmapped[key].steps.includes(e.stepName)) unmapped[key].steps.push(e.stepName);
  }
}
for (const u of Object.values(unmapped).sort((a,b) => a.location.localeCompare(b.location) || a.script.localeCompare(b.script))) {
  sheet6.push([u.location, u.script, [...u.months].sort().join(', '), u.steps.length, u.steps.join(' -> ')]);
}

// ===== WRITE =====
const out = XLSX.utils.book_new();
XLSX.utils.book_append_sheet(out, XLSX.utils.aoa_to_sheet(sheet1), 'Final Mapping');
XLSX.utils.book_append_sheet(out, XLSX.utils.aoa_to_sheet(sheet2), 'Subprocess Order');
XLSX.utils.book_append_sheet(out, XLSX.utils.aoa_to_sheet(sheet3), 'Step Evolution');
XLSX.utils.book_append_sheet(out, XLSX.utils.aoa_to_sheet(sheet4), 'Scope Matrix');
XLSX.utils.book_append_sheet(out, XLSX.utils.aoa_to_sheet(sheet5), 'ADHOC (Removed)');
XLSX.utils.book_append_sheet(out, XLSX.utils.aoa_to_sheet(sheet6), 'Still Unmapped');

for (const name of out.SheetNames) {
  const ws = out.Sheets[name];
  const range = XLSX.utils.decode_range(ws['!ref']);
  ws['!autofilter'] = { ref: XLSX.utils.encode_range(range) };
  ws['!cols'] = [8,12,25,15,15,12,10,80,10,10,60].map(w => ({ wch: w }));
}
XLSX.writeFile(out, 'golden-source-final-v6.xlsx');

// ===== Also generate matrix.json for HTML visualization =====
const matrixData = {
  locations: allLocations,
  dataIngestion: [],
  mainProcesses: [],
  locationSpecific: [],
  scope: {}, // loc -> subprocess -> true/false
};
// Categorize subprocesses
for (const r of avgRanks) {
  const entry = { name: r.subprocess, phase: Object.values(combined).find(c => c.subprocess === r.subprocess)?.phase || '?', quarterly: r.quarterly, order: r.avgRank };
  if (r.scope === 'Main Process' && entry.phase === 'Data Ingestion') matrixData.dataIngestion.push(entry);
  else if (r.scope === 'Main Process') matrixData.mainProcesses.push(entry);
  else matrixData.locationSpecific.push(entry);
}
// Sort by avg rank within groups
matrixData.dataIngestion.sort((a,b) => a.order - b.order);
matrixData.mainProcesses.sort((a,b) => a.order - b.order);
matrixData.locationSpecific.sort((a,b) => a.order - b.order);
// Build scope lookup
for (const loc of allLocations) matrixData.scope[loc] = {};
for (const c of Object.values(combined)) {
  if (c.subprocess === 'ADHOC - REMOVE' || c.subprocess === '** UNMAPPED **') continue;
  if (c.location === 'ICDB') continue;
  matrixData.scope[c.location][c.subprocess] = true;
}
fs.writeFileSync('matrix-data.json', JSON.stringify(matrixData, null, 2));

console.log('Created: golden-source-final-v6.xlsx');
console.log('Created: matrix-data.json');
console.log('  Final Mapping: ' + (sheet1.length-1));
console.log('  Subprocess Order: ' + (sheet2.length-1));
console.log('  Step Evolution: ' + (sheet3.length-1));
console.log('  Scope Matrix: ' + (allLocations.length) + ' locations x ' + subColumns.length + ' subprocesses');
console.log('  ADHOC (Removed): ' + (sheet5.length-1));
console.log('  Still Unmapped: ' + (sheet6.length-1));

console.log('\nSubprocesses (sorted, with scope tags):');
avgRanks.forEach((r, i) => {
  const tag = r.quarterly ? '[Q]' : (r.scope === 'Main Process' ? '[M]' : '[L]');
  console.log('  ' + (i+1) + '. ' + r.subprocess + ' ' + tag + ' (locs: ' + r.locCount + '/16, avgRank: ' + r.avgRank.toFixed(2) + ')');
});

console.log('\nQuarterly detected (appears only in 2512+2603):');
console.log('  ' + [...quarterlySubprocesses].join(', ') || '(none)');
