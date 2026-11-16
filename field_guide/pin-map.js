import { byId as $, node } from "./ui.js";
import { guideUrl, speciesIds } from "./map-data.js";

const frame = $("map-frame"), bubble = $("map-bubble");
let currentMarker = null;

export function pinUrl(layer, id, animals = false) {
  const url = new URL("/map", location.origin);
  if (layer !== "visit") url.searchParams.set("layer", layer);
  if (id) url.searchParams.set("pin", id);
  if (animals) url.searchParams.set("show", "animals");
  return url.pathname + url.search;
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
export function renderPinMap(layer, entries, selected, expanded) {
  $("place-count").textContent = entries.length + "곳";
  for (const [index,p] of entries.entries()) {
    const marker = action(null, pinUrl(layer, p.id), "map-point", "name");
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
      marker.append(node("span", index + 1));
      $("markers").append(marker);
    }
    if (p === selected) currentMarker = marker;
  }
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
    content.append(node("p", kind + " " + selected.species.length + "종", "bubble-count"));
    const list = node("ul", null, "bubble-animals");
    for (const name of selected.species) {
      const li = node("li"), guide = node("a", name);
      guide.href = guideUrl(speciesIds[name], name);
      guide.setAttribute("aria-label", name + " 동물 도감 열기");
      guide.append(node("span", "›"));
      li.append(guide);
      list.append(li);
    }
    content.append(list);
    bubble.append(head, content);
  }
  bubble.hidden = false;
  positionBubble();
}
