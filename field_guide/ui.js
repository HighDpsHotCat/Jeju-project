const symbols = {
  sprout:
    '<path d="M12 22v-8C6 15 3 10 2 6c6 0 10 2 10 8zm0 2C12 8 15 5 22 4c0 6-3 10-9 11"/>',
  house: '<path d="m2 11 10-9 10 9m-3-2v12h-5v-7h-4v7H5V9"/>',
  book: '<path d="M12 6c-3-3-8-3-10-2v15c3-1 7-1 10 2 3-3 7-3 10-2V4c-2-1-7-1-10 2zm0 0v15"/>',
  map: '<path d="m2 5 6-3 8 4 6-3v16l-6 3-8-4-6 3zm6-3v16m8-12v16"/>',
  paw: '<ellipse cx="5" cy="9" rx="2.5" ry="3.4" transform="rotate(-22 5 9)"/><ellipse cx="10" cy="5.3" rx="2.3" ry="3.3"/><ellipse cx="16.1" cy="5.8" rx="2.3" ry="3.3" transform="rotate(13 16 6)"/><ellipse cx="20.3" cy="10.4" rx="2.2" ry="3.2" transform="rotate(25 20 10)"/><path d="M5 17c1-2 3-3 4-5 2-3 5-3 7 0l3 4c2 4 0 6-3 5l-4-1-3 1c-4 1-6-1-4-4z"/>',
  search: '<circle cx="10.5" cy="10.5" r="7"/><path d="m16 16 5 5"/>',
  settings:
    '<path d="m9 3 1-1h4l1 3 2 1 3-.3 2 3-2 2v2l2 2-2 3-3-.3-2 1-1 3h-4l-1-3-2-1-3 .3-2-3 2-2v-2l-2-2 2-3 3 .3 2-1z"/><circle cx="12" cy="12" r="3"/>',
  camera:
    '<path d="M3 7h4l2-3h6l2 3h4v14H3z"/><circle cx="12" cy="13.5" r="4"/>',
  pin: '<path d="M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0z"/><circle cx="12" cy="10" r="2.6"/>',
  check: '<path d="m3 12 6 6L21 5"/>',
  close: '<path d="m6 6 12 12M18 6 6 18"/>',
};
export function icon(name) {
  const span = document.createElement("i");
  span.dataset.icon = name;
  draw(span);
  return span;
}
function draw(el) {
  el.setAttribute("aria-hidden", "true");
  if (Object.hasOwn(symbols, el.dataset.icon))
    el.innerHTML =
      '<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">' +
      symbols[el.dataset.icon] +
      "</svg>";
}
document.querySelectorAll("[data-icon]").forEach(draw);
export function node(tag, text, cls) {
  const el = document.createElement(tag);
  if (text != null) el.textContent = text;
  if (cls) el.className = cls;
  return el;
}
export const byId = (id) => document.getElementById(id);
