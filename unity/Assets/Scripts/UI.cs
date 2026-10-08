using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A label pinned to a world position: order bubbles, pad prices, the carry count, staff names.
public class WorldTag
{
    public enum Kind { Card, Order, Carry, Pad }
    public Kind Style = Kind.Card;
    public RectTransform Rt;
    public Image Bg, Rim, IconImg, Fill, FillBg;
    public Text Title, Value;
    public Transform Follow;
    public Vector3 Offset;
    public bool Visible = true;
    public bool Alive = true;
    public float Progress = -1f;

    public void Set(string title, string value)
    {
        if (Title) { Title.text = title ?? ""; Title.gameObject.SetActive(!string.IsNullOrEmpty(title)); }
        if (Value) Value.text = value ?? "";
    }
    public void SetIcon(Sprite s) { if (!IconImg) return; IconImg.sprite = s; IconImg.gameObject.SetActive(s != null); }
    public void Destroy() { Alive = false; if (Rt) UnityEngine.Object.Destroy(Rt.gameObject); }
}

// SPACE DINER HUD: a neon-diner look. Dark glass panels with neon rims, rolling cash, cash that flies
// into the wallet, an objective banner with the food you need, celebration banners for every unlock.
public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, hud, tagLayer, fx;
    Font F => Kit.Font;

    public static readonly Color Panel = new Color(0.06f, 0.06f, 0.15f, 0.9f), Ink = Kit.Hex("#1b1d2e"),
        Gold = Kit.Hex("#ffc93c"), Mint = Kit.Hex("#4de8c2"), Green = Kit.Hex("#7cff8a"), Pink = Kit.Hex("#ff5c8a"),
        Orange = Kit.Hex("#ff7a59"), Violet = Kit.Hex("#9b7cff"), Soft = new Color(1, 1, 1, 0.62f);

    // joystick
    public Vector2 Joy { get; private set; }
    RectTransform joyBase, joyKnob;
    int joyId = -99; Vector2 joyOrigin; bool joyMouse;
    const float JoyRadius = 110f;

    // hud
    RectTransform wallet, walletIcon, planetPanel, questCard, objective, objChip, arrowRt, boostChip, rushChip, boostBtn, soundBtn;
    Text moneyText, rateText, planetText, expText, questText, questCount, objText, objCode, arrowDist, boostChipText, rushText, boostText, soundText;
    Image objChipImg, objIcon, arrowImg, arrowGlow, planetRim;
    HudBar expBar, questBar;
    float moneyPunch, questPunch, objShown; double shownMoney;
    string lastHint;
    public bool Blocking => modal != null && modalBlocks;
    bool modalBlocks;
    RectTransform modal;

    readonly List<WorldTag> tags = new List<WorldTag>();
    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

    public static Sprite Icon(Item i)
    {
        var n = Items.Icon(i);
        if (n == null) return null;
        if (!icons.TryGetValue(n, out var s)) icons[n] = s = Resources.Load<Sprite>("Icons/" + n);
        return s;
    }

    public void Init()
    {
        I = this;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;

        tagLayer = Fill("tags", root);
        hud = Fill("hud", root);
        BuildHud();
        fx = Fill("fx", root);

        joyBase = (RectTransform)Img(root, HudArt.RingS, Vector2.zero, Vector2.zero, new Vector2(JoyRadius * 2.2f, JoyRadius * 2.2f)).transform;
        joyBase.GetComponent<Image>().color = new Color(1, 1, 1, 0.45f);
        var jg = Img(joyBase, HudArt.Disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(JoyRadius * 2f, JoyRadius * 2f)); jg.color = new Color(0.06f, 0.06f, 0.15f, 0.12f);
        joyKnob = (RectTransform)Img(joyBase, HudArt.Disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(112, 112)).transform;
        joyKnob.GetComponent<Image>().color = new Color(1, 1, 1, 0.9f);
        var kr = Img(joyKnob, HudArt.RingS, new Vector2(.5f, .5f), Vector2.zero, new Vector2(112, 112)); kr.color = Kit.A(Pink, 0.75f);
        joyBase.gameObject.SetActive(false);

        RefreshSound();
        StartCoroutine(Intro());
    }

    // ---------------- building blocks ----------------
    RectTransform Rect(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    RectTransform Fill(string n, Transform p)
    {
        var rt = Rect(n, p, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    Image Sliced(Transform p, Sprite s, Color c, float inset = 0)
    {
        var rt = Fill("s", p); rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        var i = rt.gameObject.AddComponent<Image>(); i.sprite = s; i.type = Image.Type.Sliced; i.color = c; i.raycastTarget = false;
        return i;
    }

    // A dark glass panel with a drop shadow and a neon rim.
    RectTransform PanelBox(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color rim, float alpha = 0.9f)
    {
        var rt = Rect("panel", p, anchor, pos, size);
        var sh = Sliced(rt, HudArt.Round, new Color(0, 0, 0, 0.32f), -6); sh.rectTransform.anchoredPosition = new Vector2(0, -6);
        Sliced(rt, HudArt.Round, Kit.A(Panel, alpha));
        Sliced(rt, HudArt.Rim, rim);
        return rt;
    }

    Image Img(Transform p, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect("img", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s; img.preserveAspect = true; img.raycastTarget = false;
        return img;
    }

    Text Txt(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Color c, TextAnchor align = TextAnchor.MiddleCenter, float w = 600)
    {
        size = Mathf.Max(size, 24);
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Normal; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void Shadow(Text t, float d = 3) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.6f); o.effectDistance = new Vector2(d, -d); }

    Button Button(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, out Text text)
    {
        // shadow, face and gloss are all children so the shadow sits under the face (a parent Image would draw first)
        var rt = Rect("btn", p, anchor, pos, size);
        var sh = Sliced(rt, HudArt.Round, new Color(0, 0, 0, 0.3f), -3); sh.rectTransform.anchoredPosition = new Vector2(0, -7);
        var img = Sliced(rt, HudArt.Round, bg); img.raycastTarget = true;
        Sliced(rt, HudArt.GlossRound, new Color(1, 1, 1, 0.55f));
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() => { Sfx.I.Click(); WebBridge.Vibrate(10); onClick(); });
        rt.gameObject.AddComponent<Press>();
        text = Txt(rt, label, 44, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        return b;
    }

    // ---------------- HUD ----------------
    void BuildHud()
    {
        // wallet: cash + income rate
        wallet = PanelBox(hud, new Vector2(0, 1), new Vector2(40 + 190, -72), new Vector2(380, 112), Kit.A(Green, 0.38f));
        var wg = Img(wallet, HudArt.Glow, new Vector2(0, .5f), new Vector2(64, 4), new Vector2(150, 150)); wg.color = Kit.A(Green, 0.18f);
        walletIcon = (RectTransform)Img(wallet, Kit.CashSprite, new Vector2(0, .5f), new Vector2(64, 4), new Vector2(84, 44)).transform;
        walletIcon.localRotation = Quaternion.Euler(0, 0, 10);
        moneyText = Txt(wallet, "0", 56, new Vector2(0, .5f), new Vector2(124, 14), Color.white, TextAnchor.MiddleLeft, 260);
        moneyText.rectTransform.pivot = new Vector2(0, .5f); Shadow(moneyText, 2);
        rateText = Txt(wallet, "", 26, new Vector2(0, .5f), new Vector2(126, -30), Kit.A(Green, 0.95f), TextAnchor.MiddleLeft, 260);
        rateText.rectTransform.pivot = new Vector2(0, .5f);

        // planet + diner expansion
        planetPanel = PanelBox(hud, new Vector2(.5f, 1), new Vector2(0, -72), new Vector2(400, 112), Kit.A(Pink, 0.4f));
        planetRim = planetPanel.GetComponentsInChildren<Image>()[2];
        planetText = Txt(planetPanel, "", 34, new Vector2(.5f, 1), new Vector2(0, -32), Color.white, TextAnchor.MiddleCenter, 380); Shadow(planetText, 2);
        expBar = new HudBar(planetPanel, new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(330, 22), Pink, Kit.Hex("#ffd0de"), 0, 0, F);
        expText = Txt(planetPanel, "", 24, new Vector2(.5f, 0), new Vector2(0, 54), Kit.A(Pink, 0.95f), TextAnchor.MiddleCenter, 380);

        // sound
        soundBtn = (RectTransform)Button(hud, "", new Vector2(1, 1), new Vector2(-92, -72), new Vector2(124, 112), Kit.A(Panel, 0.95f), Color.white, () =>
        {
            Game.I.Save.muted = !Game.I.Save.muted; Sfx.I.SetMuted(Game.I.Save.muted); RefreshSound();
        }, out soundText).transform;
        soundText.fontSize = 26;

        // quest card
        questCard = PanelBox(hud, new Vector2(0, 1), new Vector2(40 + 240, -214), new Vector2(480, 128), Kit.A(Gold, 0.32f));
        var qs = Img(questCard, null, new Vector2(0, .5f), new Vector2(14, 0), new Vector2(8, 96)); qs.color = Gold; qs.preserveAspect = false;
        Txt(questCard, "QUEST", 24, new Vector2(0, 1), new Vector2(36 + 60, -26), Gold, TextAnchor.MiddleLeft, 200).rectTransform.pivot = new Vector2(0, .5f);
        questText = Txt(questCard, "", 32, new Vector2(0, .5f), new Vector2(36, 6), Color.white, TextAnchor.MiddleLeft, 430);
        questText.rectTransform.pivot = new Vector2(0, .5f);
        questBar = new HudBar(questCard, new Vector2(0, 0), new Vector2(36 + 160, 26), new Vector2(320, 20), Gold, Kit.Hex("#fff3c4"), 0, 0, F);
        questCount = Txt(questCard, "", 24, new Vector2(1, 0), new Vector2(-24, 26), Soft, TextAnchor.MiddleRight, 120);
        questCount.rectTransform.pivot = new Vector2(1, .5f);

        // status chips (live timers)
        boostChip = Chip(Gold, out boostChipText);
        rushChip = Chip(Orange, out rushText);

        // free / ad 2x cash: a glossy gold button that pulses when it's ready
        boostBtn = (RectTransform)Button(hud, "", new Vector2(0, 1), new Vector2(40 + 160, -320), new Vector2(320, 92), Gold, Ink, () => Game.I.RequestBoost(), out boostText).transform;
        boostText.fontSize = 34;

        // objective banner (bottom): food icon or code chip + what to do
        objective = PanelBox(hud, new Vector2(.5f, 0), new Vector2(0, 250), new Vector2(760, 104), Kit.A(Gold, 0.45f), 0.94f);
        objChip = Rect("chip", objective, new Vector2(0, .5f), new Vector2(70, 0), new Vector2(104, 76));
        objChipImg = objChip.gameObject.AddComponent<Image>(); objChipImg.sprite = HudArt.Round; objChipImg.type = Image.Type.Sliced; objChipImg.raycastTarget = false;
        objCode = Txt(objChip, "", 34, new Vector2(.5f, .5f), Vector2.zero, Ink, TextAnchor.MiddleCenter, 104);
        objIcon = Img(objChip, null, new Vector2(.5f, .5f), Vector2.zero, new Vector2(62, 62));
        objText = Txt(objective, "", 36, new Vector2(0, .5f), new Vector2(140, 0), Color.white, TextAnchor.MiddleLeft, 600);
        objText.rectTransform.pivot = new Vector2(0, .5f);

        // objective arrow
        arrowRt = Rect("arrow", hud, Vector2.zero, Vector2.zero, new Vector2(96, 96));
        arrowGlow = Img(arrowRt, HudArt.Glow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(170, 170)); arrowGlow.color = Kit.A(Gold, 0.4f);
        arrowImg = Img(arrowRt, Sprite.Create(Kit.Arrow, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f)), new Vector2(.5f, .5f), Vector2.zero, new Vector2(90, 90));
        arrowImg.color = Gold;
        var ao = arrowImg.gameObject.AddComponent<Outline>(); ao.effectColor = new Color(0, 0, 0, 0.45f); ao.effectDistance = new Vector2(3, -3);
        arrowDist = Txt(arrowRt, "", 26, new Vector2(.5f, .5f), new Vector2(0, -66), Color.white, TextAnchor.MiddleCenter, 160); Shadow(arrowDist, 2);

        // ZERO-G TOSS button (hold to aim, let go to throw). Space does the same on keyboards.
        tossBtn = Rect("toss", hud, new Vector2(1, 0), new Vector2(-175, 430), new Vector2(240, 240));
        var tg = Img(tossBtn, HudArt.Glow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(400, 400)); tg.color = Kit.A(Violet, 0.35f);
        var tsh = Img(tossBtn, HudArt.Disc, new Vector2(.5f, .5f), new Vector2(0, -8), new Vector2(232, 232)); tsh.color = new Color(0, 0, 0, 0.3f);
        var tImg = tossBtn.gameObject.AddComponent<Image>();
        tImg.sprite = HudArt.Disc; tImg.color = new Color(0, 0, 0, 0);   // hit area
        var face = Img(tossBtn, HudArt.Disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(224, 224)); face.color = Kit.Hex("#7C5CFF");
        var gloss = Img(tossBtn, HudArt.Disc, new Vector2(.5f, .5f), new Vector2(0, 46), new Vector2(170, 96)); gloss.color = new Color(1, 1, 1, 0.16f); gloss.preserveAspect = false;
        var rim = Img(tossBtn, HudArt.RingS, new Vector2(.5f, .5f), Vector2.zero, new Vector2(224, 224)); rim.color = Kit.A(Color.white, 0.35f);
        tossHold = tossBtn.gameObject.AddComponent<TossHold>();
        tossMeter = Img(tossBtn, HudArt.RingS, new Vector2(.5f, .5f), Vector2.zero, new Vector2(266, 266));
        tossMeter.type = Image.Type.Filled; tossMeter.fillMethod = Image.FillMethod.Radial360; tossMeter.fillOrigin = 2; tossMeter.preserveAspect = false;
        tossLabel = Txt(tossBtn, "TOSS", 54, new Vector2(.5f, .5f), new Vector2(0, 14), Color.white); Shadow(tossLabel, 3);
        Txt(tossBtn, "HOLD", 26, new Vector2(.5f, .5f), new Vector2(0, -40), Kit.A(Color.white, 0.8f));
        tossBtn.gameObject.SetActive(false);
    }

    RectTransform Chip(Color c, out Text t)
    {
        var rt = PanelBox(hud, new Vector2(0, 1), new Vector2(40 + 160, -320), new Vector2(320, 64), Kit.A(c, 0.65f), 0.92f);
        var d = Img(rt, HudArt.Disc, new Vector2(0, .5f), new Vector2(34, 0), new Vector2(20, 20)); d.color = c;
        t = Txt(rt, "", 30, new Vector2(0, .5f), new Vector2(58, 0), c, TextAnchor.MiddleLeft, 260);
        t.rectTransform.pivot = new Vector2(0, .5f);
        rt.gameObject.SetActive(false);
        return rt;
    }

    RectTransform tossBtn; TossHold tossHold; Image tossMeter; Text tossLabel;
    public bool TossHeld => tossHold && tossHold.Held;
    public void SetToss(bool show, bool aiming, float power, bool onTarget)
    {
        bool on = show && !Blocking;
        if (tossBtn.gameObject.activeSelf != on) tossBtn.gameObject.SetActive(on);
        if (!on) return;
        tossMeter.fillAmount = aiming ? power : 1f;
        tossMeter.color = aiming ? (onTarget ? Green : Kit.Hex("#FF6B6B")) : Kit.A(Color.white, 0.5f);
        tossLabel.text = aiming ? (onTarget ? "NOW!" : "TOSS") : "TOSS";
        tossBtn.localScale = Vector3.one * (aiming ? 0.94f : 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.03f);
    }

    public void RefreshSound() => soundText.text = Game.I.Save.muted ? "SOUND\nOFF" : "SOUND\nON";

    // ---------------- world tags ----------------
    public WorldTag Tag(Transform follow, Vector3 offset, bool icon)
    {
        // icon tags are order bubbles: bright speech bubbles that pop against the floor
        var t = new WorldTag { Follow = follow, Offset = offset, Style = icon ? WorldTag.Kind.Order : WorldTag.Kind.Card };
        t.Rt = Rect("tag", tagLayer, Vector2.zero, Vector2.zero, new Vector2(220, 96));
        var sh = Sliced(t.Rt, HudArt.Round, new Color(0, 0, 0, 0.3f), -4); sh.rectTransform.anchoredPosition = new Vector2(0, -5);
        t.Bg = Sliced(t.Rt, HudArt.Round, icon ? new Color(1, 1, 1, 0.97f) : Panel);
        t.Rim = Sliced(t.Rt, HudArt.Rim, icon ? new Color(0, 0, 0, 0.08f) : Kit.A(Pink, 0.45f));
        t.Title = Txt(t.Rt, "", 22, new Vector2(.5f, .5f), new Vector2(0, 22), icon ? Kit.Hex("#6b6f85") : Gold, TextAnchor.MiddleCenter, 300);
        t.Value = Txt(t.Rt, "", 38, new Vector2(.5f, .5f), new Vector2(0, -10), icon ? Ink : Color.white, TextAnchor.MiddleCenter, 300);
        t.FillBg = Img(t.Rt, null, new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(160, 8)); t.FillBg.color = new Color(1, 1, 1, 0.12f); t.FillBg.preserveAspect = false;
        t.Fill = Img(t.FillBg.rectTransform, null, new Vector2(0, .5f), Vector2.zero, new Vector2(0, 8)); t.Fill.color = Gold; t.Fill.preserveAspect = false;
        t.Fill.rectTransform.pivot = new Vector2(0, .5f);
        if (icon) { t.IconImg = Img(t.Rt, null, new Vector2(.5f, .5f), new Vector2(-38, 0), new Vector2(66, 66)); t.IconImg.gameObject.SetActive(false); }
        t.Title.gameObject.SetActive(false);
        tags.Add(t);
        return t;
    }

    // ---------------- floating text ----------------
    public void Emote(Transform at, string s, Color c)
    {
        var t = Txt(fx, s, 46, Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 400);
        Shadow(t, 3);
        StartCoroutine(RiseText(t, at.position + Vector3.up * 2f));
    }

    System.Collections.IEnumerator RiseText(Text t, Vector3 world)
    {
        float k = 0; var c = t.color;
        while (k < 1f && t)
        {
            k += Time.unscaledDeltaTime / 1.1f;
            t.rectTransform.position = Game.I.Cam.WorldToScreenPoint(world) + Vector3.up * (k * 140f * canvas.scaleFactor);
            float s = k < 0.15f ? Mathf.Lerp(0.3f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, (k - 0.15f) * 3f);
            t.rectTransform.localScale = Vector3.one * s;
            t.color = Kit.A(c, Mathf.Clamp01((1f - k) * 3f));
            yield return null;
        }
        if (t) Destroy(t.gameObject);
    }

    public void FloatText(Vector3 world, string s, Color c, int size = 48)
    {
        var rt = Rect("float", fx, Vector2.zero, Vector2.zero, new Vector2(200, size * 1.6f));
        var bg = Sliced(rt, HudArt.Round, new Color(0.06f, 0.06f, 0.15f, 0.8f));
        var t = Txt(rt, s, size, new Vector2(.5f, .5f), Vector2.zero, c, TextAnchor.MiddleCenter, 1000);
        Shadow(t, 2);
        rt.sizeDelta = new Vector2(t.preferredWidth + 48, size * 1.55f);
        StartCoroutine(Rise(rt, bg, t, world));
    }

    System.Collections.IEnumerator Rise(RectTransform rt, Image bg, Text t, Vector3 world)
    {
        float k = 0;
        var c = t.color;
        while (k < 1f && rt)
        {
            k += Time.unscaledDeltaTime / 1.3f;
            rt.position = Game.I.Cam.WorldToScreenPoint(world) + Vector3.up * (Mathf.Sqrt(k) * 150f * canvas.scaleFactor);
            float s = k < 0.12f ? Mathf.Lerp(0.4f, 1.12f, k / 0.12f) : Mathf.Lerp(1.12f, 1f, (k - 0.12f) * 4f);
            rt.localScale = Vector3.one * s;
            float a = Mathf.Clamp01((1f - k) * 3f);
            t.color = Kit.A(c, a); bg.color = new Color(0.06f, 0.06f, 0.15f, 0.8f * a);
            yield return null;
        }
        if (rt) Destroy(rt.gameObject);
    }

    // Bills collected at the register fly up into the wallet.
    float lastFly;
    public void CashFly(Vector3 world)
    {
        if (Time.unscaledTime - lastFly < 0.045f) return;
        lastFly = Time.unscaledTime;
        var img = Img(fx, Kit.CashSprite, Vector2.zero, Vector2.zero, new Vector2(64, 34));
        StartCoroutine(FlyCash(img.rectTransform, Game.I.Cam.WorldToScreenPoint(world + Vector3.up)));
    }

    System.Collections.IEnumerator FlyCash(RectTransform rt, Vector3 from)
    {
        float k = 0, spin = UnityEngine.Random.Range(-200f, 200f);
        var mid = from + new Vector3(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(60f, 200f), 0) * canvas.scaleFactor;
        while (k < 1f && rt)
        {
            k += Time.unscaledDeltaTime / 0.55f;
            float e = k * k * (3f - 2f * k);
            var to = walletIcon.position;
            var a = Vector3.Lerp(from, mid, e); var b = Vector3.Lerp(mid, to, e);
            rt.position = Vector3.Lerp(a, b, e);
            rt.localRotation = Quaternion.Euler(0, 0, spin * k);
            rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.7f, e);
            yield return null;
        }
        if (rt) Destroy(rt.gameObject);
        moneyPunch = 1f;
    }

    // ---------------- celebration banner (unlocks, hires, quests, new planet) ----------------
    readonly Queue<(string kicker, string title, string sub, Color c, Sprite icon)> celebrations = new Queue<(string, string, string, Color, Sprite)>();
    bool celebrating;
    public void Celebrate(string kicker, string title, string sub, Color c, Sprite icon = null)
    {
        celebrations.Enqueue((kicker, title, sub, c, icon));
        if (!celebrating) StartCoroutine(RunCelebrations());
    }

    System.Collections.IEnumerator RunCelebrations()
    {
        celebrating = true;
        while (celebrations.Count > 0)
        {
            var (kicker, title, sub, c, icon) = celebrations.Dequeue();
            var rt = PanelBox(fx, new Vector2(.5f, .5f), new Vector2(0, 380), new Vector2(820, 230), Kit.A(c, 0.85f), 0.96f);
            var glow = Img(rt, HudArt.Glow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1200, 520)); glow.color = Kit.A(c, 0.22f); glow.transform.SetAsFirstSibling();
            var bar = Img(rt, null, new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(740, 8)); bar.color = c; bar.preserveAspect = false;
            Txt(rt, kicker, 28, new Vector2(.5f, 1), new Vector2(0, -46), c);
            var t1 = Txt(rt, title, 66, new Vector2(.5f, .5f), new Vector2(icon ? 44 : 0, 2), Color.white); Shadow(t1, 3);
            if (icon)
            {
                var ic = Img(rt, icon, new Vector2(.5f, .5f), new Vector2(-t1.preferredWidth * 0.5f - 20, 4), new Vector2(84, 84));
                ic.transform.localRotation = Quaternion.Euler(0, 0, -8);
            }
            Txt(rt, sub, 30, new Vector2(.5f, 0), new Vector2(0, 42), Soft);
            var shine = Img(rt, HudArt.Shine, new Vector2(0, .5f), Vector2.zero, new Vector2(140, 300)); shine.color = new Color(1, 1, 1, 0.22f); shine.rectTransform.localRotation = Quaternion.Euler(0, 0, -20);
            float k = 0;
            while (k < 2.6f && rt)
            {
                k += Time.unscaledDeltaTime;
                float pop = Mathf.Clamp01(k / 0.35f), fade = Mathf.Clamp01((2.6f - k) / 0.35f);
                rt.localScale = Vector3.one * Kit.EaseOutBack(pop) * Mathf.Lerp(0.9f, 1f, fade);
                rt.anchoredPosition = new Vector2(0, 380 + (1f - fade) * 60f);
                foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.canvasRenderer.SetAlpha(fade);
                // the shine sweeps once across the card, clipped by fading in only inside it
                float sx = Mathf.Lerp(-100f, 920f, Mathf.Clamp01((k - 0.25f) / 0.6f));
                shine.rectTransform.anchoredPosition = new Vector2(sx, 0);
                shine.canvasRenderer.SetAlpha(fade * (sx > 40 && sx < 780 ? 1f : 0f));
                yield return null;
            }
            if (rt) Destroy(rt.gameObject);
        }
        celebrating = false;
    }

    // ---------------- brand intro ----------------
    System.Collections.IEnumerator Intro()
    {
        var o = Fill("intro", root);
        var bg = o.gameObject.AddComponent<Image>(); bg.color = new Color(0.04f, 0.04f, 0.12f, 1f); bg.raycastTarget = false;
        var glow = Img(o, HudArt.Glow, new Vector2(.5f, .55f), Vector2.zero, new Vector2(1300, 900)); glow.color = Kit.A(Pink, 0.2f);
        var a = Txt(o, "SPACE", 170, new Vector2(.5f, .55f), new Vector2(0, 90), Color.white); Shadow(a, 6);
        var b = Txt(o, "DINER", 170, new Vector2(.5f, .55f), new Vector2(0, -80), Pink); Shadow(b, 6);
        Txt(o, "COOK · SERVE · EXPAND", 38, new Vector2(.5f, .55f), new Vector2(0, -210), Soft);
        var burger = Img(o, Icon(Item.Burger), new Vector2(.5f, .55f), new Vector2(250, 190), new Vector2(120, 120));
        float k = 0;
        while (k < 1.9f)
        {
            k += Time.unscaledDeltaTime;
            if (k > 0.4f && (Input.anyKeyDown || Input.touchCount > 0)) k = Mathf.Max(k, 1.5f);
            float inn = Mathf.Clamp01(k / 0.5f), outt = Mathf.Clamp01((1.9f - k) / 0.4f);
            a.rectTransform.localScale = b.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Kit.EaseOutBack(inn));
            if (burger) burger.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * 4f) * 12f);
            foreach (var g in o.GetComponentsInChildren<Graphic>()) g.canvasRenderer.SetAlpha(outt * (g == bg ? 1f : inn));
            yield return null;
        }
        Destroy(o.gameObject);
    }

    // ---------------- modal panels ----------------
    RectTransform upgrades;
    readonly List<Action> upgradeRefresh = new List<Action>();

    RectTransform Modal(float h, bool blocking = true)
    {
        CloseModal();
        modal = Fill("modal", root);
        modalBlocks = blocking;
        if (blocking) { var shade = modal.gameObject.AddComponent<Image>(); shade.color = new Color(0.02f, 0.02f, 0.08f, 0.6f); }
        // Non-blocking panels sit low so the player can still steer (walk off the pad to close).
        var card = PanelBox(modal, blocking ? new Vector2(.5f, .5f) : new Vector2(.5f, 0), blocking ? new Vector2(0, -40) : new Vector2(0, h * 0.5f + 60), new Vector2(940, h), Kit.A(Pink, 0.45f), 0.97f);
        card.GetComponentsInChildren<Image>()[1].raycastTarget = true;
        var top = Img(card, null, new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(860, 6)); top.color = Kit.A(Pink, 0.85f); top.preserveAspect = false;
        StartCoroutine(PopIn(card));
        return card;
    }

    System.Collections.IEnumerator PopIn(RectTransform r)
    {
        float k = 0;
        while (k < 1f && r) { k += Time.unscaledDeltaTime / 0.25f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }   // the card can close mid-pop
    }

    public void CloseModal()
    {
        if (modal) Destroy(modal.gameObject);
        modal = null; upgrades = null;
        upgradeRefresh.Clear();
    }

    public void OpenUpgrades()
    {
        var g = Game.I;
        var rows = new List<(string code, string name, string desc, Color c, Func<int> lv, int max, Func<double> cost, Action buy)>
        {
            (">>", "SPEED", "Walk faster", Mint, () => g.Save.lvSpeed, 6, () => g.UpgradeCost(0), () => g.Save.lvSpeed++),
            ("+1", "CAPACITY", "Carry a taller stack", Gold, () => g.Save.lvCap, 8, () => g.UpgradeCost(1), () => g.Save.lvCap++),
            ("$", "PROFIT", "+15% per sale", Green, () => g.Save.lvProfit, 10, () => g.UpgradeCost(2), () => g.Save.lvProfit++),
        };
        if (g.StaffCount > 0) rows.Add(("x2", "STAFF", "Faster aliens", Pink, () => g.Save.lvStaff, 6, () => g.UpgradeCost(3), () => g.Save.lvStaff++));

        var card = Modal(250 + rows.Count * 168, false);
        upgrades = card;
        Txt(card, "DINER TERMINAL", 26, new Vector2(.5f, 1), new Vector2(0, -50), Pink);
        var title = Txt(card, "UPGRADES", 60, new Vector2(.5f, 1), new Vector2(0, -100), Color.white); Shadow(title, 3);
        Button(card, "X", new Vector2(1, 1), new Vector2(-70, -74), new Vector2(84, 84), new Color(1, 1, 1, 0.1f), Color.white, CloseUpgrades, out _);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            float y = -220 - i * 168;
            var row = PanelBox(card, new Vector2(.5f, 1), new Vector2(0, y), new Vector2(860, 148), Kit.A(r.c, 0.25f), 0.9f);
            var badge = Img(row, HudArt.Disc, new Vector2(0, .5f), new Vector2(76, 0), new Vector2(104, 104)); badge.color = Kit.A(r.c, 0.18f);
            var ring = Img(row, HudArt.RingS, new Vector2(0, .5f), new Vector2(76, 0), new Vector2(104, 104)); ring.color = r.c;
            Txt(row, r.code, 40, new Vector2(0, .5f), new Vector2(76, 0), r.c, TextAnchor.MiddleCenter, 104);
            var nm = Txt(row, r.name, 40, new Vector2(0, .5f), new Vector2(150, 30), Color.white, TextAnchor.MiddleLeft, 340); nm.rectTransform.pivot = new Vector2(0, .5f);
            var ds = Txt(row, r.desc, 26, new Vector2(0, .5f), new Vector2(150, -6), Soft, TextAnchor.MiddleLeft, 340); ds.rectTransform.pivot = new Vector2(0, .5f);
            var pips = new List<Image>();
            for (int k = 0; k < r.max; k++)
            {
                var pip = Img(row, HudArt.Round, new Vector2(0, .5f), new Vector2(160 + k * 30, -42), new Vector2(22, 14)); pip.type = Image.Type.Sliced; pip.preserveAspect = false;
                pips.Add(pip);
            }
            Text costText = null; Button btn = null;
            btn = Button(row, "", new Vector2(1, .5f), new Vector2(-140, 0), new Vector2(230, 100), Kit.Hex("#35C46A"), Color.white, () =>
            {
                if (r.lv() >= r.max) return;
                double c = r.cost();
                if (g.Money < c) { Sfx.I.Deny(); return; }
                g.AddMoney(-c, false);
                r.buy();
                g.Persist();
                Sfx.I.LevelUp(); WebBridge.Vibrate(25);
                foreach (var a in upgradeRefresh) a();
            }, out costText);
            costText.fontSize = 38;
            Action refresh = () =>
            {
                int lv = r.lv();
                bool max = lv >= r.max;
                for (int k = 0; k < pips.Count; k++) pips[k].color = k < lv ? r.c : new Color(1, 1, 1, 0.12f);
                costText.text = max ? "MAX" : Kit.Money(r.cost());
                bool afford = !max && g.Money >= r.cost();
                ((Image)btn.targetGraphic).color = max ? new Color(1, 1, 1, 0.12f) : afford ? Kit.Hex("#35C46A") : new Color(1, 1, 1, 0.1f);
                costText.color = afford || max ? Color.white : new Color(1, 1, 1, 0.45f);
            };
            upgradeRefresh.Add(refresh);
            refresh();
        }
        WebBridge.Event("open_upgrades");
    }

    public void CloseUpgrades() { if (upgrades) CloseModal(); }

    public void Confirm(string title, string body, string ok, Action onOk, string ad = null, Action onAd = null, string cancel = null)
    {
        var card = Modal(ad != null || cancel != null ? 660 : 540);
        var t = Txt(card, title, 60, new Vector2(.5f, 1), new Vector2(0, -92), Color.white); Shadow(t, 3);
        var b = Txt(card, body, 36, new Vector2(.5f, 1), new Vector2(0, -220), Soft, TextAnchor.MiddleCenter, 820);
        b.horizontalOverflow = HorizontalWrapMode.Wrap; b.rectTransform.sizeDelta = new Vector2(820, 160);
        float y = 150;
        if (ad != null) Button(card, "WATCH AD: " + ad, new Vector2(.5f, 0), new Vector2(0, y + 150), new Vector2(720, 120), Gold, Ink, () => { CloseModal(); onAd(); }, out _);
        Button(card, ok, new Vector2(.5f, 0), new Vector2(cancel != null ? 180 : 0, y - (ad != null ? 10 : 0)), new Vector2(cancel != null ? 340 : 720, 120), Kit.Hex("#35C46A"), Color.white, () => { CloseModal(); onOk?.Invoke(); }, out _);
        if (cancel != null) Button(card, cancel, new Vector2(.5f, 0), new Vector2(-190, y), new Vector2(340, 120), new Color(1, 1, 1, 0.12f), Color.white, CloseModal, out _);
    }

    // ---------------- per-frame ----------------
    void Update()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        // landscape: the canvas matches height, so scale the HUD up to a comfortable size
        float hs = root.rect.width > root.rect.height ? 1.3f : 1f;
        hud.anchorMin = hud.anchorMax = new Vector2(.5f, .5f);
        hud.sizeDelta = root.rect.size / hs; hud.localScale = Vector3.one * hs;
        tagScale = hs > 1f ? 1.2f : 1f; tagLayer.localScale = Vector3.one * tagScale;   // world tags grow a little too (they position by screen point)
        UpdateJoystick();
        if (Game.I.Dev && Input.GetKeyDown(KeyCode.H)) canvas.enabled = !canvas.enabled;   // dev: clean frames for store art

        var g = Game.I;
        float udt = Time.unscaledDeltaTime;
        shownMoney = shownMoney + (g.Money - shownMoney) * (1f - Mathf.Exp(-udt * 10f));
        if (Math.Abs(g.Money - shownMoney) < 1) shownMoney = g.Money;
        moneyText.text = Kit.Money(shownMoney).Substring(1);
        double rate = g.IncomePerMin;
        rateText.text = rate >= 1 ? "+" + Kit.Money(rate) + " / MIN" : "SERVE FOOD TO EARN";
        moneyPunch = Mathf.MoveTowards(moneyPunch, 0, udt * 4f);
        wallet.localScale = Vector3.one * (1f + moneyPunch * 0.07f);
        walletIcon.localScale = Vector3.one * (1f + moneyPunch * 0.25f);

        planetText.text = g.PlanetName;
        expBar.Set(g.ExpansionProgress, udt);
        expText.text = "DINER  " + g.PadsDone + " / " + g.PadsTotal;

        questCard.gameObject.SetActive(g.QuestVisible && !Blocking);
        if (g.QuestVisible)
        {
            questText.text = g.QuestText;
            questBar.Set(g.QuestProgress, udt);
            questCount.text = Mathf.RoundToInt(g.QuestProgress * 100) + "%";
            questPunch = Mathf.MoveTowards(questPunch, 0, udt * 3f);
            questCard.localScale = Vector3.one * (1f + questPunch * 0.08f);
        }

        // left column under the quest: timers, then the boost offer
        float y = g.QuestVisible ? -320 : -214;
        boostChip.gameObject.SetActive(g.BoostLeft > 0);
        if (g.BoostLeft > 0) { boostChipText.text = "2X CASH  " + Clock(g.BoostLeft); boostChip.anchoredPosition = new Vector2(40 + 160, y); y -= 80; }
        rushChip.gameObject.SetActive(g.RushLeft > 0);
        if (g.RushLeft > 0) { rushText.text = "RUSH HOUR  " + Mathf.CeilToInt(g.RushLeft) + "s"; rushChip.anchoredPosition = new Vector2(40 + 160, y); rushChip.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 0.04f); y -= 80; }
        bool offer = g.BoostLeft <= 0 && g.BoostReady && !Blocking;
        boostBtn.gameObject.SetActive(offer);
        if (offer)
        {
            boostText.text = WebBridge.AdsAvailable ? "AD: 2X CASH" : "FREE 2X CASH";
            boostBtn.anchoredPosition = new Vector2(40 + 160, y - 14);
            boostBtn.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f);
        }

        // objective banner: slides up when there's something to do
        bool show = !string.IsNullOrEmpty(g.Hint) && modal == null;
        objShown = Mathf.MoveTowards(objShown, show ? 1 : 0, udt * 5f);
        objective.gameObject.SetActive(objShown > 0.01f);
        if (show && g.Hint != lastHint) { lastHint = g.Hint; objText.text = g.Hint; objective.localScale = Vector3.one * 1.06f; }
        objective.localScale = Vector3.Lerp(objective.localScale, Vector3.one, 1f - Mathf.Exp(-udt * 10f));
        float objY = Screen.width > Screen.height ? 120 : 250;
        objective.anchoredPosition = new Vector2(0, Mathf.Lerp(objY - 100, objY, Kit.EaseOutBack(objShown)));
        if (show)
        {
            bool hasIcon = g.HintIcon != null;
            objIcon.gameObject.SetActive(hasIcon);
            if (hasIcon) objIcon.sprite = g.HintIcon;
            objCode.text = hasIcon ? "" : string.IsNullOrEmpty(g.HintCode) ? "GO" : g.HintCode;
            objChipImg.color = hasIcon ? new Color(1, 1, 1, 0.95f) : g.HintColor;
        }
        objective.sizeDelta = new Vector2(Mathf.Max(520, objText.preferredWidth + 200), 104);
        foreach (var gr in objective.GetComponentsInChildren<Graphic>()) gr.canvasRenderer.SetAlpha(objShown);

        foreach (var a in upgradeRefresh) a();
    }

    float tagScale = 1f;
    public void PunchMoney() => moneyPunch = 1f;
    public void PunchQuest() => questPunch = 1f;
    public static string Clock(float secs) { int s = Mathf.Max(0, Mathf.CeilToInt(secs)); return (s / 60) + ":" + (s % 60).ToString("00"); }

    void LateUpdate()
    {
        var cam = Game.I.Cam;
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            var t = tags[i];
            if (!t.Alive || !t.Follow) { if (t.Rt) Destroy(t.Rt.gameObject); tags.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(t.Follow.position + t.Offset);
            bool on = t.Visible && sp.z > 0 && !Blocking;
            if (t.Rt.gameObject.activeSelf != on) t.Rt.gameObject.SetActive(on);
            if (!on) continue;
            bool hasTitle = t.Title.gameObject.activeSelf;
            bool hasIcon = t.IconImg && t.IconImg.gameObject.activeSelf;
            bool bar = t.Style == WorldTag.Kind.Pad && t.Progress >= 0;
            switch (t.Style)
            {
                case WorldTag.Kind.Carry:
                    t.Rt.localScale = Vector3.one * 0.62f;
                    bool max = t.Value.text == "MAX";
                    t.Value.color = max ? Color.Lerp(Orange, Color.white, Mathf.PingPong(Time.time * 3f, 1f) * 0.5f) : Color.white;
                    t.Rim.color = max ? Kit.A(Orange, 0.9f) : new Color(1, 1, 1, 0.15f); break;
                case WorldTag.Kind.Pad:
                    t.Rt.localScale = Vector3.one * 0.82f; t.Value.color = Gold; t.Title.color = Color.white; t.Rim.color = Kit.A(Gold, 0.55f); break;
                case WorldTag.Kind.Order:
                    t.Rt.localScale = Vector3.one * (0.86f + Mathf.Max(0, Mathf.Sin(Time.time * 3f + i)) * 0.03f); break;
                default:
                    t.Rt.localScale = Vector3.one * 0.8f; break;
            }
            t.FillBg.gameObject.SetActive(bar);
            float wv = Mathf.Max(t.Value.preferredWidth, hasTitle ? t.Title.preferredWidth : 0) + (hasIcon ? 110 : 54);
            float h = hasTitle ? (bar ? 104 : 92) : (hasIcon ? 84 : 66);
            t.Rt.sizeDelta = new Vector2(Mathf.Max(wv, t.Style == WorldTag.Kind.Pad ? 200 : 90), h);
            if (hasIcon) t.IconImg.rectTransform.anchoredPosition = new Vector2(-t.Rt.sizeDelta.x * 0.5f + 46f, 0);
            t.Title.rectTransform.anchoredPosition = new Vector2(hasIcon ? 30 : 0, bar ? 28 : 20);
            t.Value.rectTransform.anchoredPosition = new Vector2(hasIcon ? 30 : 0, hasTitle ? (bar ? -6 : -12) : 0);
            if (bar) { t.FillBg.rectTransform.sizeDelta = new Vector2(t.Rt.sizeDelta.x - 40, 8); t.Fill.rectTransform.sizeDelta = new Vector2((t.Rt.sizeDelta.x - 40) * Mathf.Clamp01(t.Progress), 8); }
            float half = t.Rt.sizeDelta.x * 0.5f * t.Rt.localScale.x * tagScale * canvas.scaleFactor + 8f;
            sp.x = Mathf.Clamp(sp.x, half, Screen.width - half);   // keep bubbles on screen
            t.Rt.position = sp;
        }

        // objective arrow: bob over the target, or ride the screen edge pointing at it with the distance
        var target = Game.I.ArrowTarget;
        arrowRt.gameObject.SetActive(target.HasValue && !Blocking);
        if (target.HasValue)
        {
            var sp = cam.WorldToScreenPoint(target.Value + Vector3.up * 1.6f);
            var rect = new Rect(90, 90, Screen.width - 180, Screen.height - 280);
            bool inside = sp.z > 0 && rect.Contains(sp);
            arrowRt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.08f);
            if (inside)
            {
                arrowRt.position = sp + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 30f * canvas.scaleFactor);
                arrowImg.rectTransform.localRotation = Quaternion.identity;
                arrowDist.text = "";
            }
            else
            {
                var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
                var d = ((Vector2)sp - c) * (sp.z < 0 ? -1 : 1);
                float s = Mathf.Min(rect.width / 2f / Mathf.Abs(d.x + 0.001f), rect.height / 2f / Mathf.Abs(d.y + 0.001f));
                arrowRt.position = c + d * Mathf.Min(s, 1f);
                arrowImg.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f);
                var pp = Game.I.Player.transform.position; pp.y = 0; var tt = target.Value; tt.y = 0;
                arrowDist.text = Mathf.RoundToInt(Vector3.Distance(pp, tt)) + "m";
            }
        }
    }

    void UpdateJoystick()
    {
        Vector2? pos = null;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (joyId == -99 && t.phase == TouchPhase.Began && !OverUI(t.fingerId)) { joyId = t.fingerId; joyOrigin = t.position; joyMouse = false; }
                if (t.fingerId == joyId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) joyId = -99;
                    else pos = t.position;
                }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && !OverUI(-1)) { joyId = -1; joyOrigin = Input.mousePosition; joyMouse = true; }
            if (joyMouse && joyId == -1)
            {
                if (Input.GetMouseButton(0)) pos = Input.mousePosition;
                else joyId = -99;
            }
        }

        if (pos.HasValue && !Blocking)
        {
            float r = JoyRadius * canvas.scaleFactor;
            var d = pos.Value - joyOrigin;
            if (d.magnitude > r) { joyOrigin += d.normalized * (d.magnitude - r); d = pos.Value - joyOrigin; }   // base follows finger
            Joy = d / r;
            joyBase.gameObject.SetActive(true);
            joyBase.position = joyOrigin;
            joyKnob.anchoredPosition = d / canvas.scaleFactor;
        }
        else
        {
            Joy = Vector2.zero;
            joyBase.gameObject.SetActive(false);
            if (!pos.HasValue) joyId = -99;
        }
    }

    static bool OverUI(int id)
    {
        if (!EventSystem.current) return false;
        return id < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(id);
    }
}

// Hold-to-aim button: tracks every pointer pressing it.
public class TossHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    readonly HashSet<int> ids = new HashSet<int>();
    public bool Held => ids.Count > 0;
    public void OnPointerDown(PointerEventData e) => ids.Add(e.pointerId);
    public void OnPointerUp(PointerEventData e) => ids.Remove(e.pointerId);
    void OnDisable() => ids.Clear();
}
