# Jeju-project

제주 AI 에이전트 공모전 팀의 Flutter 앱입니다.

## 로컬 실행

팀원은 GitHub 초대를 수락한 계정으로 아래 명령을 실행하세요. 이 프로젝트에서 사용한 Flutter 버전은 `3.47.9`입니다.

```sh
git clone https://github.com/HighDpsHotCat/Jeju-project.git
cd Jeju-project
flutter pub get
flutter run
```

기본 위젯 테스트는 `flutter test`로 실행합니다.

## 마당과 동물 도감

![Android 앱에서 예시 기록을 켠 마당과 동물 도감](docs/explorer-preview.jpg)

마당 하단의 **도감**에서 우측 상단 메뉴로 목록을 전환합니다. 기본 **제주 목격 생물**은 제주 종 기준표의 원자료 관찰 건수가 1건 이상인 4,875종입니다. **전국 도감**은 2025년 국가생물종목록에 등재된 동물 34,875종을 해당 목록의 국명·학명·분류로 표시합니다. 전환해도 검색어와 분류 선택은 유지되며, 발견한 동물 수는 선택한 목록 기준입니다. 두 자료의 학명·국명이 유일하게 대조된 종은 같은 발견 기록을 사용합니다. 원자료를 갱신할 때는 Python 환경에 `openpyxl`을 설치하고 `python3 tool/build_guide_catalog.py`를 실행하세요.

**설정 → 예시 기록 보기**를 켜면 12종이 등록된 시안을 체험할 수 있습니다. 기본 화면은 기기 기록을 표시합니다. 모바일 앱은 SQLite 데이터베이스 디렉터리의 `jeju_catalog.sqlite3`에서 `species_card` 뷰를 읽습니다. `device_profile.user_id` 환경설정 값이 있으면 그 사용자를, 없으면 기기의 단일 프로필을 사용합니다.

**카메라**에서는 현장 사진을 촬영하고 미리 볼 수 있습니다. 사진의 제주 위치 검증과 종 식별을 거쳐 도감에 쓰는 과정은 후속 연동 범위입니다.

## 공유 웹 도감과 관찰 지도

[동물 도감](https://jeju-wildlife-field-guide.gimhoyeon.chatgpt.site/) · [제주 관찰 지도](https://jeju-wildlife-field-guide.gimhoyeon.chatgpt.site/map)

공유 웹 화면의 원본은 `field_guide/`에 있습니다. 저장소 루트에서 `python field_guide/preview.py`를 실행하고 `http://127.0.0.1:8787`을 열면 현재 CSV와 앱의 전국 종 색인으로 확인할 수 있습니다. 탐험 설정에서 예시 기록을 켤 수 있습니다. 웹에서 선택한 사진은 해당 브라우저에서만 미리 봅니다.

앱 마당의 **관찰 지도**에서 같은 지도를 엽니다. **탐방 장소**는 안내센터·공원·탐조대·탐방로 등 6개 출발점과 길찾기 또는 장소 검색을 제공합니다. **서식 기록**은 7개 권역의 문헌 출처와 관련 동물을 보여줍니다. **관찰 기록**은 날짜가 있는 GBIF 기록을 행정시·종별로 볼 수 있습니다. 동물 이름을 누르면 제주 또는 전국 도감의 해당 종 카드가 열립니다. 도감 카드에서도 연결된 장소와 기록으로 돌아갈 수 있습니다. 생태·자연도는 지도 아래에서 선택해 겹쳐 봅니다.

장소와 서식 자료의 출처는 `field_guide/map-data.js`에 있습니다. 관찰 자료는 백엔드 브랜치 `feat/3-api-species-lookup`의 `backend/data/region_grade_records.csv`를 사용합니다. 갱신하려면 이 브랜치의 루트에서 `python tool/build_map_records.py <CSV경로>`를 실행하세요. 공개 자료에는 행정시, 종, 날짜, GBIF 원문 ID, 데이터셋, 제공자 및 이용허락을 포함합니다.
