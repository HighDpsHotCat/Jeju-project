import { byId as $, node, icon } from "./ui.js";
import { visits, habitats, speciesIds, mapUrl, loadRecords, loadNearby, pinUrl } from "./map-data.js";

const featured = new Map([
  ["JJ-000837", ["roe_deer", "숲과 초원에 사는 동물"]],
  ["JJ-004873", ["white_eye", "눈 주변의 하얀 테가 특징"]],
  ["JJ-003382", ["swallowtail", "노란 날개와 검은 무늬"]],
  ["JJ-004729", ["dolphin", "제주 바다에서 만나는 동물"]],
]);
const examples = new Set(
  [...featured.keys()]
    .slice(0, 3)
    .concat([
      "JJ-003554",
      "JJ-002293",
      "JJ-003570",
      "JJ-003658",
      "JJ-004398",
      "JJ-003478",
      "JJ-003671",
      "JJ-002187",
      "JJ-000261",
    ]),
);
const groups = {
  Mammalia: "포유류",
  Aves: "조류",
  Insecta: "곤충",
  Amphibia: "양서류",
  Reptilia: "파충류",
};
const cache = new Map(),
  pageSize = 20;
const params = new URLSearchParams(location.search);
let requestedSpecies = params.get("species");
let group = [...$("groups").children].some(
  (b) => b.dataset.group === params.get("group"),
)
  ? params.get("group")
  : "";
let rows = [],
  results = [],
  page = 0,
  generation = 0,
  photoURL;
function stored(key) {
  try {
    return localStorage.getItem("jeju." + key);
  } catch {
    return null;
  }
}
const demo = stored("examples") === "true";
const explorer = stored("explorer") || "";
const view = params.get("view") === "yard" ? "yard" :
  (params.get("view") === "guide" || ["region","species","group","q"].some(key => params.has(key))) ? "guide" : "main";
for (const tab of ["main","guide","yard"]) $(tab).hidden = tab !== view;
$(view + "-nav").setAttribute("aria-current","page");
document.body.dataset.view = view;
if (params.get("from") === "yard") {
  $("guide-back").href = "/?view=yard";
  $("guide-back").setAttribute("aria-label","마당으로 돌아가기");
}
if (params.get("region") === "national") $("region").value = "national";
$("query").value = params.get("q") || "";
function animalId(row) {
  return row.jeju_id || row.species_id;
}
function found(row) {
  return demo && examples.has(animalId(row));
}
function animalName(row) {
  if ($("region").value === "jeju" && (!row.korean_name || row.korean_name === "[국명없음]"))
    return "국명 미확인 · " + row.species_id;
  return row.korean_name || row.scientific_name;
}
function thumb(row) {
  const item = featured.get(animalId(row));
  if (!item && !row.photo) {
    const el = node("span", null, "thumb fallback");
    el.append(icon("paw"));
    return el;
  }
  const img = node("img", null, "thumb");
  img.src = row.photo ? "/guide-photos/" + row.species_id.replace(/^KTSN-/, "") + ".jpg" : "/art/" + item[0] + ".jpg";
  if (row.photo?.unmodified) img.style.objectFit = "contain";
  img.alt = "";
  return img;
}
function badge(row) {
  const known = found(row),
    el = node("span", null, known ? "badge found" : "badge");
  el.append(icon(known ? "check" : "close"), known ? "등록 완료" : "미등록");
  return el;
}
function sheet(title, ...parts) {
  $("sheet-title").textContent = title;
  $("sheet-body").replaceChildren(...parts);
  if (!$("sheet").open) $("sheet").showModal();
}
$("close").onclick = () => $("sheet").close();
$("sheet").addEventListener("click", (e) => {
  if (e.target === $("sheet") && e.clientX !== 0) {
    const r = $("sheet").getBoundingClientRect();
    if (
      e.clientX < r.left ||
      e.clientX > r.right ||
      e.clientY < r.top ||
      e.clientY > r.bottom
    )
      $("sheet").close();
  }
});
let gbifSnapshot;
function gbifRecords(id) {
  const panel = node("details");
  if (!gbifSnapshot) {
    gbifSnapshot = fetch("/gbif.json").then(response => {
      if (!response.ok) throw Error("GBIF snapshot unavailable");
      return response.json();
    }).catch(error => { gbifSnapshot = null; throw error; });
  }
  gbifSnapshot.then(({collected, records}) => {
    const rows = records[id];
    if (!rows?.length) return;
    panel.append(node("summary", "GBIF 제주 관찰 표본 " + rows.length + "건"));
    panel.addEventListener("toggle", () => {
      if (!panel.open || panel.childElementCount > 1) return;
      panel.append(node("small", "수집일 " + collected));
      const list = node("ul");
      for (const row of rows) {
        const item = node("li"), source = node("a", row.date.split("T")[0] + " · GBIF 원문 ↗");
        source.href = "https://www.gbif.org/occurrence/" + row.id;
        const license = node("a", "이용허락");
        license.href = row.license;
        item.append(source, node("small", [row.credit, row.dataset].filter(Boolean).join(" · ")), license);
        list.append(item);
      }
      panel.append(list);
    });
    panel.hidden = false;
  }).catch(() => {});
  panel.hidden = true;
  return panel;
}
function observedPlaces(id) {
  const section = node("section", null, "observed-places");
  const status = node("small", "관찰 장소를 불러오는 중…");
  section.append(node("h3", "제주 관찰 장소"), status);
  loadNearby().then(({ places, radiusM }) => {
    const links = node("div", null, "guide-map-links");
    for (const place of visits) {
      const record = places[place.id]?.find(item => item.species === id);
      if (!record) continue;
      const link = node("a", place.name);
      link.href = pinUrl("visit", place.id, true);
      links.append(link);
    }
    status.textContent = links.childElementCount
      ? "지도 핀 반경 " + radiusM / 1000 + "km 안의 GBIF 관찰 기록"
      : "수집한 GBIF 표본에는 제주 지도 핀 주변 관찰 기록이 없어요.";
    section.append(links);
  }).catch(() => { status.textContent = "관찰 장소를 불러오지 못했어요. 도감을 다시 열어 주세요."; });
  return section;
}
function details(row) {
  const dl = node("dl");
  const entries = [
    ["학명", row.scientific_name],
    ["분류", groups[row.class] || row.class],
    ["종 ID", row.species_id],
  ];
  if ($("region").value === "jeju")
    entries.push(
      ["원자료 관찰 기록", row.source_occurrence_count + "건"],
      ["학명 대조", row.name_match_status],
      ["국명 출처", row.name_source],
    );
  else
    entries.push(
      ["출처", "2025년 국가생물종목록"],
      ["제주 종 기준표", row.jeju_id],
    );
  for (const [label, value] of entries)
    if (value) dl.append(node("dt", label), node("dd", value));
  const parts = [badge(row), observedPlaces(animalId(row)), dl];
  const image = featured.get(animalId(row));
  if (row.photo || image) {
    const img = node("img");
    img.src = row.photo ? "/guide-photos/" + row.species_id.replace(/^KTSN-/, "") + ".jpg" : "/art/" + image[0] + ".jpg";
    img.alt = animalName(row);
    if (row.photo) {
      const photo = row.photo;
      const figure = node("figure", null, "guide-photo");
      const credit = node("figcaption");
      const source = node("a", photo.title || "사진 원본");
      source.href = photo.source;
      const license = node("a", photo.license);
      license.href = photo.licenseUrl;
      credit.append(photo.note ? photo.note + " · " : "", (photo.displayCredit || photo.author) + " · ", source, " · ", license,
        photo.unmodified ? " · 원본 그대로 제공" : " · 도감용 크기 조정. 원본과 같은 이용 조건.");
      figure.append(img, credit);
      parts.unshift(figure);
    } else {
      parts.unshift(img);
    }
  }
  const mapLinks = node("div", null, "guide-map-links");
  function addLink(label, url) {
    const a = node("a", label);
    a.href = url;
    mapLinks.append(a);
  }
  const id = animalId(row);
  for (const [layer, label, entries] of [["visit", "탐방", visits], ["habitat", "서식", habitats]]) {
    for (const place of entries) {
      if (place.species.some((name) => [id, row.species_id].includes(speciesIds[name])))
        addLink(label + " · " + place.name, mapUrl(layer, place.id));
    }
  }
  loadRecords().then((regions) => {
    for (const [name, records] of Object.entries(regions)) {
      if (records.some((record) => record.species === id)) {
        const url = new URL(mapUrl("records", name === "제주시" ? "jeju" : "seogwipo"), location.origin);
        url.searchParams.set("species", id);
        addLink("관찰 기록 · " + name, url.href);
      }
    }
  }).catch(() => { /* The catalog is also usable when regional records are unavailable. */ });
  parts.push(gbifRecords(id), mapLinks);
  sheet(animalName(row), ...parts);
}
function counts() {
  const label = demo ? "발견한 동물 · 예시" : "발견한 동물";
  $("yard-count-label").textContent = $("count-label").textContent = label;
  $("yard-count").textContent = (demo ? examples.size : 0) + "종";
  $("guide-count").textContent = (demo ? rows.filter(found).length : 0) + "종";
}
function render() {
  const start = page * pageSize,
    animals = $("animals");
  const fragment = document.createDocumentFragment();
  for (const row of results.slice(start, start + pageSize)) {
    const button = node("button", null, "animal-row"),
      copy = node("span", null, "animal-copy");
    copy.append(
      node("b", animalName(row)),
      node(
        "small",
        featured.get(animalId(row))?.[1] ||
          ($("region").value === "jeju"
            ? [groups[row.class], "제주 관찰 기록 " + row.source_occurrence_count + "건"].filter(Boolean).join(" · ")
            : row.scientific_name),
      ),
    );
    button.append(thumb(row), copy, badge(row));
    button.onclick = () => details(row);
    fragment.append(button);
  }
  animals.replaceChildren(fragment);
  animals.scrollTop = 0;
  $("status").textContent = results.length
    ? results.length.toLocaleString("ko-KR") +
      "종 중 " +
      (start + 1) +
      "–" +
      Math.min(start + pageSize, results.length) +
      "번째"
    : "조건에 맞는 동물이 없어요.";
  $("prev").disabled = page === 0;
  $("next").disabled = start + pageSize >= results.length;
}
function apply() {
  const q = $("query").value.trim().toLowerCase();
  results = rows.filter(
    (row) =>
      (!group || row.class === group) &&
      (!q ||
        [
          row.species_id,
          row.jeju_id,
          row.korean_name,
          row.scientific_name,
        ].some((v) => v && v.toLowerCase().includes(q))),
  );
  page = 0;
  for (const b of $("groups").children)
    b.setAttribute("aria-pressed", String(b.dataset.group === group));
  const url = new URL(location.href);
  for (const [key, value] of [
    ["q", $("query").value],
    ["group", group],
    ["region", $("region").value === "national" ? "national" : ""],
  ]) {
    if (value) url.searchParams.set(key, value);
    else url.searchParams.delete(key);
  }
  history.replaceState(null, "", url);
  render();
}
async function load() {
  const scope = $("region").value,
    version = ++generation;
  rows = [];
  results = [];
  render();
  counts();
  $("status").textContent = "도감을 불러오는 중…";
  try {
    if (!cache.has(scope)) {
      const response = await fetch(
        scope === "jeju" ? "/species.json" : "/national.json",
      );
      if (!response.ok) throw Error();
      const data = await response.json();
      const catalog =
        scope === "jeju"
          ? data.filter((r) => r.in_guide !== false && Number(r.source_occurrence_count) > 0)
          : data.map(
              ([ktsn, korean_name, scientific_name, taxon, jeju_id, photo]) => ({
                species_id: "KTSN-" + ktsn,
                korean_name,
                scientific_name,
                class: taxon,
                jeju_id,
                photo,
              }),
            );
      cache.set(scope, catalog);
    }
    if (version !== generation) return;
    rows = cache.get(scope);
    const mammals = $("groups").querySelector('[data-group="Mammalia"]');
    mammals.textContent = "포유류 " + rows.filter(row => row.class === "Mammalia").length + "종";
    apply();
    counts();
    if (requestedSpecies) {
      const row = rows.find((item) => item.species_id === requestedSpecies || item.jeju_id === requestedSpecies);
      requestedSpecies = null;
      if (row) details(row);
    }
  } catch {
    if (version !== generation) return;
    const retry = node("button", "다시 불러오기", "pill");
    retry.onclick = load;
    $("status").replaceChildren("도감을 불러오지 못했어요. ", retry);
  }
}
$("query").oninput = apply;
document.querySelector(".search").onsubmit = (e) => {
  e.preventDefault();
  apply();
};
$("groups").onclick = (e) => {
  const b = e.target.closest("button");
  if (b) {
    group = b.dataset.group;
    apply();
  }
};
$("region").onchange = load;
function turn(step) {
  page += step;
  render();
  if (matchMedia("(max-width:740px)").matches)
    $("guide-title").scrollIntoView({ block: "start" });
}
$("prev").onclick = () => turn(-1);
$("next").onclick = () => turn(1);
async function showCredits() {
  const content = node("div");
  content.textContent = "사진 출처를 불러오는 중…";
  sheet("저작권 및 출처", content);
  try {
    const response = await fetch("/photo-credits.json");
    if (!response.ok) throw Error();
    const photos = Object.values(await response.json());
    photos.sort((a, b) => a.name.localeCompare(b.name, "ko"));
    const list = node("ul", null, "credit-list");
    for (const photo of photos) {
      const item = node("li");
      const source = node("a", photo.name);
      source.href = photo.source;
      const license = node("a", photo.license);
      license.href = photo.licenseUrl;
      item.append(source, " · " + (photo.displayCredit || photo.author) + " · ", license);
      list.append(item);
    }
    content.replaceChildren(list);
  } catch {
    const retry = node("button", "다시 불러오기", "pill");
    retry.onclick = showCredits;
    content.replaceChildren("사진 출처를 불러오지 못했어요. ", retry);
  }
}
document.querySelectorAll(".credits-button").forEach(button => button.onclick = showCredits);
function updateCharacter() {
  document.querySelectorAll(".greeting").forEach(el => {
    el.textContent = explorer ? explorer + "님, 어떤 동물을 만날까요?" : "오늘은 어떤 동물을 만날까요?";
  });
}
$("camera").onclick = () => {
  $("capture").value = "";
  $("capture").click();
};
$("capture").onchange = () => {
  const file = $("capture").files[0];
  if (!file) return;
  if (photoURL) URL.revokeObjectURL(photoURL);
  photoURL = URL.createObjectURL(file);
  const img = node("img");
  img.src = photoURL;
  img.alt = "선택한 관찰 사진";
  img.onerror = () =>
    sheet("관찰 사진", node("p", "이 사진 형식은 미리 볼 수 없어요."));
  sheet(
    "관찰 사진",
    img,
    node(
      "small",
      "이 기기에서만 미리 봅니다. 도감 등록은 앱의 확인된 관찰 기록을 사용합니다.",
    ),
  );
};
$("sheet").addEventListener("close", () => {
  const url = new URL(location.href);
  if (url.searchParams.has("species")) {
    url.searchParams.delete("species");
    history.replaceState(null, "", url);
  }
  if (photoURL) {
    URL.revokeObjectURL(photoURL);
    photoURL = null;
  }
});
updateCharacter();
counts();
if (view === "guide") load();
