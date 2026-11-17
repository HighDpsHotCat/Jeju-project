import {byId as $, node} from './ui.js';
import {reviewModelKeys, showReviewModel} from './review-model.js';
import {visits} from './map-data.js';
import {adoptionToken} from './adoption-profile.js';

const dialog=$('adoption-camera'), video=dialog.querySelector('video'), status=dialog.querySelector('p');
const params=new URLSearchParams(location.search);
const embedded=params.has('appMap'), examples=params.has('examples');
const requested=params.get('spot');
let token;
try { token=adoptionToken(); } catch { /* Browsing is available without storage. */ }
let active=false, run=0, spots=[], selected=requested, timer, offset=0, error='', loaded=false;
let cameraRun=0, stream, message='', pending=false, autoOpen=params.has('camera');

function vicinity(s) {
  if (s.reviewArea) return s.reviewArea;
  if (s.lat>33.85) return '추자군도 일대';
  let nearest, best=Infinity;
  for (const place of visits) {
    if (!place.destination) continue;
    const [lat,lon]=place.destination, distance=Math.hypot((s.lat-lat)*111,(s.lon-lon)*93);
    if(distance<best) { best=distance; nearest=place; }
  }
  return nearest ? nearest.name+'에서 약 '+Math.max(1,Math.round(best))+'km 부근' : '제주 연안';
}
function remaining(s) {
  const mins=Math.max(0,Math.ceil((s.expires-Date.now()-offset)/60000));
  return (mins>=60 ? Math.floor(mins/60)+'시간 ' : '')+(mins%60)+'분 남음';
}
const card = $('spot-card');
let triggerType = 'marker';
function selectSpot(spot, source) {
  selected = spot.id;
  triggerType = source;
  message = '';
  draw();
}
function updateCard(spot) {
  const model = reviewModelKeys[spot.species];
  const picture = $('spot-card-image');
  picture.hidden = !model;
  if (model) picture.src = '/art/spot-' + model + '.jpg';
  $('spot-card-kind').textContent = spot.reviewArea ? '심사용 분양 스팟' : '분양 스팟';
  $('spot-card-name').textContent = spot.name;
  $('spot-card-area').textContent = vicinity(spot);
  $('spot-card-time').textContent = remaining(spot) + (spot.reviewArea ? ' · 24시간마다 갱신' : '');
  $('spot-card-note').textContent = examples ? '실제 도감에서 분양받을 수 있어요.' :
    !token ? '등록하려면 브라우저 저장 공간을 허용해 주세요.' : '반경 2km 안에서 주변을 촬영하면 도감에 등록돼요.';
  $('spot-card-status').hidden = !message;
  $('spot-card-status').textContent = message;
  const preview = $('spot-card-preview');
  preview.hidden = !spot.reviewArea || !model;
  preview.onclick = () => showReviewModel(spot);
  const camera = $('spot-card-camera');
  camera.disabled = !token || examples;
  camera.onclick = () => openCamera(spot);
  if (!card.open) card.showModal();
}
function dismissCard() {
  const id = selected;
  selected = null;
  message = '';
  card.close();
  draw();
  document.querySelector('[data-spot="' + id + '"][data-source="' + triggerType + '"]')?.focus({preventScroll:true});
}
$('spot-card-close').onclick = dismissCard;
card.addEventListener('cancel', event => { event.preventDefault(); dismissCard(); });
card.addEventListener('click', event => {
  if (event.target !== card) return;
  const box = card.getBoundingClientRect();
  if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom)
    dismissCard();
});
function bindTrigger(button, spot, source) {
  button.dataset.spot = spot.id;
  button.dataset.source = source;
  button.setAttribute('aria-haspopup','dialog');
  button.setAttribute('aria-controls',card.id);
  button.onclick = () => selectSpot(spot, source);
}
function draw() {
  if (!active) return;
  const live=spots.filter(s=>s.expires>Date.now()+offset);
  const chosen=live.find(s=>s.id===selected);
  const panel=$('adoption-panel'), markers=$('markers');
  markers.replaceChildren(); $('offshore').replaceChildren(); panel.replaceChildren(); $('place-count').textContent=loaded ? live.length+'곳' : '';
  if(error) {
    const retry=node('button','다시 불러오기','pill'); retry.onclick=load;
    panel.append(node('p',error),retry);
  }
  if(!live.length) {
    if(!error) panel.append(node('p',loaded?'현재 열린 분양 스팟이 없어요.':'분양 스팟을 불러오는 중…'));
  }
  const list=node('div',null,'spot-list');
  for(const s of live) {
    // Local affine fit to the basemap's 43 surveyed control points (<1 px residual).
    const x=617.655+9.334*(s.lat-33.4)+1691.428*(s.lon-126.5);
    const y=422.833-2016.616*(s.lat-33.4)+7.329*(s.lon-126.5);
    const button=node('button',s.name+' · '+remaining(s),'pill');
    bindTrigger(button,s,'list'); list.append(button);
    if(x>=0 && x<=1440 && y>=0 && y<=910) {
      const circle=node('button',null,'spot-circle');
      circle.style.left=x/1440*100+'%'; circle.style.top=y/910*100+'%';
      circle.setAttribute('aria-label',s.name+' 분양 · '+vicinity(s));
      circle.setAttribute('aria-pressed',String(s===chosen)); bindTrigger(circle,s,'marker');
      markers.append(circle);
    } else {
      const island=node('button',null,'spot-offshore');
      island.setAttribute('aria-label',s.name+' 분양 · '+vicinity(s));
      island.append(node('span', s.lat>33.85 ? '추자군도' : '제주 연안')); bindTrigger(island,s,'marker');
      $('offshore').append(island);
    }
  }
  if(chosen) {
    updateCard(chosen);
    if(autoOpen) { autoOpen=false; openCamera(chosen); }
  } else if(selected && loaded && !error) {
    selected=null;
    if(dialog.open) dialog.close();
    if(card.open) card.close();
    message='선택한 스팟의 분양 시간이 끝났어요.';
  }
  if(message && !chosen) { const notice=node('p',message); notice.setAttribute('role','status'); panel.append(notice); }
  if(live.length) panel.append(list);
}
async function load() {
  const generation=++run;
  try {
    const r=await fetch('/api/adoption/spots',{cache:'no-store'});
    if(!r.ok) throw Error();
    const data=await r.json();
    if(generation!==run || !active) return;
    spots=data.spots; offset=data.now-Date.now(); loaded=true; error='';
  } catch { if(generation!==run || !active) return; error='분양 스팟을 불러오지 못했어요.'; }
  draw();
}
export function updateAdoptionMap(show) {
  $('adoption-panel').hidden=!show;
  if(show!==active) {
    active=show; ++run; clearInterval(timer);
    if(show) { load(); timer=setInterval(load,30000); }
    else { if(dialog.open) dialog.close(); if(card.open) card.close(); selected=null; message=''; }
  }
  draw();
}
function stopCamera() {
  ++cameraRun;
  stream?.getTracks().forEach(t=>t.stop()); stream=null; video.srcObject=null;
}
dialog.addEventListener('close',stopCamera);
window.addEventListener('pagehide',()=>{ if(dialog.open) dialog.close(); stopCamera(); });
$('capture-cancel').onclick=()=>dialog.close();
async function openCamera(s) {
  if(!token || examples || dialog.open) return;
  if(embedded) {
    location.assign('/map?layer=adoption&camera=1&spot='+s.id);
    return;
  }
  const generation=++cameraRun;
  pending=false; $('capture-now').disabled=true;
  status.textContent='카메라를 여는 중…'; dialog.showModal();
  try {
    const media=await navigator.mediaDevices.getUserMedia({video:{facingMode:{ideal:'environment'}},audio:false});
    if(generation!==cameraRun || !dialog.open) {media.getTracks().forEach(t=>t.stop());return;}
    stream=media; video.srcObject=media; await video.play();
    if(generation!==cameraRun) return;
    status.textContent=s.name+' 스팟 주변을 촬영해 주세요.';
    $('capture-now').disabled=false; $('capture-now').onclick=()=>capture(s,generation);
  } catch { if(generation===cameraRun) status.textContent='브라우저의 카메라 권한을 확인한 뒤 다시 열어 주세요.'; }
}
async function capture(s,generation) {
  if(pending || !video.videoWidth) return;
  pending=true; $('capture-now').disabled=true;
  try {
    status.textContent='촬영 위치를 확인하는 중…';
    const position=await new Promise((resolve,reject)=>navigator.geolocation.getCurrentPosition(resolve,reject,
      {enableHighAccuracy:true,maximumAge:0,timeout:15000}));
    if(generation!==cameraRun) return;
    const canvas=document.createElement('canvas'), scale=Math.min(1,640/Math.max(video.videoWidth,video.videoHeight));
    canvas.width=Math.round(video.videoWidth*scale); canvas.height=Math.round(video.videoHeight*scale);
    const taken=Date.now(); canvas.getContext('2d').drawImage(video,0,0,canvas.width,canvas.height);
    const photo=await new Promise(resolve=>canvas.toBlob(resolve,'image/png'));
    if(generation!==cameraRun) return;
    if(!photo) throw Error('사진을 저장하지 못했어요. 다시 촬영해 주세요.');
    const form=new FormData();
    form.set('photo',photo,'field.png'); form.set('taken',String(Math.round(taken+offset)));
    form.set('fix',String(Math.round(position.timestamp+offset))); form.set('lat',position.coords.latitude); form.set('lon',position.coords.longitude);
    status.textContent='도감에 등록하는 중…';
    const r=await fetch('/api/adoption/claim/'+s.id,{method:'POST',headers:{Authorization:'Bearer '+token},body:form});
    const result=await r.json(); if(!r.ok) throw Error(result.error);
    message=s.name+(result.already?' · 이미 분양받은 동물이에요.':' 도감 등록 완료');
    if(dialog.open) dialog.close(); draw();
  } catch(e) {
    if(generation===cameraRun) status.textContent=e.code?'현재 위치 권한과 GPS를 확인하고 다시 촬영해 주세요.':e.message || '등록하지 못했어요. 다시 시도해 주세요.';
  } finally { if(generation===cameraRun) { pending=false; $('capture-now').disabled=false; } }
}
