using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates the project contracts that are easy to break while adding content.</summary>
public static class ProjectHealthValidator
{
    private const string GameRootPath = "Assets/Game/Prefabs/Core/GameRoot.prefab";
    private const string GameDataPath = "Assets/Game/Data/Catalogs/GameDataCatalog.asset";
    private const string UiCatalogPath = "Assets/Game/Data/UI/UIPanelCatalog.asset";
    private const string MainMenuViewPath = "Assets/Game/Prefabs/UI/Frontend/MainMenuScreen.prefab";
    private const string SaveSelectViewPath = "Assets/Game/Prefabs/UI/Frontend/SaveSelectScreen.prefab";
    private const string CharacterCreationViewPath = "Assets/Game/Prefabs/UI/Frontend/CharacterCreationScreen.prefab";

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
        ValidateSceneContents(errors);
        ValidateGameRoot(errors);
        ValidateData(errors);
        ValidateUiCatalog(errors);
        ValidateFrontendViews(errors);
        ValidatePrefabs(errors);

        if (errors.Count > 0)
            throw new BuildFailedException("Project validation failed:\n- " + string.Join("\n- ", errors));
    }

    private static void ValidateSceneContents(List<string> errors)
    {
        for (int i = 0; i < RequiredScenes.Length; i++)
        {
            string path = RequiredScenes[i];
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedForValidation) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                ValidateMissingScripts(scene, path, errors);

                if (path.EndsWith("/MainMenu.unity", System.StringComparison.Ordinal))
                {
                    MainMenuController[] controllers = FindSceneComponents<MainMenuController>(scene);
                    if (controllers.Length != 1 || !controllers[0].IsConfigured)
                        errors.Add("MainMenu scene must contain exactly one configured MainMenuController.");
                }
                else if (path.EndsWith("/GameScene.unity", System.StringComparison.Ordinal))
                {
                    UIRoot[] uiRoots = FindSceneComponents<UIRoot>(scene);
                    if (uiRoots.Length != 1 || !uiRoots[0].IsConfigured)
                        errors.Add("GameScene must contain exactly one configured UIRoot.");

                    GameplayEntryPoint[] entryPoints = FindSceneComponents<GameplayEntryPoint>(scene);
                    if (entryPoints.Length != 1 || !entryPoints[0].IsConfigured)
                        errors.Add("GameScene must contain exactly one configured GameplayEntryPoint.");
                }
            }
            catch (System.Exception exception)
            {
                errors.Add("Could not validate scene " + path + ": " + exception.Message);
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            result.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return result.ToArray();
    }

    private static void ValidateMissingScripts(Scene scene, string path, List<string> errors)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[j].gameObject);
                if (missing > 0)
                    errors.Add(path + " contains " + missing + " missing script(s) on " + transforms[j].name + ".");
            }
        }
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
        RequireComponent<SettingsService>(prefab, errors);
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
        else
        {
            IReadOnlyList<CharacterAppearanceDatabase.Entry> appearances = catalog.CharacterAppearances.Entries;
            for (int i = 0; i < appearances.Count; i++)
            {
                CharacterAppearanceDatabase.Entry entry = appearances[i];
                if (entry == null || entry.AnimationSet == null) continue;
                if (!entry.AnimationSet.IsValid(out string error))
                {
                    string path = AssetDatabase.GetAssetPath(entry.AnimationSet);
                    errors.Add("Invalid character animation set at " + path + ": " + error);
                }
            }
        }
        if (catalog.CharacterNames == null || catalog.CharacterNames.Pools.Count == 0)
            errors.Add("GameDataCatalog has no character name pools.");
        else
        {
            if (!string.Equals(catalog.CharacterNames.DefaultLocaleCode, "en", System.StringComparison.OrdinalIgnoreCase))
                errors.Add("English must remain the default name locale until localization is enabled.");

            CharacterNameDatabase.NamePool englishNames = catalog.CharacterNames.FindPool("en");
            bool hasEnglishName = false;
            if (englishNames != null)
            {
                for (int i = 0; i < englishNames.Names.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(englishNames.Names[i])) continue;
                    hasEnglishName = true;
                    break;
                }
            }
            if (!hasEnglishName) errors.Add("The English character-name pool is missing or empty.");
        }
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
        if (catalog.Find(SettingsPanel.PanelId) == null)
            errors.Add("UIPanelCatalog must register the shared settings panel.");
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

        GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterCreationViewPath);
        CharacterCreationView characterView = characterPrefab != null
            ? characterPrefab.GetComponent<CharacterCreationView>()
            : null;
        if (characterView == null || !characterView.IsConfigured)
            errors.Add("CharacterCreationView is missing or incomplete: " + CharacterCreationViewPath);
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
