using UnityEngine;

public static class MainMenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachController()
    {
        GameObject menu = GameObject.Find("MenuPanel");
        if (menu != null && menu.GetComponent<MainMenuController>() == null)
            menu.AddComponent<MainMenuController>();
    }
}
