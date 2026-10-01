# Tiny Farm Runtime Architecture

## Project layout

```text
Assets/Game
├─ Art/Fonts
├─ Data
│  ├─ Catalogs
│  ├─ CharacterAppearance
│  └─ UI
├─ Docs
├─ Editor
├─ Input
├─ Prefabs
│  ├─ Core
│  └─ UI
├─ Scenes
│  ├─ Boot.unity
│  ├─ Frontend
│  └─ Gameplay
└─ Scripts
   ├─ Core
   ├─ Player
   ├─ Save
   └─ UI
      ├─ Framework
      └─ Frontend
```

Do not create another singular `Scene` folder or place game-owned editor tools in the project-level `Assets/Editor` folder. Add feature folders only when they contain real assets; avoid empty placeholder directories.

## Scene flow

The player build starts in `Boot`. Its persistent `GameRoot` loads `MainMenu`, and the front-end loads `GameScene` only after a save has been created or loaded.

Do not put title-menu UI, save selection, or character creation back into gameplay scenes. New world locations can later be split into additive scenes while `GameScene` becomes the gameplay shell.

## Service boundaries

- `GameFlowController`: title/new/load/return-to-title flow only.
- `SceneLoader`: scene loading only.
- `SaveService`: versioned disk persistence only.
- `GameSession`: selected slot and currently loaded runtime data.
- `TransitionService`: global fade and input blocking during transitions.
- `InputModeService`: current gameplay/UI/dialogue input ownership.
- `UIService`: panel loading, layers, cache policy, and back stack.

Feature logic belongs to feature services and models, not to `UIService`.

## UI prefab policy

Create panel source assets under `Assets/Game/Prefabs/UI` and register reusable gameplay panels in `UIPanelCatalog`.

- `Resident`: HUD, loading indicators, and notification hosts.
- `Cached`: inventory, quests, map, relationships, and other frequently reopened screens.
- `Transient`: confirmations, tooltips, and one-shot popups.

The gameplay `UIRoot` has these ordered layers: `Hud`, `Screen`, `Window`, `Popup`, `Toast`, and `Transition`.

Panel scripts should derive from `UIPanel` and use serialized references to their own child views. Avoid global `GameObject.Find` calls and avoid putting inventory, quests, shops, or save serialization inside a panel controller.

## Adding a gameplay panel

1. Create a prefab under the appropriate `Prefabs/UI` feature folder.
2. Add a component derived from `UIPanel`.
3. Add the prefab, layer, lifetime, HUD policy, and pause policy to `UIPanelCatalog`.
4. Open it with `GameRoot.Instance.UI.Open("panel-id", args)`.
5. Close the topmost modal with `GameRoot.Instance.UI.CloseTop()`.

## Save evolution

Add new serializable feature data beneath `GameSaveData`. Increment the envelope version in `SaveService` and add sequential migration steps before shipping a changed schema. Existing character-only JSON files are imported automatically as version-two saves.
