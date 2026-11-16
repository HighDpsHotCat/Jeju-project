import { byId as $, node } from "./ui.js";

let catalogRequest;
export function loadAnalysisCatalog() {
  catalogRequest ??= fetch("/species.json").then(readJson).catch(error => {
    catalogRequest = null;
    throw error;
  });
  return catalogRequest;
}

async function readJson(response) {
  if (!response.ok) throw Error("HTTP " + response.status);
  return response.json();
}

export function analysisLink(id, label) {
  const link = node("a", label);
  const url = new URL(location.href);
  url.searchParams.set("analyze", id);
  link.href = url.href;
  link.dataset.analyze = id;
  return link;
}

const api = "https://api.gbif.org/v1/";
const cityCodes = new Map([["KOR.12.1_2", "제주시"], ["KOR.12.2_2", "서귀포시"]]);
const number = n => n.toLocaleString("ko-KR");
const amount = n => number(n) + "건";

async function resolveSpecies(scientific, signal) {
  const params = new URLSearchParams({name: scientific, rank: "SPECIES", kingdom: "Animalia", strict: "true"});
  const match = await fetch(api + "species/match?" + params, {signal}).then(readJson);
  if (match.matchType !== "EXACT" || match.kingdom !== "Animalia" || match.rank !== "SPECIES") return null;
  return match.acceptedUsageKey || match.speciesKey || match.usageKey;
}

function occurrenceQuery(key, period) {
  const params = new URLSearchParams({
    taxonKey: key, gadmGid: "KOR.12_1", country: "KR", kingdomKey: "1",
    basisOfRecord: "HUMAN_OBSERVATION", occurrenceStatus: "PRESENT",
    hasCoordinate: "true", hasGeospatialIssue: "false",
  });
  if (period !== "all") params.set("year", period);
  return params;
}

function facetCounts(data, field) {
  const facet = data.facets?.find(item => item.field === field);
  if (!facet && data.count > 0) throw Error("Missing " + field);
  return new Map((facet?.counts || []).map(({name, count}) => [name, count]));
}

export function summarize(data) {
  const total = data.count;
  if (!Number.isSafeInteger(total) || total < 0) throw Error("Invalid total");
  const regionCounts = facetCounts(data, "GADM_LEVEL_2_GID");
  const monthCounts = facetCounts(data, "MONTH");
  const regions = [...cityCodes].map(([code, name]) => [name, regionCounts.get(code) || 0]);
  const months = Array.from({length: 12}, (_, i) => monthCounts.get(String(i + 1)) || 0);
  const dated = months.reduce((a, b) => a + b, 0);
  const unlocated = total - regions.reduce((a, [, n]) => a + n, 0);
  if (dated > total || unlocated < 0) throw Error("Inconsistent facets");
  return {total, regions, months, dated, unlocated};
}

// Compare equal monthly shares with a multinomial fitted to the observed months.
// BIC penalizes the 11 extra parameters before describing a concentration.
export function monthFinding(months, dated) {
  if (!dated) return "관찰한 달을 확인할 수 있는 기록이 없어요.";
  const peak = Math.max(...months);
  const leaders = months.flatMap((n, i) => n === peak ? [i + 1] : []);
  if (leaders.length === 12) return "모든 달의 기록 수가 같아요.";
  const names = leaders.map(i => i + "월").join("·");
  const peakText = names + (leaders.length > 1 ? "에 각각 " : "에 ") + amount(peak) + "으로 가장 많이 기록됐어요.";
  if (dated < 24) return peakText + " 월 정보가 있는 기록이 적어 반복되는 경향을 말하기는 어려워요.";
  const gain = months.reduce((sum, n) => n ? sum + n * Math.log(12 * n / dated) : sum, 0);
  return peakText + (2 * gain > 11 * Math.log(dated)
    ? " 월 정보가 있는 기록은 일부 달에 집중돼 있어요."
    : " 전체적으로 특정 달에 집중됐다고 볼 근거는 약해요.");
}

function renderReport(report, query, sourceUrl) {
  const {total, regions, months, dated, unlocated} = report;
  const box = $("analysis-result");
  const range = $("analysis-period").selectedOptions[0].textContent;
  const lead = node("p", range + " · 제주에서 관찰된 기록", "analysis-caption");
  const count = node("strong", amount(total), "analysis-total");
  const lines = [];
  if (!total) lines.push("이 조건에 맞는 공개 기록이 없어요. 제주에 살지 않는다는 뜻은 아니에요.");
  else {
    const [[first, high], [second, low]] = [...regions].sort((a,b) => b[1] - a[1]);
    if (high) lines.push(high === low ? "두 행정시에 각각 " + amount(high) + "이 기록됐어요."
      : "지역을 확인할 수 있는 기록은 " + first + " " + amount(high) + ", " + second + " " + amount(low) + "이에요.");
    else lines.push("행정시를 확인할 수 있는 기록이 없어요.");
    lines.push(monthFinding(months, dated));
  }
  box.replaceChildren(lead, count, node("p", lines.join(" "), "analysis-story"));
  const area = node("dl", null, "analysis-regions");
  for (const [label, n] of [...regions, ...(unlocated ? [["행정시 미상", unlocated]] : [])]) {
    const row = node("div");
    row.append(node("dt", label), node("dd", amount(n)));
    area.append(row);
  }
  const bars = node("ol", null, "analysis-months");
  const max = Math.max(1, ...months);
  months.forEach((n, i) => {
    const row = node("li");
    const track = node("span", null, "analysis-track");
    track.setAttribute("aria-hidden", "true");
    const bar = node("i");
    bar.style.width = (100 * n / max) + "%";
    track.append(bar);
    row.append(node("span", (i + 1) + "월"), track, node("span", amount(n)));
    bars.append(row);
  });
  box.append(node("h3", "지역별 목격 건수"), area, node("h3", "월별 목격 빈도"), node("p",
    "같은 달의 기록을 합산했어요. 월 확인 " + amount(dated) + " · 월 미상 " + amount(total - dated), "analysis-caption"), bars);
  const evidence = node("a", "GBIF 관찰 원문 보기 ↗");
  const portal = new URL("https://www.gbif.org/occurrence/search");
  for (const [key, value] of query) portal.searchParams.set(key.replace(/[A-Z]/g, c => "_" + c.toLowerCase()), value);
  evidence.href = portal.href;
  const raw = node("a", "집계 데이터 ↗");
  raw.href = sourceUrl;
  for (const link of [evidence, raw]) { link.target = "_blank"; link.rel = "noopener"; }
  const sources = node("p", null, "analysis-sources");
  sources.append(evidence, raw);
  box.append(node("p", "좌표가 있는 사람의 관찰 기록을 한 건씩 셌어요. 같은 동물이 여러 번 기록될 수 있고, 사람들이 자주 찾는 곳의 기록이 많을 수 있어요.", "analysis-caption"), sources);
}

const dialog = $("analysis-dialog");
const period = $("analysis-period");
const year = Number(new Intl.DateTimeFormat("en", {timeZone: "Asia/Seoul", year: "numeric"}).format(new Date()));
period.add(new Option((year - 5) + "–" + (year - 1) + "년", (year - 5) + "," + (year - 1)));
period.add(new Option((year - 1) + "년", String(year - 1)));
let active, controller;
async function runAnalysis() {
  controller?.abort();
  const request = controller = new AbortController();
  const timer = setTimeout(() => request.abort(), 22000);
  const result = $("analysis-result");
  result.replaceChildren(node("p", "GBIF에서 제주 관찰 기록을 확인하고 있어요…"));
  try {
    const animal = (await loadAnalysisCatalog()).find(r => r.species_id === active);
    if (request.signal.aborted) throw Error("Cancelled");
    if (!animal) { result.textContent = "제주 도감에서 이 종을 찾을 수 없어요."; return; }
    $("analysis-title").textContent = (animal.korean_name || animal.scientific_name) + " 목격 분석";
    const key = await resolveSpecies(animal.scientific_name, request.signal);
    if (request !== controller) return;
    if (!key) { result.textContent = "GBIF에서 이 동물의 학명을 정확하게 확인할 수 없어 집계를 보류했어요."; return; }
    const query = occurrenceQuery(key, period.value);
    const url = new URL(api + "occurrence/search?" + query);
    url.searchParams.set("limit", "0");
    for (const facet of ["gadmLevel2Gid", "month"]) url.searchParams.append("facet", facet);
    url.searchParams.set("facetLimit", "20");
    const data = await fetch(url, {signal: request.signal}).then(readJson);
    if (request === controller) renderReport(summarize(data), query, url.href);
  } catch {
    if (request !== controller) return;
    const retry = node("button", "다시 조회", "pill");
    retry.onclick = runAnalysis;
    result.replaceChildren(node("p", "관찰 자료를 불러오지 못했어요. 연결을 확인한 뒤 다시 조회해 주세요."), retry);
  } finally { clearTimeout(timer); }
}

function syncAnalysis() {
  const id = new URL(location.href).searchParams.get("analyze");
  if (!id) {
    controller?.abort();
    controller = null;
    active = null;
    if (dialog.open) dialog.close();
    return;
  }
  if (id === active) return;
  active = id;
  period.value = "all";
  $("analysis-title").textContent = "동물 목격 분석";
  if (!dialog.open) dialog.showModal();
  dialog.scrollTop = 0;
  runAnalysis();
}
export function openAnalysis(id) {
  const url = new URL(location.href);
  url.searchParams.set("analyze", id);
  history.pushState({...history.state, analysisOpen: true}, "", url);
  syncAnalysis();
}
function closeAnalysis() {
  if (history.state?.analysisOpen) history.back();
  else {
    const url = new URL(location.href);
    url.searchParams.delete("analyze");
    history.replaceState(history.state, "", url);
    syncAnalysis();
  }
}
document.addEventListener("click", e => {
  const link = e.target.closest("a[data-analyze]");
  if (!link || e.button || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
  e.preventDefault();
  openAnalysis(link.dataset.analyze);
});
$("analysis-close").onclick = closeAnalysis;
dialog.addEventListener("cancel", e => { e.preventDefault(); closeAnalysis(); });
period.onchange = runAnalysis;
window.addEventListener("popstate", syncAnalysis);
syncAnalysis();
