import { byId as $, node } from "./ui.js";
import { visits, habitats, guideUrl, mapUrl, loadRecords } from "./map-data.js";
import { readVisit, renderVisits, filterUrl } from "./visit-map.js";

import { clearPinMap, pinUrl, positionBubble, renderPinMap } from "./pin-map.js";

const layers = {
  visit: ["탐방 장소", "핀을 누르면 반경 2km에서 관찰된 동물이 보여요."],
  habitat: ["서식 기록", "핀을 누른 뒤 권역 이름을 누르면 기록된 동물이 보여요."],
  records: ["관찰 기록", "행정시를 고르면 날짜가 있는 관찰 기록과 원문을 볼 수 있습니다."],
};
const cities = [
  { id: "jeju", name: "제주시", pixel: [720, 272] },
  { id: "seogwipo", name: "서귀포시", pixel: [722, 666] },
];
let records, recordError = false;
let layer, selected, filterSpecies, trip, pinMode, pinPlace, expanded;

function readSelection() {
  const params = new URLSearchParams(location.search);
  layer = Object.hasOwn(layers, params.get("layer")) ? params.get("layer") : "visit";
  trip = readVisit(params);
  const entries = layer === "visit" ? visits : layer === "habitat" ? habitats : cities;
  const place = entries.find((p) => p.id === params.get("place"));
  const pin = entries.find((p) => p.id === params.get("pin"));
  const regionList = layer === "visit" && trip.zone && !place && !pin;
  pinMode = layer !== "records" && !regionList;
  pinPlace = pinMode ? pin || place : null;
  expanded = Boolean(pinPlace) && (pin ? params.get("show") === "animals" : true);
  selected = pinMode ? pinPlace : place || trip.zone || entries[0];
  filterSpecies = layer === "records" ? params.get("species") || "" : "";
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
  clearPinMap();
  $("layer-title").textContent = layers[layer][0];
  $("layer-note").textContent = layers[layer][1];
  $("layer-select").value = layer;
  $("markers").replaceChildren();
  $("offshore").replaceChildren();
  $("places").replaceChildren();
  $("stage").dataset.layer = layer;
  $("atlas-layout").classList.toggle("visit-layout", layer !== "records");
  $("districts").style.display = layer === "visit" ? "block" : "none";
  $("map-panel").hidden = !pinMode && layer !== "records";
  $("detail").hidden = pinMode;
  $("places").hidden = layer !== "records";
  $("pin-key").hidden = !pinMode || layer !== "visit";
  $("zone-lists").hidden = layer !== "visit";
  if (pinMode) {
    renderPinMap(layer, layer === "visit" ? visits : habitats, pinPlace, expanded);
    return;
  }
  if (layer === "visit") { renderVisits(trip); return; }
  $("place-count").textContent = records ? Object.values(records).flat().length + "건" : "2개 행정시";
  for (const [i, city] of cities.entries()) {
    const count = records ? records[city.name].length + "건" : "";
    const marker = node("button", [city.name, count].filter(Boolean).join(" "), "city-marker");
    marker.style.left = city.pixel[0] / 1440 * 100 + "%";
    marker.style.top = city.pixel[1] / 910 * 100 + "%";
    marker.setAttribute("aria-label", city.name);
    const option = node("button", null, "place-option");
    option.append(node("span", i + 1, "place-number"), node("b", city.name), node("small", count));
    for (const button of [marker, option]) {
      button.setAttribute("aria-pressed", String(city === selected));
      button.onclick = () => choose("records", city.id, true);
    }
    $("markers").append(marker);
    $("places").append(option);
  }
  cityDetail(selected);
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
    id === "visit" && layer === "habitat" && selected ? visits.find((p) => p.habitat === selected.id) : null;
  if (id === "records") choose(id, cities[0].id);
  else openTrip(pinUrl(id, equivalent?.id));
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
  positionBubble();
};
function depth() {
  return trip.place ? 2 : trip.zone ? 1 : 0;
}
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
function navigate() {
  const oldDepth = depth(), oldLayer = layer, oldTrip = trip, lastPin = pinPlace;
  readSelection();
  render();
  if (pinMode) {
    const focus = $("map-bubble").querySelector(".bubble-name, .bubble-head strong") ||
      (lastPin && $("map-frame").querySelector('.map-point[data-id="' + lastPin.id + '"]')) || $("map-heading");
    focus.focus({preventScroll: true});
    return;
  }
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
  const rows = screen.querySelectorAll(".trip-card, .animal-entry");
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
  const hit = event.target.closest("[data-pin-action]");
  if (hit) {
    event.preventDefault();
    const samePin = hit.dataset.pinAction === "name" && hit.dataset.id === pinPlace?.id;
    if (hit.dataset.pinAction === "close" || (samePin && expanded)) dismissPin();
    else {
      const steps = history.state?.pinSteps;
      if (hit.dataset.pinAction === "name" && !samePin && pinPlace) {
        history.replaceState(history.state, "", hit.href);
      } else {
        const nextSteps = steps ? steps + 1 : pinPlace ? 0 : 1;
        const href = samePin ? pinUrl(layer, pinPlace.id, true) : hit.href;
        history.pushState({pinSteps: nextSteps}, "", href);
      }
      navigate();
    }
    return;
  }
  if (pinPlace && event.target.closest("#viewport")) {
    dismissPin();
    return;
  }
  const target = event.target.closest("[data-trip]");
  if (!target) return;
  event.preventDefault();
  openTrip(target.href, target.dataset.trip);
});
function dismissPin() {
  if (history.state?.pinSteps) history.go(-history.state.pinSteps);
  else {
    history.replaceState(null, "", pinUrl(layer));
    navigate();
  }
}
document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && pinPlace) {
    event.preventDefault();
    dismissPin();
  }
});
readSelection();
render();
fetchRecords();
