import { byId as $, node, icon } from "./ui.js";
import { visits, habitats, speciesIds, guideUrl, mapUrl } from "./map-data.js";

const zones = [
  { id: "jeju", name: "제주시", cover: "dongbaek", pixel: [720, 255], sample: "한라수목원 · 동백동산" },
  { id: "seogwipo", name: "서귀포시", cover: "cheonjiyeon", pixel: [740, 651], sample: "천지연폭포 · 제주곶자왈도립공원" },
];
const kinds = { all: "전체", sight: "관광지", park: "생태공원·수목원" };
const photoCredits = {
  arboretum: ["한라수목원 야간 안내 표지", "Mar del Este", "Arboretum_of_Halla1.jpg", "by", "3.0"],
  cheonjiyeon: ["천지연폭포와 계곡", "Jaeyeong951", "Jeju_Cheonjiyeon_Waterfall.jpg", "by-sa", "3.0"],
  gotjawal: ["제주곶자왈도립공원의 숲", "Grapesurgeon", "Jeju_Gotjawal_Provincial_Park_03.jpg", "by-sa", "4.0"],
  dongbaek: ["동백동산습지 수면에 비친 숲", "Aspere", "Jeju_Dongbaek_Dongsan_Wetland.jpg", "zero", "1.0"],
  highland: ["눈이 내린 1100고지습지", "Aleksandr Tsarev", "1100고지습지.jpg", "by-sa", "3.0"],
};
const licenseUrl = (code, version) => "https://creativecommons.org/" +
  (code === "zero" ? "publicdomain/zero" : "licenses/" + code) + "/" + version + "/";
function link(label, href, cls) {
  const a = node("a", label, cls);
  a.href = href;
  return a;
}
function photo(id, decorative = false) {
  const image = node("img", null, "location-photo");
  image.src = "/art/visit_" + id + ".jpg";
  image.alt = decorative ? "" : photoCredits[id][0];
  image.dataset.site = id;
  return image;
}
function credit(id) {
  const [title, author, file, code, version] = photoCredits[id];
  const line = node("span", null, "photo-credit");
  line.append(link(title + " · " + author, "https://commons.wikimedia.org/wiki/File:" + encodeURIComponent(file)), " · ",
    link((code === "zero" ? "CC0" : "CC " + code.toUpperCase()) + " " + version, licenseUrl(code, version)), " · 크기 조정");
  return line;
}
function tripUrl(zone, place, kind = "all") {
  const params = new URLSearchParams({layer: "visit"});
  if (zone) params.set("zone", zone);
  if (place) params.set("place", place);
  if (kind !== "all") params.set("kind", kind);
  return "/map?" + params;
}
export function readVisit(params) {
  const place = visits.find((p) => p.id === params.get("place"));
  const zone = zones.find((z) => z.id === (place?.zone || params.get("zone")));
  const kind = Object.hasOwn(kinds, params.get("kind")) ? params.get("kind") : "all";
  return {zone, place, kind: place && kind !== place.kind ? "all" : kind};
}
function inZone(zone) { return visits.filter((p) => p.zone === zone.id); }
function route(label, href, cls) {
  const a = link(label, href, cls);
  a.dataset.trip = "detail";
  return a;
}
function position(marker, coords) {
  marker.style.left = coords[0] / 1440 * 100 + "%";
  marker.style.top = coords[1] / 910 * 100 + "%";
}
function thumbnail(place) {
  if (photoCredits[place.id]) return photo(place.id, true);
  const tile = node("span", null, "mini-map");
  const dot = node("i", null, "mini-dot");
  position(dot, place.pixel);
  tile.setAttribute("aria-hidden", "true");
  tile.append(dot);
  return tile;
}
function renderOverview(state) {
  const picker = $("region-picker");
  picker.replaceChildren(node("h2", "여행 지역"));
  for (const zone of zones) {
    const a = route(null, tripUrl(zone.id), "zone-choice");
    if (zone === state.zone) a.setAttribute("aria-current", "true");
    a.append(photo(zone.cover, true));
    const copy = node("span", null, "zone-copy");
    copy.append(node("strong", zone.name), node("small", zone.sample));
    a.append(copy, node("span", inZone(zone).length + "곳 ›", "zone-count"));
    picker.append(a);
  }
  const details = node("details", null, "cover-credits");
  details.append(node("summary", "지역 사진 출처"));
  for (const z of zones) details.append(credit(z.cover));
  picker.append(details);
  for (const path of $("districts").querySelectorAll("path")) {
    path.classList.toggle("current-zone", path.dataset.zone === state.zone?.id);
    path.dataset.tripZone = path.dataset.zone;
  }
  if (!state.zone) {
    for (const p of visits) {
      const dot = route(null, tripUrl(p.zone), "overview-point");
      dot.title = p.name;
      dot.setAttribute("aria-label", p.name + " 지역 관광지 보기");
      dot.tabIndex = -1; // The two region links provide the same keyboard actions.
      position(dot, p.pixel);
      $("markers").append(dot);
    }
    for (const zone of zones) {
      const a = route(zone.name + "  ›", tripUrl(zone.id), "district-label");
      position(a, zone.pixel);
      $("markers").append(a);
    }
    return;
  }
  const visible = inZone(state.zone).filter((p) => state.kind === "all" || p.kind === state.kind);
  for (const [i, p] of visible.entries()) {
    const marker = route(null, tripUrl(state.zone.id, p.id, state.kind), "pin visit-pin");
    marker.setAttribute("aria-label", p.name);
    marker.setAttribute("aria-pressed", String(state.place === p));
    if (p.pixel[0] < 350) marker.classList.add("label-right");
    position(marker, p.pixel);
    marker.append(node("span", i + 1));
    $("markers").append(marker);
  }
}
function regionList({zone, kind}) {
  const places = inZone(zone);
  const visible = places.filter((p) => kind === "all" || p.kind === kind);
  const head = node("div", null, "trip-heading");
  head.append(node("h2", zone.name + " 탐방 장소"), node("span", visible.length + "곳"));
  const filters = node("div", null, "kind-tabs");
  filters.setAttribute("aria-label", "탐방 장소 유형");
  for (const [value, label] of Object.entries(kinds)) {
    const a = route(label, tripUrl(zone.id, null, value), "kind-tab");
    if (kind === value) a.setAttribute("aria-current", "true");
    filters.append(a);
  }
  const grid = node("div", null, "trip-grid");
  for (const [i, place] of visible.entries()) {
    const card = route(null, tripUrl(zone.id, place.id, kind), "trip-card");
    const copy = node("span", null, "trip-copy");
    copy.append(node("small", (i + 1) + " · " + place.type), node("strong", place.name),
      node("span", place.description));
    card.append(thumbnail(place), copy, node("span", "›", "trip-arrow"));
    grid.append(card);
  }
  if (!visible.length) grid.append(node("p", "이 지역에는 등록된 장소가 없습니다."));
  const parts = [head, filters, grid];
  const photos = visible.filter((p) => photoCredits[p.id]);
  if (photos.length) {
    const credits = node("details", null, "cover-credits");
    credits.append(node("summary", "관광지 사진 출처"));
    for (const p of photos) credits.append(credit(p.id));
    parts.push(credits);
  }
  $("detail").replaceChildren(...parts);
}
function direction(place) {
  const url = place.search ? "https://map.kakao.com/?" + new URLSearchParams({q: place.search}) :
    "https://map.kakao.com/link/to/" + encodeURIComponent(place.destinationName || place.name) + "," + place.destination.join(",");
  return link(place.search ? place.search + " 찾기 ↗" : "탐방 출발점 길찾기 ↗", url);
}
function wildlife(place) {
  const region = habitats.find((h) => h.id === place.habitat);
  const managed = place.id === "park";
  const el = node("section", null, "wildlife-block");
  el.append(node("h3", managed ? "보호·사육 관찰장의 동물" : "이곳에서 관찰된 동물과 서식종"));
  const grid = node("div", null, "animal-grid");
  for (const name of place.species) {
    const [type, note, source] = place.evidence?.[name] || (managed
      ? ["보호·사육", "제주도 안내서에 실린 관찰장 동물입니다. 당일 관람 여부는 공원에 확인하세요.", place.sources[0][1]]
      : ["지역 서식 자료", region.description, region.sources[0][1]]);
    const card = node("section", null, "wildlife-card");
    const animal = link(null, guideUrl(speciesIds[name], name), "animal-entry");
    animal.setAttribute("aria-label", name + " 동물 도감 열기");
    const art = {노루:"roe_deer", 남방큰돌고래:"dolphin", 동박새:"white_eye"}[name];
    const mark = node("span", null, "animal-mark");
    if (art) {
      const img = node("img"); img.src = "/art/" + art + ".jpg"; img.alt = ""; mark.append(img);
    } else mark.append(icon("book"));
    const header = node("span");
    header.append(node("small", type), node("strong", name));
    animal.append(mark, header, node("span", "도감 ›", "guide-arrow"));
    card.append(animal, node("p", note), link("근거 자료 ↗", source, "evidence-link"));
    grid.append(card);
  }
  el.append(grid);
  return el;
}
function renderPlace(state) {
  const {place:p, zone, kind} = state;
  const back = route("‹ " + zone.name + " 장소 목록", tripUrl(zone.id, null, kind), "trip-back");
  const overview = node("div", null, "trip-overview");
  const title = node("div", null, "trip-description");
  title.append(node("small", kinds[p.kind] + " · " + p.type), node("h2", p.name), node("p", p.area, "trip-address"),
    node("p", p.description), node("p", p.access, "visit-access"));
  const actions = node("div", null, "actions");
  actions.append(direction(p));
  if (p.habitat) actions.append(link("서식 기록 보기", mapUrl("habitat", p.habitat)));
  title.append(actions);
  if (photoCredits[p.id]) {
    const figure = node("figure", null, "place-photo");
    figure.append(photo(p.id), credit(p.id));
    overview.append(figure);
  }
  overview.append(title);
  const sources = node("details", null, "place-sources");
  sources.append(node("summary", "장소 정보와 출처"));
  if (p.csvName) sources.append(node("p", "제주관광공사 여행장소 CSV 대조 명칭 · " + p.csvName),
    link("VISIT JEJU 여행장소 자료 ↗", "https://www.data.go.kr/data/15049995/fileData.do"));
  for (const [label,url] of p.sources) sources.append(link(label + " ↗", url));
  const parts = [back, overview, wildlife(p)];
  if ($("grades").checked && p.grade) parts.push(node("p", p.grade, "grade-box"));
  parts.push(sources);
  $("detail").replaceChildren(...parts);
}
export function renderVisits(state) {
  $("place-count").textContent = visits.length + "곳";
  renderOverview(state);
  $("detail").hidden = !state.zone;
  if (state.place) renderPlace(state);
  else if (state.zone) regionList(state);
}
