// v3: same as v2 but with bold totals + adds two summary sheets
// - Sheet 1: Efficient Runtime by Step (with bolded total rows)
// - Sheet 2: Subprocess Summary (pivot with frequency, change vs 2602, driver explanation)
// - Sheet 3: Location Summary (E2E efficient runtime totals per location)

const fs = require('fs');
const XLSX = require('xlsx');

const wb = XLSX.readFile('q3 logs.ods');
const months = ['2512', '2601', '2602', '2603'];

function parseMonth(m) {
  return XLSX.utils.sheet_to_json(wb.Sheets[m], { defval: '' });
}

const allLogs = [];
for (const m of months) {
  for (const r of parseMonth(m)) {
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

const golden = JSON.parse(fs.readFileSync('../../backend/Data/SeedData/golden-mapping.json', 'utf8'));

// Same adhoc + subprocess rules
const adhocPrefixes = ['ICDB_MCP','ICDB R','ICDB MCP','Copy_dbwruns','Copydbworuns','Copy_variables','CSRBB_Sideload','LAD_SF'];
function isAdhoc(script, location, steps) {
  if (!script) return true;
  for (const p of adhocPrefixes) if (script.startsWith(p)) return true;
  if ((location==='NLBTR'||location==='BEGT'||location==='DEGT') &&
    (script.toUpperCase().includes('_RAS')||script.toUpperCase().includes('RAS)')||
     script.toLowerCase().includes('ras run')||script.includes(' to RAS')||
     script.toUpperCase().endsWith('_RAS'))) return true;
  if (location==='AUDB' && script.startsWith('TRISS_')) return true;
  if (location==='AUDB' && (script.includes('CSRBB_Export')||script.includes('_CSRBB_Export'))) return true;
  if (steps.length===1 && steps[0]==='CSRBB exports' && !script.includes('_Export_updated')) return true;
  return false;
}
const scriptToSubprocess = [
  ['Delete_Portfolio','Delete Portfolio'],['Load_compare_MP','Model Parameters'],['Load_MP','Model Parameters'],
  ['Load_compare_Spreads','Spreads'],['Load_Spreads','Spreads'],['LOAD_POS_NLBTR_MCP_RAS','Load Position (RAS)'],
  ['Load_POS_','Load Position'],['LOAD_POS_','Load Position'],['Load_ELP','Load ELP and Mortgages'],
  ['Load_Index','Load Index History'],['Copy_Portfolio_SLM','SLM'],['Copy_Portfolio_DEGT','Load Position (RAS)'],
  ['Copy_Portfolio_NLBTR','Load Position (RAS)'],['Copy_Portfolio','Load Position'],['POS_MCP_Export','Load Position'],
  ['Import_Strategies_Risk','Import Strategies'],['Import_Strategies_IG','Import Strategies'],
  ['Import_Strategies','Import Strategies'],['Valuation_AIC','AIC'],['Valuation_NLBTR_MCP_RAS','Valuation (RAS)'],
  ['Valuation_MCP_Export','Valuation'],['Valuation_MCP','Valuation'],['Valuation_','Valuation'],
  ['CSRBB_Valuation_','CSRBB (Val)'],['CSRBB_Planning_','CSRBB (Plan)'],['EC_NPV_Export','EC'],['EC_MCP','EC'],
  ['EC_','EC'],['KR_BPV_Export','KR'],['KR_MCP','KR'],['KR_','KR'],['GAP_ITS','GAP ITS Planning'],
  ['GAP_','Gap + Liquidity IR'],['Planning_NLBTR_MCP_RAS','NII / Planning risk (RAS)'],
  ['Planning_DEGT_MCP_RAS','NII / Planning risk (RAS)'],['Combined_Planning','NII / Planning risk'],
  ['Planning_MCP_Export','NII / Planning risk'],['Planning_MCP','NII / Planning risk'],
  ['Planning_','NII / Planning risk'],['ITS_','ITS Planning'],['FTP_EVE','FTP EVE SOT'],
  ['SOT_Create','SOT'],['SOT_','SOT'],['Finance_Planning','Planning Finance'],
  ['Finance_Strategies','Planning Finance'],['Cost_Pricing','Cost Pricing'],['SOLO_','SOLO'],
  ['SLM_','SLM'],['EVE_Monthly','EVE (Monthly)'],['EVE_','EVE Optionality'],['TRISS_','TRISS export'],
  ['Fair_Value_','Fair Value'],['CSRBB_Export_updated','CSRBB Export'],
];
function mapToSubprocess(scriptName) {
  if (scriptName && scriptName.toUpperCase().includes('PDCE')) return 'PDCE';
  if (scriptName && (scriptName.includes('CSRBB_Export_updated')||scriptName==='CSRBB_Export'||scriptName.endsWith('_CSRBB_Export_updated'))) return 'CSRBB Export';
  for (const [prefix, sub] of scriptToSubprocess) if (scriptName && scriptName.startsWith(prefix)) return sub;
  return null;
}

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

// Build (loc, sub, step, month) -> MAX completed duration
const lookup = {};
for (const r of allLogs) {
  if (r.stateName !== 'Completed') continue;
  if (!r._subprocess || !r.stepName) continue;
  const key = r.location + '|' + r._subprocess + '|' + r.stepName + '|' + r.reportMonth;
  const dur = r.totalRuntimeHours || 0;
  if (!lookup[key] || dur > lookup[key]) lookup[key] = dur;
}

const hadEntries = new Set();
for (const r of allLogs) if (r._subprocess) hadEntries.add(r.location + '|' + r._subprocess + '|' + r.reportMonth);

// Sort mappings by location then globalSeq
const mappings = golden.mappings.slice().sort((a,b) =>
  a.location.localeCompare(b.location) || a.globalSeq - b.globalSeq);
const byLoc = {};
for (const m of mappings) {
  if (!byLoc[m.location]) byLoc[m.location] = [];
  byLoc[m.location].push(m);
}
const locOrder = Object.keys(byLoc).sort();

// ===== Sheet 1: Efficient Runtime by Step (with bold totals) =====
const sheet1Rows = [['Location','Subprocess','Stepname','2512','2601','2602','2603']];
const boldRowIndices = [0]; // header is also bold
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
      sheet1Rows.push([loc, sub.subprocess, step, ...vals]);
    }
    const totalVals = months.map((m, idx) => {
      if (stepTotals[idx] > 0) return parseFloat(stepTotals[idx].toFixed(2));
      if (hadEntries.has(loc + '|' + sub.subprocess + '|' + m)) return 'run incomplete';
      return '';
    });
    boldRowIndices.push(sheet1Rows.length);
    sheet1Rows.push(['', '', 'total sum for ' + sub.subprocess, ...totalVals]);
    sheet1Rows.push([]);
  }
}

// ===== Sheet 2: Subprocess Summary (with change vs 2602 + driver) =====
const sheet2Rows = [['Location','Subprocess','Phase','Frequency','2512 (h)','2601 (h)','2602 (h)','2603 (h)','Δ 2512→2602','Δ 2601→2602','Δ 2603→2602','Driver — what changed']];

// Collect step-level data per (loc, sub, month) to compute step-level diffs
const subStepMap = {}; // key: loc|sub → { month: { step: runtime } }
for (const loc of locOrder) {
  for (const sub of byLoc[loc]) {
    const k = loc + '|' + sub.subprocess;
    subStepMap[k] = { '2512': {}, '2601': {}, '2602': {}, '2603': {} };
    for (const step of sub.steps) {
      for (const m of months) {
        const key = loc + '|' + sub.subprocess + '|' + step + '|' + m;
        const val = lookup[key];
        if (val !== undefined && val > 0) subStepMap[k][m][step] = val;
      }
    }
  }
}

// Explain driver of change between two months: which steps disappeared / appeared / changed significantly
function explainDiff(subStep, fromMonth, toMonth) {
  const fromSteps = subStep[fromMonth] || {};
  const toSteps = subStep[toMonth] || {};
  const notes = [];
  const allSteps = new Set([...Object.keys(fromSteps), ...Object.keys(toSteps)]);
  for (const s of allSteps) {
    const from = fromSteps[s];
    const to = toSteps[s];
    if (from && !to) notes.push('- removed: ' + s + ' (' + from.toFixed(1) + 'h)');
    else if (!from && to) notes.push('+ added: ' + s + ' (' + to.toFixed(1) + 'h)');
    else if (from && to) {
      const diff = to - from;
      if (Math.abs(diff) > 0.5 && Math.abs(diff) / Math.max(from, 0.01) > 0.2) {
        notes.push((diff < 0 ? '↓ ' : '↑ ') + s + ': ' + from.toFixed(1) + 'h → ' + to.toFixed(1) + 'h (' + (diff>0?'+':'') + diff.toFixed(1) + 'h)');
      }
    }
  }
  return notes.length ? notes.join('; ') : 'no significant change';
}

for (const loc of locOrder) {
  for (const sub of byLoc[loc]) {
    const subStep = subStepMap[loc + '|' + sub.subprocess];
    // Sum per month
    const totals = months.map(m => {
      const perStep = subStep[m] || {};
      return Object.values(perStep).reduce((a,b) => a+b, 0);
    });
    const format = v => v > 0 ? parseFloat(v.toFixed(2)) : (hadEntries.has(loc + '|' + sub.subprocess + '|' + months[totals.indexOf(v)]) ? 'run incomplete' : '');
    const [t12, t01, t02, t03] = totals;
    const delta = (a, b) => {
      if (a === 0 && b === 0) return '';
      if (a === 0) return '+' + b.toFixed(2) + ' (new)';
      if (b === 0) return '-' + a.toFixed(2) + ' (gone)';
      const d = b - a;
      return (d>=0?'+':'') + d.toFixed(2) + ' (' + ((d/a)*100).toFixed(0) + '%)';
    };
    // Driver explanations — compare each non-2602 month to 2602
    let drivers = [];
    if (t12 !== t02) drivers.push('2512→2602: ' + explainDiff(subStep, '2512', '2602'));
    if (t01 !== t02) drivers.push('2601→2602: ' + explainDiff(subStep, '2601', '2602'));
    if (t03 !== t02) drivers.push('2603→2602: ' + explainDiff(subStep, '2603', '2602'));
    const driverText = drivers.length ? drivers.join(' | ') : 'stable across months';

    sheet2Rows.push([
      loc,
      sub.subprocess,
      sub.phaseDisplay || sub.phase,
      sub.quarterly ? 'Quarterly' : 'Monthly',
      totals[0] > 0 ? parseFloat(totals[0].toFixed(2)) : (hadEntries.has(loc + '|' + sub.subprocess + '|2512') ? 'run incomplete' : ''),
      totals[1] > 0 ? parseFloat(totals[1].toFixed(2)) : (hadEntries.has(loc + '|' + sub.subprocess + '|2601') ? 'run incomplete' : ''),
      totals[2] > 0 ? parseFloat(totals[2].toFixed(2)) : (hadEntries.has(loc + '|' + sub.subprocess + '|2602') ? 'run incomplete' : ''),
      totals[3] > 0 ? parseFloat(totals[3].toFixed(2)) : (hadEntries.has(loc + '|' + sub.subprocess + '|2603') ? 'run incomplete' : ''),
      delta(t12, t02),
      delta(t01, t02),
      delta(t03, t02),
      driverText,
    ]);
  }
}

// ===== Sheet 3: Location Summary (E2E total efficient runtime per location) =====
const sheet3Rows = [['Location','2512 Total (h)','2601 Total (h)','2602 Total (h)','2603 Total (h)','Subprocesses Active','Avg/Month (h)']];
for (const loc of locOrder) {
  const locTotals = months.map(() => 0);
  let subsActive = 0;
  for (const sub of byLoc[loc]) {
    const subStep = subStepMap[loc + '|' + sub.subprocess];
    const subTotals = months.map(m => {
      const perStep = subStep[m] || {};
      return Object.values(perStep).reduce((a,b) => a+b, 0);
    });
    for (let i = 0; i < 4; i++) locTotals[i] += subTotals[i];
    if (subTotals.some(v => v > 0)) subsActive++;
  }
  const nonZero = locTotals.filter(v => v > 0);
  const avgPerMonth = nonZero.length > 0 ? nonZero.reduce((a,b)=>a+b,0) / nonZero.length : 0;
  sheet3Rows.push([
    loc,
    parseFloat(locTotals[0].toFixed(2)),
    parseFloat(locTotals[1].toFixed(2)),
    parseFloat(locTotals[2].toFixed(2)),
    parseFloat(locTotals[3].toFixed(2)),
    subsActive,
    parseFloat(avgPerMonth.toFixed(2))
  ]);
}
// Grand total row
const grand = [0,0,0,0];
for (let i = 1; i < sheet3Rows.length; i++) {
  for (let j = 0; j < 4; j++) grand[j] += sheet3Rows[i][j+1] || 0;
}
sheet3Rows.push(['TOTAL (all locations)', ...grand.map(v => parseFloat(v.toFixed(2))), '', parseFloat(((grand.reduce((a,b)=>a+b,0))/4).toFixed(2))]);

// ===== WRITE =====
const out = XLSX.utils.book_new();

// Sheet 1 with bold total rows
const ws1 = XLSX.utils.aoa_to_sheet(sheet1Rows);
ws1['!autofilter'] = { ref: XLSX.utils.encode_range({s:{r:0,c:0}, e:{r:0,c:6}}) };
ws1['!cols'] = [{wch:10},{wch:28},{wch:45},{wch:12},{wch:12},{wch:16},{wch:16}];
// Apply bold to total rows. xlsx library needs cell-level styling; we use s.font.bold = true
for (const ri of boldRowIndices) {
  for (let c = 0; c < 7; c++) {
    const addr = XLSX.utils.encode_cell({ r: ri, c });
    if (!ws1[addr]) ws1[addr] = { t: 's', v: '' };
    ws1[addr].s = { font: { bold: true } };
  }
}
XLSX.utils.book_append_sheet(out, ws1, 'Efficient Runtime by Step');

// Sheet 2
const ws2 = XLSX.utils.aoa_to_sheet(sheet2Rows);
ws2['!autofilter'] = { ref: XLSX.utils.encode_range({s:{r:0,c:0}, e:{r:0,c:11}}) };
ws2['!cols'] = [{wch:10},{wch:28},{wch:14},{wch:10},{wch:12},{wch:12},{wch:12},{wch:16},{wch:16},{wch:16},{wch:16},{wch:80}];
// Bold header
for (let c = 0; c < 12; c++) {
  const addr = XLSX.utils.encode_cell({r:0, c});
  if (ws2[addr]) ws2[addr].s = { font: { bold: true } };
}
XLSX.utils.book_append_sheet(out, ws2, 'Subprocess Summary');

// Sheet 3
const ws3 = XLSX.utils.aoa_to_sheet(sheet3Rows);
ws3['!autofilter'] = { ref: XLSX.utils.encode_range({s:{r:0,c:0}, e:{r:0,c:6}}) };
ws3['!cols'] = [{wch:12},{wch:14},{wch:14},{wch:14},{wch:14},{wch:16},{wch:14}];
// Bold header + last (TOTAL) row
for (let c = 0; c < 7; c++) {
  const addrH = XLSX.utils.encode_cell({r:0, c});
  if (ws3[addrH]) ws3[addrH].s = { font: { bold: true } };
  const addrT = XLSX.utils.encode_cell({r:sheet3Rows.length-1, c});
  if (ws3[addrT]) ws3[addrT].s = { font: { bold: true } };
}
XLSX.utils.book_append_sheet(out, ws3, 'Location Summary');

// Use cellStyles to preserve our bold formatting
XLSX.writeFile(out, 'prod-efficient-runtime-by-step-v3.xlsx', { cellStyles: true });
console.log('Wrote: prod-efficient-runtime-by-step-v3.xlsx');
console.log('  Sheet 1 (Efficient Runtime by Step): ' + (sheet1Rows.length-1) + ' rows, ' + boldRowIndices.length + ' bold (total) rows');
console.log('  Sheet 2 (Subprocess Summary): ' + (sheet2Rows.length-1) + ' rows');
console.log('  Sheet 3 (Location Summary): ' + (sheet3Rows.length-1) + ' rows');
console.log('\n=== LOCATION SUMMARY PREVIEW ===');
for (const r of sheet3Rows) console.log('  ' + String(r[0]).padEnd(24) + ' | ' + String(r[1]).padStart(10) + ' | ' + String(r[2]).padStart(10) + ' | ' + String(r[3]).padStart(10) + ' | ' + String(r[4]).padStart(10) + ' | ' + String(r[6]).padStart(10));
