import { byId as $, node, icon } from "./ui.js";
import { visits, habitats, speciesIds, guideUrl, mapUrl, loadRecords } from "./map-data.js";
import { readVisit, renderVisits, filterUrl } from "./visit-map.js";

const layers = {
  visit: ["지역 선택", "지도를 눌러 여행할 지역을 선택하세요."],
  habitat: ["서식 기록", "자료에 동물이 기록된 습지·해안·섬의 대략적인 위치입니다."],
  records: ["관찰 기록", "행정시를 고르면 날짜가 있는 관찰 기록과 원문을 볼 수 있습니다."],
};
const cities = [
  { id: "jeju", name: "제주시", pixel: [720, 272] },
  { id: "seogwipo", name: "서귀포시", pixel: [722, 666] },
];
let records, recordError = false;
let layer, selected, filterSpecies, trip;

function readSelection() {
  const params = new URLSearchParams(location.search);
  layer = Object.hasOwn(layers, params.get("layer")) ? params.get("layer") : "visit";
  trip = readVisit(params);
  selected = layer === "visit" ? trip.place || trip.zone : items().find((p) => p.id === params.get("place")) || items()[0];
  filterSpecies = layer === "records" ? params.get("species") || "" : "";
}
function items() {
  return layer === "habitat" ? habitats : cities;
}
function anchor(label, url, cls) {
  const a = node("a", label, cls);
  a.href = url;
  return a;
}
function section(heading, ...children) {
  const el = node("section", null, "detail-section");
  el.append(node("h3", heading), ...children);
  return el;
}
function choose(nextLayer, id, scroll = false, species = "") {
  const url = new URL(mapUrl(nextLayer, id), location.origin);
  if (species) url.searchParams.set("species", species);
  if (url.href !== location.href) history.pushState(null, "", url);
  readSelection();
  render();
  if (scroll && matchMedia("(max-width:820px)").matches)
    $("detail").scrollIntoView({ block: "start" });
}
function placeDetail(p) {
  const address = node("p", null, "area");
  address.append(icon("pin"), p.area);
  const animals = node("div", null, "animals-here");
  for (const name of p.species) animals.append(anchor(name + " ↗", guideUrl(speciesIds[name], name)));
  const parts = [node("span", p.type, "tag"), node("h2", p.name),
    address, node("p", p.description), section("관련 동물 도감", animals)];
  const actions = node("div", null, "actions");
  const visit = visits.find((v) => v.habitat === p.id);
  if (visit) actions.append(anchor("탐방 출발점 보기", mapUrl("visit", visit.id)));
  const sources = node("div", null, "source-links");
  for (const [label,url] of p.sources) sources.append(anchor(label + " ↗", url));
  if (actions.childElementCount) parts.push(actions);
  if ($("grades").checked && p.grade)
    parts.push(section("대표 지점 생태·자연도", node("p", p.grade)));
  parts.push(section("자료 출처", sources));
  $("detail").replaceChildren(...parts);
}
function cityDetail(city) {
  const detail = $("detail");
  detail.replaceChildren(node("span", "행정시 단위", "tag"), node("h2", city.name + " 관찰 기록"));
  if (!records) {
    detail.append(node("p", recordError ? "관찰 자료를 불러오지 못했습니다." : "관찰 자료를 불러오는 중…"));
    if (recordError) {
      const retry = node("button", "다시 불러오기", "pill");
      retry.onclick = fetchRecords;
      detail.append(retry);
    }
    return;
  }
  const rows = records[city.name];
  const species = new Map(rows.map((r) => [r.species, r.name]));
  detail.append(node("p", rows.length + "건 · " + species.size + "종. 지도에는 기록이 속한 행정시를 표시합니다."));
  const label = node("label", "관찰 동물", "record-filter");
  const select = node("select");
  select.append(new Option("전체 동물", ""));
  for (const [id, name] of [...species].sort((a,b) => a[1].localeCompare(b[1],"ko")))
    select.append(new Option(name, id));
  select.value = species.has(filterSpecies) ? filterSpecies : "";
  select.onchange = () => choose("records", city.id, false, select.value);
  label.append(select);
  const filtered = rows.filter((row) => !select.value || row.species === select.value);
  const list = node("ol", null, "record-list");
  for (const row of filtered) {
    const li = node("li");
    const title = node("div", null, "record-title");
    const date = node("time", row.date.slice(0, 10));
    date.dateTime = row.date;
    title.append(anchor(row.name + " ↗", guideUrl(row.species, row.name)), date);
    const credit = node("small", [row.credit, row.dataset].filter(Boolean).join(" · "));
    const sources = node("div", null, "source-links");
    sources.append(anchor("GBIF 원문 ↗", "https://www.gbif.org/occurrence/" + row.id), anchor("이용허락 ↗", row.license));
    li.append(title, credit, sources);
    list.append(li);
  }
  detail.append(label, section("날짜순 기록 " + filtered.length + "건", list));
}
function render() {
  const listing = items();
  $("layer-title").textContent = layers[layer][0];
  $("layer-note").textContent = layers[layer][1];
  $("place-count").textContent = layer === "records" ? (records ? Object.values(records).flat().length + "건" : "2개 행정시") : listing.length + "곳";
  $("layer-select").value = layer;
  $("markers").replaceChildren();
  $("offshore").replaceChildren();
  $("places").replaceChildren();
  $("stage").dataset.layer = layer;
  const visiting = layer === "visit";
  $("atlas-layout").classList.toggle("visit-layout", visiting);
  $("map-panel").hidden = false;
  $("districts").style.display = visiting ? "block" : "none";
  $("places").hidden = visiting;
  $("detail").hidden = false;
  if (visiting) { renderVisits(trip); return; }
  for (const [i, p] of listing.entries()) {
    const active = String(p.id === selected.id);
    const number = i + 1;
    const region = layer === "records";
    const count = region && records ? records[p.name].length + "건" : "";
    const marker = node("button", null, p.offshore ? "offshore-button" : region ? "city-marker" : "pin");
    marker.setAttribute("aria-label", p.name);
    marker.setAttribute("aria-pressed", active);
    marker.onclick = () => choose(layer, p.id, true);
    if (p.offshore) {
      marker.textContent = "본섬 밖 · " + p.name;
      $("offshore").append(marker);
    } else {
      marker.style.left = p.pixel[0] / 1440 * 100 + "%";
      marker.style.top = p.pixel[1] / 910 * 100 + "%";
      if (p.pixel[0] < 350) marker.classList.add("label-right");
      marker.append(node("span", region ? [p.name, count].filter(Boolean).join(" ") : number));
      $("markers").append(marker);
    }
    const b = node("button", null, "place-option");
    b.setAttribute("aria-pressed", active);
    b.append(node("span", number, "place-number"), node("b", p.name),
      node("small", region ? count : p.type));
    b.onclick = marker.onclick;
    $("places").append(b);
  }
  if (layer === "records") cityDetail(selected);
  else placeDetail(selected);
}
async function fetchRecords() {
  recordError = false;
  if (layer === "records") render();
  try { records = await loadRecords(); } catch { recordError = true; }
  if (layer === "records") render();
}
$("layer-select").onchange = (event) => {
  const id = event.target.value;
  if (id === layer) return;
  const equivalent = id === "habitat" ? habitats.find((p) => p.id === selected?.habitat) :
    id === "visit" && layer === "habitat" ? visits.find((p) => p.habitat === selected.id) : null;
  if (id === "visit" && !equivalent) openTrip("/map");
  else choose(id, (equivalent || (id === "habitat" ? habitats[0] : cities[0])).id);
};
$("grades").onchange = () => {
  $("stage").classList.toggle("show-grades", $("grades").checked);
  $("grade-legend").hidden = !$("grades").checked;
  render();
};
$("zoom").onclick = () => {
  const stage = $("stage"), view = $("viewport");
  const active = stage.classList.toggle("zoomed");
  $("zoom").setAttribute("aria-pressed", String(active));
  $("zoom").textContent = active ? "− 축소" : "＋ 확대";
  $("zoom").setAttribute("aria-label", active ? "지도 축소" : "지도 확대");
  const [x, y] = selected?.pixel || [720,455];
  view.scrollTo(active ? stage.offsetWidth * x / 1440 - view.clientWidth / 2 : 0,
    active ? stage.offsetHeight * y / 910 - view.clientHeight / 2 : 0);
};
function depth() {
  return trip.place ? 2 : trip.zone ? 1 : 0;
}
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
function navigate() {
  const oldDepth = depth(), oldLayer = layer, oldTrip = trip;
  readSelection();
  render();
  if (layer !== "visit") return;
  const step = depth() - oldDepth;
  const screen = trip.zone ? $("detail") : $("map-panel");
  let focus = screen.querySelector("h2");
  if (step < 0 && oldLayer === "visit") {
    const id = oldTrip.place?.id || oldTrip.zone?.id;
    focus = screen.querySelector('[data-return-to="' + id + '"]') || focus;
  } else if (!step) {
    focus = screen.querySelector(".kind-select") || focus;
  }
  focus.focus({preventScroll: true});
  if (reducedMotion.matches || (!step && oldLayer === layer)) return;
  screen.getAnimations().forEach((a) => a.cancel());
  const offset = step < 0 ? -32 : 36;
  screen.animate([
    {opacity: 0.25, transform: "translateX(" + offset + "px) scale(.985)"},
    {opacity: 1, transform: "translateX(0) scale(1)"}
  ], {duration: 235, easing: "cubic-bezier(.2,.75,.25,1)"});
  const rows = screen.querySelectorAll(".trip-card, .animal-entry, .district-label");
  Array.from(rows).slice(0, 5).forEach((row, index) => {
    row.animate([{opacity: 0}, {opacity: 1}], {
      duration: 170, delay: 24 + index * 22, fill: "backwards"
    });
  });
}
window.addEventListener("popstate", navigate);
function openTrip(href, action = "open") {
  const url = new URL(href, location.origin);
  const state = history.state || {};
  if (action === "back") {
    const parent = state.tripParent && new URL(state.tripParent, location.origin);
    const target = readVisit(url.searchParams);
    const previous = parent && readVisit(parent.searchParams);
    if (previous && previous.zone === target.zone && previous.kind === target.kind && !previous.place) {
      history.back();
      return;
    }
    history.replaceState(null, "", url);
  } else if (action === "filter") {
    history.replaceState({...state, tripScroll: 0}, "", url);
  } else if (url.href !== location.href) {
    const parent = location.pathname + location.search;
    const list = $("detail").querySelector(".trip-scroll");
    if (layer === "visit" && trip.zone && !trip.place && list)
      history.replaceState({...state, tripScroll: list.scrollTop}, "");
    history.pushState({tripParent: layer === "visit" ? parent : null}, "", url);
  }
  navigate();
}
$("detail").addEventListener("change", (event) => {
  if (event.target.matches(".kind-select")) openTrip(filterUrl(trip, event.target.value), "filter");
});
$("atlas-layout").addEventListener("click", (event) => {
  if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
  const target = event.target.closest("[data-trip], #districts path[data-zone]");
  if (!target) return;
  event.preventDefault();
  openTrip(target.dataset.zone ? "/map?layer=visit&zone=" + target.dataset.zone : target.href, target.dataset.trip);
});
readSelection();
render();
fetchRecords();
