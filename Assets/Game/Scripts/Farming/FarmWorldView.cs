using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public sealed class FarmWorldTarget
{
    public Vector2 Position;
    public string Id;
    public string Kind;
    public string Title;
    public int PlotIndex = -1;
    public SpriteRenderer Renderer;
}

/// <summary>Replaceable scene presentation. Only FarmGame mutates gameplay data.</summary>
public sealed class FarmWorldView : MonoBehaviour
{
    public readonly List<FarmWorldTarget> Targets = new List<FarmWorldTarget>();
    private FarmRuntime runtime;
    private Transform worldRoot;
    private readonly List<SpriteRenderer> soil = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> crops = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> sprinklers = new List<SpriteRenderer>();
    private Sprite square;
    private Texture2D pixel;
    private SpriteRenderer highlight;
    private SpriteRenderer nightOverlay;
    private Camera worldCamera;
    private SortingGroup playerSorting;
    private Color dayColor = new Color(0.64f, 0.79f, 0.68f);
    private Rect worldBounds = new Rect(-13, -9, 26, 18);
    public void Initialize(FarmRuntime owner)
    {
        runtime = owner;
        pixel = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        pixel.SetPixel(0, 0, Color.white); pixel.Apply();
        square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
        worldRoot = new GameObject("FarmWorld").transform;
        worldRoot.SetParent(transform, false);
        worldCamera = Camera.main;
        if (worldCamera != null) worldCamera.backgroundColor = dayColor;
        playerSorting = runtime.Player.GetComponent<SortingGroup>();
        if (playerSorting == null) playerSorting = runtime.Player.gameObject.AddComponent<SortingGroup>();
        var content = runtime.Game.Content;
        if (runtime.Layout != null && runtime.Layout.IsConfigured) BuildSceneWorld(content, runtime.Layout);
        else BuildDemoWorld(content);
        highlight = Draw("InteractionHighlight", Vector2.zero, null, new Color(1, 0.9f, 0.35f, 0.28f), Vector2.one * 1.1f, 800);
        highlight.enabled = false;
        nightOverlay = Draw("Nightfall", worldBounds.center, null, Color.clear, new Vector2(worldBounds.width + 2, worldBounds.height + 2), 700);
        if (!worldBounds.Contains(runtime.Player.position))
            runtime.Player.position = runtime.Layout != null ? runtime.Layout.spawnPoint.position : Vector3.zero;
        if (worldCamera != null)
            worldCamera.transform.position = CameraPosition(worldCamera);
        runtime.Game.StateChanged += OnStateChanged;
        Refresh();
    }
    private void BuildDemoWorld(FarmContent content)
    {
        for (int y = -9; y <= 9; y++)
            for (int x = -13; x <= 13; x++)
                Draw("Grass", new Vector2(x, y), content.grass, new Color(0.66f, 0.8f, 0.55f), Vector2.one, -1000);
        Draw("MainPath", new Vector2(0f, 2.2f), null, new Color(0.79f, 0.68f, 0.43f), new Vector2(24, 1.5f), -950);
        Draw("CrossPath", new Vector2(-1.5f, 0f), null, new Color(0.79f, 0.68f, 0.43f), new Vector2(1.5f, 17), -950);
        Draw("PondEdge", new Vector2(-9f, 5.5f), null, new Color(0.81f, 0.75f, 0.51f), new Vector2(6.5f, 4.5f), -940);
        Draw("Pond", new Vector2(-9f, 5.5f), null, new Color(0.29f, 0.62f, 0.72f), new Vector2(6f, 4f), -930);
        Collider("PondCollision", new Vector2(-9f, 5.5f), new Vector2(6f, 4f));
        Label("WILLOW FARM", new Vector2(3.5f, 7.5f), 3f);
        Label("FIELD", new Vector2(4.5f, -4.8f), 1.7f);
        for (int i = 0; i < FarmSaveData.Width * FarmSaveData.Height; i++)
        {
            Vector2 position = PlotPosition(i);
            soil.Add(Draw("Plot_" + i, position, content.soil, Color.white, Vector2.one * 0.94f, -800));
            crops.Add(Draw("Crop_" + i, position + Vector2.up * 0.13f, null, Color.clear, Vector2.one * 0.8f, Depth(position.y)));
            sprinklers.Add(Draw("Sprinkler_" + i, position + new Vector2(0.32f, -0.28f), content.Item("sprinkler")?.icon, Color.clear, Vector2.one * 0.22f, Depth(position.y) + 1));
            Targets.Add(new FarmWorldTarget { Position = position, Id = "plot-" + i, Kind = "plot", Title = "Farm plot", PlotIndex = i });
        }
        Station("home", "sleep", "HOME / SLEEP", new Vector2(-4, 4.4f), content.bed, new Color(0.82f, 0.58f, 0.43f));
        Station("shop", "shop", "SEED SHOP", new Vector2(0.2f, 5f), content.workbench, new Color(0.95f, 0.75f, 0.38f));
        Station("shipping", "shipping", "SHIPPING", new Vector2(3f, 5f), content.chest, new Color(0.85f, 0.67f, 0.4f));
        Station("storage", "storage", "STORAGE", new Vector2(5.8f, 5f), content.chest, new Color(0.63f, 0.76f, 0.93f));
        Station("crafting", "crafting", "WORKBENCH", new Vector2(8.6f, 5f), content.workbench, Color.white);
        for (int i = 0; i < content.villagers.Length; i++)
        {
            FarmNpc npc = content.villagers[i];
            Vector2 position = new Vector2(-4f - i * 2.5f, 0.1f);
            bool layered = npc.portrait == null && npc.layers != null && npc.layers.Length > 0;
            Station(npc.id, "villager", npc.title, position, npc.portrait, layered ? Color.clear : Color.white);
            if (layered)
                for (int layer = 0; layer < npc.layers.Length; layer++)
                    if (npc.layers[layer] != null) Draw(npc.id + "Part" + layer, position, npc.layers[layer], Color.white, Vector2.one * 1.5f, Depth(position.y) + layer);
        }
        for (int i = 0; i < 6; i++)
        {
            Vector2 position = new Vector2(-10.5f + (i % 3) * 2.4f, -3f - (i / 3) * 3f);
            string kind = i < 3 ? "wood" : "stone";
            Station(kind + i, kind, i < 3 ? "TREE" : "ROCK", position, i < 3 ? content.tree : content.rock, Color.white);
        }
        Station("berry0", "berry", "BERRIES", new Vector2(10.5f, -2f), content.Item("berry")?.icon, Color.white);
        Collider("NorthFence", new Vector2(0, 9.5f), new Vector2(28, 1));
        Collider("SouthFence", new Vector2(0, -9.5f), new Vector2(28, 1));
        Collider("WestFence", new Vector2(-13.5f, 0), new Vector2(1, 20));
        Collider("EastFence", new Vector2(13.5f, 0), new Vector2(1, 20));
    }
    private void BuildSceneWorld(FarmContent content, FarmSceneLayout layout)
    {
        worldBounds = layout.worldBounds;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (TilemapRenderer renderer in root.GetComponentsInChildren<TilemapRenderer>())
                renderer.sortingOrder = renderer.name == "Water" ? -980 : -1000 + renderer.sortingOrder;
        foreach (Transform scenery in layout.sceneryRoots)
        {
            if (scenery == null) continue;
            foreach (SpriteRenderer renderer in scenery.GetComponentsInChildren<SpriteRenderer>())
            {
                Bounds bounds = renderer.bounds;
                float foot = bounds.min.y + 0.15f;
                renderer.sortingOrder = Depth(foot);
                if (renderer.GetComponent<Collider2D>() != null) continue;
                bool tree = renderer.name.EndsWith("Tree", System.StringComparison.Ordinal);
                bool fence = renderer.name.StartsWith("Fence", System.StringComparison.Ordinal);
                Vector2 size = tree ? new Vector2(0.65f, 0.45f) :
                    new Vector2(Mathf.Max(0.2f, bounds.size.x * (fence ? 0.9f : 0.8f)), fence ? 0.25f : Mathf.Min(1.4f, bounds.size.y * 0.35f));
                Collider(renderer.name + "Footprint", new Vector2(bounds.center.x, bounds.min.y + size.y * 0.5f), size);
            }
        }
        foreach (Tilemap map in layout.blockedTilemaps)
        {
            if (map == null) continue;
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) Collider("WaterBlocker", map.GetCellCenterWorld(cell), map.layoutGrid.cellSize);
        }
        for (int i = 0; i < FarmSaveData.Width * FarmSaveData.Height; i++)
        {
            Vector2 position = layout.PlotPosition(i);
            soil.Add(Draw("Plot_" + i, position, content.soil, Color.white, Vector2.one * 0.94f, -800));
            crops.Add(Draw("Crop_" + i, position + Vector2.up * 0.13f, null, Color.clear, Vector2.one * 0.8f, Depth(position.y)));
            sprinklers.Add(Draw("Sprinkler_" + i, position + new Vector2(0.32f, -0.28f), content.Item("sprinkler")?.icon, Color.clear, Vector2.one * 0.22f, Depth(position.y) + 1));
            Targets.Add(new FarmWorldTarget { Position = position, Id = "plot-" + i, Kind = "plot", Title = "Farm plot", PlotIndex = i });
        }
        foreach (FarmSceneLayout.Station station in layout.stations)
        {
            if (station == null || station.marker == null) continue;
            Vector2 position = station.marker.position;
            if (station.sceneRenderer != null)
            {
                Targets.Add(new FarmWorldTarget { Id = station.id, Kind = station.kind, Title = station.title, Position = position, Renderer = station.sceneRenderer });
                Label(station.title, position + Vector2.up * 0.8f, 2.6f);
                continue;
            }
            FarmNpc npc = station.kind == "villager" ? System.Array.Find(content.villagers, x => x.id == station.id) : null;
            Sprite sprite = station.kind == "wood" ? content.tree : station.kind == "stone" ? content.rock :
                station.kind == "berry" ? content.Item("berry")?.icon : station.kind == "sleep" ? content.bed :
                station.kind == "shipping" || station.kind == "storage" ? content.chest :
                station.kind == "villager" ? npc?.portrait : content.workbench;
            bool layered = npc != null && npc.portrait == null && npc.layers != null && npc.layers.Length > 0;
            Station(station.id, station.kind, station.title, position, sprite, layered ? Color.clear : Color.white);
            if (layered)
                for (int layer = 0; layer < npc.layers.Length; layer++)
                    if (npc.layers[layer] != null) Draw(npc.id + "Part" + layer, position, npc.layers[layer], Color.white, Vector2.one * 1.5f, Depth(position.y) + layer);
        }
        Collider("NorthBoundary", new Vector2(worldBounds.center.x, worldBounds.yMax + 0.5f), new Vector2(worldBounds.width + 2, 1));
        Collider("SouthBoundary", new Vector2(worldBounds.center.x, worldBounds.yMin - 0.5f), new Vector2(worldBounds.width + 2, 1));
        Collider("WestBoundary", new Vector2(worldBounds.xMin - 0.5f, worldBounds.center.y), new Vector2(1, worldBounds.height + 2));
        Collider("EastBoundary", new Vector2(worldBounds.xMax + 0.5f, worldBounds.center.y), new Vector2(1, worldBounds.height + 2));
    }
    public static Vector2 PlotPosition(int index) => new Vector2(1 + index % FarmSaveData.Width, 1 - index / FarmSaveData.Width);
    public static int Depth(float y) => 100 - Mathf.RoundToInt(y * 10);
    public void SetHighlight(FarmWorldTarget target)
    {
        highlight.enabled = target != null;
        if (target != null) highlight.transform.position = target.Position;
    }
    private SpriteRenderer Draw(string name, Vector2 position, Sprite sprite, Color color, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(worldRoot, false);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : square;
        renderer.color = color;
        renderer.sortingOrder = order;
        Vector2 bounds = renderer.sprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, bounds.x), size.y / Mathf.Max(0.01f, bounds.y), 1);
        return renderer;
    }
    private void Label(string text, Vector2 position, float width)
    {
        Draw(text + "Plate", position, null, new Color(0.16f, 0.23f, 0.14f, 0.85f), new Vector2(width, 0.43f), 590);
        var go = new GameObject(text + "Label");
        go.transform.SetParent(worldRoot, false);
        go.transform.position = position;
        var label = go.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = 2.5f; label.alignment = TextAlignmentOptions.Center;
        label.color = CozyUi.Cream;
        label.rectTransform.sizeDelta = new Vector2(width, 0.6f);
        label.GetComponent<MeshRenderer>().sortingOrder = 600;
    }
    private void Station(string id, string kind, string title, Vector2 position, Sprite sprite, Color tint)
    {
        bool tree = kind == "wood";
        var renderer = Draw(title, position, sprite, tint, tree ? new Vector2(1.7f, 2.1f) : new Vector2(1.15f, 1.15f), Depth(position.y));
        Targets.Add(new FarmWorldTarget { Id = id, Kind = kind, Title = title, Position = position, Renderer = renderer });
        Label(title, position + Vector2.down * (tree ? 1.3f : 0.9f), 2.6f);
        if (kind != "berry") Collider(id + "Collider", position + Vector2.down * 0.25f, new Vector2(0.8f, 0.55f));
    }
    private void Collider(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(worldRoot, false); go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = size;
    }
    private void OnStateChanged(FarmChange changes)
    {
        if ((changes & FarmChange.World) != 0) Refresh();
    }
    private void Refresh()
    {
        var game = runtime.Game;
        for (int i = 0; i < soil.Count; i++)
        {
            var plot = game.Save.farm.plots[i];
            soil[i].color = !plot.tilled ? new Color(0.76f, 0.65f, 0.4f) : plot.watered ? new Color(0.42f, 0.29f, 0.22f) : new Color(0.7f, 0.46f, 0.29f);
            FarmCrop crop = game.Content.Crop(plot.seedId);
            crops[i].enabled = crop != null;
            if (crop != null)
            {
                if (crop.stages.Length > 0)
                    crops[i].sprite = crop.stages[Mathf.Clamp(Mathf.FloorToInt((float)plot.growth / crop.growthDays * (crop.stages.Length - 1)), 0, crop.stages.Length - 1)];
                else crops[i].sprite = game.Content.Item(crop.harvestId)?.icon ?? square;
                crops[i].color = game.Farming.IsReady(plot) ? new Color(1f, 0.94f, 0.62f) : Color.white;
                Vector2 bounds = crops[i].sprite != null ? (Vector2)crops[i].sprite.bounds.size : Vector2.one;
                crops[i].transform.localScale = new Vector3(0.8f / Mathf.Max(bounds.x, 0.01f), 0.9f / Mathf.Max(bounds.y, 0.01f), 1);
            }
            sprinklers[i].color = plot.sprinkler ? Color.white : Color.clear;
        }
        foreach (var target in Targets)
            if (target.Kind == "wood" || target.Kind == "stone" || target.Kind == "berry")
                target.Renderer.color = game.Farming.NodeAvailable(target.Id) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.3f);
    }
    private void LateUpdate()
    {
        if (runtime == null || runtime.Player == null) return;
        playerSorting.sortingOrder = Depth(runtime.Player.position.y) + 2;
        if (worldCamera == null) return;
        worldCamera.transform.position = Vector3.Lerp(worldCamera.transform.position, CameraPosition(worldCamera), 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        float darkness = Mathf.InverseLerp(16f, 22f, runtime.Game.Save.world.hour + runtime.Game.Save.world.minute / 60f);
        worldCamera.backgroundColor = Color.Lerp(dayColor, new Color(0.16f, 0.22f, 0.36f), darkness);
        nightOverlay.color = new Color(0.08f, 0.12f, 0.28f, darkness * 0.48f + (runtime.Game.Save.world.weatherId == "rainy" ? 0.12f : 0));
    }
    private Vector3 CameraPosition(Camera camera)
    {
        float halfY = camera.orthographicSize, halfX = halfY * camera.aspect;
        float x = halfX * 2 >= worldBounds.width ? worldBounds.center.x : Mathf.Clamp(runtime.Player.position.x, worldBounds.xMin + halfX, worldBounds.xMax - halfX);
        float y = halfY * 2 >= worldBounds.height ? worldBounds.center.y : Mathf.Clamp(runtime.Player.position.y, worldBounds.yMin + halfY, worldBounds.yMax - halfY);
        return new Vector3(x, y, -10);
    }
    private void OnDestroy()
    {
        if (runtime != null && runtime.Game != null) runtime.Game.StateChanged -= OnStateChanged;
        if (square != null) Destroy(square);
        if (pixel != null) Destroy(pixel);
    }
}
