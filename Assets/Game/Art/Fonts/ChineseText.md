# Chinese body font

`ChineseText.otf` is a regular-weight subset of Noto Sans CJK SC, renamed **TinyFarm Chinese Text**. It retains all basic CJK ideographs (U+4E00–U+9FFF), ASCII, CJK punctuation, and common/full-width symbols. This covers arbitrary names using basic Chinese characters as well as the authored UI.

Source: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf

License: SIL Open Font License 1.1. The copyright notice and full license are bundled at `Assets/Game/Resources/Fonts/ChineseText-LICENSE.txt`, so they are included in player builds.

`China.ttf` is the original decorative font; it is retained as source artwork. UI body text uses the readable font above via `ChineseUiFont`, including world labels and dynamic HUD text. Rebuild its TMP asset and UI references with **Tools > Tiny Farm > UI > Apply Chinese Font and Layout**.
