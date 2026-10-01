using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Validates the project contracts that are easy to break while adding content.</summary>
public static class ProjectHealthValidator
{
    private const string GameRootPath = "Assets/Game/Prefabs/Core/GameRoot.prefab";
    private const string GameDataPath = "Assets/Game/Data/Catalogs/GameDataCatalog.asset";
    private const string UiCatalogPath = "Assets/Game/Data/UI/UIPanelCatalog.asset";
    private const string MainMenuViewPath = "Assets/Game/Prefabs/UI/Frontend/MainMenuScreen.prefab";
    private const string SaveSelectViewPath = "Assets/Game/Prefabs/UI/Frontend/SaveSelectScreen.prefab";

    private static readonly string[] RequiredScenes =
    {
        "Assets/Game/Scenes/Boot.unity",
        "Assets/Game/Scenes/Frontend/MainMenu.unity",
        "Assets/Game/Scenes/Gameplay/GameScene.unity"
    };

    [MenuItem("Tools/Tiny Farm/Validate Project")]
    public static void ValidateFromMenu()
    {
        try
        {
            ValidateOrThrow();
            Debug.Log("Tiny Farm project validation passed.");
        }
        catch (BuildFailedException exception)
        {
            Debug.LogError(exception.Message);
        }
    }

    public static void ValidateOrThrow()
    {
        var errors = new List<string>();
        ValidateBuildScenes(errors);
        ValidateGameRoot(errors);
        ValidateData(errors);
        ValidateUiCatalog(errors);
        ValidateFrontendViews(errors);
        ValidatePrefabs(errors);

        if (errors.Count > 0)
            throw new BuildFailedException("Project validation failed:\n- " + string.Join("\n- ", errors));
    }

    private static void ValidateBuildScenes(List<string> errors)
    {
        var enabledScenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) enabledScenes.Add(scene.path.Replace('\\', '/'));

        for (int i = 0; i < RequiredScenes.Length; i++)
        {
            string path = RequiredScenes[i];
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                errors.Add("Required scene is missing: " + path);
            if (enabledScenes.Count <= i || enabledScenes[i] != path)
                errors.Add("Build Settings scene " + i + " must be " + path + ".");
        }
    }

    private static void ValidateGameRoot(List<string> errors)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameRootPath);
        if (prefab == null)
        {
            errors.Add("GameRoot prefab is missing: " + GameRootPath);
            return;
        }

        RequireComponent<GameRoot>(prefab, errors);
        RequireComponent<GameSession>(prefab, errors);
        RequireComponent<SceneLoader>(prefab, errors);
        RequireComponent<TransitionService>(prefab, errors);
        RequireComponent<InputModeService>(prefab, errors);
        RequireComponent<PauseService>(prefab, errors);
        RequireComponent<UIService>(prefab, errors);
        RequireComponent<GameFlowController>(prefab, errors);
    }

    private static void RequireComponent<T>(GameObject prefab, List<string> errors) where T : Component
    {
        if (prefab.GetComponent<T>() == null)
            errors.Add("GameRoot prefab is missing component " + typeof(T).Name + ".");
    }

    private static void ValidateData(List<string> errors)
    {
        GameDataCatalog catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(GameDataPath);
        if (catalog == null)
        {
            errors.Add("GameDataCatalog is missing: " + GameDataPath);
            return;
        }

        if (catalog.CharacterAppearances == null || catalog.CharacterAppearances.Entries.Count == 0)
            errors.Add("GameDataCatalog has no character appearance entries.");
        if (catalog.CharacterNames == null || catalog.CharacterNames.Pools.Count == 0)
            errors.Add("GameDataCatalog has no character name pools.");
    }

    private static void ValidateUiCatalog(List<string> errors)
    {
        UIPanelCatalog catalog = AssetDatabase.LoadAssetAtPath<UIPanelCatalog>(UiCatalogPath);
        if (catalog == null)
        {
            errors.Add("UIPanelCatalog is missing: " + UiCatalogPath);
            return;
        }

        var ids = new HashSet<string>();
        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            UIPanelCatalog.Entry entry = catalog.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
            {
                errors.Add("UIPanelCatalog entry " + i + " has no id.");
                continue;
            }
            if (!ids.Add(entry.id)) errors.Add("Duplicate UI panel id: " + entry.id);
            if (entry.prefab == null) errors.Add("UI panel " + entry.id + " has no prefab.");
        }
    }

    private static void ValidateFrontendViews(List<string> errors)
    {
        GameObject menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuViewPath);
        MainMenuView menuView = menuPrefab != null ? menuPrefab.GetComponent<MainMenuView>() : null;
        if (menuView == null || !menuView.IsConfigured)
            errors.Add("MainMenuView is missing or incomplete: " + MainMenuViewPath);

        GameObject savePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SaveSelectViewPath);
        SaveSelectView saveView = savePrefab != null ? savePrefab.GetComponent<SaveSelectView>() : null;
        if (saveView == null || !saveView.IsConfigured)
            errors.Add("SaveSelectView is missing or incomplete: " + SaveSelectViewPath);
    }

    private static void ValidatePrefabs(List<string> errors)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[j].gameObject);
                if (missing > 0)
                    errors.Add(path + " contains " + missing + " missing script(s) on " + transforms[j].name + ".");
            }
        }
    }
}

public sealed class ProjectHealthBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => -900;
    public void OnPreprocessBuild(BuildReport report) => ProjectHealthValidator.ValidateOrThrow();
}
