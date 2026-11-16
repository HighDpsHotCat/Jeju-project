import { analysisLink } from "./observation-analysis.js";
import { byId as $, node } from "./ui.js";
import { guideUrl, speciesIds, pinUrl, loadNearby } from "./map-data.js";

const frame = $("map-frame"), bubble = $("map-bubble");
let currentMarker = null;

function nearbyAnimals(place) {
  const block = node("section", null, "nearby-records");
  block.append(node("p", "주변 GBIF 관찰 기록을 불러오는 중…", "bubble-count"));
  loadNearby().then(({places, radiusM, collected}) => {
    const rows = places[place.id];
    if (!rows) throw Error("Missing place");
    block.replaceChildren(node("p", "반경 " + radiusM / 1000 + "km · GBIF 관찰 " + rows.length + "종", "bubble-count"));
    if (!rows.length) block.append(node("small", "수집 표본에 일치하는 관찰이 없습니다."));
    const list = node("ul", null, "bubble-animals");
    const params = new URLSearchParams(location.search);
    const match = params.get("pin") === place.id && rows.find(r => r.species === params.get("species"));
    const ordered = match ? [match, ...rows.filter(row => row !== match)] : rows;
    for (const r of ordered) {
      const li = node("li"), guide = node("a", r.name + " ›");
      guide.href = guideUrl(r.species, r.name);
      const source = node("a", r.date.split("T")[0] + " · " + r.metres.toLocaleString("ko-KR") + "m · GBIF ↗");
      source.href = "https://www.gbif.org/occurrence/" + r.id;
      source.title = [r.credit, r.dataset, r.license].filter(Boolean).join(" · ");
      source.className = "nearby-source";
      const analysis = analysisLink(r.species, "제주 전체 목격 분석");
      analysis.className = "pin-analysis";
      li.append(guide, analysis, source);
      list.append(li);
    }
    block.append(list, node("small", "표본 수집 " + collected));
    positionBubble();
  }).catch(() => { block.replaceChildren(node("small", "관찰 기록을 불러오지 못했습니다. 핀을 다시 열어주세요.")); });
  return block;
}

function action(label, href, cls, name) {
  const a = node("a", label, cls);
  a.href = href;
  a.dataset.pinAction = name;
  return a;
}
function keepInside(value, min, max) {
  return Math.max(min, Math.min(value, max));
}
export function positionBubble() {
  if (!currentMarker || bubble.hidden) return;
  const region = frame.getBoundingClientRect();
  const mark = currentMarker.getBoundingClientRect();
  const x = mark.left - region.left + mark.width / 2;
  const y = mark.top - region.top + mark.height / 2;
  const visible = x >= 0 && x <= region.width && y >= 0 && y <= region.height;
  bubble.style.visibility = visible ? "visible" : "hidden";
  if (!visible) return;
  const pad = 7, gap = 6, w = bubble.offsetWidth;
  const content = bubble.querySelector(".bubble-content");
  const chrome = bubble.offsetHeight - (content?.offsetHeight || 0);
  const naturalHeight = chrome + Math.min(content?.scrollHeight || 0, 180);
  const above = mark.top - region.top - gap;
  const below = region.bottom - mark.bottom - gap;
  const roomLeft = mark.left - region.left - gap, roomRight = region.right - mark.right - gap;
  const horizontal = (roomLeft >= w + pad || roomRight >= w + pad) &&
    (!content || Math.max(above, below) < naturalHeight + pad);
  if (horizontal) {
    const leftSide = roomLeft >= w + pad;
    if (content) bubble.style.setProperty("--content-limit", Math.max(35, Math.min(180, region.height - 2 * pad - chrome)) + "px");
    const h = bubble.offsetHeight;
    const top = keepInside(y - h / 2, pad, region.height - h - pad);
    bubble.style.left = (leftSide ? roomLeft - w : mark.right - region.left + gap) + "px";
    bubble.style.top = top + "px";
    bubble.style.setProperty("--tail-y", keepInside(y - top, 11, h - 11) + "px");
    bubble.dataset.side = leftSide ? "left" : "right";
    return;
  }
  const up = above >= naturalHeight + pad || (below < naturalHeight + pad && above >= below);
  if (content) bubble.style.setProperty("--content-limit", Math.max(35, Math.min(180, (up ? above : below) - chrome - pad)) + "px");
  const h = bubble.offsetHeight;
  const left = keepInside(x - w / 2, pad, region.width - w - pad);
  const top = keepInside(up ? above - h : mark.bottom - region.top + gap, pad, region.height - h - pad);
  bubble.style.left = left + "px";
  bubble.style.top = top + "px";
  bubble.style.setProperty("--tail-x", keepInside(x - left, 12, w - 12) + "px");
  bubble.dataset.side = up ? "above" : "below";
}
new ResizeObserver(positionBubble).observe(frame);
new ResizeObserver(positionBubble).observe(bubble);
$("viewport").addEventListener("scroll", positionBubble, {passive:true});

export function clearPinMap() {
  currentMarker = null;
  bubble.hidden = true;
  bubble.replaceChildren();
}
// Compare marker footprints in screen pixels so gaps grow naturally as the map zooms.
function groupMarkers(markers, selected, layer, level, zoomTo, maxLevel) {
  const rect = $("stage").getBoundingClientRect();
  const groups = markers.filter(item => item.place !== selected).map(item => {
    const box = item.marker.getBoundingClientRect();
    return {items:[item], x:box.left + box.width / 2 - rect.left,
      y:box.top + box.height / 2 - rect.top, width:box.width, height:box.height};
  });
  while (true) {
    let pair, nearest = Infinity;
    for (let i = 0; i < groups.length; i++) {
      for (let j = i + 1; j < groups.length; j++) {
        const a = groups[i], b = groups[j];
        const dx = Math.abs(a.x - b.x), dy = Math.abs(a.y - b.y);
        const distance = dx * dx + dy * dy;
        if (dx < (a.width + b.width) / 2 + 6 && dy < (a.height + b.height) / 2 + 6 && distance < nearest) {
          nearest = distance;
          pair = [i,j];
        }
      }
    }
    if (!pair) break;
    const a = groups[pair[0]], b = groups.splice(pair[1], 1)[0];
    const n = a.items.length, m = b.items.length;
    a.x = (a.x*n + b.x*m) / (n+m);
    a.y = (a.y*n + b.y*m) / (n+m);
    a.items.push(...b.items);
    a.width = a.height = 40;
  }
  for (const group of groups) {
    if (group.items.length < 2) continue;
    const places = group.items.map(({place, marker}) => { marker.remove(); return place; });
    const count = places.length;
    const cluster = node("button", String(count), "place-cluster");
    cluster.setAttribute("aria-label", "가까운 장소 " + count + "곳 " + (level < maxLevel ? "확대" : "보기"));
    cluster.style.left = group.x / rect.width * 100 + "%";
    cluster.style.top = group.y / rect.height * 100 + "%";
    cluster.onclick = event => {
      event.stopPropagation();
      if (level < maxLevel) {
        zoomTo(level + 1, [group.x / rect.width * 1440, group.y / rect.height * 910]);
        return;
      }
      clearPinMap();
      currentMarker = cluster;
      bubble.dataset.view = "animals";
      bubble.setAttribute("aria-label", "가까운 장소 선택");
      const head = node("div", null, "bubble-head"), close = node("button", "×", "bubble-close");
      close.setAttribute("aria-label", "말풍선 닫기");
      close.onclick = () => { clearPinMap(); cluster.focus({preventScroll:true}); };
      head.append(node("strong", "가까운 장소 " + count + "곳"), close);
      const content = node("div", null, "bubble-content"), list = node("ul", null, "bubble-animals");
      for (const place of places) {
        const li = node("li");
        li.append(action(place.name, pinUrl(layer, place.id, true), "", "animals"));
        list.append(li);
      }
      for (const part of [head, content]) part.addEventListener("keydown", event => {
        if (event.key === "Escape") { event.stopPropagation(); close.click(); }
      });
      content.append(list);
      bubble.replaceChildren(head, content);
      bubble.hidden = false;
      positionBubble();
      close.focus({preventScroll:true});
    };
    $("markers").append(cluster);
  }
}
export function renderPinMap(layer, entries, selected, expanded, level, zoomTo, maxLevel) {
  $("place-count").textContent = entries.length + "곳";
  const markers = [];
  for (const [index,p] of entries.entries()) {
    const marker = action(null, pinUrl(layer, p.id, layer === "visit"), "map-point", layer === "visit" ? "animals" : "name");
    marker.dataset.id = p.id;
    marker.dataset.kind = p.kind || "habitat";
    marker.setAttribute("aria-label", (index + 1) + "번 " + p.name);
    marker.setAttribute("aria-expanded", String(p === selected));
    marker.setAttribute("aria-controls", "map-bubble");
    if (p.offshore) {
      marker.classList.add("island-point");
      marker.append(node("span", index + 1), node("small", "추자군도"));
      $("offshore").append(marker);
    } else {
      marker.style.left = "clamp(15px," + p.pixel[0] / 1440 * 100 + "%,calc(100% - 15px))";
      marker.style.top = p.pixel[1] / 910 * 100 + "%";
      marker.classList.add("place-label");
      marker.append(node("b", p.name));
      if (level >= 2) {
        const summary = node("small", layer === "visit" ? "관찰 기록 조회 중…" : p.species.slice(0, 2).join(" · "));
        marker.append(summary);
        if (layer === "visit") loadNearby().then(({places}) => {
          const rows = places[p.id] || [];
          summary.textContent = rows.length ? rows.slice(0, 2).map(r => r.name).join(" · ") + (rows.length > 2 ? " 외 " + (rows.length - 2) + "종" : "") : "주변 관찰 표본 없음";
        }).catch(() => { summary.textContent = "관찰 기록 조회 실패"; });
      }
      marker.setAttribute("aria-label", p.name + " 동물 보기");
      $("markers").append(marker);
      markers.push({place:p, marker});
    }
    if (p === selected) currentMarker = marker;
  }
  groupMarkers(markers, selected, layer, level, zoomTo, maxLevel);
  if (!currentMarker) return;
  bubble.dataset.view = expanded ? "animals" : "name";
  bubble.setAttribute("aria-label", selected.name + (expanded ? " 동물 목록" : " 장소 이름"));
  const dismiss = action("×", pinUrl(layer), "bubble-close", "close");
  dismiss.setAttribute("aria-label", "말풍선 닫기");
  if (!expanded) {
    const name = action(null, pinUrl(layer, selected.id, true), "bubble-name", "animals");
    name.append(node("strong", selected.name), node("span", "›"));
    name.setAttribute("aria-label", selected.name + " 동물 보기");
    bubble.append(name, dismiss);
  } else {
    const head = node("div", null, "bubble-head");
    const title = node("strong", selected.name);
    title.tabIndex = -1;
    head.append(title, dismiss);
    const content = node("div", null, "bubble-content");
    const kind = layer === "visit" && selected.id === "park" ? "보호·사육 동물" : "관찰·서식 동물";
    const reference = layer === "visit" ? node("details") : content;
    if (layer === "visit") {
      content.append(nearbyAnimals(selected));
      if (selected.species.length) content.append(reference);
      reference.append(node("summary", kind + " 자료 " + selected.species.length + "종"));
    } else content.append(node("p", kind + " " + selected.species.length + "종", "bubble-count"));
    const list = node("ul", null, "bubble-animals");
    for (const name of selected.species) {
      const li = node("li"), guide = node("a", name);
      guide.href = guideUrl(speciesIds[name], name);
      guide.setAttribute("aria-label", name + " 동물 도감 열기");
      guide.append(node("span", "›"));
      li.append(guide);
      if (speciesIds[name]?.startsWith("JJ-")) {
        const analysis = analysisLink(speciesIds[name], "제주 전체 목격 분석");
        analysis.className = "pin-analysis";
        li.append(analysis);
      }
      list.append(li);
    }
    reference.append(list);
    bubble.append(head, content);
  }
  bubble.hidden = false;
  positionBubble();
}
