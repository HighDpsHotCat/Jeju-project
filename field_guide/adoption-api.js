// Public endpoints never return the encounter coordinates or a profile credential.
const spotDay = 24 * 60 * 60 * 1000;
const utf8 = new TextEncoder();
const hex = data => Array.from(new Uint8Array(data), b => b.toString(16).padStart(2, '0')).join('');
const digest = async value => hex(await crypto.subtle.digest('SHA-256', utf8.encode(value)));
function metersBetween(a, b) {
  const rad = Math.PI / 180;
  const h = Math.sin((b.lat-a.lat)*rad/2)**2 +
    Math.cos(a.lat*rad)*Math.cos(b.lat*rad)*Math.sin((b.lon-a.lon)*rad/2)**2;
  return 12742000 * Math.asin(Math.sqrt(Math.min(1, h)));
}
function jejuPoint(p) {
  return p && Number.isFinite(p.lat) && Number.isFinite(p.lon) &&
    ((p.lat >= 33.08 && p.lat <= 33.59 && p.lon >= 126.13 && p.lon <= 127.02) ||
     (p.lat >= 33.88 && p.lat <= 34 && p.lon >= 126.24 && p.lon <= 126.38));
}
const adoptReply = (data, status = 200) => Response.json(data, {status, headers:{
  'Cache-Control':'no-store', 'Access-Control-Allow-Origin':'*',
  'Access-Control-Allow-Methods':'GET,POST,OPTIONS',
  'Access-Control-Allow-Headers':'Authorization,Content-Type',
}});
function roundedCoord(n) { return Math.round(n * 200) / 200; }
function expiryAt(spot, now) {
  return spot.review_area ? spot.created+(Math.floor((now-spot.created)/spotDay)+1)*spotDay : spot.expires;
}
async function publishSpot(req, env, owner) {
  const {payload, signature} = await req.json();
  if (!env.ADOPTION_SIGNING_KEY) return adoptReply({error:'분양 연결 설정을 확인해 주세요.'},503);
  if (typeof payload !== 'string' || payload.length > 3000 || !/^[a-f0-9]{64}$/.test(signature))
    return adoptReply({error:'판별 기록이 필요해요.'},403);
  const key = await crypto.subtle.importKey('raw', utf8.encode(env.ADOPTION_SIGNING_KEY), {name:'HMAC',hash:'SHA-256'},false,['verify']);
  if (!await crypto.subtle.verify('HMAC',key,Uint8Array.from(signature.match(/../g),x=>parseInt(x,16)),utf8.encode(payload)))
    return adoptReply({error:'판별 기록을 확인할 수 없어요.'},403);
  const proof = JSON.parse(payload), now = Date.now();
  if (proof.owner !== owner || !Number.isSafeInteger(proof.issued) || proof.issued > now+30000 || now-proof.issued >= spotDay)
    return adoptReply({error:'유효한 등록 기록이 필요해요.'},403);
  const name = adoptionSpecies[proof.species], points = proof.points;
  if (!Object.hasOwn(adoptionSpecies,proof.species)) return adoptReply({state:'ineligible'});
  if (!Array.isArray(points) || !points.length || points.length > 5 ||
      points.some(p => !jejuPoint(p) || metersBetween(points[0],p)>2000)) return adoptReply({state:'location'});
  if (points.some(p => captiveSites.some(site => metersBetween(p,site) <= site.meters)))
    return adoptReply({state:'facility'});
  const id = await digest(owner+'/'+proof.species);
  await env.DB.prepare('DELETE FROM spots WHERE expires<=? AND review_area IS NULL').bind(now).run();
  await env.DB.prepare('INSERT INTO spots(id,owner,species,name,lat,lon,created,expires) VALUES (?,?,?,?,?,?,?,?) ON CONFLICT(id) DO NOTHING')
    .bind(id,owner,proof.species,name,points[0].lat,points[0].lon,proof.issued,proof.issued+spotDay).run();
  return adoptReply({state:'shared'});
}
// The shutter emits a small non-interlaced PNG. Decode the compressed raster
// with a size cap before accepting it, without retaining the visitor's picture.
async function readablePhoto(file) {
  if (!(file instanceof Blob) || file.type !== 'image/png' || file.size < 60) return false;
  const bytes=new Uint8Array(await file.arrayBuffer()), view=new DataView(bytes.buffer);
  if (view.getUint32(0)!==0x89504e47 || view.getUint32(4)!==0x0d0a1a0a ||
      view.getUint32(8)!==13 || view.getUint32(12)!==0x49484452) return false;
  const width=view.getUint32(16), height=view.getUint32(20), channels=({0:1,2:3,4:2,6:4})[bytes[25]];
  if (!width || !height || width>640 || height>640 || !channels || bytes[24]!==8 ||
      bytes[26] || bytes[27] || bytes[28]) return false;
  const chunks=[];
  let ended=false;
  for (let i=33;i+12<=bytes.length;) {
    const len=view.getUint32(i), tag=view.getUint32(i+4);
    if (i+len+12>bytes.length) return false;
    if (tag===0x49444154) chunks.push(bytes.subarray(i+8,i+8+len));
    i+=len+12;
    if (tag===0x49454e44) { ended=(len===0 && i===bytes.length); break; }
  }
  if (!ended || !chunks.length) return false;
  const expected=height*(1+width*channels);
  let count=0;
  try {
    const reader=new Blob(chunks).stream().pipeThrough(new DecompressionStream('deflate')).getReader();
    while(true) {
      const {value,done}=await reader.read();
      if(done) return count===expected;
      count+=value.length;
      if(count>expected) { await reader.cancel(); return false; }
    }
  } catch { return false; }
}
async function claimSpot(req,env,owner,id) {
  if (!req.headers.get('Content-Type')?.startsWith('multipart/form-data') ||
      Number(req.headers.get('Content-Length')) > 2*1024*1024) return adoptReply({error:'현장 사진을 다시 촬영해 주세요.'},400);
  // Bound even streamed uploads before parsing a form.
  const reader = req.body?.getReader(), chunks = [];
  if (!reader) return adoptReply({error:'촬영한 사진이 필요해요.'},400);
  let size = 0;
  while (true) {
    const {value,done} = await reader.read();
    if (done) break;
    size += value.length;
    if (size > 2*1024*1024) { await reader.cancel(); return adoptReply({error:'사진 용량을 줄여 주세요.'},413); }
    chunks.push(value);
  }
  const form = await new Response(new Blob(chunks),{headers:{'Content-Type':req.headers.get('Content-Type')}}).formData();
  const photo = form.get('photo');
  const number = k => typeof form.get(k)==='string' && form.get(k).trim() ? Number(form.get(k)) : NaN;
  const p = {lat:number('lat'), lon:number('lon')}, taken = number('taken'), fix = number('fix');
  const now = Date.now();
  const spot = await env.DB.prepare('SELECT * FROM spots WHERE id=?').bind(id).first();
  if (!spot || expiryAt(spot,now)<=now) return adoptReply({error:'분양 시간이 끝났어요.'},410);
  if (owner===spot.owner) return adoptReply({error:'직접 발견해 만든 스팟이에요.'},403);
  if (!Number.isSafeInteger(taken) || !Number.isFinite(fix) || taken < spot.created || taken >= expiryAt(spot,now) ||
      taken > now+10000 || now-taken > 120000 || Math.abs(fix-taken)>30000)
    return adoptReply({error:'현재 위치에서 주변을 다시 촬영해 주세요.'},422);
  if (!jejuPoint(p) || metersBetween(p,spot)>2000)
    return adoptReply({error:'발견 지점 반경 2km 안에서 촬영해 주세요.'},422);
  if (!await readablePhoto(photo)) return adoptReply({error:'주변 사진을 다시 촬영해 주세요.'},422);
  const result = await env.DB.prepare('INSERT INTO adoption_claims(owner,species,claimed) SELECT ?,species,? FROM spots WHERE id=? AND (expires>? OR review_area IS NOT NULL) ON CONFLICT(owner,species) DO NOTHING')
    .bind(owner,taken,id,Date.now()).run();
  if (!result.meta.changes && !await env.DB.prepare('SELECT 1 FROM adoption_claims WHERE owner=? AND species=?').bind(owner,spot.species).first())
    return adoptReply({error:'분양 시간이 끝났어요.'},410);
  return adoptReply({species:spot.species,name:spot.name,already:!result.meta.changes});
}
async function adoptionApi(req, env, url) {
  const path = url.pathname.slice('/api/adoption/'.length);
  try {
    if (req.method==='OPTIONS') return adoptReply({});
    if (req.method==='GET' && path==='spots') {
      const now=Date.now();
      const {results} = await env.DB.prepare('SELECT id,species,name,lat,lon,created,expires,review_area FROM spots WHERE expires>? OR review_area IS NOT NULL').bind(now).all();
      return adoptReply({now, spots:results.map(s=>({id:s.id,species:s.species,name:s.name,lat:roundedCoord(s.lat),lon:roundedCoord(s.lon),expires:expiryAt(s,now),reviewArea:s.review_area})).sort((a,b)=>a.expires-b.expires)});
    }
    const token = /^Bearer ([a-f0-9]{64})$/.exec(req.headers.get('Authorization') || '')?.[1];
    if (!token) return adoptReply({error:'도감 프로필을 다시 열어 주세요.'},401);
    const owner = await digest(token);
    if (req.method==='GET' && path==='collection') {
      const {results} = await env.DB.prepare('SELECT species,claimed FROM adoption_claims WHERE owner=?').bind(owner).all();
      return adoptReply({claims:results});
    }
    if (req.method==='POST' && path==='spots') return await publishSpot(req,env,owner);
    if (req.method==='POST' && /^claim\/[a-f0-9]{64}$/.test(path)) return await claimSpot(req,env,owner,path.slice(6));
    return adoptReply({error:'요청을 확인해 주세요.'},404);
  } catch (err) {
    if (err instanceof SyntaxError || err instanceof TypeError && /form|parse/i.test(err.message))
      return adoptReply({error:'촬영 정보를 다시 보내 주세요.'},400);
    return adoptReply({error:'분양 기록을 연결하지 못했어요. 잠시 후 다시 시도해 주세요.'},503);
  }
}
