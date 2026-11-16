import { byId as $, node } from "./ui.js";
import { visits, loadNearby } from "./map-data.js";

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
export function renderVisits({zone, kind}) {
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
    const summary = node("small", p.type);
    copy.append(node("strong", p.name), summary);
    loadNearby().then(({places}) => {
      if (places[p.id]) summary.textContent = p.type + " · 주변 관찰 " + places[p.id].length + "종";
    }).catch(() => { /* Keep the place type available offline. */ });
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
export function filterUrl(state, kind) {
  return tripUrl(state.zone.id, null, kind);
}
