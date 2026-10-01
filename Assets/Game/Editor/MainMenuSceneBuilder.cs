using UnityEditor;
using UnityEngine;

/// <summary>Front-end UI now lives in prefabs; this shortcut opens the save panel for editing.</summary>
public static class MainMenuSceneBuilder
{
    private const string SavePanelPrefabPath = "Assets/Game/Prefabs/UI/Frontend/SaveSelectScreen.prefab";

    [MenuItem("Tools/Tiny Farm/UI/Edit Save Select Screen")]
    public static void OpenSavePanelPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SavePanelPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Save panel prefab was not found at " + SavePanelPrefabPath);
            return;
        }
        AssetDatabase.OpenAsset(prefab);
    }
}
