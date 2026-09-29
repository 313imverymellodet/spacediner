using UnityEngine;

// Synthesized SFX + a lo-fi music loop. No audio files shipped.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;

    AudioSource[] voices; int next;
    AudioSource music;
    AudioClip pop, drop, coin, register, unlock, whoosh, click, trash, deny, levelUp;
    public bool Muted { get; private set; }
    System.Random rnd = new System.Random(3);
    float N() => (float)(rnd.NextDouble() * 2 - 1);
    float lastCoin;
    int coinStep;

    void Awake()
    {
        I = this;
        voices = new AudioSource[8];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = 0.32f; music.playOnAwake = false;
        Build();
        music.clip = Music();
    }

    public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0 : 1; }
    public void StartMusic() { if (!music.isPlaying) music.Play(); }

    void Play(AudioClip c, float vol, float pitch = 1f)
    {
        var s = voices[next]; next = (next + 1) % voices.Length;
        s.pitch = pitch; s.PlayOneShot(c, vol);
    }

    public void Pop() => Play(pop, 0.35f, Random.Range(0.92f, 1.12f));
    public void Drop() => Play(drop, 0.35f, Random.Range(0.95f, 1.05f));
    public void Register() => Play(register, 0.55f);
    public void Unlock() => Play(unlock, 0.6f);
    public void Whoosh() => Play(whoosh, 0.5f);
    public void Click() => Play(click, 0.4f);
    public void Trash() => Play(trash, 0.4f, Random.Range(0.9f, 1.1f));
    public void Deny() => Play(deny, 0.35f);
    public void LevelUp() => Play(levelUp, 0.6f);
    public void MusicBoost() => music.volume = 0.42f;
    public void Rush() { Play(unlock, 0.7f, 1.25f); music.pitch = 1.12f; }
    public void RushEnd() => music.pitch = 1f;

    // Rapid coins climb in pitch — the "money vacuum" feeling.
    public void Coin()
    {
        if (Time.unscaledTime - lastCoin > 0.5f) coinStep = 0;
        lastCoin = Time.unscaledTime;
        Play(coin, 0.3f, 1f + Mathf.Min(coinStep++, 14) * 0.04f);
    }

    static AudioClip Clip(string n, float[] d) { var c = AudioClip.Create(n, d.Length, 1, SR, false); c.SetData(d, 0); return c; }
    delegate float Gen(float t, float dt);
    static float[] R(float dur, Gen g)
    {
        int n = (int)(SR * dur); var d = new float[n]; float dt = 1f / SR;
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i * dt, dt) * Mathf.Clamp01((n - i) / (SR * 0.01f)), -1, 1);
        return d;
    }

    void Build()
    {
        float ph = 0;
        pop = Clip("pop", R(0.08f, (t, dt) => { ph += TAU * Mathf.Lerp(380, 900, t / 0.08f) * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 45) * 0.8f; }));
        ph = 0;
        drop = Clip("drop", R(0.09f, (t, dt) => { ph += TAU * Mathf.Lerp(700, 300, t / 0.09f) * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 40) * 0.8f; }));
        ph = 0;
        coin = Clip("coin", R(0.16f, (t, dt) => { ph += TAU * (t < 0.04f ? 1760 : 2637) * dt; return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2)) * Mathf.Exp(-t * 22) * 0.5f; }));
        float[] rn = { 1046.5f, 1318.5f, 1568f };
        ph = 0;
        register = Clip("register", R(0.45f, (t, dt) =>
        {
            float bell = 0;
            int k = Mathf.Min((int)(t / 0.06f), 2);
            ph += TAU * rn[k] * dt;
            bell = Mathf.Sin(ph) * Mathf.Exp(-(t - k * 0.06f) * (k < 2 ? 20 : 7)) * 0.45f;
            float ching = t < 0.03f ? N() * 0.4f * (1 - t / 0.03f) : 0;
            return bell + ching;
        }));
        float[] un = { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f };
        ph = 0;
        unlock = Clip("unlock", R(1.0f, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / 0.075f), 6);
            ph += TAU * un[k] * dt;
            float env = Mathf.Exp(-(t - k * 0.075f) * (k < 6 ? 12 : 3.5f));
            return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2) + 0.15f * Mathf.Sin(ph * 3)) * env * 0.38f;
        }));
        float lp = 0;
        whoosh = Clip("whoosh", R(0.5f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.03f, 0.35f, Mathf.Sin(t / 0.5f * Mathf.PI)); return lp * Mathf.Sin(t / 0.5f * Mathf.PI) * 1.2f; }));
        ph = 0;
        click = Clip("click", R(0.04f, (t, dt) => { ph += TAU * 1300 * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 90) * 0.6f; }));
        lp = 0;
        trash = Clip("trash", R(0.18f, (t, dt) => { lp += (N() - lp) * 0.25f; ph += TAU * 180 * dt; return (lp * 0.8f + Mathf.Sin(ph) * 0.3f) * Mathf.Exp(-t * 18); }));
        ph = 0;
        deny = Clip("deny", R(0.18f, (t, dt) => { ph += TAU * 160 * dt; return Mathf.Sign(Mathf.Sin(ph)) * 0.25f * Mathf.Exp(-t * 10); }));
        float[] lu = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f };
        ph = 0;
        levelUp = Clip("levelup", R(0.7f, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / 0.06f), 4);
            ph += TAU * lu[k] * dt;
            return (Mathf.Sin(ph) + 0.25f * Mathf.Sin(ph * 3)) * Mathf.Exp(-(t - k * 0.06f) * (k < 4 ? 14 : 4)) * 0.4f;
        }));
    }

    // Chill lo-fi loop: Fmaj7 – Em7 – Dm7 – Cmaj7 at 84 bpm, soft kick/hat, warm bass, electric-piano chords.
    AudioClip Music()
    {
        float bpm = 84f, beat = 60f / bpm;
        int bars = 4; float dur = beat * 4 * bars;
        int n = (int)(SR * dur); var d = new float[n];
        float[][] chords =
        {
            new[] { 174.61f, 220f, 261.63f, 329.63f },
            new[] { 164.81f, 196f, 246.94f, 293.66f },
            new[] { 146.83f, 174.61f, 220f, 261.63f },
            new[] { 130.81f, 164.81f, 196f, 246.94f },
        };
        float[] roots = { 87.31f, 82.41f, 73.42f, 65.41f };
        float[] mel = { 659.25f, 587.33f, 523.25f, 587.33f, 659.25f, 783.99f, 659.25f, 523.25f };
        float hp = 0, blp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, bt = t / beat;
            int bi = (int)bt, bar = (bi / 4) % bars;
            float ib = (bt - bi) * beat;
            // swingy kick on 1 and 3, soft snare-ish on 2 and 4
            float kick = (bi % 2 == 0) ? Mathf.Sin(TAU * (50 + 70 * Mathf.Exp(-ib * 30)) * ib) * Mathf.Exp(-ib * 9) * 0.5f : 0;
            float nz = N();
            float snare = (bi % 2 == 1) ? (nz - hp) * Mathf.Exp(-ib * 22) * 0.12f : 0;
            float e8 = bt * 2; int e8i = (int)e8; float i8 = (e8 - e8i) * beat / 2;
            float hat = (nz - hp) * Mathf.Exp(-i8 * 70) * 0.05f; hp = nz;
            // bass
            float bf = roots[bar] * ((bi % 4 == 3 && ib > beat * 0.5f) ? 1.5f : 1f);
            blp += (Mathf.Sin(TAU * bf * t) + 0.4f * Mathf.Sin(TAU * bf * 2 * t) - blp) * 0.2f;
            float bass = blp * 0.28f * (1f - 0.4f * Mathf.Exp(-ib * 6));
            // e-piano chord stabs on beat 1 and the "and" of 2
            float barT = (bt % 4) * beat;
            float stab1 = barT, stab2 = barT - beat * 1.5f;
            float ep = 0;
            foreach (var f in chords[bar])
            {
                if (stab1 >= 0) ep += Mathf.Sin(TAU * f * stab1 + Mathf.Sin(TAU * f * 2 * stab1) * 0.4f * Mathf.Exp(-stab1 * 3)) * Mathf.Exp(-stab1 * 1.6f);
                if (stab2 >= 0) ep += Mathf.Sin(TAU * f * stab2 + Mathf.Sin(TAU * f * 2 * stab2) * 0.4f * Mathf.Exp(-stab2 * 3)) * Mathf.Exp(-stab2 * 2.2f) * 0.6f;
            }
            ep *= 0.05f;
            // sparse bell melody on beats
            int mi = (bi % 8);
            float bell = (bi % 2 == 0) ? Mathf.Sin(TAU * mel[mi] * ib) * Mathf.Exp(-ib * 5) * 0.045f : 0;
            // vinyl crackle
            float crackle = rnd.NextDouble() < 0.0006 ? N() * 0.15f : 0;
            d[i] = Mathf.Clamp((kick + snare + hat + bass + ep + bell + crackle) * 0.9f, -1, 1);
        }
        return Clip("music", d);
    }
}
