// Regenerates the efficient-runtime-by-step extract using q3 logs.ods as the source of truth
// (the corrected 2601 data lives here, not in the old production data 1 month.xlsx)

const fs = require('fs');
const XLSX = require('xlsx');

const wb = XLSX.readFile('q3 logs.ods');
const months = ['2512', '2601', '2602', '2603'];

// Parse all 4 months from a single source
function parseMonth(monthSheet) {
  return XLSX.utils.sheet_to_json(wb.Sheets[monthSheet], { defval: '' });
}

const allLogs = [];
for (const m of months) {
  const rows = parseMonth(m);
  for (const r of rows) {
    if (!r.Location) continue;
    allLogs.push({
      reportMonth: String(r.reportMonth),
      location: r.Location,
      scriptName: r.ScriptName,
      stepName: r.stepName,
      stateName: r.StateName,
      iteration: r.iteration || 1,
      totalRuntimeHours: r.TotalQ3RuntimeHours || 0,
    });
  }
}
console.log('Total rows across 4 months:', allLogs.length);

// Use the same golden mapping for the step list + ordering
const golden = JSON.parse(fs.readFileSync('../../backend/Data/SeedData/golden-mapping.json', 'utf8'));

// Re-apply adhoc + subprocess classification (same rules as build-final-mapping.js)
const adhocPrefixes = ['ICDB_MCP', 'ICDB R', 'ICDB MCP', 'Copy_dbwruns', 'Copydbworuns', 'Copy_variables', 'CSRBB_Sideload', 'LAD_SF'];
function isAdhoc(script, location, steps) {
  if (!script) return true;
  for (const p of adhocPrefixes) if (script.startsWith(p)) return true;
  if ((location === 'NLBTR' || location === 'BEGT' || location === 'DEGT') &&
    (script.toUpperCase().includes('_RAS') || script.toUpperCase().includes('RAS)') ||
     script.toLowerCase().includes('ras run') || script.includes(' to RAS') ||
     script.toUpperCase().endsWith('_RAS'))) return true;
  if (location === 'AUDB' && script.startsWith('TRISS_')) return true;
  if (location === 'AUDB' && (script.includes('CSRBB_Export') || script.includes('_CSRBB_Export'))) return true;
  if (steps.length === 1 && steps[0] === 'CSRBB exports' && !script.includes('_Export_updated')) return true;
  return false;
}
const scriptToSubprocess = [
  ['Delete_Portfolio', 'Delete Portfolio'],
  ['Load_compare_MP', 'Model Parameters'],
  ['Load_MP', 'Model Parameters'],
  ['Load_compare_Spreads', 'Spreads'],
  ['Load_Spreads', 'Spreads'],
  ['LOAD_POS_NLBTR_MCP_RAS', 'Load Position (RAS)'],
  ['Load_POS_', 'Load Position'],
  ['LOAD_POS_', 'Load Position'],
  ['Load_ELP', 'Load ELP and Mortgages'],
  ['Load_Index', 'Load Index History'],
  ['Copy_Portfolio_SLM', 'SLM'],
  ['Copy_Portfolio_DEGT', 'Load Position (RAS)'],
  ['Copy_Portfolio_NLBTR', 'Load Position (RAS)'],
  ['Copy_Portfolio', 'Load Position'],
  ['POS_MCP_Export', 'Load Position'],
  ['Import_Strategies_Risk', 'Import Strategies'],
  ['Import_Strategies_IG', 'Import Strategies'],
  ['Import_Strategies', 'Import Strategies'],
  ['Valuation_AIC', 'AIC'],
  ['Valuation_NLBTR_MCP_RAS', 'Valuation (RAS)'],
  ['Valuation_MCP_Export', 'Valuation'],
  ['Valuation_MCP', 'Valuation'],
  ['Valuation_', 'Valuation'],
  ['CSRBB_Valuation_', 'CSRBB (Val)'],
  ['CSRBB_Planning_', 'CSRBB (Plan)'],
  ['EC_NPV_Export', 'EC'],
  ['EC_MCP', 'EC'],
  ['EC_', 'EC'],
  ['KR_BPV_Export', 'KR'],
  ['KR_MCP', 'KR'],
  ['KR_', 'KR'],
  ['GAP_ITS', 'GAP ITS Planning'],
  ['GAP_', 'Gap + Liquidity IR'],
  ['Planning_NLBTR_MCP_RAS', 'NII / Planning risk (RAS)'],
  ['Planning_DEGT_MCP_RAS', 'NII / Planning risk (RAS)'],
  ['Combined_Planning', 'NII / Planning risk'],
  ['Planning_MCP_Export', 'NII / Planning risk'],
  ['Planning_MCP', 'NII / Planning risk'],
  ['Planning_', 'NII / Planning risk'],
  ['ITS_', 'ITS Planning'],
  ['FTP_EVE', 'FTP EVE SOT'],
  ['SOT_Create', 'SOT'],
  ['SOT_', 'SOT'],
  ['Finance_Planning', 'Planning Finance'],
  ['Finance_Strategies', 'Planning Finance'],
  ['Cost_Pricing', 'Cost Pricing'],
  ['SOLO_', 'SOLO'],
  ['SLM_', 'SLM'],
  ['EVE_Monthly', 'EVE (Monthly)'],
  ['EVE_', 'EVE Optionality'],
  ['TRISS_', 'TRISS export'],
  ['Fair_Value_', 'Fair Value'],
  ['CSRBB_Export_updated', 'CSRBB Export'],
];
function mapToSubprocess(scriptName) {
  if (scriptName && scriptName.toUpperCase().includes('PDCE')) return 'PDCE';
  if (scriptName && (scriptName.includes('CSRBB_Export_updated') || scriptName === 'CSRBB_Export' || scriptName.endsWith('_CSRBB_Export_updated'))) return 'CSRBB Export';
  for (const [prefix, sub] of scriptToSubprocess) if (scriptName && scriptName.startsWith(prefix)) return sub;
  return null;
}

// Classify each log row — tag subprocess, skip adhoc / ICDB
const byScript = {};
for (const r of allLogs) {
  const k = r.location + '|' + r.scriptName;
  if (!byScript[k]) byScript[k] = [];
  byScript[k].push(r);
}
const adhocKeys = new Set();
for (const [k, rows] of Object.entries(byScript)) {
  const steps = [...new Set(rows.map(r => r.stepName))];
  if (isAdhoc(rows[0].scriptName, rows[0].location, steps)) adhocKeys.add(k);
}

for (const r of allLogs) {
  r._subprocess = null;
  if (r.location === 'ICDB') continue;
  if (adhocKeys.has(r.location + '|' + r.scriptName)) continue;
  r._subprocess = mapToSubprocess(r.scriptName);
}

// Build lookup: (loc, sub, step, month) -> MAX totalRuntimeHours among Completed
const lookup = {};
for (const r of allLogs) {
  if (r.stateName !== 'Completed') continue;
  if (!r._subprocess || !r.stepName) continue;
  const key = r.location + '|' + r._subprocess + '|' + r.stepName + '|' + r.reportMonth;
  const dur = r.totalRuntimeHours || 0;
  if (!lookup[key] || dur > lookup[key]) lookup[key] = dur;
}

// Also track if (loc, sub, month) had any entries at all (for 'run incomplete' marker)
const hadEntries = new Set();
for (const r of allLogs) {
  if (r._subprocess) hadEntries.add(r.location + '|' + r._subprocess + '|' + r.reportMonth);
}

// Build output using golden mapping ordering + step list
const rows = [['Location', 'Subprocess', 'Stepname', '2512', '2601', '2602', '2603']];
const mappings = golden.mappings.slice().sort((a, b) => a.location.localeCompare(b.location) || a.globalSeq - b.globalSeq);
const byLoc = {};
for (const m of mappings) {
  if (!byLoc[m.location]) byLoc[m.location] = [];
  byLoc[m.location].push(m);
}
const locOrder = Object.keys(byLoc).sort();

for (const loc of locOrder) {
  for (const sub of byLoc[loc]) {
    const stepTotals = months.map(() => 0);
    for (const step of sub.steps) {
      const vals = months.map((m, idx) => {
        const key = loc + '|' + sub.subprocess + '|' + step + '|' + m;
        const val = lookup[key];
        if (val !== undefined && val > 0) {
          stepTotals[idx] += val;
          return parseFloat(val.toFixed(2));
        }
        return '';
      });
      rows.push([loc, sub.subprocess, step, ...vals]);
    }
    const totalVals = months.map((m, idx) => {
      if (stepTotals[idx] > 0) return parseFloat(stepTotals[idx].toFixed(2));
      if (hadEntries.has(loc + '|' + sub.subprocess + '|' + m)) return 'run incomplete';
      return '';
    });
    rows.push(['', '', 'total sum for ' + sub.subprocess, ...totalVals]);
    rows.push([]);
  }
}

const out = XLSX.utils.book_new();
const ws = XLSX.utils.aoa_to_sheet(rows);
ws['!autofilter'] = { ref: XLSX.utils.encode_range({ s: { r: 0, c: 0 }, e: { r: 0, c: 6 } }) };
ws['!cols'] = [{ wch: 10 }, { wch: 28 }, { wch: 45 }, { wch: 12 }, { wch: 12 }, { wch: 16 }, { wch: 16 }];
XLSX.utils.book_append_sheet(out, ws, 'Efficient Runtime by Step');
XLSX.writeFile(out, 'prod-efficient-runtime-by-step-v2.xlsx');
console.log('Wrote: prod-efficient-runtime-by-step-v2.xlsx');
console.log('  Rows:', rows.length - 1);

// Verify FRDB NII / Planning risk in 2601
console.log('\n=== FRDB NII / Planning risk verification ===');
const prev = rows.filter(r => r[0] === 'FRDB' && r[1] === 'NII / Planning risk');
for (const r of prev) console.log('  ' + String(r[2]).padEnd(45) + ' | ' + String(r[3] || '').padStart(8) + ' | ' + String(r[4] || '').padStart(8) + ' | ' + String(r[5] || '').padStart(10) + ' | ' + String(r[6] || '').padStart(14));
const totals = rows.filter(r => r[0] === '' && r[2] === 'total sum for NII / Planning risk');
// Index: FRDB is 7th location in locOrder (AUDB, BEDB, BEGT, DEDB, DEGT, ESDB, FRDB)
console.log('\n  FRDB NII total expected: 23.27 | 28.97 | 27.9 | 0 (per Rose manual)');
console.log('  FRDB NII total actual:   ' + (totals[6] ? totals[6].slice(3).join(' | ') : 'not found'));
