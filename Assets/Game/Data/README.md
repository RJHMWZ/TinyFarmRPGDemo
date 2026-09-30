# 角色外观资源约定

`GameDataCatalog.asset` 是运行时静态配置的唯一入口。场景和 Prefab 应引用 Catalog，
业务代码不要依赖资源文件夹路径。配置数据使用 ScriptableObject；玩家进度使用
`Application.persistentDataPath/Saves` 下的版本化 JSON，二者不可混用。

角色创建系统会扫描 `Assets/Game/Data` 下所有 `PlayerPartAnimationSet`，无需在代码中维护数量。

- 一级目录决定类型：`Skin`、`Clothes`、`Eyes`、`Hair`、`Accessory`。
- 路径中包含 `/Male/` 或 `/Female/` 时，该资源只对对应性别显示；未包含时为通用资源。
- 文件名和子目录可以自由扩展，排序采用自然排序（例如 `Hair_2` 位于 `Hair_10` 前）。
- 资源的 Unity GUID 是存档 ID；资源可移动或改名，但不要删除 `.meta`，否则旧存档会失去对应项。
- 新增、删除或移动资源后，编辑器会自动重建
  `Assets/Game/Data/CharacterAppearanceDatabase.asset`。
- 也可通过 `Tools > Character Creation > Rebuild Database` 手动重建；正式打包前还会强制重建一次。

每套资源需要保持现有逐帧规范：Idle 为 4 个方向 × 4 帧，Walk 为 4 个方向 × 6 帧。

## 随机名字

`CharacterNameDatabase.asset` 按语言代码维护名字池。内置 `zh-CN` 与 `en`，以后可以继续
增加 `ja`、`ko` 等名字池，不需要修改随机生成逻辑。空白名字会在运行时自动忽略。
