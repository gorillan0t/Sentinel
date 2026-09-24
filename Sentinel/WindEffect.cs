using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace Sentinel;

/// <summary>Local-only particle, audio, and foliage effects for the latest wind presets.</summary>
public sealed class WindEffect : MonoBehaviour
{

    private static readonly string[] Names     = { "Off", "Forest", "Desert", "Winter", "Blizzard", "Sandstorm", "Leaf storm", };
    private static readonly float[]  Densities = { 0.5f, 1f, 1.5f, 2f, 3f, };
    private static readonly float[]  Sizes     = { 0.75f, 1f, 1.5f, 2f, 3f, };
    private static readonly float[]  Volumes   = { 0.25f, 0.5f, 0.75f, 1f, };
    private static readonly Preset[] Presets =
    {
            new(0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f),
            new(45f, 6f, 0.05f, 0.12f, 4f, -0.25f, 0.45f, 0f),
            new(240f, 4f, 0.012f, 0.035f, 7f, -0.1f, 0.3f, 0.005f),
            new(220f, 6f, 0.02f, 0.05f, 3.5f, -0.8f, 0.4f, 0f),
            new(1100f, 3f, 0.025f, 0.06f, 13f, -2.2f, 0.9f, 0.008f),
            new(1400f, 2.5f, 0.015f, 0.045f, 15f, -0.3f, 1f, 0.01f),
            new(260f, 4f, 0.05f, 0.13f, 11f, -0.6f, 1.2f, 0f),
    };

    private readonly List<SwayMaterial> nearbyFoliage = new();
    private          int                activePreset;
    private          bool               attemptedLoad;
    private          GameObject         effectRoot;
    private          Shader             foliageShader;
    private          ParticleSystem     haze;
    private          Material           hazeMaterial;
    private          Vector3            lastPosition;
    private          float              nextFoliageScan;
    private          Material           particleMaterial;
    private          ParticleSystem     particles;
    private          Shader             particleShader;
    private          AssetBundle        shaderBundle;
    private          AudioSource        windAudio;
    private          AudioClip          windClip;

    public static string Status { get; private set; } = "Off";

    private void Awake() => SceneManager.sceneUnloaded += OnSceneUnloaded;

    private void Update()
    {
        int           selected = Mathf.Clamp(Cfg.WindPreset.Value, 0, 6);
        GorillaTagger tagger   = GorillaTagger.Instance;
        Transform     camera   = tagger != null && tagger.mainCamera != null ? tagger.mainCamera.transform : null;
        if (selected == 0 || camera == null)
        {
            StopEffect();
            Status = selected == 0 ? "Off" : "Waiting for camera";

            return;
        }

        LoadShaders();
        if (effectRoot == null || activePreset != selected)
        {
            StopEffect();
            CreateEffect(selected, camera.position);
        }

        Preset  preset    = Presets[selected];
        float   strength  = Strength();
        float   heading   = Cfg.WindDirection.Value * Mathf.Deg2Rad;
        Vector3 direction = new(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        float   gust      = 0.86f + Mathf.Sin(Time.unscaledTime * 0.63f) * 0.14f;
        Vector3 velocity  = direction * (preset.Speed * strength * gust);
        if ((camera.position - lastPosition).sqrMagnitude > 100f)
        {
            particles.Clear();
            haze.Clear();
            RestoreFoliage();
        }

        lastPosition                  = camera.position;
        effectRoot.transform.position = camera.position - direction * (strength * preset.Speed * 0.6f) + Vector3.up * 2f;
        SetParticleParameters(particles, preset, velocity, strength);
        SetHazeParameters(preset, velocity * 0.3f, strength);
        UpdateAudio(preset, strength, gust);
        if (Cfg.WindFoliage.Value && strength > 0f && foliageShader != null)
        {
            if (Time.unscaledTime >= nextFoliageScan)
            {
                nextFoliageScan = Time.unscaledTime + 5f;
                ScanFoliage(camera.position);
            }

            foreach (SwayMaterial item in nearbyFoliage)
            {
                if (item.Renderer == null) continue;
                Bounds bounds = item.Renderer.bounds;
                foreach (Material material in item.Substitutes)
                {
                    if (material == null) continue;
                    material.SetVector("_WindDir", new Vector4(direction.x, 0f, direction.z, strength * gust));
                    material.SetFloat("_WindTime",   Time.unscaledTime);
                    material.SetFloat("_BaseHeight", bounds.min.y);
                    material.SetFloat("_Height",     bounds.size.y);
                }
            }
        }
        else RestoreFoliage();

        Status = strength == 0f ? "Calm" : PresetName() + " active";
    }

    private void OnDisable() => StopEffect();

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        StopEffect();
        if (shaderBundle != null) shaderBundle.Unload(false);
    }
    public static string PresetName()                                  => Names[Mathf.Clamp(Cfg.WindPreset.Value, 0, Names.Length - 1)];
    public static void   CyclePreset()                                 => Cfg.WindPreset.Value = (Mathf.Clamp(Cfg.WindPreset.Value, 0, 6) + 1) % 7;
    public static string StrengthName()                                => Strength() switch { <= 0f => "Calm 0%", < 0.35f => $"Breeze {Strength():P0}", < 0.75f => $"Windy {Strength():P0}", var _ => $"Storm {Strength():P0}", };
    public static void   ChangeStrength(float amount)                  => Cfg.WindStrength.Value = Mathf.Clamp01(Cfg.WindStrength.Value + amount);
    public static void   Rotate(int           degrees)                 => Cfg.WindDirection.Value = ((Cfg.WindDirection.Value + degrees) % 360 + 360) % 360;
    public static string DirectionName()                               => $"{(Cfg.WindDirection.Value % 360 + 360) % 360}° (world heading)";
    public static void   StepDensity(int direction, bool wrap = false) => Cfg.WindDensity.Value = Step(Densities, Cfg.WindDensity.Value, direction, wrap);
    public static string DensityName()                              => "x" + Cfg.WindDensity.Value.ToString("0.##", CultureInfo.InvariantCulture);
    public static void   StepSize(int direction, bool wrap = false) => Cfg.WindSize.Value = Step(Sizes, Cfg.WindSize.Value, direction, wrap);
    public static string SizeName()                                   => "x" + Cfg.WindSize.Value.ToString("0.##", CultureInfo.InvariantCulture);
    public static void   StepVolume(int direction, bool wrap = false) => Cfg.WindVolume.Value = Step(Volumes, Cfg.WindVolume.Value, direction, wrap);
    public static string VolumeName() => $"{Mathf.RoundToInt(Mathf.Clamp01(Cfg.WindVolume.Value) * 100f)}%";

    private static float Strength() => Mathf.Clamp01(Cfg.WindStrength.Value);

    private static float Step(float[] choices, float value, int direction, bool wrap)
    {
        int current = 0;
        for (int i = 1; i < choices.Length; i++)
            if (Mathf.Abs(choices[i] - value) < Mathf.Abs(choices[current] - value))
                current = i;

        int next       = current + Math.Sign(direction);
        if (wrap) next = (next % choices.Length + choices.Length) % choices.Length;

        return choices[Mathf.Clamp(next, 0, choices.Length - 1)];
    }

    private void OnSceneUnloaded(Scene scene) => StopEffect();

    private void LoadShaders()
    {
        if (attemptedLoad) return;
        attemptedLoad = true;
        try
        {
            using Stream resource = typeof(WindEffect).Assembly.GetManifestResourceStream("Sentinel.wind-effects.bundle");

            if (resource == null) return;
            using MemoryStream bytes = new();
            resource.CopyTo(bytes);
            shaderBundle = AssetBundle.LoadFromMemory(bytes.ToArray());

            if (shaderBundle == null) return;
            particleShader = shaderBundle.LoadAsset<Shader>("Assets/WindParticles.shader");
            foliageShader  = shaderBundle.LoadAsset<Shader>("Assets/WindFoliage.shader");
            if (particleShader != null && !particleShader.isSupported) particleShader = null;
            if (foliageShader  != null && !foliageShader.isSupported) foliageShader   = null;
        }
        catch (Exception exception) { Debug.LogWarning("[Sentinel] Wind shaders unavailable: " + exception.Message); }
    }

    private void CreateEffect(int preset, Vector3 position)
    {
        activePreset = preset;
        effectRoot   = new GameObject("Sentinel local wind");
        effectRoot.transform.SetParent(transform, false);
        effectRoot.transform.position = lastPosition = position;
        if (particleShader != null)
        {
            particleMaterial = new Material(particleShader);
            hazeMaterial     = new Material(particleShader);
            hazeMaterial.SetFloat("_Ripple", 0f);
        }

        particles              = CreateParticles("particles", particleMaterial);
        haze                   = CreateParticles("haze",      hazeMaterial);
        windAudio              = effectRoot.AddComponent<AudioSource>();
        windAudio.loop         = true;
        windAudio.spatialBlend = 0f;
        windClip               = CreateWindClip();
        windAudio.clip         = windClip;
    }

    private ParticleSystem CreateParticles(string name, Material material)
    {
        GameObject root = new(name);
        root.transform.SetParent(effectRoot.transform, false);
        ParticleSystem            system = root.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main   = system.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 5000;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(24f, 8f, 24f);
        ParticleSystemRenderer renderer               = root.GetComponent<ParticleSystemRenderer>();
        if (material != null) renderer.sharedMaterial = material;
        system.Play();

        return system;
    }

    private void SetParticleParameters(ParticleSystem system, Preset preset, Vector3 wind, float strength)
    {
        ParticleSystem.MainModule main = system.main;
        main.startLifetime   = preset.Lifetime;
        main.startSize       = new ParticleSystem.MinMaxCurve(preset.MinSize * Mathf.Clamp(Cfg.WindSize.Value, 0.75f, 3f), preset.MaxSize * Mathf.Clamp(Cfg.WindSize.Value, 0.75f, 3f));
        main.startSpeed      = 0f;
        main.gravityModifier = -preset.Gravity;
        main.maxParticles    = Mathf.Clamp(Mathf.CeilToInt(preset.Particles * preset.Lifetime * 1.2f * Mathf.Clamp(Cfg.WindDensity.Value, 0.5f, 3f)), 1, 5000);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = preset.Particles * strength * Mathf.Clamp(Cfg.WindDensity.Value, 0.5f, 3f);
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space   = ParticleSystemSimulationSpace.World;
        velocity.x       = wind.x;
        velocity.z       = wind.z;
        if (strength <= 0f) system.Clear();
    }

    private void SetHazeParameters(Preset preset, Vector3 wind, float strength)
    {
        ParticleSystem.MainModule main = haze.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSize     = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.maxParticles  = 140;
        ParticleSystem.EmissionModule emission = haze.emission;
        emission.rateOverTime = Cfg.WindFog.Value ? preset.Haze * 500f * strength * Mathf.Clamp(Cfg.WindDensity.Value, 0.5f, 3f) : 0f;
        ParticleSystem.VelocityOverLifetimeModule velocity = haze.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space   = ParticleSystemSimulationSpace.World;
        velocity.x       = wind.x;
        velocity.z       = wind.z;
        if (!Cfg.WindFog.Value || strength <= 0f) haze.Clear();
    }

    private void UpdateAudio(Preset preset, float strength, float gust)
    {
        windAudio.volume = Cfg.WindAudio.Value ? Mathf.Clamp01(strength * strength * 0.3f * gust * Mathf.Clamp01(Cfg.WindVolume.Value)) : 0f;
        windAudio.pitch  = 0.9f + strength * 0.15f;
        if (windAudio.volume > 0f  && !windAudio.isPlaying) windAudio.Play();
        if (windAudio.volume == 0f && windAudio.isPlaying) windAudio.Stop();
    }

    private static AudioClip CreateWindClip()
    {
        const int frequency = 22050;
        float[]   samples   = new float[frequency * 12];
        Random    random    = new(6149);
        float     lowPass   = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            lowPass    += ((float)random.NextDouble() * 2f - 1f - lowPass) * 0.04f;
            samples[i] =  lowPass                                          * (0.55f + 0.25f * Mathf.Sin(i * Mathf.PI * 6f / samples.Length));
        }

        AudioClip clip = AudioClip.Create("Sentinel local wind", samples.Length, 1, frequency, false);
        clip.SetData(samples, 0);

        return clip;
    }

    private void ScanFoliage(Vector3 origin)
    {
        RestoreFoliage();
        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
        int            count     = 0;
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null || (renderer.bounds.center - origin).sqrMagnitude > 24f * 24f || count >= 24) continue;
            Material[] original     = renderer.sharedMaterials;
            Material[] replacements = (Material[])original.Clone();
            bool       changed      = false;
            for (int i = 0; i < original.Length; i++)
            {
                Material source = original[i];

                if (source == null || source.mainTexture == null || source.renderQueue > 2500) continue;
                string name = source.name.ToLowerInvariant();

                if (!name.Contains("leaves") && !name.Contains("foliage") && !name.Contains("leaf")) continue;
                Material copy = new(foliageShader) { name = "Sentinel sway / " + source.name, mainTexture = source.mainTexture, };
                copy.mainTextureScale  = source.mainTextureScale;
                copy.mainTextureOffset = source.mainTextureOffset;
                if (source.HasProperty("_Color")) copy.SetColor("_Color", source.GetColor("_Color"));
                copy.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
                replacements[i] = copy;
                changed         = true;
            }

            if (!changed) continue;
            renderer.sharedMaterials = replacements;
            nearbyFoliage.Add(new SwayMaterial { Renderer = renderer, Original = original, Substitutes = replacements, });
            count++;
        }
    }

    private void RestoreFoliage()
    {
        foreach (SwayMaterial item in nearbyFoliage)
        {
            if (item.Renderer != null)
            {
                Material[] current = item.Renderer.sharedMaterials;
                for (int i = 0; i < current.Length && i < item.Substitutes.Length; i++)
                    if (current[i] == item.Substitutes[i])
                        current[i] = item.Original[i];

                item.Renderer.sharedMaterials = current;
            }

            for (int i = 0; i < item.Substitutes.Length; i++)
                if (item.Substitutes[i] != item.Original[i])
                    Destroy(item.Substitutes[i]);
        }

        nearbyFoliage.Clear();
    }

    private void StopEffect()
    {
        RestoreFoliage();
        if (windAudio        != null) windAudio.Stop();
        if (effectRoot       != null) Destroy(effectRoot);
        if (windClip         != null) Destroy(windClip);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (hazeMaterial     != null) Destroy(hazeMaterial);
        effectRoot       = null;
        particles        = haze = null;
        windAudio        = null;
        windClip         = null;
        particleMaterial = hazeMaterial = null;
        activePreset     = 0;
        nextFoliageScan  = 0f;
    }

    private readonly struct Preset
    {
        public readonly float Particles, Lifetime, MinSize, MaxSize, Speed, Gravity, Gust, Haze;
        public Preset(float particles, float lifetime, float minSize, float maxSize, float speed, float gravity, float gust, float haze)
        {
            Particles = particles;
            Lifetime  = lifetime;
            MinSize   = minSize;
            MaxSize   = maxSize;
            Speed     = speed;
            Gravity   = gravity;
            Gust      = gust;
            Haze      = haze;
        }
    }

    private sealed class SwayMaterial
    {
        public Material[]   Original;
        public MeshRenderer Renderer;
        public Material[]   Substitutes;
    }
}