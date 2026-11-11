# 제주 종 조회 API

## 공모전 앱의 오프라인 DB

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
