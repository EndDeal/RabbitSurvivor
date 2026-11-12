# 레빗서바이벌

캐릭터를 선택하고, 몰려오는 적을 피하며 무기를 강화해 제한 시간까지 버티는 Unity 2D 생존 게임입니다.

## 실행

1. Unity Hub에서 이 저장소의 루트 폴더를 Unity `2022.3.57f1`로 엽니다.
2. `Assets/RabbitSurvivor/Scenes/Title.unity`를 열고 Play를 누릅니다.
3. 로딩이 끝나면 `게임 시작!`을 누르고 캐릭터를 선택합니다. 오프닝은 왼쪽 위 `스킵` 버튼으로 건너뛸 수 있습니다.
4. 게임에 들어가면 `WASD` 또는 방향키로 이동합니다.

`File > Build Settings`에는 `Title`과 `RabbitSurvivor`가 순서대로 등록되어 있습니다. 스토리 이미지와 대사는 `RabbitSurvivor` 씬의 `Story Canvas`에서 바꿀 수 있습니다.

## 프로젝트 구조

```text
Assets/
  RabbitSurvivor/
    Art/Story/          오프닝 스토리 이미지
    Scenes/             타이틀과 메인 플레이 씬
    Settings/           URP 2D 렌더러 설정
    Scripts/
      Core/             게임 진행, 오디오, 오브젝트 풀
      Player/           이동과 캐릭터 속성
      Combat/           무기, 투사체, 장비, 적 탐색
      Enemies/          적 행동과 생성
      Progression/      아이템 데이터와 캐릭터 잠금 해제
      UI/               HUD, 레벨업 선택, 결과 화면
      World/            맵 타일과 화면 밖 오브젝트 재배치
  Undead Survivor/      골드메탈 에셋, 프리팹, 입력 설정, 데모
Packages/               Unity 패키지와 버전
ProjectSettings/        에디터, 빌드, 그래픽 설정
```

Unity 에셋을 이동할 때는 Project 창을 사용하거나 대응하는 `.meta` 파일도 함께 이동해 씬과 프리팹의 참조를 유지합니다.
