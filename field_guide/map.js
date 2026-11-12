import { byId as $, node, icon } from "./ui.js";

const places = [
  {
    id: "hado",
    name: "하도리 철새도래지",
    type: "야생 조류",
    area: "제주시 구좌읍 · 해안습지",
    point: [126.896255, 33.51021],
    pixel: [1288.4, 202.9],
    species: [
      ["저어새", "jeju"],
      ["청둥오리", "jeju"],
    ],
    why: "바닷물과 민물이 만나는 해안습지입니다. 겨울에는 물새가 갈대 가장자리와 모래밭에서 먹이를 찾고 쉬는 모습을 관찰할 수 있습니다.",
    grade: "대표 지점 1등급",
    near: "반경 500m에는 1·2등급 구역이 있습니다.",
    visit: "겨울철에 쌍안경을 챙겨 공개된 관찰 구역에서 물가를 살펴보세요.",
    source:
      "https://www.visitjeju.net/kr/detail/view?contentsid=CNTS_000000000022753",
    extra:
      "https://www.visitjeju.net/kr/detail/view?contentsid=CONT_000000000500610",
  },
  {
    id: "park",
    name: "제주자연생태공원",
    type: "보호·사육",
    area: "서귀포시 성산읍 · 금백조로 446",
    point: [126.823609, 33.43972902],
    pixel: [1165.4, 344.8],
    species: [
      ["노루", "jeju"],
      ["독수리", "jeju"],
      ["반달가슴곰", "national"],
    ],
    why: "제주특별자치도 무장애여행 안내서에 맹금류와 노루, 반달가슴곰을 보호·사육하는 관찰장이 소개되어 있습니다.",
    grade: "주출입구 등급 도형 없음",
    near: "입구 반경 500m에는 2·3등급 구역이 있습니다. 가장 가까운 2등급 경계까지는 약 167m입니다.",
    visit:
      "맹금류 관찰장 앞에는 잔디가 섞여 있고 전망대 방향에는 약 9° 오르막이 있습니다. 당일 관람 가능한 동물은 공원(064-787-4711)에 확인하세요.",
    source: "https://jejudatahub.net/data/view/data/740",
  },
  {
    id: "oreum",
    name: "물영아리오름",
    type: "야생 습지동물",
    area: "서귀포시 남원읍 · 산정 습지",
    point: [126.7167428, 33.3275795],
    pixel: [984.1, 570.6],
    species: [
      ["맹꽁이", "jeju"],
      ["물장군", "national"],
    ],
    why: "분화구 습지에 맹꽁이와 물장군의 서식 기록이 있습니다. 계절과 습지의 물 상태에 따라 관찰할 수 있는 생물이 달라집니다.",
    grade: "관광 대표 지점 등급 도형 없음",
    near: "대표 지점 반경 500m에는 2등급 구역이 있습니다. 표시 지점은 관광 안내 위치입니다.",
    visit: "지정 탐방로와 관찰 데크에서 습지를 살펴보세요.",
    source:
      "https://m.visitjeju.net/kr/detail/view?contentsid=CNTS_000000000021614",
  },
];
let filter = "전체";
let selected =
  places.find(
    (p) => p.id === new URL(location.href).searchParams.get("place"),
  ) || places[0];
function link(label, url, cls) {
  const a = node("a", label, cls);
  a.href = url;
  return a;
}
function block(title, content, cls = "detail-section") {
  const div = node("section", null, cls);
  div.append(node("h3", title), ...content);
  return div;
}
function render() {
  const p = selected,
    shown = places.filter((item) => filter === "전체" || item.type === filter);
  $("place-count").textContent = shown.length + "곳";
  for (const b of $("filters").children)
    b.setAttribute("aria-pressed", String(b.textContent === filter));
  for (const b of document.querySelectorAll("[data-place]")) {
    b.hidden = !shown.some((item) => item.id === b.dataset.place);
    b.setAttribute("aria-pressed", String(b.dataset.place === p.id));
  }
  const tag = node("span", p.type, p.id === "park" ? "tag managed" : "tag");
  const address = node("p", null, "area");
  address.append(icon("pin"), p.area);
  const species = node("div", null, "animals-here");
  for (const [name, scope] of p.species) {
    const params = new URLSearchParams({ q: name, region: scope });
    const a = link(name + " ↗", "/?" + params);
    a.setAttribute(
      "aria-label",
      name + " " + (scope === "national" ? "전국" : "제주") + " 도감 열기",
    );
    species.append(a);
  }
  const actions = node("div", null, "actions");
  actions.append(
    link(
      "길찾기 ↗",
      "https://map.kakao.com/link/to/" +
        encodeURIComponent(p.name) +
        "," +
        p.point[1] +
        "," +
        p.point[0],
    ),
    link("장소 자료 ↗", p.source),
  );
  if (p.extra) actions.append(link("철새 기록 ↗", p.extra));
  $("detail").replaceChildren(
    tag,
    node("h2", p.name),
    address,
    block("관찰할 수 있는 동물", [species, node("p", p.why)]),
    block(
      "생태·자연도",
      [node("strong", p.grade), node("p", p.near)],
      "grade-box",
    ),
    block("현장 관찰 안내", [node("p", p.visit)]),
    actions,
  );
}
function select(place, scroll = false) {
  selected = place;
  const url = new URL(location.href);
  url.searchParams.set("place", place.id);
  history.replaceState(null, "", url);
  render();
  if (scroll && matchMedia("(max-width:820px)").matches)
    $("detail").scrollIntoView({ block: "start" });
}
for (const type of ["전체", ...places.map((p) => p.type)]) {
  const b = node("button", type);
  b.onclick = () => {
    filter = type;
    select(type === "전체" ? selected : places.find((p) => p.type === type));
  };
  $("filters").append(b);
}
for (const [index, p] of places.entries()) {
  const pin = node("button", null, "pin");
  pin.dataset.place = p.id;
  pin.setAttribute("aria-label", p.name);
  pin.style.left = (p.pixel[0] / 1440) * 100 + "%";
  pin.style.top = (p.pixel[1] / 910) * 100 + "%";
  pin.append(node("span", index + 1));
  pin.onclick = () => select(p, true);
  $("stage").append(pin);
  const b = node("button", null, "place-option");
  b.dataset.place = p.id;
  b.append(
    node("span", index + 1, "place-number"),
    node("b", p.name),
    node("small", p.type),
  );
  b.onclick = () => select(p, true);
  $("places").append(b);
}
$("zoom").onclick = () => {
  const active = $("stage").classList.toggle("zoomed");
  $("zoom").setAttribute("aria-pressed", String(active));
  $("zoom").setAttribute("aria-label", active ? "지도 축소" : "지도 확대");
  $("zoom").textContent = active ? "− 축소" : "＋ 확대";
  const view = $("viewport"),
    stage = $("stage");
  view.scrollTo(
    active
      ? (stage.offsetWidth * selected.pixel[0]) / 1440 - view.clientWidth / 2
      : 0,
    active
      ? (stage.offsetHeight * selected.pixel[1]) / 910 - view.clientHeight / 2
      : 0,
  );
};
render();
