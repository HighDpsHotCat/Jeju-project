import {readFileSync,writeFileSync,mkdirSync,copyFileSync,readdirSync} from 'node:fs';
import {gzipSync,gunzipSync} from 'node:zlib';
import {eligible} from './adoption-policy.mjs';
const read = path => readFileSync(path,'utf8');
const species = JSON.parse(read('species.json'));
if(species.length !== 4875) throw Error('Species catalog mismatch');
const home = read('index.html');
const rows = gunzipSync(readFileSync('../assets/catalog/animals.tsv.gz')).toString('utf8').trimEnd().split('\n').map(line => line.split('\t'));
const guideOrder = new Map(rows.filter(row => row[5] === 'jeju').map((row, i) => [row[0], i]));
for (const animal of species) {
  if (!guideOrder.has(animal.species_id)) animal.in_guide = false;
}
species.sort((a, b) => (guideOrder.get(a.species_id) ?? Infinity) - (guideOrder.get(b.species_id) ?? Infinity));
const credits = JSON.parse(read('../assets/guide_photos/credits.json'));
const breedPhotos = {};
for (const [id, credit] of Object.entries(credits)) {
  const parent = id.startsWith('BREED-') ? 'JJ-000821' : id.startsWith('CAT-') ? 'JJ-001834' : '';
  if (parent) (breedPhotos[parent] ??= []).push({id, ...credit});
}
for (const animal of species) {
  if (breedPhotos[animal.species_id]) animal.breeds = breedPhotos[animal.species_id];
  if (credits[animal.species_id]) animal.photo = credits[animal.species_id];
}
const national = rows.filter(row => row[5] === 'national')
  .map(([jejuId, ktsn, korean, scientific, taxon]) => [ktsn, korean, scientific, taxon, jejuId, credits[ktsn] ?? null, breedPhotos[jejuId] ?? null]);
const assets = {'/':home,'/yard':read('yard.html'),'/yard/index.html':read('../assets/yard/index.html'),'/yard/player.js':read('../assets/yard/player.js'),'/map':read('map.html'),'/ecology':read('ecology.html'),'/species.json':JSON.stringify(species),
  '/photo-credits.json':JSON.stringify(credits),
  '/national.json':JSON.stringify(national)};
for (const file of ['field.css','map.css','ui.js','demo-roster.js','guide.js','map.js','map-data.js','adoption-map.js','review-model.js','adoption-profile.js','visit-map.js','pin-map.js','observation-analysis.js','observations.json','gbif.json','nearby.json','visit-jeju.json']) assets['/'+file] = read(''+file);
for (const model of ['dolphin','leopard_cat','rhino_beetle'])
  assets['/review-models/'+model+'.json'] = read('review-models/'+model+'.json');
// Keep the single Worker below its source size limit without changing the media.
for (const key in assets) if (key.endsWith('.json'))
  assets[key] = gzipSync(assets[key], {level:9}).toString('base64');
const alphabet = '0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#';
function packImage(file) {
  const data = readFileSync(file);
  const out = Buffer.alloc(1 + Math.ceil(data.length / 4) * 5);
  out[0] = 48 + (4 - data.length % 4) % 4;
  for (let i=0, j=1; i<data.length; i+=4,j+=5) {
    let n = data[i]*16777216 + (data[i+1] || 0)*65536 + (data[i+2] || 0)*256 + (data[i+3] || 0);
    for (let k=4; k>=0; k--) { out[j+k]=alphabet.charCodeAt(n%85); n=Math.floor(n/85); }
  }
  return out.toString('ascii');
}
for (const file of ['jeju-grades.png','jeju-map.png',...readdirSync('../assets/art').filter(f=>f.endsWith('.jpg') && f!=='explorer_portrait.jpg').map(f=>'art/'+f)]) {
  assets['/'+file] = packImage(file.startsWith('art/') ? '../assets/'+file : file);
}
for (const id of Object.keys(credits)) {
  assets['/guide-photos/'+id+'.jpg'] = packImage('../assets/guide_photos/'+id+'.jpg');
}
mkdirSync('dist/server',{recursive:true});
mkdirSync('dist/.openai',{recursive:true});
copyFileSync('.openai/hosting.json','dist/.openai/hosting.json');
const adoptionSpecies = Object.fromEntries(species.filter(eligible).map(row =>
  [row.species_id, row.korean_name || row.scientific_name]));
for (const [id, name, scientific, taxon, localId] of national) {
  if (!localId && (name === '삵' || (taxon === 'Insecta' && eligible({class:taxon,korean_name:name,scientific_name:scientific})))) {
    adoptionSpecies[id] = name;
  }
}
const adoptionCode = 'const adoptionSpecies = '+JSON.stringify(adoptionSpecies)+';\nconst captiveSites = '+
  read('captive-sites.json')+';\n'+read('adoption-api.js')+'\n';
writeFileSync('dist/server/index.js','const assets = '+JSON.stringify(assets)+ ';\nconst imageAlphabet = ' + JSON.stringify(alphabet) + ';\n' + adoptionCode + read('worker.js'));
