const upstream='https://open.jejudatahub.net/api/proxy/6bttat6t84Daa1b18aat1a6DD4648Daa/';
const slot='private/ecology-key';
const json=(body,status=200)=>Response.json(body,{status,headers:{'Cache-Control':'no-store'}});
const fail=(error,status)=>json({error},status);
const bytes=s=>Uint8Array.from(atob(s),c=>c.charCodeAt(0));
const imageDigits = new Uint8Array(128);
for (let i=0;i<imageAlphabet.length;i++) imageDigits[imageAlphabet.charCodeAt(i)]=i;
function imageBytes(s) {
 const size=(s.length-1)/5*4 - Number(s[0]), result=new Uint8Array(size);
 for (let i=1,j=0;i<s.length;i+=5,j+=4) {
  let n=0; for(let k=0;k<5;k++) n=n*85+imageDigits[s.charCodeAt(i+k)];
  result[j]=n>>>24; result[j+1]=n>>>16; result[j+2]=n>>>8; result[j+3]=n;
 }
 return result;
}
const b64=b=>btoa(String.fromCharCode(...new Uint8Array(b)));
async function cipher(env){
 return crypto.subtle.importKey('raw',bytes(env.GUIDE_KEY_ENCRYPTION),'AES-GCM',false,['encrypt','decrypt']);
}
async function lookup(key,params){
 // Workers supports manual redirects. Never forward the key to another URL.
 const r=await fetch(upstream+encodeURIComponent(key)+'?'+params,{redirect:'manual',signal:AbortSignal.timeout(15000)});
 if(r.status>=300&&r.status<400)throw new HubError('허브가 다른 주소로 응답했습니다. 도감 관리자에게 알려주세요.');
 if(!r.ok){
  const hints={401:'프로젝트키와 프로젝트의 데이터 이용 상태를 확인하세요.',403:'허브가 접근을 거절했습니다. 해당 프로젝트에 생태자연도 정보가 등록되어 사용 가능한지 확인하세요.',429:'허브 호출 한도에 도달했습니다. 나중에 다시 시도하세요.'};
  throw new HubError('제주데이터허브 HTTP '+r.status+' · '+(hints[r.status]||'허브 요청을 처리하지 못했습니다.'));
 }
 const result=await r.json();
 const total=typeof result?.totCnt==='string'&&/^\d+$/.test(result.totCnt)?Number(result.totCnt):result?.totCnt;
 const more=({'true':true,'false':false})[String(result?.hasMore)];
 if(!Number.isSafeInteger(total)||total<0||typeof more!=='boolean'||!Array.isArray(result?.data)||result.data.some(x=>!x||typeof x!=='object'||Array.isArray(x)))throw new HubError('허브가 예상한 데이터 목록을 반환하지 않았습니다. 프로젝트의 API 이용 상태를 확인하세요.');
 return {totCnt:total,hasMore:more,data:result.data};
}
class HubError extends Error {}
export default {async fetch(req,env,ctx){
 const url=new URL(req.url);
 const email=req.headers.get('oai-authenticated-user-email')?.toLowerCase();
 const owner=Boolean(email&&email===env.GUIDE_OWNER_EMAIL?.toLowerCase());
 try{
  if(url.pathname.startsWith('/api/adoption/')) return await adoptionApi(req,env,url);
  if(url.pathname==='/api/setup'&&req.method==='POST'){
   if(!owner||req.headers.get('Origin')!==url.origin)return fail('도감 소유자로 로그인하세요.',403);
   const {key}=await req.json();
   if(typeof key!=='string'||!key.trim()||key.length>512)return fail('프로젝트키를 확인하세요.',400);
   try{await lookup(key,new URLSearchParams({number:'1',limit:'1'}))}catch(error){return fail(error instanceof HubError?error.message:'허브 연결 또는 응답 읽기에 실패했습니다. 잠시 후 다시 시도하세요.',422)}
   const iv=crypto.getRandomValues(new Uint8Array(12));
   const ct=await crypto.subtle.encrypt({name:'AES-GCM',iv},await cipher(env),new TextEncoder().encode(key));
   await env.BUCKET.put(slot,JSON.stringify({iv:b64(iv),ct:b64(ct)}));return json({ok:true});
  }
  if(req.method!=='GET')return fail('허용되지 않는 요청입니다.',405);
  if(url.pathname==='/api/session')return json({owner,signedIn:Boolean(email)});
  if(url.pathname==='/api/ecology'){
   const number=url.searchParams.get('number')??'1',rank=url.searchParams.get('wetlandRank');
   if(!/^[1-9][0-9]{0,7}$/.test(number)||(rank!==null&&!['1','2','3'].includes(rank)))return fail('조회 조건을 확인하세요.',400);
   const obj=await env.BUCKET.get(slot);
   if(!obj&&!env.JEJU_DATAHUB_PROJECT_KEY)return fail('관리자가 제주데이터허브 프로젝트키를 설정하면 조회할 수 있습니다.',503);
   const params=new URLSearchParams({number,limit:'50'});if(rank)params.set('wetlandRank',rank);
   const pageKey='ecology-pages/v1/'+(obj?.etag||'env')+'/'+(rank||'all')+'/'+number;
   let saved=null, snapshotEtag=null;
   try{
    const item=await env.BUCKET.get(pageKey);
    if(item){
     snapshotEtag=item.etag;
     saved=await new Response(item.body).json();
    }
   }catch{/* A snapshot read failure must not block a live lookup. */}
   if(saved && Date.now()-Date.parse(saved.fetchedAt)<300000)return json({...saved,source:'cache'});
   let key=env.JEJU_DATAHUB_PROJECT_KEY;
   if(obj){
    try{
     const blob=JSON.parse(await new Response(obj.body).text());
     key=new TextDecoder().decode(await crypto.subtle.decrypt({name:'AES-GCM',iv:bytes(blob.iv)},await cipher(env),bytes(blob.ct)));
    }catch{return fail('저장된 키를 읽는 중 오류가 발생했습니다.',502)}
   }
   let result;
   try{result=await lookup(key,params)}catch(e){
    if(saved)return json({...saved,source:'stale'});
    return fail(e instanceof HubError?e.message:'저장된 결과가 없는 페이지입니다. 잠시 후 다시 조회하세요.',502);
   }
   const snapshot={...result,fetchedAt:new Date().toISOString()};
   // Avoid storing arbitrarily many empty pages beyond the end of the dataset.
   if(result.data.length || number==='1' || saved){
    const onlyIf=snapshotEtag?{etagMatches:snapshotEtag}:{etagDoesNotMatch:'*'};
    try{await env.BUCKET.put(pageKey,JSON.stringify(snapshot),{onlyIf})}
    catch{/* A storage failure must not hide the successful hub response. */}
   }
   return json({...snapshot,source:'live'});
  }
  if(Object.hasOwn(assets,url.pathname)) {
   const ext=url.pathname.split('.').pop();
   const mime=({jpg:'image/jpeg',png:'image/png',css:'text/css',js:'text/javascript',json:'application/json'})[ext] || 'text/html';
   const binary=mime.startsWith('image/');
   const value=assets[url.pathname];
   const body=binary?imageBytes(value):ext==='json'
    ?new Response(bytes(value)).body.pipeThrough(new DecompressionStream('gzip')):value;
   return new Response(body,{headers:{
    'Content-Type':binary?mime:mime+'; charset=utf-8'
   }});
  }
  return fail('페이지를 찾을 수 없습니다.',404);
 }catch{return fail('생태자연도 서버에 연결하지 못했습니다. 잠시 후 다시 시도하세요.',502)}
}};
