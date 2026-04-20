---
name: unity-build-deploy
description: Unity 프로젝트를 빌드하고 설정을 관리합니다
---

# Unity Build & Deploy

프로젝트 빌드와 설정 관리에 사용합니다.

## 빌드

`unity_build`로 플레이어를 빌드합니다:
- `outputPath`: 빌드 출력 경로 (필수)
- `target`: "Windows", "macOS", "Linux", "Android", "iOS", "WebGL" (미지정 시 현재 타겟)
- `scenes`: 빌드할 씬 JSON 배열 (미지정 시 Build Settings 사용)

예시:
```
unity_build:
  outputPath: "Builds/Windows/MyGame.exe"
  target: "Windows"
  scenes: '["Assets/Scenes/Main.unity", "Assets/Scenes/Game.unity"]'
```

## Project Settings 관리

`unity_project_settings`로 설정을 조회/변경합니다:

### 조회 (action="get")
- `category="player"`: companyName, productName, bundleVersion 등
- `category="quality"`: 퀄리티 레벨 이름, 현재 레벨
- `category="physics"`: gravity, defaultContactOffset
- `category="time"`: fixedDeltaTime, timeScale

### 설정 (action="set")
```
unity_project_settings:
  action: "set"
  category: "player"
  propertyName: "productName"
  value: "My Game"
```

## 일반적인 빌드 전 체크리스트

1. `unity_get_console_logs`(logType="Error")로 컴파일 에러 확인
2. `unity_project_settings`(category="player")로 버전/이름 확인
3. `unity_save_scene`으로 현재 씬 저장
4. `unity_build`로 빌드 실행
