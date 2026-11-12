# Jeju-project

제주 AI 에이전트 공모전 팀의 Flutter 앱입니다.

## 로컬 실행

팀원은 GitHub 초대를 수락한 계정으로 아래 명령을 실행하세요. 이 프로젝트에서 사용한 Flutter 버전은 `3.47.9`입니다.

```sh
git clone -b feat/on-device-identification https://github.com/HighDpsHotCat/Jeju-project.git
cd Jeju-project
python tool/prepare_identification.py
flutter pub get
flutter run
```

기본 위젯 테스트는 `flutter test`로 실행합니다.

## 카메라로 동물 식별하기

**카메라 → 사진 촬영 → 셔터 누르기**로 노루, 참새, 칠성무당벌레, 붉은귀거북, 청개구리 한 마리를 찍으면 앱이 사진의 픽셀로 동물군과 종을 추정합니다. **갤러리 사진으로 식별**도 같은 모델을 사용합니다. 후보를 누르면 도감 상세를 볼 수 있고 **이 종으로 확인** 또는 **미확인으로 두기**를 선택할 수 있습니다. 사진과 GPS는 기기에 남습니다.

**현장 기록 저장**을 누르면 그 시점의 AI 결과도 함께 저장합니다. 기록을 다시 열어 **다시 식별**하거나 후보를 정정한 뒤에는 **식별 결과 저장**을 누르세요. 갤러리와 웹에서 연 사진은 식별 시연에만 사용합니다. 휴대폰 없이 시험하려면 `python tool/prepare_identification.py --web`과 `flutter run -d chrome`을 차례대로 실행하고 **카메라 → 사진 열기**를 누르세요.

특징 추출 모델은 첫 빌드 전에 체크섬을 검증해 받습니다. Android 앱에는 모델을 포함하므로 설치 후 식별에는 인터넷이 필요 없습니다. [학습·평가 방법과 종별 결과](docs/AI_IDENTIFICATION_RESULTS.md)

## 마당과 동물 도감

![Android 앱에서 예시 기록을 켠 마당과 동물 도감](docs/explorer-preview.jpg)

마당 하단의 **도감**에서 국명·학명·종 ID로 검색하고 지역과 분류군을 고를 수 있습니다. 제주 종 기준표 4,875건과 국가생물종목록의 동물 34,875건을 앱에 포함합니다. 원자료를 갱신할 때는 Python 환경에 `openpyxl`을 설치하고 `python3 tool/build_guide_catalog.py`를 실행하세요.

**설정 → 예시 기록 보기**를 켜면 12종이 등록된 시안을 체험할 수 있습니다. 기본 화면은 기기 기록을 표시합니다. 모바일 앱은 SQLite 데이터베이스 디렉터리의 `jeju_catalog.sqlite3`에서 `species_card` 뷰를 읽습니다. `device_profile.user_id` 환경설정 값이 있으면 그 사용자를, 없으면 기기의 단일 프로필을 사용합니다.

**현장 촬영(Android·iOS)**: 하단 카메라 → 사진 촬영 → 위치 측정 상태 확인 → 셔터 누르기 → 미리보기 → 현장 기록 저장. 저장한 사진과 측정값은 카메라 화면의 **저장한 현장 기록**에서 다시 열 수 있습니다.

촬영 시각은 앱에서 셔터를 누른 UTC 시각이고, GPS 측정 시각은 기기가 제공한 위치값의 시각입니다. 셔터 이후 수신된 위치는 그 사진에 사용하지 않습니다. 두 시각, 위도·경도, 수평 정확도(m)를 사진과 함께 앱 전용 폴더 `field_captures/<photo_id>/`에 기록합니다. 화면에서는 기기의 현지 시각으로 표시합니다.

위치 권한 거부·서비스 꺼짐·측정 전에는 사진과 촬영 시각을 저장하며 좌표는 비워 둡니다. 셔터보다 15초 넘게 오래된 값 또는 미래 시각의 값은 측정 시각 차이가 있다고 표시합니다. 15초는 미리보기의 임시 신선도 기준입니다. 현장 사진의 종 확인은 그 기록에만 저장되고 개인 도감 수집 수는 그대로 유지됩니다.

iOS에서 Swift Package Manager로 빌드할 때는 `BYPASS_PERMISSION_LOCATION_ALWAYS=1 flutter build ios`로 앱 사용 중 위치 권한만 사용합니다.
