# 제주 동물 사진 판별

이 폴더에서 사진 경로를 넘기면 BioCLIP 2가 제주 도감의 학명 후보를 비교해 자동 등록 기준을 통과하면 상위 세 후보를, 보류하면 빈 목록을 JSON으로 출력합니다. 설치 후에는 인터넷이나 API 키 없이 동작합니다.

```sh
.venv/bin/python identify.py "../../assets/guide_photos/120000002157.jpg"
.venv/bin/python identify.py "/사진이 있는 폴더/동물.heic"
```

결과의 `name`은 도감 이름, `scientific`은 학명, `jeju_ids`는 연결된 도감 ID입니다. `similarity`는 사진과 학명의 코사인 유사도입니다. 후보 목록 안에서의 순위를 나타내므로 1위가 정답일 확률로 해석할 수 없습니다. 자동 등록은 1위 유사도가 0.70 이상이고, 사람·풍경·사물 등 배경 문구의 최고 점수보다 0.05 이상, 다른 종의 최고 점수보다 0.04 이상 높을 때만 허용합니다. 다른 종과의 비교에는 제주 도감에 없는 전국 목록의 종도 포함합니다. 이 기준은 현재 모델과 문구에 맞춘 값입니다.

첫 실행 때 도감의 학명과 배경 문구를 벡터로 만들어 `.model/`에 저장합니다. 다음 실행부터는 이 벡터를 재사용합니다. 도감 학명이나 입력 문구가 바뀌면 새로 계산합니다. Mac에서는 사용 가능한 경우 Apple GPU(MPS)를 사용합니다.

## 앱에서 사용

프로젝트 루트에서 다음 명령을 실행합니다.

```sh
flutter build web --no-web-resources-cdn
tool/bioclip2/.venv/bin/python tool/bioclip2/server.py --host 0.0.0.0
```

Mac에서는 `http://localhost:8765`, 같은 Wi-Fi의 휴대폰에서는 `http://<Mac의 LAN IP>:8765`를 엽니다. **카메라**에서 같은 동물의 사진을 1~5장 모아 **다음**을 누르면 함께 판별합니다. 각 사진에 동물이 보이고 여러 각도의 평균 점수가 한 종을 지지할 때 등록합니다. 특정 사진이 다른 종을 뚜렷하게 지지하면 등록을 보류합니다. 종 판별 전에 각 사진의 전체 구도에서 화면·종이의 테두리와 여러 영역에 걸쳐 반복되는 격자·색 간섭 무늬를 검사합니다. 흔적이 발견되면 해당 사진 번호를 반환하고 관찰 묶음의 등록을 보류합니다. 테두리나 무늬가 보이지 않는 재촬영은 통과할 수 있습니다. 웹앱과 API는 같은 주소에서 제공되며 서버는 판별 후 업로드에 사용한 임시 파일을 닫고 삭제합니다. 개인 Mac에서만 테스트하려면 `--host` 옵션을 생략하세요.

Android 에뮬레이터는 기본으로 `10.0.2.2:8765`, Mac 앱과 iOS 시뮬레이터는 `127.0.0.1:8765`를 사용합니다. Android 실기기에서 같은 Wi-Fi로 테스트하려면 Mac의 실제 IP를 넣어 `flutter build apk --debug --dart-define=BIOCLIP_SERVER=http://<Mac의 LAN IP>:8765`로 빌드합니다. 외부 네트워크에서 사용할 네이티브 배포판은 접속 가능한 HTTPS 서버 주소를 `--dart-define=BIOCLIP_SERVER=https://<호스트>`로 지정합니다.

## 다른 Mac에 설치

이 폴더에서 Python 3.13으로 실행합니다. 아래 명령은 공식 모델의 고정된 리비전에서 설정과 가중치를 받습니다. [공식 모델 카드](https://huggingface.co/imageomics/bioclip-2)

```sh
python3.13 -m venv .venv
.venv/bin/python -m pip install open_clip_torch==4.2.2 pillow-heif==1.8.1 starlette==1.10.0 uvicorn==0.54.0 python-multipart==0.0.33 opencv-python-headless==5.0.0.93
mkdir -p .model
base=https://huggingface.co/imageomics/bioclip-2/resolve/2957b322090f9cb17ae72c71981c7218a28d81e0
curl -fLsS --retry 3 "$base/open_clip_config.json" -o .model/open_clip_config.json
curl -fLsS --retry 3 "$base/open_clip_model.safetensors" -o .model/weights.part &&
  echo 'b7b2bf6fbc95799e42630e394cf95803892ab447c1a8ab629dbc82fbeaf7dfef  .model/weights.part' | shasum -a 256 -c - &&
  mv .model/weights.part .model/open_clip_model.safetensors
```

## 분양 지도 연결

현재 프로필에서 사진 판별 후 새로 등록한 제주 동물은 촬영 위치가 확인되면 분양 지도에 전달됩니다. 서버 실행 환경의 `ADOPTION_SIGNING_KEY` 또는 이 디렉터리의 비공개 `.adoption-key` 파일에 Sites의 동명 시크릿과 같은 값을 설정하세요. 이 키는 판별 서버와 지도 서버에서만 사용합니다. 새 판별 서버로 이전할 때도 같은 값을 설정해야 분양 스팟이 생성됩니다. 일반 도감 등록은 키 없이도 사용할 수 있습니다.

지도에서 다른 탐험가의 스팟을 선택한 뒤 24시간 안에 반경 2km에서 주변을 촬영하면 같은 종을 내 도감에 등록합니다. 앱에서 연 지도의 등록은 앱으로 돌아올 때 해당 프로필에 반영됩니다. 단독 브라우저의 도감은 해당 브라우저의 프로필을 사용합니다.
