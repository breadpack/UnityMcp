# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [0.1.0] - 2026-03-11

### Added

- Initial release
- TCP server for MCP Bridge communication
- 23 MCP tools (12 observation + 11 scene manipulation)
- Handlers: Hierarchy, AssetHierarchy, ComponentDetails, Screen, UITree, ConsoleLogs, AvailableActions, TakeScreenshot, RefreshAssets
- Handlers: CreateGameObject, DeleteGameObject, ReparentGameObject, SetTransform, AddComponent, RemoveComponent, SetProperty, SetAssetReference, InstantiatePrefab, RenderUxml, PlayMode
- Optional Addressables support via `UNITY_MCP_ADDRESSABLES` define
