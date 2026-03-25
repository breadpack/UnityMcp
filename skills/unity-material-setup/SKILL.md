---
name: unity-material-setup
description: Unity Material을 생성하고 설정합니다
---

# Unity Material Setup

Material을 생성하고 프로퍼티를 설정할 때 사용합니다.

## 워크플로우

1. **Material 생성**: `unity_create_material`로 새 Material 에셋을 생성합니다
   - `savePath`: "Assets/Materials/MyMaterial.mat"
   - `shaderName`: "Standard" 또는 "Universal Render Pipeline/Lit" (미지정 시 자동 감지)

2. **프로퍼티 설정**: `unity_set_material_property`로 값을 설정합니다
   - Color: `propertyType="color"`, `value="{\"r\":1,\"g\":0,\"b\":0,\"a\":1}"`
   - Float: `propertyType="float"`, `value="0.5"`
   - Texture: `propertyType="texture"`, `value="Assets/Textures/MyTex.png"`
   - Int: `propertyType="int"`, `value="1"`

3. **오브젝트에 적용**: `unity_set_asset_reference`로 Renderer의 material 필드에 할당합니다

## 일반적인 셰이더 프로퍼티

| 셰이더 | 프로퍼티 | 타입 | 설명 |
|--------|---------|------|------|
| Standard | _Color | color | 메인 색상 |
| Standard | _MainTex | texture | 메인 텍스처 |
| Standard | _Metallic | float | 메탈릭 (0-1) |
| Standard | _Glossiness | float | 스무스니스 (0-1) |
| URP/Lit | _BaseColor | color | 베이스 색상 |
| URP/Lit | _BaseMap | texture | 베이스 맵 |
| URP/Lit | _Metallic | float | 메탈릭 (0-1) |
| URP/Lit | _Smoothness | float | 스무스니스 (0-1) |
