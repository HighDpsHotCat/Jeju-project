# 제주 종 조회 API

Python 3에서 저장소 최상위 폴더를 열고 다음 명령을 실행합니다.

    python backend/server.py

브라우저에서 http://127.0.0.1:8765/ 를 열면 국명, 학명, 종 ID로 검색할 수 있습니다. 시작할 때 저장소의 CSV를 읽어 로컬 SQLite DB를 갱신합니다.

- GET /species/JJ-003554 — 가마우지의 전체 종 정보. 없는 ID는 HTTP 404입니다.
- GET /species?q=가마우지 — 부분 일치 결과를 ID 순서로 최대 30건 반환합니다.

Flutter는 단건 응답의 species_id, korean_name, scientific_name을 사용합니다. 국명이 비어 있으면 학명을 표시합니다.
같은 Wi-Fi의 휴대폰에서 테스트하려면 서버에 --host 0.0.0.0 옵션을 주고 PC의 LAN IP와 포트 8765를 사용하세요.
