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

## Unity CLI로 할 때 (Pipeline 연결 시 우선)

빌드·테스트·프로젝트 설정·패키지 관리는 CLI가 더 풍부하다. Editor가 열려 있으면 `unity command`, CI처럼 닫혀 있으면 `unity build`/`unity test`가 Editor를 batch 모드로 띄운다.

```bash
# 열린 Editor 안에서
unity command list_build_profiles --json
unity command build --json -- --profile "Windows Release" --output_path Builds/Windows/MyGame.exe
unity command build_status --json                                 # 완료까지 폴링, 또는 --detach 후 unity job wait
unity command get_player_settings --json
unity command set_player_settings --json -- --product_name "My Game" --dry_run true   # 미리보기 후 --confirm true
unity command package_add --json -- --identifier com.unity.addressables --confirm true

# 헤드리스 (Editor 닫힌 상태, CI)
unity build . --profile "Windows Release" --output-path Builds/Windows/MyGame.exe --timeout 3600
unity test . --mode EditMode --report-format junit --output results.xml
unity test . --mode PlayMode --shard 1/4 --retries 1 --format github
```

- `set_*_settings`·`package_*`는 Undo가 안 된다. 반드시 `--dry_run true`로 먼저 본다.
- 종료 코드 8은 테스트 실패(재시도 금지), 6은 빌드 실패, 7은 서비스 불가(재시도 가능).

## 일반적인 빌드 전 체크리스트

1. `unity_get_console_logs`(logType="Error")로 컴파일 에러 확인
2. `unity_project_settings`(category="player")로 버전/이름 확인
3. `unity_save_scene`으로 현재 씬 저장
4. `unity_build`로 빌드 실행
