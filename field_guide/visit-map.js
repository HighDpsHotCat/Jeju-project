import { byId as $, node, icon } from "./ui.js";
import { visits, habitats, speciesIds, guideUrl, mapUrl } from "./map-data.js";
import { pinUrl } from "./pin-map.js";

const zones = [
  { id: "jeju", name: "제주시" },
  { id: "seogwipo", name: "서귀포시" },
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
  a.dataset.trip = "open";
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
function screenHeader(title, backLabel, backUrl) {
  const head = node("header", null, "trip-header");
  const back = route("‹", backUrl, "trip-back");
  back.dataset.trip = "back";
  back.setAttribute("aria-label", backLabel);
  back.title = backLabel;
  const h2 = node("h2", title);
  h2.tabIndex = -1;
  head.append(back, h2);
  return head;
}
function regionList({zone, kind}) {
  const places = inZone(zone);
  const visible = places.filter((p) => kind === "all" || p.kind === kind);
  const head = screenHeader(zone.name + " 관광지", "지역 선택으로 돌아가기", "/map");
  const filter = node("select", null, "kind-select");
  filter.setAttribute("aria-label", "관광지 유형");
  for (const [value, label] of Object.entries(kinds)) {
    filter.append(new Option(label, value));
  }
  filter.value = kind;
  filter.dataset.kind = kind;
  head.append(filter);
  const body = node("div", null, "trip-scroll");
  const grid = node("div", null, "trip-list");
  for (const p of visible) {
    const card = route(null, tripUrl(zone.id, p.id, kind), "trip-card");
    card.dataset.returnTo = p.id;
    const copy = node("span", null, "trip-copy");
    copy.append(node("strong", p.name), node("small", p.type + " · 동물 " + p.species.length + "종"));
    card.append(thumbnail(p), copy, node("span", "›", "trip-arrow"));
    grid.append(card);
  }
  body.append(grid);
  if (!visible.length) body.append(node("p", "이 유형으로 등록된 관광지가 없습니다."));
  const photos = visible.filter((p) => photoCredits[p.id]);
  if (photos.length) {
    const credits = node("details", null, "cover-credits");
    credits.append(node("summary", "사진 출처"));
    for (const p of photos) credits.append(credit(p.id));
    body.append(credits);
  }
  $("detail").replaceChildren(head, body);
  body.scrollTop = history.state?.tripScroll || 0;
}
function direction(place) {
  const url = place.search ? "https://map.kakao.com/?" + new URLSearchParams({q: place.search}) :
    "https://map.kakao.com/link/to/" + encodeURIComponent(place.destinationName || place.name) + "," + place.destination.join(",");
  return link(place.search ? place.search + " 찾기 ↗" : "탐방 출발점 길찾기 ↗", url);
}
function renderPlace({place}) {
  const head = screenHeader(place.name, "지도 말풍선으로 돌아가기", pinUrl("visit", place.id, true));
  const body = node("div", null, "trip-scroll");
  const managed = place.id === "park";
  const habitat = habitats.find((h) => h.id === place.habitat);
  const label = managed ? "보호·사육 동물" : "관찰·서식 동물";
  const heading = node("h3", label + " " + place.species.length + "종", "animals-title");
  const grid = node("div", null, "animal-list");
  const evidence = node("details", null, "trip-disclosure");
  evidence.append(node("summary", "동물 기록과 출처"));
  for (const name of place.species) {
    const [type, note, source] = place.evidence?.[name] || (managed
      ? ["보호·사육", "제주도 안내서에 실린 관찰장 동물입니다. 당일 관람 여부는 공원에 확인하세요.", place.sources[0][1]]
      : ["지역 서식 자료", habitat.description, habitat.sources[0][1]]);
    const a = link(null, guideUrl(speciesIds[name], name), "animal-entry");
    a.setAttribute("aria-label", name + " 동물 도감 열기");
    const art = {노루:"roe_deer", 남방큰돌고래:"dolphin", 동박새:"white_eye"}[name];
    const mark = node("span", null, "animal-mark");
    if (art) {
      const img = node("img"); img.src = "/art/" + art + ".jpg"; img.alt = ""; mark.append(img);
    } else mark.append(icon("paw"));
    const copy = node("span", null, "trip-copy");
    copy.append(node("strong", name), node("small", type));
    a.append(mark, copy, node("span", "도감 ›", "guide-arrow"));
    grid.append(a);
    const item = node("div", null, "evidence-item");
    item.append(node("b", name), node("p", note), link("원자료 ↗", source));
    evidence.append(item);
  }
  const info = node("details", null, "trip-disclosure");
  info.append(node("summary", "장소 안내·길찾기"), node("p", place.area, "trip-address"),
    node("p", place.description), node("p", place.access));
  const actions = node("div", null, "actions");
  actions.append(direction(place));
  if (place.habitat) actions.append(link("서식 기록 보기", mapUrl("habitat", place.habitat)));
  info.append(actions);
  if ($("grades").checked && place.grade) info.append(node("p", place.grade, "grade-box"));
  const sources = node("div", null, "source-links");
  if (place.csvName) sources.append(link("VISIT JEJU 여행장소 · " + place.csvName + " ↗", "https://www.data.go.kr/data/15049995/fileData.do"));
  for (const [label,url] of place.sources) sources.append(link(label + " ↗", url));
  info.append(sources);
  body.append(heading, grid, evidence, info);
  $("detail").replaceChildren(head, body);
}
export function renderVisits(state) {
  $("map-panel").hidden = Boolean(state.zone);
  $("detail").hidden = !state.zone;
  if (state.place) renderPlace(state);
  else regionList(state);
}
export function filterUrl(state, kind) {
  return tripUrl(state.zone.id, null, kind);
}
