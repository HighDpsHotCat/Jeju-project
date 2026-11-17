// Run after node build.mjs with Node 24+. Exercise the deployed Worker routes
// against the actual migration in an isolated, in-memory SQLite database.
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {readFileSync,readdirSync} from 'node:fs';
import {createHash,createHmac,randomBytes} from 'node:crypto';
import {deflateSync,crc32} from 'node:zlib';
import worker from '../dist/server/index.js';
const db=new DatabaseSync(':memory:');
for(const file of readdirSync('drizzle').filter(f=>f.endsWith('.sql')).sort()) db.exec(readFileSync('drizzle/'+file,'utf8'));
export const env={ADOPTION_SIGNING_KEY:randomBytes(32).toString('hex'), DB:{prepare(sql){
  return {bind(...args){ const stmt=db.prepare(sql); return {
    all:()=>({results:stmt.all(...args)}),first:()=>stmt.get(...args),
    run:()=>({meta:stmt.run(...args)}),
  }; }};
}}};
const creator='a'.repeat(64), visitor='b'.repeat(64);
function call(path,token,body,contentType) {
  return worker.fetch(new Request('https://example.test/api/adoption/'+path,{
    method:body?'POST':'GET',headers:{...(token&&{Authorization:'Bearer '+token}),...(contentType&&{'Content-Type':contentType})},body}),env);
}
const place={lat:33.3483,lon:126.2679};
async function publish(species,points=[place],issued=Date.now()-3000,signatureOverride) {
  const owner=createHash('sha256').update(creator).digest('hex');
  const payload=JSON.stringify({owner,issued,species,points});
  const signature=signatureOverride??createHmac('sha256',env.ADOPTION_SIGNING_KEY).update(payload).digest('hex');
  return call('spots',creator,JSON.stringify({payload,signature}),'application/json');
}
const state=async(...args)=>(await (await publish(...args)).json()).state;
assert.equal(await state('JJ-003483'),'ineligible'); // sparrow
assert.equal(await state('JJ-003382'),'ineligible'); // swallowtail
assert.equal(await state('JJ-004705',[{lat:33.43298,lon:126.92780}]),'facility');
assert.equal(await state('JJ-004705',[{lat:33.4111,lon:126.3714}]),'facility');
assert.equal(await state('JJ-004705',[{lat:37.5,lon:127}]),'location');
assert.equal((await publish('JJ-004705',[place],Date.now()-86400000)).status,403);
assert.equal((await publish('JJ-004705',[place],Date.now(),'a'.repeat(64))).status,403);
assert.equal(await state('JJ-004705'),'shared');
assert.equal(await state('JJ-004705'),'shared');
assert.equal(await state('JJ-001626'),'shared'); // horned rhinoceros beetle
assert.equal(await state('120000053293'),'shared'); // national leopard cat
assert.equal(await state('120000017614'),'shared'); // national stag beetle
assert.equal(await state('JJ-004729'),'shared');
const {spots}=await (await call('spots')).json();
assert.equal(spots.length,8); assert.ok(spots.every(s=>!('owner' in s) && s.lat!==place.lat));
const spot=spots.find(s=>!s.reviewArea && s.species==='JJ-004705');
function pngChunk(type,data) {
  const bytes=Buffer.concat([Buffer.from(type),data]), len=Buffer.alloc(4), crc=Buffer.alloc(4);
  len.writeUInt32BE(data.length); crc.writeUInt32BE(crc32(bytes)); return Buffer.concat([len,bytes,crc]);
}
const png=Buffer.concat([Buffer.from('89504e470d0a1a0a','hex'),
  pngChunk('IHDR',Buffer.from('00000001000000010802000000','hex')),pngChunk('IDAT',deflateSync(Buffer.alloc(4))),pngChunk('IEND',Buffer.alloc(0))]);
function claim(token,lat,when=Date.now(),picture=png,target=spot,lon=place.lon) {
  const data=new FormData();data.set('photo',new Blob([picture],{type:'image/png'}),'field.png');
  for(const [k,v] of Object.entries({lat,lon,taken:when,fix:when})) data.set(k,v);
  return call('claim/'+target.id,token,data);
}
assert.equal((await claim(creator,place.lat)).status,403);
assert.equal((await claim(visitor,place.lat+2001/111194.927)).status,422);
assert.equal((await claim(visitor,place.lat,Date.now()-86400000)).status,422);
assert.equal((await claim(visitor,place.lat,Date.now(),Buffer.alloc(200))).status,422);
assert.equal((await (await claim(visitor,place.lat+1999/111194.927)).json()).already,false);
assert.equal((await (await claim(visitor,place.lat)).json()).already,true);
assert.equal((await (await call('collection',visitor)).json()).claims.length,1);
assert.equal((await (await call('collection',creator)).json()).claims.length,0);
db.prepare('UPDATE spots SET expires=? WHERE id=?').run(Date.now(),spot.id);
assert.equal((await claim(visitor,place.lat)).status,410);
assert.ok(!(await (await call('spots')).json()).spots.some(s=>s.id===spot.id));
console.log('Two-profile publication, geofences, photo claims, 2 km boundary, expiry and retries passed.');

const day=86400000, now=Date.now;
const review=spots.filter(s=>s.reviewArea);
assert.deepEqual(new Set(review.map(s=>s.species)),new Set(['JJ-004729','120000053293','JJ-004705']));
assert.equal(new Set(review.map(s=>s.reviewArea)).size,3);
try {
  for (const step of [0,4*day]) {
    Date.now=()=>review[0].expires+step;
    const list=(await (await call('spots')).json()).spots;
    assert.equal(list.length,3);
    for(const s of list) {
      assert.equal(s.expires-Date.now(),day);
      assert.equal((await claim(creator,s.lat+.05,Date.now(),png,s,s.lon)).status,422);
      assert.equal((await (await claim(creator,s.lat,Date.now(),png,s,s.lon)).json()).already,step!==0);
    }
  }
  assert.equal(await state('JJ-001626'),'shared');
  assert.equal((await (await call('spots')).json()).spots.filter(s=>s.reviewArea).length,3);
  assert.equal((await (await call('collection',creator)).json()).claims.length,3);
} finally { Date.now=now; }
console.log('All three judging spots renew at the exact 24h boundary, accept nearby photos on later cycles and keep one claim per species.');
