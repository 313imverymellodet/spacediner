using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A label pinned to a world position (order bubbles, pad prices, MAX).
public class WorldTag
{
    public RectTransform Rt;
    public Image Bg, IconImg;
    public Text Title, Value;
    public Transform Follow;
    public Vector3 Offset;
    public bool Visible = true;
    public bool Alive = true;

    public void Set(string title, string value)
    {
        if (Title) { Title.text = title ?? ""; Title.gameObject.SetActive(!string.IsNullOrEmpty(title)); }
        if (Value) Value.text = value ?? "";
    }

    public void SetIcon(Sprite s)
    {
        if (!IconImg) return;
        IconImg.sprite = s;
        IconImg.gameObject.SetActive(s != null);

    }

    public void Destroy() { Alive = false; if (Rt) UnityEngine.Object.Destroy(Rt.gameObject); }
}

public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, tagLayer;
    Font F => Kit.Font;

    // joystick
    public Vector2 Joy { get; private set; }
    RectTransform joyBase, joyKnob;
    int joyId = -99; Vector2 joyOrigin; bool joyMouse;
    const float JoyRadius = 110f;

    // hud
    Text moneyText, planetText, hintText, boostText, soundText;
    RectTransform moneyPill, hintPill, boostBtn, questCard, questFill, rushPill;
    Text questText, rushText;
    float questPunch;
    public void PunchQuest() => questPunch = 1f;
    Image arrowImg;
    float moneyPunch; double shownMoney;
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

        // money pill
        moneyPill = Box(root, new Vector2(0, 1), new Vector2(40 + 170, -60), new Vector2(340, 96), new Color(0, 0, 0, 0.45f));
        var cash = Img(moneyPill, Kit.CashSprite, new Vector2(0, 0.5f), new Vector2(62, 0), new Vector2(72, 38));
        cash.rectTransform.localRotation = Quaternion.Euler(0, 0, 12);
        moneyText = Txt(moneyPill, "$0", 54, new Vector2(0, 0.5f), new Vector2(210, 0), Color.white, TextAnchor.MiddleLeft, 240);

        // planet name sits in its own pill: bare white text vanished against the light diner floor
        var planetPill = Box(root, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(380, 76), new Color(0, 0, 0, 0.45f));
        planetText = Txt(planetPill, "", 36, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 380);

        hintPill = Box(root, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(760, 90), new Color(1, 1, 1, 0.92f));   // bottom: keeps the top clear for HUD
        hintText = Txt(hintPill, "", 40, new Vector2(0.5f, 0.5f), Vector2.zero, Kit.Hex("#1b1d2e"), TextAnchor.MiddleCenter, 740);

        var sb = Button(root, "", new Vector2(1, 1), new Vector2(-100, -60), new Vector2(150, 96), new Color(0, 0, 0, 0.45f), Color.white, () =>
        {
            Game.I.Save.muted = !Game.I.Save.muted; Sfx.I.SetMuted(Game.I.Save.muted); RefreshSound();
        }, out soundText);
        soundText.fontSize = 30;

        boostBtn = (RectTransform)Button(root, "", new Vector2(0, 1), new Vector2(40 + 150, -300), new Vector2(300, 90), Kit.Hex("#FFC93C"), Kit.Hex("#1b1d2e"), () => Game.I.RequestBoost(), out boostText).transform;
        boostText.fontSize = 34;

        // quest card: title, goal, progress bar
        questCard = Box(root, new Vector2(0, 1), new Vector2(40 + 230, -178), new Vector2(460, 118), new Color(0, 0, 0, 0.5f));
        Txt(questCard, "QUEST", 24, new Vector2(0, 1), new Vector2(40 + 60, -24), Kit.Hex("#FFC93C"), TextAnchor.MiddleLeft, 200);
        questText = Txt(questCard, "", 30, new Vector2(0, 0.5f), new Vector2(24 + 200, 2), Color.white, TextAnchor.MiddleLeft, 420);
        var barBg = Box(questCard, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(412, 14), new Color(1, 1, 1, 0.18f));
        questFill = Box(barBg, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(412, 14), Kit.Hex("#FFC93C"));
        questFill.pivot = new Vector2(0, 0.5f); questFill.anchoredPosition = Vector2.zero;

        // rush hour banner
        rushPill = Box(root, new Vector2(1, 1), new Vector2(-40 - 170, -178), new Vector2(340, 90), Kit.Hex("#FF7A59"));
        rushText = Txt(rushPill, "", 32, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 330);

        arrowImg = Img(root, Sprite.Create(Kit.Arrow, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f)), new Vector2(0, 0), Vector2.zero, new Vector2(84, 84));
        arrowImg.color = Kit.Hex("#FFC93C");
        arrowImg.raycastTarget = false;
        var ash = arrowImg.gameObject.AddComponent<Outline>(); ash.effectColor = new Color(0, 0, 0, 0.5f); ash.effectDistance = new Vector2(3, -3);

        joyBase = (RectTransform)Img(root, Sprite.Create(Kit.Ring, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f)), Vector2.zero, Vector2.zero, new Vector2(JoyRadius * 2.2f, JoyRadius * 2.2f)).transform;
        joyBase.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
        joyBase.GetComponent<Image>().raycastTarget = false;
        joyKnob = (RectTransform)Img(joyBase, Sprite.Create(Kit.Disc, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f)), new Vector2(.5f, .5f), Vector2.zero, new Vector2(110, 110)).transform;
        joyKnob.GetComponent<Image>().color = new Color(1, 1, 1, 0.85f);
        joyKnob.GetComponent<Image>().raycastTarget = false;
        joyBase.gameObject.SetActive(false);

        // ZERO-G TOSS button (hold to aim, let go to throw). Space does the same on keyboards.
        tossBtn = Rect("toss", root, new Vector2(1, 0), new Vector2(-175, 430), new Vector2(240, 240));
        var tImg = tossBtn.gameObject.AddComponent<Image>();
        tImg.sprite = Sprite.Create(Kit.Disc, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f)); tImg.color = Kit.Hex("#7C5CFF");
        tossHold = tossBtn.gameObject.AddComponent<TossHold>();
        tossMeter = Img(tossBtn, Sprite.Create(Kit.Ring, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f)), new Vector2(.5f, .5f), Vector2.zero, new Vector2(262, 262));
        tossMeter.type = Image.Type.Filled; tossMeter.fillMethod = Image.FillMethod.Radial360; tossMeter.fillOrigin = 2; tossMeter.preserveAspect = false;
        tossLabel = Txt(tossBtn, "TOSS", 54, new Vector2(.5f, .5f), new Vector2(0, 14), Color.white);
        Txt(tossBtn, "HOLD", 28, new Vector2(.5f, .5f), new Vector2(0, -40), Kit.A(Color.white, 0.8f));
        tossBtn.gameObject.SetActive(false);

        RefreshSound();
    }

    RectTransform tossBtn; TossHold tossHold; Image tossMeter; Text tossLabel;
    public bool TossHeld => tossHold && tossHold.Held;
    public void SetToss(bool show, bool aiming, float power, bool onTarget)
    {
        bool on = show && !Blocking;
        if (tossBtn.gameObject.activeSelf != on) tossBtn.gameObject.SetActive(on);
        if (!on) return;
        tossMeter.fillAmount = aiming ? power : 1f;
        tossMeter.color = aiming ? (onTarget ? Kit.Hex("#7CFF8A") : Kit.Hex("#FF6B6B")) : Kit.A(Color.white, 0.55f);
        tossLabel.text = aiming ? (onTarget ? "NOW!" : "TOSS") : "TOSS";
        tossBtn.localScale = Vector3.one * (aiming ? 0.94f : 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.03f);
    }

    public void RefreshSound() => soundText.text = Game.I.Save.muted ? "SOUND\nOFF" : "SOUND\nON";

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

    RectTransform Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
    {
        var rt = Rect("box", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false;
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
        size = Mathf.Max(size, 28);   // readable floor: Lilita below this turns to mush on phones and short desktop windows
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Normal; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Button Button(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, out Text text)
    {
        var rt = Rect("btn", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = bg;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() => { Sfx.I.Click(); onClick(); });
        rt.gameObject.AddComponent<Press>();
        text = Txt(rt, label, 44, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        return b;
    }

    // ---------------- world tags ----------------
    public WorldTag Tag(Transform follow, Vector3 offset, bool icon)
    {
        var t = new WorldTag { Follow = follow, Offset = offset };
        t.Rt = Box(tagLayer, Vector2.zero, Vector2.zero, new Vector2(icon ? 170 : 200, icon ? 84 : 96), new Color(1, 1, 1, 0.95f));
        t.Bg = t.Rt.GetComponent<Image>();
        t.Title = Txt(t.Rt, "", 24, new Vector2(.5f, .5f), new Vector2(0, 22), Kit.Hex("#6b6f85"), TextAnchor.MiddleCenter, 300);
        t.Value = Txt(t.Rt, "", 40, new Vector2(.5f, .5f), new Vector2(0, icon ? 0 : -10), Kit.Hex("#1b1d2e"), TextAnchor.MiddleCenter, 300);
        if (icon)
        {
            t.IconImg = Img(t.Rt, null, new Vector2(.5f, .5f), new Vector2(-38, 0), new Vector2(66, 66));
            t.IconImg.gameObject.SetActive(false);
        }
        t.Title.gameObject.SetActive(false);
        tags.Add(t);
        return t;
    }

    public void Emote(Transform at, string s, Color c)
    {
        var t = Txt(root, s, 46, Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 400);
        var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.6f); o.effectDistance = new Vector2(3, -3);
        StartCoroutine(Rise(t, at.position + Vector3.up * 2f));
    }

    public void FloatText(Vector3 world, string s, Color c, int size = 48)
    {
        var t = Txt(root, s, size, Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 600);
        var sh = t.gameObject.AddComponent<Outline>(); sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(3, -3);
        StartCoroutine(Rise(t, world));
    }

    System.Collections.IEnumerator Rise(Text t, Vector3 world)
    {
        float k = 0;
        var c = t.color;
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / 1.1f;
            var sp = Game.I.Cam.WorldToScreenPoint(world);
            t.rectTransform.position = sp + Vector3.up * (k * 140f * canvas.scaleFactor);
            float s = k < 0.15f ? Mathf.Lerp(0.3f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, (k - 0.15f) * 3f);
            t.rectTransform.localScale = Vector3.one * s;
            t.color = Kit.A(c, Mathf.Clamp01((1f - k) * 3f));
            yield return null;
        }
        Destroy(t.gameObject);
    }

    // ---------------- modal panels ----------------
    RectTransform upgrades;
    readonly List<Action> upgradeRefresh = new List<Action>();

    RectTransform Modal(float h, bool blocking = true)
    {
        CloseModal();
        modal = Fill("modal", root);
        modalBlocks = blocking;
        if (blocking)
        {
            var shade = modal.gameObject.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.03f, 0.1f, 0.55f);
        }
        // Non-blocking panels sit low so the player can still steer (walk off the pad to close).
        var card = Box(modal, blocking ? new Vector2(.5f, .5f) : new Vector2(.5f, 0), blocking ? new Vector2(0, -60) : new Vector2(0, h * 0.5f + 60), new Vector2(920, h), Color.white);
        card.GetComponent<Image>().raycastTarget = true;
        StartCoroutine(PopIn(card));
        return card;
    }

    System.Collections.IEnumerator PopIn(RectTransform r)
    {
        float k = 0;
        while (k < 1f) { k += Time.unscaledDeltaTime / 0.25f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }
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
        var rows = new List<(string name, string desc, Func<int> lv, int max, Func<double> cost, Action buy)>
        {
            ("SPEED", "Walk faster", () => g.Save.lvSpeed, 6, () => g.UpgradeCost(0), () => g.Save.lvSpeed++),
            ("CAPACITY", "Carry more", () => g.Save.lvCap, 8, () => g.UpgradeCost(1), () => g.Save.lvCap++),
            ("PROFIT", "+15% per sale", () => g.Save.lvProfit, 10, () => g.UpgradeCost(2), () => g.Save.lvProfit++),
        };
        if (g.StaffCount > 0) rows.Add(("STAFF", "Faster aliens", () => g.Save.lvStaff, 6, () => g.UpgradeCost(3), () => g.Save.lvStaff++));

        var card = Modal(230 + rows.Count * 170, false);
        upgrades = card;
        Txt(card, "UPGRADES", 56, new Vector2(.5f, 1), new Vector2(0, -75), Kit.Hex("#1b1d2e"));
        Button(card, "X", new Vector2(1, 1), new Vector2(-70, -70), new Vector2(90, 90), Kit.Hex("#eceef5"), Kit.Hex("#1b1d2e"), CloseUpgrades, out _);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            float y = -200 - i * 170;
            var row = Box(card, new Vector2(.5f, 1), new Vector2(0, y), new Vector2(840, 150), Kit.Hex("#f2f3f9"));
            Txt(row, r.name, 44, new Vector2(0, .5f), new Vector2(40 + 170, 22), Kit.Hex("#1b1d2e"), TextAnchor.MiddleLeft, 340);
            var desc = Txt(row, r.desc, 30, new Vector2(0, .5f), new Vector2(40 + 170, -28), Kit.Hex("#6b6f85"), TextAnchor.MiddleLeft, 340);
            var lvText = Txt(row, "", 34, new Vector2(.5f, .5f), new Vector2(40, 0), Kit.Hex("#6b6f85"), TextAnchor.MiddleCenter, 160);
            Text costText = null;
            Button btn = null;
            btn = Button(row, "", new Vector2(1, .5f), new Vector2(-150, 0), new Vector2(260, 110), Kit.Hex("#35C46A"), Color.white, () =>
            {
                if (r.lv() >= r.max) return;
                double c = r.cost();
                if (g.Money < c) { Sfx.I.Deny(); return; }
                g.AddMoney(-c, false);
                r.buy();
                g.Persist();
                Sfx.I.LevelUp();
                foreach (var a in upgradeRefresh) a();
            }, out costText);
            costText.fontSize = 40;
            Action refresh = () =>
            {
                int lv = r.lv();
                lvText.text = "LV " + (lv + 1);
                bool max = lv >= r.max;
                costText.text = max ? "MAX" : Kit.Money(r.cost());
                bool afford = !max && g.Money >= r.cost();
                btn.GetComponent<Image>().color = max ? Kit.Hex("#b9bccb") : afford ? Kit.Hex("#35C46A") : Kit.Hex("#d6d8e2");
                costText.color = afford || max ? Color.white : Kit.Hex("#8a8ea3");
            };
            upgradeRefresh.Add(refresh);
            refresh();
        }
        WebBridge.Event("open_upgrades");
    }

    public void CloseUpgrades() { if (upgrades) CloseModal(); }

    public void Confirm(string title, string body, string ok, Action onOk, string ad = null, Action onAd = null, string cancel = null)
    {
        var card = Modal(ad != null || cancel != null ? 640 : 520);
        Txt(card, title, 58, new Vector2(.5f, 1), new Vector2(0, -85), Kit.Hex("#1b1d2e"));
        var b = Txt(card, body, 36, new Vector2(.5f, 1), new Vector2(0, -210), Kit.Hex("#4b4f65"), TextAnchor.MiddleCenter, 820);
        b.horizontalOverflow = HorizontalWrapMode.Wrap; b.rectTransform.sizeDelta = new Vector2(820, 160);
        float y = 150;
        if (ad != null)
        {
            Button(card, "WATCH AD: " + ad, new Vector2(.5f, 0), new Vector2(0, y + 150), new Vector2(700, 120), Kit.Hex("#FFC93C"), Kit.Hex("#1b1d2e"), () => { CloseModal(); onAd(); }, out _);
        }
        Button(card, ok, new Vector2(.5f, 0), new Vector2(cancel != null ? 170 : 0, y - (ad != null ? 10 : 0)), new Vector2(cancel != null ? 330 : 700, 120), Kit.Hex("#35C46A"), Color.white, () => { CloseModal(); onOk?.Invoke(); }, out _);
        if (cancel != null)
            Button(card, cancel, new Vector2(.5f, 0), new Vector2(-190, y), new Vector2(330, 120), Kit.Hex("#eceef5"), Kit.Hex("#1b1d2e"), CloseModal, out _);
    }

    // ---------------- per-frame ----------------
    void Update()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        UpdateJoystick();

        var g = Game.I;
        shownMoney = shownMoney + (g.Money - shownMoney) * (1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
        if (Math.Abs(g.Money - shownMoney) < 1) shownMoney = g.Money;
        moneyText.text = Kit.Money(shownMoney).Substring(1);
        moneyPunch = Mathf.MoveTowards(moneyPunch, 0, Time.unscaledDeltaTime * 4f);
        moneyPill.localScale = Vector3.one * (1f + moneyPunch * 0.12f);

        planetText.text = g.PlanetName;
        questCard.gameObject.SetActive(g.QuestVisible && !Blocking);
        if (g.QuestVisible)
        {
            questText.text = g.QuestText;
            questFill.sizeDelta = new Vector2(412 * g.QuestProgress, 14);
            questPunch = Mathf.MoveTowards(questPunch, 0, Time.unscaledDeltaTime * 3f);
            questCard.localScale = Vector3.one * (1f + questPunch * 0.1f);
        }
        rushPill.gameObject.SetActive(g.RushLeft > 0);
        if (g.RushLeft > 0)
        {
            rushText.text = "RUSH HOUR  " + Mathf.CeilToInt(g.RushLeft) + "s";
            rushPill.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 0.05f);
        }
        hintPill.gameObject.SetActive(!string.IsNullOrEmpty(g.Hint) && modal == null);
        hintText.text = g.Hint;
        hintPill.sizeDelta = new Vector2(Mathf.Max(420, hintText.preferredWidth + 80), 90);

        if (g.BoostLeft > 0) { boostBtn.gameObject.SetActive(true); boostText.text = "2× CASH  " + Mathf.CeilToInt(g.BoostLeft) + "s"; }
        else if (g.BoostReady) { boostBtn.gameObject.SetActive(true); boostText.text = WebBridge.AdsAvailable ? "AD: 2× CASH" : "FREE 2× CASH"; boostBtn.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f); }
        else boostBtn.gameObject.SetActive(false);

        foreach (var a in upgradeRefresh) a();
    }

    public void PunchMoney() => moneyPunch = 1f;

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
            if (on)
            {
                float w = Mathf.Max(t.Value.preferredWidth, t.Title.gameObject.activeSelf ? t.Title.preferredWidth : 0) + (t.IconImg && t.IconImg.gameObject.activeSelf ? 110 : 50);
                t.Rt.sizeDelta = new Vector2(Mathf.Max(w, 110), t.Rt.sizeDelta.y);
                bool hasIcon = t.IconImg && t.IconImg.gameObject.activeSelf, hasTitle = t.Title.gameObject.activeSelf;
                if (hasIcon) t.IconImg.rectTransform.anchoredPosition = new Vector2(-t.Rt.sizeDelta.x * 0.5f + 46f, 0);
                t.Value.rectTransform.anchoredPosition = new Vector2(hasIcon ? 30f : 0, hasTitle ? -12f : 0);
                t.Title.rectTransform.anchoredPosition = new Vector2(hasIcon ? 30f : 0, 24f);
                float half = t.Rt.sizeDelta.x * 0.5f * canvas.scaleFactor + 8f;
                sp.x = Mathf.Clamp(sp.x, half, Screen.width - half);   // keep bubbles on screen
                t.Rt.position = sp;
            }
        }

        // objective arrow: bob over target, or clamp to screen edge pointing at it
        var target = Game.I.ArrowTarget;
        arrowImg.gameObject.SetActive(target.HasValue && !Blocking);
        if (target.HasValue)
        {
            var sp = cam.WorldToScreenPoint(target.Value + Vector3.up * 1.6f);
            var rect = new Rect(80, 80, Screen.width - 160, Screen.height - 260);
            bool inside = sp.z > 0 && rect.Contains(sp);
            if (inside)
            {
                arrowImg.rectTransform.position = sp + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 30f * canvas.scaleFactor);
                arrowImg.rectTransform.localRotation = Quaternion.identity;
            }
            else
            {
                var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
                var d = ((Vector2)sp - c) * (sp.z < 0 ? -1 : 1);
                float s = Mathf.Min(rect.width / 2f / Mathf.Abs(d.x + 0.001f), rect.height / 2f / Mathf.Abs(d.y + 0.001f));
                arrowImg.rectTransform.position = c + d * Mathf.Min(s, 1f);
                arrowImg.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f);
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
                if (joyId == -99 && t.phase == TouchPhase.Began && !OverUI(t.fingerId))
                { joyId = t.fingerId; joyOrigin = t.position; joyMouse = false; }
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
