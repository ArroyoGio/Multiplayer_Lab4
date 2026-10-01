using UnityEngine;

// =========================================================
// EFECTOS DE ACOMPAÑAMIENTO DEL COMBATE (punto 5)
//
// Sonido, partículas y números de daño.
//
// SonEventos NO FIABLES: si se pierde alguno no pasa absolutamente
// nada. Además NUNCA tocan la vida ni el daño: solo pintan y suena.
// =========================================================

public static class CombatVfx
{
    public static readonly Color DamageNumberColor = new Color(1f, 0.35f, 0.3f, 1f);
    public static readonly Color DeathNumberColor = new Color(1f, 0.2f, 0.2f, 1f);

    private const int SampleRate = 44100;

    private static Transform fxRoot;
    private static AudioSource audioSource;
    private static AudioClip swingClip;
    private static AudioClip impactClip;
    private static AudioClip deathClip;


    // =========================================================
    // GOLPE QUE SÍ IMPACTA
    // =========================================================

    public static void PlayImpact(Vector3 position)
    {
        PlaySound(impactClip, position, 0.9f);
        CreateParticles(position, new Color(1f, 0.75f, 0.25f), 18, 3.5f, 0.18f, 0.45f);
    }

    // =========================================================
    // MUERTE
    // =========================================================

    public static void PlayDeath(Vector3 position)
    {
        PlaySound(deathClip, position, 1f);
        CreateParticles(position, new Color(1f, 0.25f, 0.25f), 45, 5f, 0.28f, 0.8f);
    }

    // =========================================================
    // GOLPE QUE SE PIDIÓ (feedback inmediato del atacante)
    // =========================================================

    public static void PlaySwing(Vector3 position)
    {
        PlaySound(swingClip, position, 0.5f);
    }


    // =========================================================
    // PARTÍCULAS
    // =========================================================

    private static void CreateParticles(
        Vector3 position,
        Color color,
        int amount,
        float speed,
        float size,
        float lifeTime)
    {
        GameObject particlesObject = new GameObject("HitParticles");

        particlesObject.transform.position = position;
        particlesObject.transform.SetParent(GetRoot(), true);

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeTime * 0.6f, lifeTime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[1]
        {
            new ParticleSystem.Burst(0f, (short)amount)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        Object.Destroy(particlesObject, 1.5f);

        particles.Play(true);
    }


    // =========================================================
    // SONIDO
    //
    // El proyecto no trae clips, así que los generamos por código.
    // =========================================================

    private static void PlaySound(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null) return;

        AudioSource source = GetAudioSource();

        source.transform.position = position;
        source.PlayOneShot(clip, volume);
    }

    private static AudioSource GetAudioSource()
    {
        if (audioSource != null) return audioSource;

        GameObject audioObject = new GameObject("CombatVfxAudio");
        Object.DontDestroyOnLoad(audioObject);

        audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 25f;

        swingClip = CreateSwingClip();
        impactClip = CreateImpactClip();
        deathClip = CreateDeathClip();

        return audioSource;
    }

    private static AudioClip CreateSwingClip()
    {
        int count = (int)(SampleRate * 0.15f);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = 1f - t / 0.15f;
            data[i] = Mathf.Sin(2f * Mathf.PI * 420f * t) * envelope * 0.25f;
        }

        AudioClip clip = AudioClip.Create("CombatVfx_Swing", count, 1, SampleRate, false);
        clip.SetData(data, 0);

        return clip;
    }

    private static AudioClip CreateImpactClip()
    {
        int count = (int)(SampleRate * 0.18f);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = 1f - t / 0.18f;
            float noise = Random.value * 2f - 1f;
            float tone = Mathf.Sin(2f * Mathf.PI * 160f * t);

            data[i] = (noise * 0.6f + tone * 0.4f) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("CombatVfx_Impact", count, 1, SampleRate, false);
        clip.SetData(data, 0);

        return clip;
    }

    private static AudioClip CreateDeathClip()
    {
        int count = (int)(SampleRate * 0.6f);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = 1f - t / 0.6f;
            float frequency = Mathf.Lerp(220f, 60f, t / 0.6f);

            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.45f;
        }

        AudioClip clip = AudioClip.Create("CombatVfx_Death", count, 1, SampleRate, false);
        clip.SetData(data, 0);

        return clip;
    }


    // Contenedor de todos los efectos. Se comparte con el daño flotante.
    public static Transform Root => GetRoot();

    private static Transform GetRoot()
    {
        if (fxRoot != null) return fxRoot;

        GameObject rootObject = new GameObject("CombatVfx");
        Object.DontDestroyOnLoad(rootObject);

        fxRoot = rootObject.transform;

        return fxRoot;
    }
}
