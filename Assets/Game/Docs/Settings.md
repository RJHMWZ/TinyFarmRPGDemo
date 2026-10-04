# Shared device settings

The title menu and the farm menu both open `settings` through `GameRoot.UI`. The authored
`SettingsPanel.prefab` derives from `UIPanel`, uses serialized view references and is registered
as a cached Popup in `UIPanelCatalog`. `UIService` owns its pause request and restores input
when it closes. The farm menu remains underneath and retains its separate pause request.

`GameRoot.Settings` is the only runtime settings owner. `GameSettings` is a copied data model;
editing a draft cannot mutate the saved snapshot. Preferences are stored under the versioned
PlayerPrefs key `settings.device.v1`, independently of the four character saves. Existing
master-volume, fullscreen and VSync preferences are imported when the new key is absent.

## Behavior

- Display: windowed, borderless or exclusive fullscreen; monitor-supported resolutions;
  VSync and 30/60/120/144/unlimited frame rate. VSync disables the manual frame limit.
- Audio: master volume and mute while unfocused. There are no music or effects assets in this
  milestone, so no nonfunctional category controls are exposed.
- Gameplay: HUD size 80–120%, orthographic camera zoom 75–150%, 12/24 hour clock, shortcut
  hints and pause while unfocused. Farm preferences can also be configured on the title screen.
- Controls: the four movement directions and backpack can be rebound to unique A–Z keys.
  Escape is reserved for Back; arrow keys remain an independent movement/navigation fallback.
  Rebinding clones the authored Move action, preserving the asset and other consumers.

Changes preview immediately except display mode/resolution. Apply writes all preferences.
Display changes first ask for confirmation; the unscaled 15-second timer restores the previous
settings if cancelled or unattended, even while the game is paused. Back/Escape discards edits
made since the last Apply. Defaults previews factory defaults and still requires Apply to save.
Unloading or disabling the panel also rolls back uncommitted previews.

Keyboard and controller UI navigation use the existing Input System UI module. Controller
shoulders switch tabs and B/Cancel returns; rebinding is deliberately keyboard-only. Settings
cards shrink to fit small/aspect-ratio-constrained canvases. Game UI remains English, matching
the current project language policy. The trim sprite comes from the included Farm RPG asset pack.

## Authoring and verification

`Tools > Tiny Farm > UI > Rebuild Settings Panel` recreates the prefab, registers it, adds the
frontend UI layers and configures the root/bootstrap asset. Do not run it with unsaved frontend
scene edits. Run `Tools > Tiny Farm > Validate Project` after changing registrations.

EditMode tests cover corrupt values, persistence, snapshot isolation and binding conflicts.
PlayMode tests exercise title-to-farm settings continuity, preview rollback, Apply, display
revert, nested pause ownership and input restoration. Standalone window/monitor changes need
a desktop player check: the editor deliberately does not resize its own window.

Design reference: https://wiki.stardewvalley.net/Options (separate display, sound, UI size,
world zoom and input preferences; features here are limited to implemented game systems).
