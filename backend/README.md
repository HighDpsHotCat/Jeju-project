# 제주 종 조회 API

## 동물별 관찰 장소 추천

`GET /recommendations?name=저어새` 또는 `GET /recommendations?species_id=JJ-000837`를 호출합니다. 응답의 places에는 해당 종이 공식 자료에 명시된 공개 관찰 장소, 추천 이유, 탐방 안내와 근거 링크가 담깁니다. kind=wild는 야생 서식·월동 기록, managed는 보호·사육 시설입니다. 좌표는 공개 장소의 대표 위치이고 point_grade는 기존 지도에서 판정한 그 지점의 생태·자연도 등급입니다. 등급 도형에 속하지 않으면 null입니다.

regional_records는 GBIF 기록을 행정시와 등급별로 집계한 값입니다. 이 값만으로 특정 관광지에 종을 배정하지 않습니다. 두 근거는 독립적이므로 각각 빈 배열일 수 있습니다. 국명이 중복되면 409와 후보 ID를 반환합니다. 오프라인 DB에서는 place_species와 observation_place를 place_id로 연결하고 species_region_grade로 행정시 기록을 읽습니다.

## UI 연결 전 관찰 저장 계약

공모전 앱은 기기 DB를 사용합니다. 개발 단계에서는 아래 경로로 가상 사용자와 관찰을 생성해 홈·목록의 변화를 확인할 수 있습니다. 저장 요청은 로컬 서버의 `--demo-catalog` 옵션에서만 활성화됩니다. 요청 본문은 JSON입니다.

| HTTP | 경로 | 동작 |
|---|---|---|
| PUT | /guide/profiles/demo | 빈 객체 `{}`로 기기 ID 초기화. 최초 201, 재실행 200 |
| PUT | /guide/profiles/demo/observations/o1 | 촬영 정보 등록. 최초 201, 동일 요청 200, 기존 ID와 충돌하면 409 |
| GET | /guide/profiles/demo/observations/o1 | 해당 사용자의 관찰 상세 |
| GET | /guide/profiles/demo/observations | 일반 관찰 이력. species_id, limit(1~100), offset 지원 |
| PATCH | /guide/profiles/demo/observations/o1 | identification, species_id 두 필드로 종 정정 |
| DELETE | /guide/profiles/demo/observations/o1 | 관찰 행 삭제. 존재하지 않으면 404 |

등록할 가상 기록 예시:

    {"photo_id":"p1","photo_ref":"photos/p1.jpg","captured_at":"2026-11-12T09:00:00+09:00","gps_at":"2026-11-12T09:00:00+09:00","latitude":33.4,"longitude":126.6,"accuracy_m":5,"boundary_version":"fixture","capture_source":"camera","location_verdict":"inside","mode":"general","identification":"unknown","species_id":null}

등록 후 `{"identification":"confirmed","species_id":"JJ-000837"}`로 PATCH하고 `/guide/home?user_id=demo` 및 `/guide/entries?user_id=demo&q=노루`를 조회하세요. 종 수와 등록 여부는 관찰 행에서 계산됩니다. research_only 관찰은 일반 이력과 진행도에서 제외됩니다. photo_ref는 기기 파일 참조입니다. 이 모의 API는 파일을 전송·삭제하거나 GPS로 제주 경계를 직접 계산하지 않습니다.

저장소 최상위에서 `python -m unittest discover -s backend -p test_observations.py`로 실제 HTTP 경로를 통한 회귀 테스트를 실행할 수 있습니다.

## 종별 관찰 지역과 생태·자연도 등급

`GET /species/JJ-000837/region-grades`는 노루의 지역·등급별 관찰 건수(groups)와 근거 기록(records)을 반환합니다. `/guide/entries`와 기기용 `guide_entries.sql`에도 region_grades가 포함됩니다(HTTP 배열, SQLite JSON 문자열). 자료가 없는 종은 빈 배열입니다. grade=9의 표시명은 별도관리지역입니다. 공개 관찰 자료는 개인 발견 수와 등록 상태에 포함되지 않습니다.

2026-11-12 작성한 `data/region_grade_records.csv`는 제주 종 기준표 4,875종 가운데 학명이 유일하게 일치하고 좌표를 판정할 수 있었던 36종, GBIF 기록 98건입니다. GBIF Occurrence Search v1에서 country=KR, kingdomKey=1, basisOfRecord=HUMAN_OBSERVATION, occurrenceStatus=PRESENT, hasCoordinate=true, hasGeospatialIssue=false, coordinateUncertaintyInMeters=1,100, 경도 125.95~127.05·위도 33.05~34.05로 검색하고 CC0 및 CC BY 4.0 결과의 gbifID를 중복 제거했습니다. 관찰일이 예전인 기록에도 아래 동일 고시본을 대조했습니다. 등급은 그 종의 서식 확률이나 관찰 당시 등급을 나타내지 않습니다.

관찰 좌표를 EPSG:5186으로 변환한 뒤, 보고된 수평 오차 반경이 하나의 등급 도형과 하나의 시 경계 내부에 들어오는 기록만 채택했습니다. 시 경계·등급 경계에 걸치거나 자료가 겹치는 경우는 제외했습니다. 정확한 좌표 대신 행정시, 등급, 등급 도형 식별자, 출처 정보를 배포합니다.

- 등급 도형: 국립생태원 에코뱅크, 제주특별자치도 정기고시 자료(기후에너지환경부 고시 제2026-128호). [SHP 원본](https://www.nie-ecobank.kr/ecoapi/opn/file/download.do?extrcId=1086426)
- 행정시 경계: geoBoundaries / citypopulation.de, gbOpen KOR ADM2, 2020, CC BY 3.0. [사용한 경계 버전](https://github.com/wmgeolab/geoBoundaries/tree/9469f09/releaseData/gbOpen/KOR/ADM2)
- 종별 관찰 기록: GBIF 원 제공자와 데이터셋 이름, 라이선스는 CSV 및 상세 API의 attribution, source_dataset, license_url에 보관합니다. 각 행의 gbif_id는 https://www.gbif.org/occurrence/{gbif_id}, dataset_key는 https://www.gbif.org/dataset/{dataset_key} 에 대응합니다. 공간 대조 결과는 프로젝트에서 산출했습니다.

기존 기기 DB는 Python에서 `guide.upgrade(Path(...))`를 실행하면 개인 관찰을 유지한 채 참조 데이터를 갱신합니다. Flutter에서 기존 DB를 직접 마이그레이션할 때는 새로운 배포 DB에서 region_grade_record 테이블(내용 포함), grade_species_area 인덱스와 species_region_grade 뷰를 이식한 뒤 새 목록 쿼리를 사용합니다.

## 홈과 개인 도감 조회

Flutter는 기기 SQLite에서 홈과 목록을 읽을 수 있습니다. 새 배포 DB에는 조회 구조가 포함됩니다. 기존 기기 DB(전국 목록 포함)는 앱 DB 업그레이드 단계에서 `guide_schema.sql`을 적용합니다. 외부 트랜잭션 안에서 적용할 때는 파일의 BEGIN/COMMIT을 제외하세요.

- 홈은 `SELECT species_count, observation_count FROM collection_progress WHERE user_id=?`로 조회합니다. 프로필만 있고 관찰이 없는 사용자는 두 값 모두 0입니다.
- 목록은 `guide_entries.sql`의 이름 있는 인자 user_id, region, taxon, q, limit, offset을 바인딩합니다. region은 jeju 또는 national, taxon은 전체일 때 빈 문자열, 포유류·조류·곤충은 Mammalia·Aves·Insecta입니다. q는 국명·학명·ID 부분 검색입니다.
- 기본값인 `region=jeju`는 제주 종 기준표의 원자료 관찰 건수(`source_occurrence_count`)가 양의 정수인 종을 표시합니다. 개인의 등록 여부는 목록 포함 조건과 별개입니다. `region=national`은 국가 목록의 국명·학명·분류를 사용합니다.
- registered는 확인된 일반 관찰이 있으면 참입니다. photo_ref는 해당 사용자의 최신 사진 참조이며 사진이 없으면 null입니다. 촬영 시각은 같은 UTC ISO 8601 형식으로 저장합니다.
- 전국 목록은 국가생물종목록의 동물 분류군만 사용합니다. 학명·국명이 양쪽에서 유일하게 대응하고 이름 검토가 완료된 항목은 제주 ID를 공유합니다. 나머지 전국 항목은 KTSN- 접두 ID를 사용합니다.

개발 서버에서 가상 사용자와 관찰을 담은 DB로 확인할 때는 `python backend/server.py --demo-catalog build/jeju_catalog.sqlite3`로 실행하세요. user_id에는 테스트 DB의 device_profile에 있는 ID를 사용합니다. 신규 DB에는 가상 사용자를 자동 생성하지 않습니다.

    GET /guide/home?user_id=demo
    GET /guide/entries?user_id=demo&region=jeju&class=Aves&limit=20&offset=0
    GET /guide/entries?user_id=demo&region=national&q=노루

기기용 SQL과 개발용 HTTP 응답은 같은 쿼리를 사용합니다. 실제 개인 사진과 위치는 기기 안에 보관합니다.

## 생태자연도 전체 데이터 동기화

제주데이터허브에 로그인해 [생태자연도 정보](https://www.jejudatahub.net/data/view/data/864)를 프로젝트에 담습니다. 마이페이지 → 프로젝트 관리 → 해당 프로젝트에서 프로젝트키를 확인합니다. [공식 API 가이드](https://jejudatahub.net/api/file?id=2502&fileId=4644)

PowerShell 7에서 저장소 루트를 열고 다음을 실행합니다. 입력한 키는 해당 터미널 세션의 환경변수에 설정됩니다.

    $env:JEJU_DATAHUB_PROJECT_KEY = Read-Host "제주데이터허브 프로젝트키" -MaskInput
    python backend/ecology.py
    python backend/server.py

동기화 명령은 필터 없이 페이지당 100건씩 조회해 전체 건수가 맞으면 backend/ecology.sqlite3에 반영합니다. 갱신할 때도 같은 명령을 사용합니다. 실패하면 기존 결과가 유지됩니다. 서버는 키 없이 마지막 동기화본을 읽습니다. 배포 환경에서는 동기화 작업의 비밀 환경변수에 키를 등록하고 서버와 같은 영구 저장소를 사용합니다.

- 전체 첫 페이지: `GET /ecology?number=1&limit=100`
- 습지 1등급: `GET /ecology?wetlandRank=1`
- 응답: totCnt(필터 적용 건수), hasMore(다음 페이지 유무), data(원본 레코드 배열). 최초 동기화 전에는 HTTP 503입니다.
- syncedAt은 DB 전체를 마지막으로 정상 갱신한 UTC 시각입니다. 기존 DB는 다음 동기화까지 null을 반환합니다. 갱신에 실패하면 이전 데이터와 시각을 함께 유지합니다.

## 공모전 앱의 오프라인 DB

빌드 전 `python -m pip install openpyxl`을 실행합니다. 생성되는 DB의 species는 제주 종 기준표, national_species는 국립생물자원관 국가생물종목록 v1.0의 통합 시트입니다. 분류군별 시트는 통합 시트와 중복되어 별도로 합산하지 않습니다. 전국 종 수를 제주 관찰 종 수로 집계하지 않습니다.

ecology_preview에는 [제주데이터허브 생태자연도 정보](https://www.jejudatahub.net/data/view/data/864)의 [공식 미리보기 CSV](https://jejudatahub.net/api/file?id=4877&fileId=2143) 20행을 원본 순서로 담습니다. 제공처는 국립생태원 에코뱅크이며 허브가 표시한 데이터 기준일은 2022-06-29입니다. 유사한 행도 원자료대로 유지합니다. 습지 1등급 예시는 `SELECT * FROM ecology_preview WHERE 습지평가등급='1'`로 확인합니다. 전체 자료는 프로젝트 인증이 필요한 API로 제공됩니다. 미리보기에는 관찰 좌표와 등급 구역 경계가 없습니다.

저장소 최상위에서 `python backend/build_catalog.py build/jeju_catalog.sqlite3`를 실행하면 CSV 종 목록과 빈 개인 도감 테이블이 들어 있는 배포용 SQLite 파일을 만듭니다. 출력 경로는 새 파일이어야 합니다. Flutter 담당자는 이를 앱 자산에 포함하고 최초 실행 시 기기 전용 폴더에 한 번 복사합니다. 이후에는 그 복사본에 연결하고 매 연결에서 `PRAGMA foreign_keys=ON`을 실행합니다.

앱은 기기 사용자 ID를 생성해 device_profile에 넣습니다. observation에는 앱 카메라에서 촬영하고 제주 안으로 판정된 건만 저장합니다. GPS 시차·오차·섬 경계의 실제 판정은 앱에서 수행합니다. photo_ref에는 앱 전용 폴더의 사진 참조를 저장합니다. 미확인 종은 identification='unknown', species_id=NULL로 둡니다. 연구소 전용 건은 mode='research_only'입니다.

`SELECT * FROM collection_progress WHERE user_id=?`는 일반 관찰 건수와 확인된 종 수를, species_card는 종별 카드 목록을 반환합니다. 정정·삭제 직후 다시 읽으면 현재 값이 반영됩니다. 개인 기록은 기기 DB에만 저장하고, 아래 검색 API는 별도의 개발용 도감 조회 서버로 사용합니다.

## 개발용 검색 서버

Python 3에서 저장소 최상위 폴더를 열고 다음 명령을 실행합니다.

    python backend/server.py

브라우저에서 http://127.0.0.1:8765/ 를 열면 국명, 학명, 종 ID로 검색할 수 있습니다. 시작할 때 저장소의 CSV를 읽어 로컬 SQLite DB를 갱신합니다.

- GET /species/JJ-003554 — 가마우지의 전체 종 정보. 없는 ID는 HTTP 404입니다.
- GET /species?q=가마우지 — 부분 일치 결과를 ID 순서로 반환합니다. 기본 30건입니다.
- GET /species?limit=20&offset=20 — 전체 목록의 21번째부터 최대 20건을 반환합니다. 검색어와 함께 사용할 수 있습니다. limit 범위는 1~100이며 offset은 0 이상의 부호 있는 64비트 정수입니다. 잘못된 값은 HTTP 400, 마지막 페이지 이후는 빈 배열입니다.

CSV에 필수 열이나 종 ID가 없거나 데이터가 비면 가져오기를 중단하고 기존 DB를 보존합니다. 중복 종 ID도 허용하지 않습니다.

Flutter는 단건 응답의 species_id, korean_name, scientific_name을 사용합니다. 국명이 비어 있으면 학명을 표시합니다.
같은 Wi-Fi의 휴대폰에서 테스트하려면 서버에 --host 0.0.0.0 옵션을 주고 PC의 LAN IP와 포트 8765를 사용하세요.
