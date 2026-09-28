using UnityEngine;

/// <summary>
/// 场景加载后的兼容启动器。
/// 在不修改现有场景 YAML 的情况下，为名为 CharacterCreation 的面板自动挂载控制器。
/// </summary>
public static class CharacterCreationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    /// <summary>只在场景存在目标面板且尚未挂载控制器时添加，避免重复组件。</summary>
    private static void AttachController()
    {
        GameObject panel = GameObject.Find("CharacterCreation");
        if (panel != null && panel.GetComponent<CharacterCreationController>() == null)
            panel.AddComponent<CharacterCreationController>();
    }
}
