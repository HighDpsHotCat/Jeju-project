import tourismSites from "./visit-jeju.json" with { type: "json" };
// Mainland marker positions are pixels in the 1440 × 910 EPSG:5186 basemap.
const hub = (id) => "https://jejudatahub.net/data/view/data/" + id;
const ramsar = (id) => "https://rsis.ramsar.org/ris/" + id;
const visitJeju = (id) => "https://www.visitjeju.net/kr/detail/view?contentsid=" + id;

export const speciesIds = {
  "제주도롱뇽": "JJ-002257", "황조롱이": "JJ-001829",
  "남방큰돌고래": "JJ-004729", "저어새": "JJ-003720", "청둥오리": "JJ-000261",
  "노루": "JJ-000837", "독수리": "JJ-000122", "반달가슴곰": "KTSN-120000212858",
  "맹꽁이": "JJ-002364", "물장군": "KTSN-120000525839", "팔색조": "JJ-003706",
  "매": "JJ-001827", "벌매": "JJ-003537", "섬개개비": "JJ-002572",
  "원앙": "JJ-000160", "무태장어": "JJ-000284", "큰오색딱다구리": "JJ-001338",
  "동박새": "JJ-004873", "뿔논병아리": "JJ-003777",
};

export const visits = [
  {
    id: "aquaplanet", name: "아쿠아플라넷 제주", kind: "facility", type: "수족관", animalContext: "전시 동물", zone: "seogwipo",
    area: "서귀포시 성산읍 섭지코지로 95", destination: [33.43298, 126.92780], pixel: [1341.5, 358.7],
    species: ["아프리카펭귄", "바이칼물범", "동수구리", "물총고기", "빅벨리드해마"],
    sources: [["공식 생물 소개", "https://m.aquaplanet.co.kr/jeju/introduce/friend/list.do"],
      ["펭귄 전시 안내", "https://m.aquaplanet.co.kr/contents/jeju/introduce/gallery/second-floor/aquarium.do"]],
  },
  {
    id: "hwajowon", name: "화조원", kind: "facility", type: "조류 동물원", animalContext: "전시 동물", zone: "jeju",
    area: "제주시 애월읍 애원로 804", destination: [33.411109, 126.3714311], pixel: [400.6, 399.6],
    species: ["알파카", "흰머리수리", "사랑앵무", "알락꼬리여우원숭이", "잉어"],
    sources: [["공식 동물 소개", "https://www.flowerbirdpark.com/"],
      ["야외공원 안내", "https://www.flowerbirdpark.com/page2-1"]],
  },
  {
    id: "dochidol", name: "도치돌알파카목장", kind: "facility", type: "목장", animalContext: "목장 동물", zone: "jeju",
    area: "제주시 애월읍 도치돌길 303", destination: [33.4098961, 126.3665965], pixel: [392.4, 402.0],
    species: ["알파카", "양", "염소", "말", "토끼"],
    sources: [["목장 공식 안내", "https://www.alpacajeju.com/DOCHIDOLRANCH"], ["비짓제주 목장 소개", visitJeju("CNTS_200000000010953")]],
  },
  {
    id: "saebyeol-friends", name: "새별프렌즈", kind: "facility", type: "목장", animalContext: "목장 동물", zone: "jeju",
    area: "제주시 애월읍 평화로 1529", destination: [33.364338, 126.3552026], pixel: [372.5, 493.7],
    species: ["당나귀", "알파카", "양", "말", "염소"],
    sources: [["비짓제주 목장 소개", visitJeju("CNTS_200000000010957")]],
  },
  {
    id: "fathers-garden", name: "파더스가든", kind: "facility", type: "동물농장", animalContext: "농장 동물", zone: "seogwipo",
    area: "서귀포시 안덕면 병악로 44-33", destination: [33.2805107, 126.3655186], pixel: [389.0, 662.9],
    species: ["꽃사슴", "한라말", "토끼", "양", "염소"],
    sources: [["공식 동물농장 안내", "https://fathersgarden.co.kr/animal"], ["비짓제주 농원 소개", visitJeju("CNTS_200000000014189")]],
  },
  {
    id: "halla-eco-forest", name: "한라생태숲", kind: "park", type: "생태숲", animalContext: "야생 관찰", zone: "jeju",
    area: "제주시 516로 2596", destination: [33.430924, 126.595761], pixel: [780.1, 361.5],
    species: ["족제비", "큰유리새", "제비딱새", "왜가리", "비바리뱀"],
    sources: [["현장 관찰 기록", "https://www.jejubirds.com/2026/09/2026-09-13-halla-eco-forest.html"], ["비짓제주 생태숲 안내", visitJeju("CONT_000000000500688")]],
  },
  {
    id: "arboretum", name: "한라수목원", type: "수목원", zone: "jeju", kind: "park",
    area: "제주시 연동 · 수목원길 72", pixel: [604.7, 282.8], search: "제주 한라수목원 주차장",
    csvName: "한라수목원", species: ["노루", "큰오색딱다구리"],
    description: "제주시 연동에서 나무가 우거진 수목원 산책로를 걷는 코스입니다. 숲 가장자리와 나무줄기를 차례로 살펴보세요.",
    access: "수목원길 72의 한라수목원 주차장에서 산책을 시작하세요.",
    sources: [["비짓제주 수목원 안내", visitJeju("CONT_000000000500689")]],
    evidence: {
      "노루": ["사진 관찰", "2026년 3월 1일, 수목원 숲에서 촬영된 야생 노루 기록입니다.", "https://www.inaturalist.org/observations/340699023"],
      "큰오색딱다구리": ["사진 관찰", "같은 날 수목원에서 사진이 남은 딱다구리입니다. 산책로에서 나무줄기를 살펴보세요.", "https://www.inaturalist.org/observations/340699038"],
    },
  },
  {
    id: "cheonjiyeon", name: "천지연폭포", type: "폭포 산책", zone: "seogwipo", kind: "sight",
    area: "서귀포시 · 남성중로 2-15", pixel: [717.8, 736.8], search: "서귀포 천지연폭포 매표소",
    csvName: "천지연폭포", species: ["원앙", "무태장어"],
    description: "매표소에서 난대림 산책로를 따라 걸으면 폭포 아래 깊은 연못에 닿습니다. 가는 길에는 계곡의 수면도 살펴보세요.",
    access: "길찾기에서 천지연폭포 매표소를 선택하세요. 지도 핀은 관광지 대표 위치를 가리킵니다.",
    sources: [["서귀포시 관람 안내", "https://eticket.seogwipo.go.kr/contents?bmcode=cheonjiyeon"], ["비짓제주 폭포 안내", visitJeju("CONT_000000000500618")]],
    evidence: {
      "원앙": ["현장 취재", "한국관광공사 여행기에 천지연 계곡에서 헤엄치는 원앙이 소개됐습니다.", "https://korean.visitkorea.or.kr/detail/rem_detail.do?cotid=e070d872-0f55-491a-af79-2ba1e83e6ad0"],
      "무태장어": ["서식 확인", "폭포 아래 연못은 무태장어 서식지입니다. 낮에는 깊은 곳에 숨어 산책로에서 보기 어렵습니다.", visitJeju("CONT_000000000500618")],
    },
  },
  {
    id: "gotjawal", name: "제주곶자왈도립공원", type: "도립공원", zone: "seogwipo", kind: "park",
    area: "서귀포시 대정읍 · 에듀시티로 178", pixel: [229.8, 656.9], search: "제주곶자왈도립공원 탐방안내소",
    csvName: "제주곶자왈도립공원", species: ["동박새", "노루"],
    description: "대정읍의 용암 숲을 걷는 도립공원입니다. 탐방안내소에서 코스를 고른 뒤 나뭇가지 사이로 움직이는 새를 찾아보세요.",
    access: "탐방안내소와 방문객 주차장은 에듀시티로 178에 있습니다. 일부 숲길은 돌이 드러난 비포장 구간입니다.",
    sources: [["공원 오시는 길", "https://www.jejugotjawal.or.kr/kor/index.php?page_id=8"], ["비짓제주 도립공원 안내", visitJeju("CNTS_000000000021111")]],
    evidence: {
      "동박새": ["현장 조사", "2023년 5월 24일 국립중앙과학관 조사 자료에 이곳의 동박새가 기록됐습니다.", "https://www.gbif.org/occurrence/4938771589"],
      "노루": ["공원 서식 자료", "공원 운영기관이 곶자왈에 서식하는 포유류로 소개합니다.", "https://www.jejugotjawal.or.kr/kor/index.php?page_id=6"],
    },
  },
  {
    id: "gwangchigi", name: "광치기해변", type: "해안 산책", zone: "seogwipo", kind: "sight",
    area: "서귀포시 성산읍 · 섭지코지로 63", pixel: [1336.1, 319.4], search: "성산 광치기해변 주차장",
    csvName: "광치기해변", species: ["뿔논병아리"],
    description: "성산일출봉을 바라보는 동쪽 해변입니다. 모래사장에서 앞바다의 수면을 쌍안경으로 살펴보기 좋습니다.",
    access: "해변 주차장을 찾아 해안으로 걸어가세요. 제주올레 1코스와 2코스가 이어지는 지점입니다.",
    sources: [["비짓제주 해변 안내", visitJeju("CNTS_000000000018413")]],
    evidence: {"뿔논병아리": ["날짜가 있는 관찰", "2023년 3월 3일, 탐조자가 광치기해변 앞바다에서 무리를 관찰하고 사진을 남겼습니다.", "https://www.birdskoreablog.org/?p=26888"]},
  },
  {
    zone: "jeju", kind: "sight",
    id: "dongbaek", name: "동백동산 습지센터", type: "안내센터", area: "제주시 조천읍 · 동백로 77",
    pixel: [982.6, 203.7], destination: [33.5095241, 126.7152691],
    species: ["제주도롱뇽"], habitat: "dongbaek",
    description: "선흘리 곶자왈을 처음 찾는다면 습지센터에서 출발하세요. 센터에서 탐방로와 마을 해설을 안내받을 수 있습니다.",
    access: "길찾기는 습지센터로 연결됩니다. 숲길에는 돌과 나무뿌리가 있는 구간이 있습니다.",
    sources: [["습지센터 안내", hub(471)], ["센터 위치", visitJeju("CNTS_300000000014390")]],
  },
  {
    zone: "seogwipo", kind: "sight", csvName: "신도포구",
    id: "sindo", name: "신도포구 돌고래소공원", type: "해안 전망", area: "서귀포시 대정읍 · 신도리 해안",
    pixel: [59.2, 669.5], destination: [33.27611896, 126.170891], destinationName: "신도포구 돌고래소공원 주차장",
    species: ["남방큰돌고래"], habitat: "sindo",
    description: "서쪽 해안에서 바다를 바라볼 수 있는 작은 공원입니다. 신도리 연안에는 남방큰돌고래가 모습을 드러낸 기록이 있습니다.",
    access: "공원 주차장에서 시작해 육상 전망 지점을 이용하세요. 산책로 일부에는 계단이 있습니다.",
    sources: [["접근로·주차장", hub(694)], ["비짓제주 돌고래 관찰 안내", visitJeju("CNTS_300000000013099")]],
  },
  {
    zone: "seogwipo", kind: "sight", csvName: "천백고지 휴게소",
    id: "highland", name: "1100고지습지 탐방로", type: "탐방 데크", area: "한라산 1100도로 · 고산습지",
    pixel: [554.9, 508.3], destination: [33.35762241, 126.4629887],
    species: ["제주도롱뇽", "황조롱이"], habitat: "highland",
    description: "1100도로에서 습지 탐방 데크로 이어집니다. 나무 데크를 따라 걸으며 고산습지의 작은 물웅덩이와 주변 숲을 살펴볼 수 있습니다.",
    access: "길찾기는 제주데이터허브가 조사한 탐방로 시작 구간으로 연결됩니다. 출발 전 1100도로 통행 상황을 확인하세요.",
    sources: [["탐방로 조사", hub(649)], ["람사르 습지 정보", ramsar(1893)]],
  },
  {
    zone: "jeju", kind: "sight",
    id: "hado", name: "하도리 철새도래지", type: "철새 관찰", area: "제주시 구좌읍 · 하도리 해안습지",
    pixel: [1288.4, 202.9], search: "하도리 철새도래지 탐조대",
    grade: "대표 지점은 1등급이며 반경 500m에는 1·2등급 구역이 있습니다.",
    species: ["저어새", "청둥오리"], habitat: "hado",
    description: "겨울 물새를 보고 싶다면 하도리의 공개 탐조 구역을 찾아보세요. 갈대 가장자리와 얕은 물가를 쌍안경으로 살펴보기 좋습니다.",
    access: "지도에는 철새도래지 대표 위치를 표시했습니다. 아래 버튼에서 탐조대를 검색할 수 있습니다.",
    sources: [["비짓제주 철새도래지", visitJeju("CNTS_000000000022753")], ["철새 자료", visitJeju("CONT_000000000500610")]],
  },
  {
    zone: "seogwipo", kind: "park",
    id: "park", name: "제주자연생태공원", type: "생태공원", animalContext: "보호·사육 동물", area: "서귀포시 성산읍 · 금백조로 446",
    pixel: [1165.4, 344.8], destination: [33.43972902, 126.823609], destinationName: "제주자연생태공원 주출입구",
    grade: "주출입구에는 등급 도형이 없습니다. 반경 500m에는 2·3등급 구역이 있습니다.",
    species: ["노루", "독수리", "반달가슴곰", "수리부엉이", "매"],
    description: "제주도 무장애여행 안내서에 소개된 보호·사육 관찰장입니다. 안내서에 실린 동물을 도감에서 미리 살펴볼 수 있습니다.",
    access: "길찾기는 공원 주출입구로 연결됩니다. 전망대 방향에는 약 9° 오르막이 있습니다. 당일 관람 가능한 동물은 공원(064-787-4711)에 확인하세요.",
    sources: [["제주도 무장애여행 안내", hub(740)], ["한국관광공사 공원 소개", "https://korean.visitkorea.or.kr/detail/ms_detail.do?cotid=127bc76f-f4d6-4a45-9cc3-d0d91a84a59f"]],
  },
  {
    zone: "seogwipo", kind: "sight",
    id: "oreum", name: "물영아리오름", type: "오름 탐방", area: "서귀포시 남원읍 · 수망리",
    pixel: [942.9, 487.7], search: "물영아리오름 주차장",
    species: ["맹꽁이", "물장군"], habitat: "oreum",
    description: "수망리에서 지정 탐방로를 따라 분화구 습지를 둘러보는 코스입니다. 정상으로 향하는 길에는 계단과 오르막이 있습니다.",
    access: "지도에는 오름의 위치를 표시했습니다. 차량으로 갈 때는 아래 버튼에서 탐방 주차장을 찾아 출발하세요.",
    sources: [["제주도 오름 안내", "https://www.jeju.go.kr/is/oreum/info/jejuoreum/view.wp?menuId=MENU000000000000274&oreumId=145"], ["습지 자료", ramsar(1648)]],
  },
  ...tourismSites.map(place => ({ ...place, species: [] })),
];

export const animalVisits = visits.filter(place => place.animalContext);

export const habitats = [
  { id: "dongbaek", name: "동백동산습지", type: "곶자왈 습지", area: "제주시 조천읍 선흘리", pixel: [982, 215], species: ["제주도롱뇽"],
    description: "람사르 자료에는 선흘 곶자왈 습지에 제주도롱뇽이 서식한다고 기록되어 있습니다.", sources: [["람사르 습지 자료", ramsar(1947)], ["제주 습지보호지역 현황", hub(932)]] },
  { id: "highland", name: "1100고지습지", type: "고산습지", area: "1100고지 습지보호지역", pixel: [561, 521], species: ["제주도롱뇽", "황조롱이"],
    description: "습지보호지역 관리계획 조사에 제주도롱뇽과 황조롱이가 수록되어 있습니다.", sources: [["습지보호지역 관리계획", "https://rsis.ramsar.org/RISapp/files/28411793/documents/KR1648_mgt1605.pdf"], ["제주 습지보호지역 현황", hub(932)]] },
  { id: "sindo", name: "신도리 연안", type: "연안 해역", area: "서귀포시 대정읍 서쪽 바다", pixel: [54, 675], species: ["남방큰돌고래"],
    description: "비짓제주는 신도리 해안 일대를 남방큰돌고래를 육상에서 볼 수 있는 해안으로 소개합니다. 돌고래는 연안을 따라 이동합니다.", sources: [["비짓제주 해안 안내", visitJeju("CNTS_300000000013099")]] },
  { id: "hado", name: "하도리 해안습지", type: "철새 월동지", area: "제주시 구좌읍 하도리", pixel: [1288.4, 202.9], species: ["저어새", "청둥오리"],
    description: "공개 관광 자료에 저어새와 청둥오리가 찾는 철새도래지로 소개되어 있습니다.", sources: [["하도리 철새 자료", visitJeju("CONT_000000000500610")], ["제주 탐조 안내", visitJeju("CNTS_000000000022753")]] },
  { id: "oreum", name: "물영아리오름습지", type: "분화구 습지", area: "서귀포시 남원읍 수망리", pixel: [942.9, 487.7], species: ["맹꽁이", "물장군"],
    description: "제주도 습지보호지역 현황(2020년 기준)에 맹꽁이와 물장군의 서식이 기록되어 있습니다.", sources: [["제주 습지보호지역 현황", hub(932)], ["람사르 습지 자료", ramsar(1648)]] },
  { id: "muljangori", name: "물장오리오름습지", type: "보호습지", area: "제주시 봉개동", pixel: [841, 424], species: ["팔색조"],
    description: "제주도 습지보호지역 현황(2020년 기준)에 팔색조의 서식이 기록되어 있습니다. 지도는 습지 권역을 표시합니다.", sources: [["제주 습지보호지역 현황", hub(932)], ["람사르 습지 자료", ramsar(1847)]] },
  { id: "jikgu", name: "추자군도 직구도", type: "무인도서", area: "제주시 추자면 · 제주 본섬 북쪽", offshore: true, species: ["매", "벌매", "섬개개비"],
    description: "직구도의 특정도서 자료에 매·벌매·섬개개비가 기록되어 있습니다. 추자군도에 속한 무인도서로, 본섬 지도와 따로 표시합니다.", sources: [["제주데이터허브 특정도서 자료", hub(508)], ["특정도서 지정 자료", "https://www.aurum.re.kr/research/PostView.aspx?cpage=707&gid=0&mm=1&pid=25756&ss=1"]] },
];

export function guideUrl(id, name) {
  const region = id.startsWith("KTSN-") ? "national" : "jeju";
  return "/?" + new URLSearchParams({ region, species: id, q: name });
}

export function mapUrl(layer, place) {
  return "/map?" + new URLSearchParams({ layer, place });
}
let recordRequest;
export function loadRecords() {
  if (!recordRequest) {
    recordRequest = fetch("/observations.json").then((response) => {
      if (!response.ok) throw Error("Could not load observation records");
      return response.json();
    }).catch((error) => {
      recordRequest = undefined;
      throw error;
    });
  }
  return recordRequest;
}

export function pinUrl(layer, id, animals = false) {
  const url = new URL("/map", location.origin);
  if (layer !== "visit") url.searchParams.set("layer", layer);
  if (id) url.searchParams.set("pin", id);
  if (animals) url.searchParams.set("show", "animals");
  return url.pathname + url.search;
}

let nearbyRequest;
export function loadNearby() {
  nearbyRequest ??= fetch("/nearby.json").then(response => {
    if (!response.ok) throw Error("Missing observations");
    return response.json();
  }).catch(error => { nearbyRequest = null; throw error; });
  return nearbyRequest;
}
