# 游戏数据目录约定

`Catalogs/GameDataCatalog.asset` 是运行时静态配置的统一入口。Scene 和 Prefab 引用 Catalog，业务代码不依赖资源文件夹路径。

目录职责：

- `Catalogs`：数据库和总目录等 ScriptableObject 入口。
- `CharacterAppearance`：捏人系统的皮肤、服装、眼睛、头发和饰品定义。
- `UI`：UI 面板目录等配置资产。
- 玩家进度不放在 Assets 中，而是写入 `Application.persistentDataPath/Saves` 下的版本化 JSON。

## 角色外观

编辑器扫描 `Data/CharacterAppearance` 下的所有 `PlayerPartAnimationSet`，自动生成
`Catalogs/CharacterAppearanceDatabase.asset`。

- 一级目录决定分类：`Skin`、`Clothes`、`Eyes`、`Hair`、`Accessory`。
- 路径包含 `/Male/` 或 `/Female/` 时，只对相应性别显示；否则视为通用资源。
- Unity GUID 是存档中的稳定资源 ID。资源可以移动或改名，但不要删除其 `.meta` 文件。
- 可通过 `Tools > Character Creation > Rebuild Database` 手动重建；Player 构建前也会自动重建并验证。

每套动画继续遵守现有逐帧规范：Idle 为四方向乘四帧，Walk 为四方向乘六帧。

## 随机名字

`Catalogs/CharacterNameDatabase.asset` 按语言代码维护名字池。目前包含 `zh-CN` 和 `en`，以后可以继续添加 `ja`、`ko` 等语言，而不需要修改随机生成逻辑。
