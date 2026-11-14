# 레빗서바이벌 · Rabbit Survivor

농부 토끼가 씨앗을 발동하고 농기구를 진화시켜 용궁을 탈출하는 Unity 2D 픽셀 아트 뱀서라이크입니다. 이동만으로 몰려오는 바다 생물과 싸우며, 네 토끼의 시작 무기와 무작위 씨앗으로 매 판 다른 빌드를 만듭니다.

## 최신 개발 빌드

- [개발 PR #1](https://github.com/EndDeal/RabbitSurvivor/pull/1)에서 얕은 바다 전투, 네 토끼, 작물 4종과 감자 연쇄 처치, 무기 7종과 진화를 확인할 수 있습니다.
- [최신 빌드 실행 안내](https://github.com/EndDeal/RabbitSurvivor/blob/feat/cozy-2d-art/README.md)와 [엑셀 밸런스 데이터](https://github.com/EndDeal/RabbitSurvivor/blob/feat/cozy-2d-art/docs/RabbitSurvivor-Data.xlsx)는 개발 브랜치에 있습니다.

## `main` 브랜치 실행

1. Unity Hub에서 이 저장소의 루트 폴더를 Unity `2022.3.57f1`로 엽니다.
2. `Assets/RabbitSurvivor/Scenes/RabbitSurvivor.unity`를 열고 Play를 누릅니다.
3. 캐릭터를 선택한 뒤 `WASD` 또는 방향키로 이동합니다.

`File > Build Settings`에는 `RabbitSurvivor`가 시작 씬으로 등록되어 있습니다.

## 프로젝트 구조

```text
Assets/
  RabbitSurvivor/
    Scenes/             메인 플레이 씬
    Settings/           URP 2D 렌더러 설정
    Scripts/
      Core/             게임 진행, 오디오, 오브젝트 풀
      Player/           이동과 캐릭터 속성
      Combat/           무기, 투사체, 장비, 적 탐색
      Enemies/          적 행동과 생성
      Progression/      아이템 데이터와 캐릭터 잠금 해제
      UI/               HUD, 레벨업 선택, 결과 화면
      World/            맵 타일과 화면 밖 오브젝트 재배치
  Undead Survivor/      기본 브랜치의 기존 강좌 자산과 프리팹
Packages/               Unity 패키지와 버전
ProjectSettings/        에디터, 빌드, 그래픽 설정
```

Unity 에셋을 이동할 때는 Project 창을 사용하거나 대응하는 `.meta` 파일도 함께 이동해 씬과 프리팹의 참조를 유지합니다.
