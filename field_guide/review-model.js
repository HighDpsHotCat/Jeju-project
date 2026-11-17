// Read-only preview of the same vertex-painted meshes used by the Unity yard.
export const reviewModelKeys={'JJ-004729':'dolphin','120000053293':'leopard_cat','JJ-004705':'rhino_beetle'};
const dialog=document.getElementById('review-model'), canvas=dialog.querySelector('canvas'), status=dialog.querySelector('p');
const cache=new Map();
let gl, program, buffer, count=0, generation=0, yaw, pitch, drag;
function init(){
  gl=canvas.getContext('webgl',{alpha:false,antialias:true});
  if(!gl) throw Error();
  function shader(type,source){
    const s=gl.createShader(type); gl.shaderSource(s,source); gl.compileShader(s);
    if(!gl.getShaderParameter(s,gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(s));
    return s;
  }
  const vert=shader(gl.VERTEX_SHADER,`attribute vec3 point,normal,paint;
    uniform vec4 rot; uniform vec2 fit; varying vec3 tint;
    vec3 turn(vec3 v) {
      vec3 q=vec3(rot.x*v.x+rot.y*v.z,v.y,-rot.y*v.x+rot.x*v.z);
      return vec3(q.x,rot.z*q.y-rot.w*q.z,rot.w*q.y+rot.z*q.z);
    }
    void main(){vec3 p=turn(point); gl_Position=vec4(p.xy*fit,-p.z*.5,1.);
      tint=paint*(.72+.28*max(0.,dot(normalize(turn(normal)),normalize(vec3(-.4,.8,1.)))));}
  `);
  const frag=shader(gl.FRAGMENT_SHADER,'precision mediump float; varying vec3 tint; void main(){gl_FragColor=vec4(tint,1.);}');
  program=gl.createProgram();gl.attachShader(program,vert);gl.attachShader(program,frag);gl.linkProgram(program);
  if(!gl.getProgramParameter(program,gl.LINK_STATUS)) throw Error();
  gl.useProgram(program);buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);
  for(const [name,type,offset] of [['point',gl.SHORT,0],['normal',gl.BYTE,6],['paint',gl.UNSIGNED_BYTE,9]]){
    const loc=gl.getAttribLocation(program,name); gl.enableVertexAttribArray(loc);gl.vertexAttribPointer(loc,3,type,true,12,offset);
  }
  gl.enable(gl.DEPTH_TEST);
}
function draw(){
  if(!dialog.open || !count) return;
  const dpr=Math.min(2,devicePixelRatio||1), w=Math.round(canvas.clientWidth*dpr),h=Math.round(canvas.clientHeight*dpr);
  if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;}
  gl.viewport(0,0,w,h);gl.clearColor(.946,.933,.896,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
  gl.uniform4f(gl.getUniformLocation(program,'rot'),Math.cos(yaw),Math.sin(yaw),Math.cos(pitch),Math.sin(pitch));
  gl.uniform2f(gl.getUniformLocation(program,'fit'),Math.min(1,h/w)*1.07,Math.min(1,w/h)*1.07);
  gl.drawArrays(gl.TRIANGLES,0,count);
}
function rotate(dx,dy){yaw+=dx;pitch=Math.max(-1.4,Math.min(1.4,pitch+dy));draw();}
canvas.onpointerdown=e=>{drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId);};
canvas.onpointermove=e=>{if(!drag)return;rotate((e.clientX-drag[0])*.013,(e.clientY-drag[1])*.012);drag=[e.clientX,e.clientY];};
canvas.onlostpointercapture=()=>{drag=null;};
canvas.onkeydown=e=>{const d={ArrowLeft:[-.15,0],ArrowRight:[.15,0],ArrowUp:[0,-.15],ArrowDown:[0,.15]}[e.key];if(d){e.preventDefault();rotate(...d);}};
new ResizeObserver(draw).observe(canvas);
document.getElementById('review-close').onclick=()=>dialog.close();
dialog.onclose=()=>{++generation;count=0;drag=null;};
export async function showReviewModel(spot){
  const key=reviewModelKeys[spot.species]; if(!key)return;
  const n=++generation;count=0;yaw=.96;pitch=key==='rhino_beetle'?.65:.21;
  document.getElementById('review-title').textContent=spot.name;
  canvas.style.visibility='hidden';status.textContent='3D 모델을 불러오는 중…';dialog.showModal();
  try{
    if(!program)init();
    if(!cache.has(key)){
      const response=await fetch('/review-models/'+key+'.json');if(!response.ok)throw Error();
      cache.set(key,Uint8Array.from(atob(await response.json()),c=>c.charCodeAt(0)));
    }
    if(n!==generation)return;
    const bytes=cache.get(key);gl.bufferData(gl.ARRAY_BUFFER,bytes,gl.STATIC_DRAW);count=bytes.length/12;
    canvas.style.visibility='visible';draw();canvas.focus();status.textContent='드래그하거나 방향키로 돌려 보세요.';
  }catch{if(n===generation)status.textContent='3D 모델을 열지 못했어요. WebGL을 지원하는 브라우저에서 다시 열어 주세요.';}
}
