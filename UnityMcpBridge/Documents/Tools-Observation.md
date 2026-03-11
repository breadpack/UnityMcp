# Observation Tools Reference

Unity Editor의 상태를 조회하는 관찰 도구 10개.

---

## unity_ping

서버 연결 상태 확인. 항상 첫 번째로 호출하여 연결과 모드를 확인한다.

**모드**: Any

**파라미터**: 없음

**반환**:
```json
{
  "message": "pong",
  "isPlayMode": false,
  "isCompiling": false,
  "editorSettings": { }
}
```

**활용**:
- 다른 도구 호출 전 연결 상태 확인
- Play Mode 여부 확인 후 적절한 도구 선택

---

## unity_render_uxml

UXML 파일을 이미지로 렌더링. Play Mode 불필요.

**모드**: Edit

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `uxmlPath` | string | O | - | Asset 경로 (예: `Assets/_GAME_ASSETS/UI/...`) |
| `width` | int | X | PanelSettings ref | 렌더링 너비 (px) |
| `height` | int | X | PanelSettings ref | 렌더링 높이 (px) |
| `quality` | int | X | 75 | JPEG 품질 (0=PNG) |

**반환**:
```json
{
  "imageBase64": "...",
  "mimeType": "image/jpeg",
  "width": 1080,
  "height": 1920
}
```

**모범 사례**:
- 해상도 미지정 시 프로젝트 PanelSettings의 referenceResolution 사용 (scale 1.0)
- 의도적 축소 확인 시에만 width/height 지정
- 최소 해상도 50x50 (미만은 예외 발생)

**주의사항**:
- 에디터 화면 픽셀을 직접 읽으므로 윈도우가 다른 창에 가려지면 잘못 캡처됨
- USS url() 참조 이미지는 실제 Asset이 존재해야 렌더링됨
- DataBinding 미동작 (ViewModel 없이 UXML만 렌더링)
- C# 커스텀 Element는 Edit 모드에서 기본 렌더링만 됨

---

## unity_take_screenshot

현재 게임 뷰 캡처. Play Mode 필수.

**모드**: Play

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `quality` | int | X | 75 | JPEG 품질 (0=PNG) |

**반환**: Base64 이미지 + 크기 정보

**모범 사례**:
- 캡처 전 Game View를 Repaint하므로 최신 프레임 보장
- 런타임 UI 상태 (바인딩, 애니메이션, Spine) 확인 시 사용
- render_uxml과 달리 실제 실행 중인 화면 캡처

---

## unity_get_screen

현재 MenuScreenManager의 활성 화면과 ViewModel 프로퍼티 조회.

**모드**: Play

**파라미터**: 없음

**반환**:
```json
{
  "currentScreen": {
    "type": "ScreenViewModelClassName",
    "properties": {
      "PropertyName": "Value"
    }
  },
  "activePopups": [],
  "isPlayMode": true
}
```

**활용**:
- 현재 표시 중인 화면 확인
- ViewModel 바인딩 값 디버깅

---

## unity_get_ui_tree

UI Toolkit 비주얼 트리 구조 직렬화.

**모드**: Play

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `maxDepth` | int | X | 10 | 트리 탐색 깊이 |

**반환**: VisualElement 트리 JSON (name, type, classes, text 포함)

**활용**:
- 런타임 UI 요소의 실제 구조 확인
- USS 클래스 적용 검증
- 요소의 name, type, classes, text 확인

---

## unity_get_available_actions

클릭 가능한 UI 요소 목록 반환.

**모드**: Play

**파라미터**: 없음

**반환**: 클릭 가능한 요소 목록 (버튼 이름, 텍스트)

**활용**:
- 현재 화면에서 가능한 사용자 행동 파악
- 버튼 이름과 텍스트 확인

---

## unity_get_hierarchy

Scene의 GameObject 트리 구조.

**모드**: Any

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `maxDepth` | int | X | 5 | 트리 탐색 깊이 |
| `includeComponents` | bool | X | false | 컴포넌트 포함 여부 |

**반환**: GameObject 계층 트리 (maxDepth까지 재귀)

**활용**:
- Scene 구조 파악
- UI Document, Canvas 등 UI 루트 오브젝트 확인
- 컴포넌트 구성 확인 시 `includeComponents: true`

---

## unity_get_console_logs

콘솔 로그 버퍼 조회.

**모드**: Any

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `count` | int | X | 50 | 가져올 로그 수 |
| `logType` | string | X | null | 필터: "Log", "Warning", "Error", "Exception" |

**반환**:
```json
{
  "logs": [
    {
      "message": "...",
      "stackTrace": "...",
      "type": "Error",
      "timestamp": "2026-03-10T10:08:00Z"
    }
  ],
  "totalBuffered": 150
}
```

**모범 사례**:
- 오류 디버깅 시 `logType: "Error"` 필터링
- 로그가 많으면 `count`를 줄여서 최신만 확인

---

## unity_refresh_assets

AssetDatabase 강제 새로고침.

**모드**: Any

**파라미터**: 없음

**반환**: 완료 메시지

**활용 타이밍**:
- 외부에서 UXML/USS/스크립트 파일 수정 후
- render_uxml 전에 최신 Asset 상태 보장

---

## unity_play_mode

Play Mode 전환.

**모드**: Any

**파라미터**:

| 파라미터 | 타입 | 필수 | 기본값 | 설명 |
|----------|------|------|--------|------|
| `action` | string | X | "toggle" | "enter", "exit", "toggle" |

**반환**: Play Mode 상태

**중요 제약사항**:
- Play Mode 진입/퇴출 시 **도메인 리로드** 발생
- 도메인 리로드 = MCP 서버 재시작 = **연결 끊김**
- 전환 후 MCP 재연결 필요 (자동 재연결 지원)
- 컴파일 중에는 전환 불가
