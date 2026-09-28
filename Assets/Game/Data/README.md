# 角色外观资源约定

角色创建系统会扫描 `Assets/Game/Data` 下所有 `PlayerPartAnimationSet`，无需在代码中维护数量。

- 一级目录决定类型：`Skin`、`Clothes`、`Eyes`、`Hair`、`Accessory`。
- 路径中包含 `/Male/` 或 `/Female/` 时，该资源只对对应性别显示；未包含时为通用资源。
- 文件名和子目录可以自由扩展，排序采用自然排序（例如 `Hair_2` 位于 `Hair_10` 前）。
- 资源的 Unity GUID 是存档 ID；资源可移动或改名，但不要删除 `.meta`，否则旧存档会失去对应项。
- 新增、删除或移动资源后，编辑器会自动重建
  `Assets/Game/Resources/CharacterAppearanceDatabase.asset`。
- 也可通过 `Tools > Character Creation > Rebuild Database` 手动重建；正式打包前还会强制重建一次。

每套资源需要保持现有逐帧规范：Idle 为 4 个方向 × 4 帧，Walk 为 4 个方向 × 6 帧。
