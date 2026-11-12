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

마당 하단의 **도감**에서 국명·학명·종 ID로 검색하고 지역과 분류군을 고를 수 있습니다. 제주 종 기준표 4,875건과 국가생물종목록의 동물 34,875건을 앱에 포함합니다. 원자료를 갱신할 때는 Python 환경에 `openpyxl`을 설치하고 `python3 tool/build_guide_catalog.py`를 실행하세요.

**설정 → 예시 기록 보기**를 켜면 12종이 등록된 시안을 체험할 수 있습니다. 기본 화면은 기기 기록을 표시합니다. 모바일 앱은 SQLite 데이터베이스 디렉터리의 `jeju_catalog.sqlite3`에서 `species_card` 뷰를 읽습니다. `device_profile.user_id` 환경설정 값이 있으면 그 사용자를, 없으면 기기의 단일 프로필을 사용합니다.

**카메라**에서는 현장 사진을 촬영하고 미리 볼 수 있습니다. 사진의 제주 위치 검증과 종 식별을 거쳐 도감에 쓰는 과정은 후속 연동 범위입니다.
