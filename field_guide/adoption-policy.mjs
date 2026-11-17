// Game eligibility uses the Jeju guide's names. The common-species list can be
// tuned without changing the observation catalog or the photo identifier.
const common = new Set(`개 고양이 말 소 염소 노루 꽃사슴 다람쥐 생쥐 집쥐 등줄쥐 제주등줄쥐 애급쥐 굴토끼 집박쥐
참새 직박구리 멧비둘기 집비둘기 양비둘기 까치 까마귀 큰부리까마귀 떼까마귀 동박새 박새 제비 방울새 꿩
흰뺨검둥오리 청둥오리 괭이갈매기 재갈매기 갈매기 왜가리 쇠백로 중대백로 바다직박구리 딱새 곤줄박이
섬휘파람새 알락할미새 노랑할미새 청개구리 참개구리 무당개구리 황소개구리 잉어 붕어 고등어 멸치
소라 고둥 전복 따개비 갯강구 보름달물해파리`.split(/\s+/));
const stagGenera = new Set('Lucanus Dorcus Prosopocoilus Prismognathus Figulus Nigidius Platycerus Aegus Ceruchus Neolucanus'.split(' '));
export function eligible(row) {
  if (row.in_guide === false || common.has(row.korean_name)) return false;
  if (row.class !== 'Insecta') return true;
  return row.korean_name.endsWith('장수풍뎅이') || row.korean_name.endsWith('사슴벌레') ||
    stagGenera.has(row.scientific_name.split(' ')[0]) ||
    /^(Trypoxylus|Allomyrina) dichotomus\b/.test(row.scientific_name);
}
