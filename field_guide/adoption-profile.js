// Browser-only visitors have a device profile. App links scope their profile to
// this tab so switching app users cannot overwrite a browser visitor's guide.
export function adoptionToken() {
  const url = new URL(location.href);
  const linked = new URLSearchParams(url.hash.slice(1)).get('profile');
  if (/^[a-f0-9]{64}$/.test(linked || '')) {
    sessionStorage.setItem('jeju.linkedProfile',linked);
    url.hash='';
    history.replaceState(history.state,'',url);
  }
  const app = sessionStorage.getItem('jeju.linkedProfile');
  if (app) return app;
  let token = localStorage.getItem('jeju.adoptionProfile');
  if (!token) {
    token = Array.from(crypto.getRandomValues(new Uint8Array(32)), b=>b.toString(16).padStart(2,'0')).join('');
    localStorage.setItem('jeju.adoptionProfile',token);
  }
  return token;
}
export async function claimedAnimals() {
  const response = await fetch('/api/adoption/collection',{headers:{Authorization:'Bearer '+adoptionToken()}});
  if (!response.ok) throw Error('분양 도감을 불러오지 못했어요.');
  return (await response.json()).claims;
}
