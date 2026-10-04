# Tiny Farm Runtime Architecture

## Project layout

```text
Assets/Game
|-- Art/Fonts
|-- Data
|   |-- Catalogs
|   |-- CharacterAppearance
|   `-- UI
|-- Docs
|-- Editor
|-- Input
|-- Prefabs
|   |-- Core
|   `-- UI
|-- Scenes
|   |-- Boot.unity
|   |-- Frontend
|   `-- Gameplay
`-- Scripts
    |-- Core
    |-- Player
    |-- Save
    `-- UI
        |-- Framework
        `-- Frontend
```

Do not create another singular `Scene` folder or put game-owned editor tools in the project-level `Assets/Editor` folder. Add feature folders only when they contain real assets; avoid empty placeholder directories.

## Scene flow

The player build starts in `Boot`. Its persistent `GameRoot` loads `MainMenu`, and the frontend loads `GameScene` only after a save has been created or loaded.

Keep title UI, save selection, and character creation outside gameplay scenes. Future world locations may be additive scenes while `GameScene` remains the persistent gameplay shell.

## Service boundaries

- `GameFlowController`: new game, load game, return to title, and top-level flow.
- `SceneLoader`: scene loading only.
- `SaveService`: versioned disk persistence only.
- `GameSession`: selected slot and currently loaded runtime data.
- `TransitionService`: global fades and transition timing.
- `InputModeService`: gameplay, UI, dialogue, or disabled input ownership.
- `PauseService`: owner-based pause requests. Systems must not write `Time.timeScale` directly.
- `UIService`: panel loading, layers, lifetime policy, and the back stack.

Feature rules belong to feature services and models, not to `UIService`.

## Farm gameplay layer

`GameplayEntryPoint` attaches a scene-owned `FarmRuntime`, which creates one `FarmGame`
for the loaded session. `InventoryService`, `FarmingService`, `FarmEconomyService`, and
`FarmProgressionService` implement rules independently of scene/input/UI objects.
`FarmContent` is the authored definition catalog; `GameSaveData.farm` is the persisted state.
`FarmWorldView` and `FarmInteraction` adapt the world and input. `FarmHubPanel` is a cached,
serialized prefab registered with `UIService`. The HUD only presents time; `FarmRuntime`
owns clock ticks and checkpoint capture. See [FarmGameplay.md](FarmGameplay.md) for the
complete feature contract, controls, content workflow, migration rules, and remaining scope.

Runtime systems must use one state owner. Save access goes through `GameRoot.Saves`, selected-slot
state through `GameSession`, input ownership through `InputModeService`, and pause ownership through
`PauseService`. Do not add static compatibility facades or write directly to `Time.timeScale`.

Frequently queried authored catalogs build non-serialized lookup dictionaries at runtime. Keep the
serialized list as the Inspector source of truth and validate duplicate IDs during project checks.

## UI prefab policy

Create panel source assets under `Assets/Game/Prefabs/UI` and register reusable gameplay panels in `UIPanelCatalog`.

- `Resident`: HUD, loading indicators, and notification hosts.
- `Cached`: inventory, quests, map, relationships, and other frequently reopened screens.
- `Transient`: confirmations, tooltips, and one-shot popups.

The gameplay `UIRoot` owns these ordered layers: `Hud`, `Screen`, `Window`, `Popup`, `Toast`, and `Transition`.

Panel scripts should derive from `UIPanel` and use serialized references to their child views. Avoid global `GameObject.Find` calls and do not place inventory, quest, shop, or save serialization logic in panel controllers.

## Adding a gameplay panel

1. Create the prefab under the appropriate `Prefabs/UI` feature folder.
2. Add a component derived from `UIPanel`.
3. Register its id, layer, lifetime, HUD policy, and pause policy in `UIPanelCatalog`.
4. Open it with `GameRoot.Instance.UI.Open("panel-id", args)`.
5. Close the topmost modal with `GameRoot.Instance.UI.CloseTop()`.

If another system also needs to pause gameplay, call `GameRoot.Instance.Pause.SetPaused(owner, true)` and release the same owner with `false`. This prevents one system from resuming the game while another still owns a pause request.

## Current language policy

English is the only active game language in the current milestone. Character creation always generates names from the `en` pool, and project validation requires that pool to exist and contain at least one valid name.

The `zh-CN` name pool remains stored in `CharacterNameDatabase` for future localization work, but runtime UI must not select it until a complete language service, translated text catalog, and font policy are introduced together.

## Save evolution

Add new serializable feature data under `GameSaveData`. Increment the envelope version in `SaveService` and add sequential migration steps before shipping a changed schema. Existing character-only JSON files are imported automatically as version-two saves.

## Validation

Run `Tools > Tiny Farm > Validate Project` before committing structural changes. Player builds run the same validation automatically after the appearance database is rebuilt.

For a command-line or CI run, close the local editor for this project and execute:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\CI\Run-UnityValidation.ps1 -UnityPath "C:\Path\To\Unity.exe"
```

Reports are written to `Artifacts/CI`, which is intentionally excluded from version control. The script fails if Unity returns an error, a test suite fails, or a suite discovers no tests.

The validator checks:

- required scene paths and Build Settings order;
- missing scripts and required controller references inside Boot, MainMenu, and GameScene;
- required `GameRoot` services;
- character appearance, animation-frame layout, and name catalogs;
- duplicate or incomplete UI panel registrations;
- missing MonoBehaviour scripts on game-owned prefabs.

See `CommercialRoadmap.md` for the current English-only boundary and the ordered production priorities.
