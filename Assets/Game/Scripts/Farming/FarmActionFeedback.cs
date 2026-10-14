using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Bounded visual pools and short sounds; rewards are committed by the farming service, never by effects.</summary>
public sealed class FarmActionFeedback : MonoBehaviour
{
    private sealed class Particle
    {
        public SpriteRenderer view;
        public Vector2 origin, velocity;
        public float age, life;
        public Color color;
        public bool pickup;
    }
    private sealed class Caption
    {
        public TextMeshPro view;
        public Vector2 origin;
        public float age;
    }
    private readonly List<Particle> particles = new List<Particle>();
    private readonly List<Caption> captions = new List<Caption>();
    private readonly Dictionary<PlayerAnimationType, AudioClip> sounds = new Dictionary<PlayerAnimationType, AudioClip>();
    private FarmRuntime runtime;
    private FarmActionAssets assets;
    private AudioSource audioSource;
    private Sprite square;
    private Texture2D pixel;
    private int particleCursor, captionCursor;
    private Transform reacting;
    private Vector3 restingScale;
    private float reactionAge;
    public int ActiveParticleCount => particles.FindAll(x => x.view.enabled).Count;

    public void Initialize(FarmRuntime owner)
    {
        runtime = owner;
        assets = Resources.Load<FarmActionAssets>("FarmActionAssets");
        pixel = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        pixel.SetPixel(0, 0, Color.white); pixel.Apply();
        square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0; audioSource.volume = 0.45f;
        for (int i = 0; i < 48; i++)
        {
            var view = new GameObject("ActionParticle" + i).AddComponent<SpriteRenderer>();
            view.transform.SetParent(transform, false); view.enabled = false;
            particles.Add(new Particle { view = view });
        }
        for (int i = 0; i < 8; i++)
        {
            var view = new GameObject("ActionCaption" + i).AddComponent<TextMeshPro>();
            view.transform.SetParent(transform, false);
            view.font = ChineseUiFont.Font; view.fontSize = 2.3f;
            view.alignment = TextAlignmentOptions.Center;
            view.rectTransform.sizeDelta = new Vector2(4, 0.5f);
            view.GetComponent<MeshRenderer>().sortingOrder = 650;
            view.gameObject.SetActive(false);
            captions.Add(new Caption { view = view });
        }
        foreach (var action in new[] { PlayerAnimationType.Hoe, PlayerAnimationType.Pickaxe, PlayerAnimationType.Axe, PlayerAnimationType.Water, PlayerAnimationType.Harvest, PlayerAnimationType.Plant })
            sounds[action] = MakeSound(action);
        runtime.Game.DayEnded += Clear;
    }

    public void Play(PlayerAnimationType action, FarmWorldTarget target, FarmResult result, string itemId, int count)
    {
        if (!result.Success)
        {
            ShowCaption(target.Position, "无法使用", new Color(1, 0.58f, 0.48f));
            return;
        }
        AudioClip sound = assets != null ? assets.Find(action)?.sound : null;
        if (sound == null) sounds.TryGetValue(action, out sound);
        if (sound != null) audioSource.PlayOneShot(sound);
        Color tint = action == PlayerAnimationType.Water ? new Color(0.45f, 0.82f, 1) :
            action == PlayerAnimationType.Pickaxe ? new Color(0.8f, 0.83f, 0.86f) :
            action == PlayerAnimationType.Harvest || action == PlayerAnimationType.Plant ? new Color(0.72f, 0.95f, 0.42f) : new Color(0.76f, 0.54f, 0.32f);
        for (int i = 0; i < 9; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2 / 9;
            Spawn(target.Position, square, tint, 0.06f + (i % 3) * 0.025f,
                new Vector2(Mathf.Cos(angle) * 0.8f, 0.6f + Mathf.Sin(angle) * 0.65f), 0.35f + i * 0.025f, false);
        }
        if (count > 0 && !string.IsNullOrEmpty(itemId))
        {
            FarmItem item = runtime.Game.Content.Item(itemId);
            if (item?.icon != null) Spawn(target.Position, item.icon, Color.white, 0.38f, Vector2.up * 1.5f, 0.7f, true);
            ShowCaption(target.Position, "+" + count + " " + (item?.title ?? itemId), new Color(1, 0.94f, 0.62f));
        }
        else ShowCaption(target.Position, action == PlayerAnimationType.Water ? "已浇水" : action == PlayerAnimationType.Hoe ? "已耕地" :
            action == PlayerAnimationType.Pickaxe ? "已清除" : "已播种", tint);
        RestoreReaction();
        if (target.Renderer != null)
        {
            reacting = target.Renderer.transform;
            restingScale = reacting.localScale;
            reactionAge = 0;
        }
    }

    private void Spawn(Vector2 origin, Sprite sprite, Color color, float size, Vector2 velocity, float life, bool pickup)
    {
        var p = particles[particleCursor++ % particles.Count];
        p.origin = origin; p.velocity = velocity; p.age = 0; p.life = life; p.pickup = pickup; p.color = color;
        p.view.sprite = sprite; p.view.color = color; p.view.enabled = true;
        p.view.sortingOrder = 620;
        p.view.transform.position = origin;
        p.view.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
    }
    private void ShowCaption(Vector2 origin, string text, Color color)
    {
        Vector2 position = origin + Vector2.up * 0.65f;
        var c = captions.Find(x => x.view.gameObject.activeSelf && (x.origin - position).sqrMagnitude < 0.04f);
        if (c == null) c = captions[captionCursor++ % captions.Count];
        c.age = 0; c.origin = position;
        c.view.text = text; c.view.color = color;
        c.view.transform.position = c.origin;
        c.view.gameObject.SetActive(true);
    }
    private void Update()
    {
        var root = GameRoot.Instance;
        if (runtime == null || root == null || root.Pause.IsPaused || root.InputModes.CurrentMode != GameInputMode.Gameplay) return;
        foreach (var p in particles)
        {
            if (!p.view.enabled) continue;
            p.age += Time.deltaTime;
            float t = p.age / p.life;
            if (t >= 1) { p.view.enabled = false; continue; }
            Vector2 position = p.origin + p.velocity * p.age + Vector2.down * (2.2f * p.age * p.age);
            if (p.pickup)
            {
                Vector2 top = p.origin + Vector2.up * 0.65f;
                position = t < 0.4f ? Vector2.Lerp(p.origin, top, t / 0.4f) :
                    Vector2.Lerp(top, (Vector2)runtime.Player.position + Vector2.up * 0.4f, Mathf.SmoothStep(0, 1, (t - 0.4f) / 0.6f));
            }
            p.view.transform.position = position;
            Color color = p.color; color.a *= 1 - Mathf.InverseLerp(0.65f, 1, t); p.view.color = color;
        }
        foreach (var c in captions)
        {
            if (!c.view.gameObject.activeSelf) continue;
            c.age += Time.deltaTime;
            if (c.age >= 0.85f) { c.view.gameObject.SetActive(false); continue; }
            c.view.transform.position = c.origin + Vector2.up * c.age * 0.5f;
            Color color = c.view.color; color.a = 1 - Mathf.InverseLerp(0.45f, 0.85f, c.age); c.view.color = color;
        }
        if (reacting != null)
        {
            reactionAge += Time.deltaTime;
            if (reactionAge >= 0.22f) RestoreReaction();
            else reacting.localScale = restingScale * (1 + Mathf.Sin(reactionAge / 0.22f * Mathf.PI * 2) * 0.07f * (1 - reactionAge / 0.22f));
        }
    }
    private static AudioClip MakeSound(PlayerAnimationType action)
    {
        const int rate = 22050;
        float length = action == PlayerAnimationType.Water ? 0.24f : 0.16f;
        var samples = new float[Mathf.CeilToInt(rate * length)];
        uint seed = 17;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate, progress = t / length;
            seed = seed * 1664525 + 1013904223;
            float noise = ((seed >> 8) / 16777215f * 2 - 1);
            float frequency = action == PlayerAnimationType.Harvest ? 660 + progress * 440 :
                action == PlayerAnimationType.Water ? 950 : action == PlayerAnimationType.Pickaxe ? 320 : action == PlayerAnimationType.Plant ? 420 : 150;
            float tone = Mathf.Sin(2 * Mathf.PI * frequency * t);
            float mix = action == PlayerAnimationType.Harvest || action == PlayerAnimationType.Plant ? 0.85f : action == PlayerAnimationType.Water ? 0.12f : 0.4f;
            float envelope = Mathf.Min(1, t * 600) * Mathf.Pow(1 - progress, 2);
            samples[i] = (tone * mix + noise * (1 - mix)) * envelope * 0.4f;
        }
        var clip = AudioClip.Create("Farm_" + action, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    private void RestoreReaction()
    {
        if (reacting != null) reacting.localScale = restingScale;
        reacting = null;
    }
    public void Clear()
    {
        RestoreReaction();
        foreach (var p in particles) p.view.enabled = false;
        foreach (var c in captions) c.view.gameObject.SetActive(false);
        if (audioSource != null) audioSource.Stop();
    }
    private void OnDestroy()
    {
        if (runtime != null && runtime.Game != null) runtime.Game.DayEnded -= Clear;
        RestoreReaction();
        foreach (var clip in sounds.Values) if (clip != null) Destroy(clip);
        if (square != null) Destroy(square);
        if (pixel != null) Destroy(pixel);
    }
}
