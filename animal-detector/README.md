# 제주 동물 사진 판별기

참새, 갈매기, 비둘기, 강아지, 고양이, 노루, 돌고래를 판별하는 독립 실행형 브라우저 앱입니다. animal-detector.html을 내려받아 Chrome에서 열고 사진을 선택하세요. 첫 실행 시 모델 약 180MB를 내려받으며 사진은 기기에서 처리합니다.

- [최종 판별기](animal-detector.html)
- [추가 학습 후 최종 시험 결과](animal-training-results.html)
- [초기 모델 실사진 시험 결과](animal-photo-test.html)
- [오프라인 앱, 학습 사진 및 초기 Caltech 데이터 다운로드](https://github.com/HighDpsHotCat/Jeju-project/releases/tag/animal-detector-v1.0.0)

최종 시험은 449장 중 383장(85.3%)에서 기대한 종 이름을 표시하거나 대상 외 사진을 올바르게 제외했습니다. 별도 위치 검증에서는 31장 중 12장(38.7%)에서 종과 위치(IoU 0.5 이상)를 모두 맞혔습니다. 학습 사진별 출처와 이용 조건은 해당 ZIP 안에 있습니다.
