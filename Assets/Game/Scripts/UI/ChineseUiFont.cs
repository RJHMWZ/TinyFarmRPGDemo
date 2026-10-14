using TMPro;
using UnityEngine;

/// <summary>The bundled Chinese font is shared by generated HUD, world labels and authored UI.</summary>
public static class ChineseUiFont
{
    private static TMP_FontAsset font;
    public static TMP_FontAsset Font
    {
        get
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/ChineseUI");
            return font != null ? font : TMP_Settings.defaultFontAsset;
        }
    }
}
