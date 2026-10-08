using UnityEngine;
using UnityEngine.UI;

// Procedural HUD art: crisp rounded panels, rims, glossy bar fills, shine sweeps, a heart. No texture files.
public static class HudArt
{
    static Sprite round, rim, fill, gloss, shine, heart, glow, ring, disc;

    static Texture2D Tex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    static float RoundAlpha(int x, int y, int n, float r)
    {
        float inner = n / 2f - r;
        float dx = Mathf.Max(Mathf.Abs(x + .5f - n / 2f) - inner, 0), dy = Mathf.Max(Mathf.Abs(y + .5f - n / 2f) - inner, 0);
        return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
    }

    // rounded rect (radius 12), 9-sliced
    public static Sprite Round
    {
        get
        {
            if (round) return round;
            int n = 48; var t = Tex(n, n); var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, RoundAlpha(x, y, n, 12));
            t.SetPixels(px); t.Apply();
            return round = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        }
    }

    // just the outline of the same rounded rect (2.5 px), 9-sliced
    public static Sprite Rim
    {
        get
        {
            if (rim) return rim;
            int n = 48; var t = Tex(n, n); var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float outer = RoundAlpha(x, y, n, 12);
                // inset shape: same rounding, 2.5 px in
                float inner = n / 2f - 12, r2 = 9.5f;
                float dx = Mathf.Max(Mathf.Abs(x + .5f - n / 2f) - inner, 0), dy = Mathf.Max(Mathf.Abs(y + .5f - n / 2f) - inner, 0);
                float inA = Mathf.Clamp01(r2 - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                if (Mathf.Abs(x + .5f - n / 2f) < inner && Mathf.Abs(y + .5f - n / 2f) < n / 2f - 2.5f) inA = 1;
                if (Mathf.Abs(y + .5f - n / 2f) < inner && Mathf.Abs(x + .5f - n / 2f) < n / 2f - 2.5f) inA = 1;
                px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(outer - inA));
            }
            t.SetPixels(px); t.Apply();
            return rim = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        }
    }

    // shading for the fill: clear at the top, darker toward the bottom (drawn over the flat colour)
    public static Sprite Fill
    {
        get
        {
            if (fill) return fill;
            int w = 4, h = 64; var t = Tex(w, h); var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                float a = Mathf.Pow(1f - v, 1.4f) * 0.42f + (v < 0.07f ? 0.15f : 0f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(0, 0, 0, a);
            }
            t.SetPixels(px); t.Apply();
            return fill = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
        }
    }

    // rounded fill with the shading baked in (tinted by the bar colour), and a rounded gloss to lay on top
    static Sprite fillRound, glossRound;
    public static Sprite FillRound
    {
        get
        {
            if (fillRound) return fillRound;
            int n = 48; var t = Tex(n, n); var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float v = (y + .5f) / n, b = Mathf.Lerp(0.58f, 1f, Mathf.Pow(v, 0.8f));
                if (v < 0.1f) b *= 0.8f;
                px[y * n + x] = new Color(b, b, b, RoundAlpha(x, y, n, 12));
            }
            t.SetPixels(px); t.Apply();
            return fillRound = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        }
    }
    public static Sprite GlossRound
    {
        get
        {
            if (glossRound) return glossRound;
            int n = 48; var t = Tex(n, n); var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float v = (y + .5f) / n;
                float a = v > 0.55f ? Mathf.Lerp(0.04f, 0.38f, (v - 0.55f) / 0.45f) : 0f;
                if (v > 0.9f) a *= 0.6f;
                px[y * n + x] = new Color(1, 1, 1, a * RoundAlpha(x, y, n, 12));
            }
            t.SetPixels(px); t.Apply();
            return glossRound = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        }
    }

    // glossy top half
    public static Sprite Gloss
    {
        get
        {
            if (gloss) return gloss;
            int w = 4, h = 64; var t = Tex(w, h); var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                float a = v > 0.52f ? Mathf.Lerp(0.05f, 0.42f, (v - 0.52f) / 0.48f) : 0f;
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels(px); t.Apply();
            return gloss = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
        }
    }

    // a soft vertical band for shine sweeps
    public static Sprite Shine
    {
        get
        {
            if (shine) return shine;
            int w = 32, h = 8; var t = Tex(w, h); var px = new Color[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = (x + .5f) / w * 2f - 1f;
                float a = Mathf.Exp(-u * u * 5f);
                for (int y = 0; y < h; y++) px[y * w + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels(px); t.Apply();
            return shine = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f));
        }
    }

    public static Sprite Glow => glow ? glow : (glow = Sprite.Create(Kit.Glow, new Rect(0, 0, Kit.Glow.width, Kit.Glow.height), new Vector2(.5f, .5f)));
    public static Sprite RingS => ring ? ring : (ring = Sprite.Create(Kit.Ring, new Rect(0, 0, Kit.Ring.width, Kit.Ring.height), new Vector2(.5f, .5f)));
    public static Sprite Disc => disc ? disc : (disc = Sprite.Create(Kit.Disc, new Rect(0, 0, Kit.Disc.width, Kit.Disc.height), new Vector2(.5f, .5f)));

    // a shaded heart with a highlight
    public static Sprite Heart
    {
        get
        {
            if (heart) return heart;
            int n = 96; var t = Tex(n, n); var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                // classic implicit heart: (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0
                float fx = (x + .5f) / n * 2.6f - 1.3f, fy = (y + .5f) / n * 2.6f - 1.25f;
                float a = 0;
                for (int s = 0; s < 4; s++)   // 2x2 supersample for a smooth edge
                {
                    float sx = fx + ((s & 1) - .5f) * 1.3f / n, sy = fy + ((s >> 1) - .5f) * 1.3f / n;
                    float q = sx * sx + sy * sy - 1f;
                    if (q * q * q - sx * sx * sy * sy * sy <= 0) a += 0.25f;
                }
                float shade = Mathf.Lerp(0.62f, 1f, (y + .5f) / n);
                float hl = Mathf.Exp(-(((fx + 0.45f) * (fx + 0.45f)) + ((fy - 0.45f) * (fy - 0.45f))) * 18f) * 0.6f;
                float c = Mathf.Clamp01(shade + hl);
                px[y * n + x] = new Color(c, Mathf.Clamp01(c - 0.0f), Mathf.Clamp01(c - 0.0f), a);
            }
            t.SetPixels(px); t.Apply();
            return heart = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
        }
    }
}

// A polished stat bar: frame + rim, dark track, delayed "chip" (damage drains after a beat; gains show as a
// bright incoming band), glossy gradient fill, segment ticks, glowing tip, periodic shine sweep, flash on change.
public class HudBar
{
    public RectTransform Root;
    public Text Label;
    readonly RectTransform track, clip, fillRt, chipClip, chipRt, tip, shineRt;
    readonly Image fillImg, chipImg, rimImg, tipImg, flashImg;
    public Color Col, ChipCol, RimCol = new Color(1, 1, 1, 0.16f);
    float shown = -1, chip = -1, chipHold, flash, shineT = 1.5f, pulse;
    public float ShineEvery = 3.2f;
    public bool Low;                 // pulse + red rim (low health / empty lantern)
    public Color LowCol = new Color(1f, 0.25f, 0.3f);

    static RectTransform R(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        return (RectTransform)go.transform;
    }
    static void Stretch(RectTransform r, float inset = 0) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); }
    static Image I(RectTransform r, Sprite s, Color c, bool sliced = false)
    {
        var i = r.gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
        if (sliced) i.type = Image.Type.Sliced;
        return i;
    }

    public HudBar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color col, Color chipCol, int ticks, int labelSize, Font font)
    {
        Col = col; ChipCol = chipCol;
        Root = R("bar", parent);
        Root.anchorMin = Root.anchorMax = anchor; Root.anchoredPosition = pos; Root.sizeDelta = size;
        // drop shadow + frame + rim
        var sh = R("shadow", Root); Stretch(sh, -6); sh.anchoredPosition = new Vector2(0, -4); I(sh, HudArt.Round, new Color(0, 0, 0, 0.45f), true);
        I(Root, HudArt.Round, new Color(0.04f, 0.03f, 0.07f, 0.92f), true);
        // track
        track = R("track", Root); Stretch(track, Mathf.Clamp(size.y * 0.14f, 3f, 5f));
        I(track, HudArt.Round, new Color(0.12f, 0.1f, 0.17f, 1f), true);
        // chip (behind the fill)
        chipClip = R("chipclip", track); chipClip.anchorMin = Vector2.zero; chipClip.anchorMax = new Vector2(0, 1); chipClip.pivot = new Vector2(0, .5f); chipClip.offsetMin = chipClip.offsetMax = Vector2.zero;
        chipClip.gameObject.AddComponent<RectMask2D>();
        chipRt = R("chip", chipClip); chipRt.anchorMin = Vector2.zero; chipRt.anchorMax = new Vector2(0, 1); chipRt.pivot = new Vector2(0, .5f); chipRt.offsetMin = chipRt.offsetMax = Vector2.zero;
        chipImg = I(chipRt, HudArt.FillRound, chipCol, true);
        // fill
        clip = R("clip", track); clip.anchorMin = Vector2.zero; clip.anchorMax = new Vector2(0, 1); clip.pivot = new Vector2(0, .5f); clip.offsetMin = clip.offsetMax = Vector2.zero;
        clip.gameObject.AddComponent<RectMask2D>();
        fillRt = R("fill", clip); fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0, 1); fillRt.pivot = new Vector2(0, .5f); fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fillImg = I(fillRt, HudArt.FillRound, col, true);
        var gloss = R("gloss", fillRt); Stretch(gloss); I(gloss, HudArt.GlossRound, Color.white, true);
        flashImg = I(R("flash", fillRt), HudArt.Round, new Color(1, 1, 1, 0), true); Stretch(flashImg.rectTransform);
        // shine sweep
        shineRt = R("shine", clip); shineRt.anchorMin = shineRt.anchorMax = new Vector2(0, .5f); shineRt.sizeDelta = new Vector2(size.y * 1.6f, size.y * 2.2f);
        shineRt.localRotation = Quaternion.Euler(0, 0, -22f);
        I(shineRt, HudArt.Shine, new Color(1, 1, 1, 0.55f));
        // segment ticks
        for (int i = 1; i < ticks; i++)
        {
            var tk = R("tick", track); tk.anchorMin = new Vector2(i / (float)ticks, 0.18f); tk.anchorMax = new Vector2(i / (float)ticks, 0.82f); tk.sizeDelta = new Vector2(2.5f, 0);
            I(tk, null, new Color(0, 0, 0, 0.32f));
        }
        // glowing tip
        tip = R("tip", track); tip.anchorMin = tip.anchorMax = new Vector2(0, .5f); tip.sizeDelta = new Vector2(size.y * 1.4f, size.y * 1.9f);
        tipImg = I(tip, HudArt.Glow, new Color(1, 1, 1, 0.5f));
        // rim on top
        var rimRt = R("rim", Root); Stretch(rimRt); rimImg = I(rimRt, HudArt.Rim, RimCol, true);
        if (labelSize > 0)
        {
            var lr = R("label", Root); Stretch(lr); lr.offsetMin = new Vector2(10, 0); lr.offsetMax = new Vector2(-10, 0);
            Label = lr.gameObject.AddComponent<Text>();
            Label.font = font; Label.fontSize = labelSize; Label.alignment = TextAnchor.MiddleCenter; Label.color = Color.white; Label.raycastTarget = false;
            Label.horizontalOverflow = HorizontalWrapMode.Overflow; Label.verticalOverflow = VerticalWrapMode.Overflow;
            var o = lr.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.85f); o.effectDistance = new Vector2(2, -2);
        }
    }

    // Snap without animation (new run, level-up wrap).
    public void Reset(float v) { shown = chip = Mathf.Clamp01(v); chipHold = 0; }
    public void Flash(float a = 1f) => flash = Mathf.Max(flash, a);
    public void Shine() => shineT = 0f;

    public void Set(float v, float dt)
    {
        v = Mathf.Clamp01(v);
        if (shown < 0) Reset(v);
        if (v < shown - 0.0005f)
        {
            // damage: the fill drops at once, the chip hangs on for a moment then drains
            // only a real hit holds the chip and flashes; a slow drain (lantern oil) just follows
            bool hit = shown - v > 0.012f;
            if (hit) { chip = Mathf.Max(chip, shown); chipHold = 0.45f; flash = Mathf.Max(flash, 0.7f); }
            else if (chipHold <= 0) chip = v;
            shown = v;
        }
        else if (v > shown + 0.0005f)
        {
            // gain: a bright band shows what's coming, the fill sweeps up to meet it
            chip = Mathf.Max(chip, v);
            shown = Mathf.Lerp(shown, v, 1f - Mathf.Exp(-dt * 7f));
            if (v - shown < 0.002f) shown = v;
        }
        if (chipHold > 0) chipHold -= dt;
        else chip = Mathf.Lerp(chip, Mathf.Max(shown, v), 1f - Mathf.Exp(-dt * 4f));
        if (chip < shown) chip = shown;

        float w = track.rect.width;
        clip.sizeDelta = new Vector2(w * shown, 0);
        chipClip.sizeDelta = new Vector2(w * chip, 0);
        fillRt.sizeDelta = new Vector2(w, 0);
        chipRt.sizeDelta = new Vector2(w, 0);
        tip.anchoredPosition = new Vector2(w * shown, 0);

        pulse += dt * (Low ? 7f : 0f);
        float lp = Low ? 0.5f + 0.5f * Mathf.Sin(pulse) : 0f;
        fillImg.color = Color.Lerp(Col, Color.Lerp(Col, Color.white, 0.45f), lp * 0.6f);
        chipImg.color = ChipCol;
        flash = Mathf.Max(0, flash - dt * 3.5f);
        flashImg.color = new Color(1, 1, 1, flash * 0.55f);
        tipImg.color = Kit.A(Color.Lerp(Col, Color.white, 0.5f), shown > 0.01f && shown < 0.995f ? 0.55f + flash * 0.4f : 0f);
        rimImg.color = Color.Lerp(RimCol, Kit.A(LowCol, 0.9f), lp);

        shineT += dt;
        float k = shineT / 0.7f;
        shineRt.gameObject.SetActive(k < 1f);
        if (k < 1f) shineRt.anchoredPosition = new Vector2(Mathf.Lerp(-40f, w + 40f, k), 0);
        if (shineT > ShineEvery) shineT = 0f;
    }
}
