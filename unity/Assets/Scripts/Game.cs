using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TutStep { Move, PickUp, Drop, Serve, Collect, Buy, Clean, Trash, Done }

[Serializable]
public class SaveData
{
    public int planet;
    public double money;
    public List<string> done = new List<string>();
    public List<string> padIds = new List<string>();
    public List<double> padPaid = new List<double>();
    public int lvSpeed, lvCap, lvProfit, lvStaff;
    public long last;
    public double rate;
    public int tut;
    public bool muted;
    public double lifetime;
    public int launches;
    // per-planet stats (reset on launch) — drive the quest chain
    public int pServed, pDeliveries, pVip, pTips, quest;
    public double pEarned;
    // live diner state, so a reload picks up exactly where you left off
    public List<string> stackKeys = new List<string>();
    public List<string> stackItems = new List<string>();
    public double cashPile, deliveryCash;
    public List<string> tipKeys = new List<string>();
    public List<double> tipVals = new List<double>();
    public float boostLeft, boostCd = -1f, rushLeft;
    public float px, pz; public bool hasPos;
}

// SPACE DINER — build and run a burger joint on the Moon, then launch to Mars and beyond.
public class Game : MonoBehaviour
{
    public static Game I;

    // ------------------------------------------------ planets
    struct Planet
    {
        public string name; public Color ground, ground2, sky, floorA, floorB, line, rim, ambient;
        public Item a, b, c; public string aModel, bModel, bName, cName, aName;
    }

    static readonly Planet[] Planets =
    {
        new Planet { name = "MOON BASE", ground = Kit.Hex("#7d8290"), ground2 = Kit.Hex("#c3c7d1"), sky = Kit.Hex("#10163a"),
            floorA = Kit.Hex("#e6ddcc"), floorB = Kit.Hex("#c7b89c"), line = Kit.Hex("#b3a384"), rim = Kit.Hex("#ff9a3c"), ambient = Kit.Hex("#b4bbd8"),
            a = Item.Burger, b = Item.Fries, c = Item.Soda, aName = "GRILL", bName = "FRYER", cName = "SODA" },
        new Planet { name = "MARS OUTPOST", ground = Kit.Hex("#8f3f24"), ground2 = Kit.Hex("#d9804f"), sky = Kit.Hex("#2a0e1c"),
            floorA = Kit.Hex("#eadbc8"), floorB = Kit.Hex("#cfae8c"), line = Kit.Hex("#b8946f"), rim = Kit.Hex("#3cc7ff"), ambient = Kit.Hex("#e0b9a8"),
            a = Item.HotDog, b = Item.Pizza, c = Item.IceCream, aName = "HOT DOGS", bName = "PIZZA OVEN", cName = "ICE CREAM" },
        new Planet { name = "EUROPA STATION", ground = Kit.Hex("#8fb6cc"), ground2 = Kit.Hex("#eef8fc"), sky = Kit.Hex("#061a2c"),
            floorA = Kit.Hex("#dfe9ee"), floorB = Kit.Hex("#b9ccd6"), line = Kit.Hex("#a3b8c4"), rim = Kit.Hex("#ff5ca8"), ambient = Kit.Hex("#b6d8ea"),
            a = Item.Taco, b = Item.Donut, c = Item.Coffee, aName = "TACOS", bName = "DONUTS", cName = "COFFEE" },
    };
    Planet P => Planets[Save.planet % Planets.Length];
    public string PlanetName => P.name + (Save.planet >= Planets.Length ? " " + Roman(Save.planet / Planets.Length + 1) : "");
    static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };

    // ------------------------------------------------ state
    public SaveData Save;
    public Camera Cam;
    public Player Player;
    public Counter Counter;
    public CashPile Cash;
    public TrashCan Trash;
    public readonly List<Producer> Producers = new List<Producer>();
    public readonly List<Table> Tables = new List<Table>();
    public readonly List<Customer> Customers = new List<Customer>();
    public readonly List<Bot> Bots = new List<Bot>();
    readonly List<Pad> pads = new List<Pad>();
    Transform world;
    Light sun;

    public double Money => Save.money;
    public double PlanetMult => Math.Pow(4, Save.planet);
    public double PriceMult => PlanetMult * (1 + 0.15 * Save.lvProfit) * (BoostLeft > 0 ? 2 : 1) * (RushLeft > 0 ? 1.5 : 1);
    public float RushLeft;
    float rushT = 150f;
    public float Popularity = 1f;       // jukebox / plants make customers arrive faster
    public DeliveryWindow Delivery;
    public double BillValue => 5 * PlanetMult;
    public float MoveSpeed => 4.3f * (1f + 0.1f * Save.lvSpeed);
    public int Capacity => 4 + 2 * Save.lvCap;
    public float StaffSpeed => 1f + 0.2f * Save.lvStaff;
    public float CookBoost => BoostLeft > 0 ? 1.5f : 1f;
    public int StaffCount => Bots.Count;

    public bool AutoPlay, Dev;
    public Zone TerminalZone;
    float autoBuyT;
    static readonly int[] UpgradeMax = { 6, 8, 10, 6 };

    public double CheapestUpgrade(out int kind)
    {
        kind = -1; double best = double.MaxValue;
        int[] lv = { Save.lvSpeed, Save.lvCap, Save.lvProfit, Save.lvStaff };
        for (int k = 0; k < (Bots.Count > 0 ? 4 : 3); k++)
            if (lv[k] < UpgradeMax[k] && UpgradeCost(k) < best) { best = UpgradeCost(k); kind = k; }
        return best;
    }
    public string Hint;
    public Vector3? ArrowTarget;
    public float BoostLeft;
    public bool BoostReady => boostCooldown <= 0 && Save.tut >= (int)TutStep.Buy && !launching;
    float boostCooldown = 60f;

    float spawnT = 1f, saveT, rateT;
    double earnedThisSecond;
    bool dirty, launching;

    // ------------------------------------------------ layout
    static readonly float[] SlotX = { -6.6f, -4.3f, -1.6f, 1.5f };  // A1, A2, B, C
    const float SlotZ = 8.2f, StandZ = 7.05f, PickZ = 6.15f;
    const float CounterZ = 3.2f, AisleX = 0.2f, WestAisle = -4.4f;
    static readonly Vector2[] TablePos = { new Vector2(-6.2f, -1f), new Vector2(-2.6f, -1f), new Vector2(-6.2f, -4.6f), new Vector2(-2.6f, -4.6f), new Vector2(-6.2f, -8.2f), new Vector2(-2.6f, -8.2f),
        new Vector2(5.0f, -1.3f), new Vector2(8.2f, -1.3f), new Vector2(5.0f, -4.3f), new Vector2(8.2f, -4.3f) };
    static readonly Vector3 Spawn = new Vector3(5f, 0, -14f), Exit = new Vector3(5.3f, 0, -14.6f);
    static readonly Vector3 RocketPos = new Vector3(7.3f, 0, -8.4f);

    struct PadDef { public string id; public double cost; public Vector3 pos; public string label; public Action build; }
    List<PadDef> padDefs;

    // ======================================================================
    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;   // WebGL: let the browser's requestAnimationFrame drive frames (a fixed rate stutters)
        QualitySettings.shadowDistance = 38f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.antiAliasing = 2;

        Load();
        var url = Application.absoluteURL;
        Dev = url.Contains("dev=1") && (url.Contains("://localhost") || url.Contains("://127.0.0.1"));   // cheats never on the live site
        DevCam.Install(Dev);
        AutoPlay = url.Contains("bot=1");                  // public: demo / trailer mode
        if (Dev && url.Contains("fresh=1")) Save = new SaveData();
        var sm = System.Text.RegularExpressions.Regex.Match(Application.absoluteURL, @"speed=(\d+)");
        if (Dev && sm.Success) Time.timeScale = Mathf.Clamp(int.Parse(sm.Groups[1].Value), 1, 8);
        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();

        Cam = Camera.main;
        Cam.fieldOfView = 40f;
        Cam.nearClipPlane = 0.5f; Cam.farClipPlane = 120f;
        Cam.clearFlags = CameraClearFlags.SolidColor;

        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(52, -32, 0);
        sun.shadows = LightShadows.Hard;
        sun.shadowStrength = 0.55f;
        sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
        sun.intensity = 0.95f;
        sun.color = Kit.Hex("#fff3e2");

        new GameObject("UI").AddComponent<UI>().Init();

        BuildWorld(false);
        RestoreSnapshot();
        if (hadSave && !OfflineEarnings()) Toast("WELCOME BACK! Your diner is just how you left it.");
        WebBridge.Ready();
        WebBridge.Gameplay(true);
    }

    // ======================================================================
    // Save / load
    void Load()
    {
        // Two copies: Unity's PlayerPrefs (IndexedDB, flushed asynchronously) and a synchronous localStorage mirror.
        // Whichever was written last wins, so closing the tab mid-flush never rolls progress back.
        SaveData a = Parse(PlayerPrefs.GetString("sd_save", "")), b = Parse(WebBridge.LoadMirror("sd_save"));
        Save = a == null ? b : b == null ? a : (b.last > a.last ? b : a);
        if (Save == null) Save = new SaveData();
        hadSave = Save.lifetime > 0 || Save.done.Count > 0 || Save.planet > 0;
    }
    bool hadSave;

    static SaveData Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<SaveData>(json); } catch { return null; }
    }

    public void Persist()
    {
        if (Save == null) return;
        Snapshot();
        Save.last = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = JsonUtility.ToJson(Save);
        PlayerPrefs.SetString("sd_save", json);
        PlayerPrefs.Save();
        WebBridge.SaveMirror("sd_save", json);
        dirty = false;
    }

    // ------------------------------------------------ live-state snapshot
    static string Key(Vector3 p) => Mathf.RoundToInt(p.x * 10) + "_" + Mathf.RoundToInt(p.z * 10);

    IEnumerable<(string key, ItemStack stack)> SavedStacks()
    {
        if (Player) yield return ("player", Player.Stack);
        if (Counter) foreach (var kv in Counter.Stock) yield return ("counter_" + (int)kv.Key, kv.Value);
        foreach (var pr in Producers) if (pr) yield return ("prod_" + Key(pr.transform.position), pr.Output);
        foreach (var t in Tables)
            if (t) for (int i = 0; i < t.Seats.Count; i++) yield return ("plate_" + Key(t.transform.position) + "_" + i, t.Seats[i].Plate);
    }

    void Snapshot()
    {
        Save.stackKeys.Clear(); Save.stackItems.Clear(); Save.tipKeys.Clear(); Save.tipVals.Clear();
        Save.cashPile = Save.deliveryCash = 0;
        Save.hasPos = false;
        if (launching || !world) return;      // a launch wipes the diner
        foreach (var (key, st) in SavedStacks())
        {
            if (!st || st.Empty) continue;
            var sb = new System.Text.StringBuilder();
            foreach (var it in st.Types())
            {
                if (key.StartsWith("plate_") && it != Item.Trash) continue;   // half-eaten meals leave with their customer
                if (it == Item.Cash) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append((int)it);
            }
            if (sb.Length == 0) continue;
            Save.stackKeys.Add(key); Save.stackItems.Add(sb.ToString());
        }
        if (Cash) Save.cashPile = Cash.Value;
        if (Delivery && Delivery.Cash) Save.deliveryCash = Delivery.Cash.Value;
        foreach (var t in Tables) if (t && t.TipValue > 0) { Save.tipKeys.Add(Key(t.transform.position)); Save.tipVals.Add(t.TipValue); }
        Save.boostLeft = BoostLeft; Save.boostCd = boostCooldown; Save.rushLeft = RushLeft;
        if (Player) { Save.px = Player.transform.position.x; Save.pz = Player.transform.position.z; Save.hasPos = true; }
    }

    void RestoreSnapshot()
    {
        var byKey = new Dictionary<string, ItemStack>();
        foreach (var (key, st) in SavedStacks()) byKey[key] = st;
        for (int i = 0; i < Save.stackKeys.Count && i < Save.stackItems.Count; i++)
        {
            if (!byKey.TryGetValue(Save.stackKeys[i], out var st)) continue;   // station no longer exists
            foreach (var part in Save.stackItems[i].Split(','))
                if (int.TryParse(part, out var n) && Enum.IsDefined(typeof(Item), n) && st.CanAccept((Item)n))
                    st.AddInstant((Item)n);
        }
        if (Save.cashPile > 0 && Cash) Cash.Deposit(Save.cashPile, Cash.transform.position + Vector3.up);
        if (Save.deliveryCash > 0 && Delivery && Delivery.Cash) Delivery.Cash.Deposit(Save.deliveryCash, Delivery.Cash.transform.position + Vector3.up);
        for (int i = 0; i < Save.tipKeys.Count && i < Save.tipVals.Count; i++)
        {
            var t = Tables.Find(x => x && Key(x.transform.position) == Save.tipKeys[i]);
            if (t) t.AddTip(Save.tipVals[i], t.transform.position + Vector3.up);
        }
        if (Save.boostCd >= 0) { BoostLeft = Save.boostLeft; boostCooldown = Save.boostCd; }
        RushLeft = Save.rushLeft;
        if (Save.hasPos && Player)
            Player.Teleport(new Vector3(Mathf.Clamp(Save.px, -9f, 9f), 0, Mathf.Clamp(Save.pz, -11f, 9f)));
    }

    void OnApplicationPause(bool p) { if (p) Persist(); }
    void OnApplicationFocus(bool f) { if (!f) Persist(); }
    void OnApplicationQuit() => Persist();

    public void SavePad(string id, double paid)
    {
        int i = Save.padIds.IndexOf(id);
        if (i < 0) { Save.padIds.Add(id); Save.padPaid.Add(paid); }
        else Save.padPaid[i] = paid;
        dirty = true;
    }

    double PadPaid(string id) { int i = Save.padIds.IndexOf(id); return i < 0 ? 0 : Save.padPaid[i]; }

    bool OfflineEarnings()
    {
        if (Save.last <= 0 || Save.rate <= 0) return false;
        double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Save.last;
        if (away < 90) return false;
        double secs = Math.Min(away, 3 * 3600);
        double amount = Math.Floor(Save.rate * secs * 0.35);
        if (amount < 5) return false;
        string span = away >= 3600 ? (away / 3600).ToString("0.#") + "h" : Math.Round(away / 60) + " min";
        string body = "Your diner kept cooking for " + span + ".\nYou earned " + Kit.Money(amount) + "!";
        if (WebBridge.AdsAvailable)
            UI.I.Confirm("WELCOME BACK!", body, "COLLECT", () => AddMoney(amount, true),
                "COLLECT 2× (" + Kit.Money(amount * 2) + ")", () => WebBridge.I.ShowRewarded(ok => { AddMoney(ok ? amount * 2 : amount, true); if (ok) WebBridge.Event("ad_offline2x"); }));
        else
            UI.I.Confirm("WELCOME BACK!", body, "COLLECT", () => AddMoney(amount, true));
        return true;
    }

    // ======================================================================
    // Economy
    public void AddMoney(double v, bool earned)
    {
        Save.money = Math.Max(0, Save.money + v);
        if (earned && v > 0) { Save.lifetime += v; Save.pEarned += v; earnedThisSecond += v; UI.I.PunchMoney(); }
        dirty = true;
    }

    public double UpgradeCost(int kind)
    {
        double[] b = { 40, 50, 90, 150 };
        int lv = kind switch { 0 => Save.lvSpeed, 1 => Save.lvCap, 2 => Save.lvProfit, _ => Save.lvStaff };
        return Math.Round(b[kind] * PlanetMult * Math.Pow(1.85, lv));
    }

    public void RequestBoost()
    {
        if (BoostLeft > 0 || !BoostReady) return;
        if (!WebBridge.AdsAvailable) { GrantBoost(90f, 300f); return; }   // no ad network: a free, rarer boost
        WebBridge.I.ShowRewarded(ok =>
        {
            if (!ok) return;
            GrantBoost(120f, 240f);
            WebBridge.Event("ad_boost");
        });
    }

    void GrantBoost(float secs, float cooldown)
    {
        BoostLeft = secs; boostCooldown = cooldown;
        Sfx.I.LevelUp();
        UI.I.FloatText(Player.transform.position + Vector3.up * 2.5f, "2× CASH!", Kit.Hex("#FFC93C"), 64);
    }

    // ======================================================================
    // World
    void BuildWorld(bool fresh)
    {
        if (world) Destroy(world.gameObject);
        foreach (var c in Customers) if (c) Destroy(c.gameObject);
        foreach (var b in Bots) if (b) Destroy(b.gameObject);
        Customers.Clear(); Bots.Clear(); Producers.Clear(); Tables.Clear(); pads.Clear();
        world = new GameObject("World").transform;
        NavGrid.MarkDirty();
        Popularity = 1f; Delivery = null; TerminalZone = null;

        var p = P;
        Cam.backgroundColor = p.sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambient * 0.62f;
        RenderSettings.ambientEquatorColor = Color.Lerp(p.ambient, p.ground, 0.4f) * 0.5f;
        RenderSettings.ambientGroundColor = Color.Lerp(p.ground, Color.black, 0.5f) * 0.5f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(p.sky, p.ground, 0.35f);
        RenderSettings.fogStartDistance = 30f; RenderSettings.fogEndDistance = 75f;

        BuildGround(p);
        BuildKitchenShell(p);

        // Core stations present from the start
        Counter = BuildCounter();
        BuildProducer(0, p.a, true);
        BuildTable(0);
        Trash = BuildTrash(new Vector3(-8.1f, 0, 5.4f));
        Cash = BuildCash(new Vector3(3.3f, 0, CounterZ));
        BuildRocket();
        StartCoroutine(Shuttle());

        Player = Player.Create(world, new Vector3(-2f, 0, 5f));

        // Unlock progression. Costs scale with planet.
        double m = PlanetMult;
        padDefs = new List<PadDef>
        {
            new PadDef { id = "table2", cost = 25, pos = V(TablePos[1]), label = "NEW TABLE", build = () => BuildTable(1) },
            new PadDef { id = "table3", cost = 60, pos = V(TablePos[2]), label = "NEW TABLE", build = () => BuildTable(2) },
            new PadDef { id = "grillA2", cost = 120, pos = new Vector3(SlotX[1], 0, PickZ + 0.2f), label = "2ND " + p.aName, build = () => BuildProducer(1, p.a, false) },
            new PadDef { id = "upgrades", cost = 90, pos = new Vector3(6.4f, 0, 5.7f), label = "UPGRADES", build = BuildTerminal },
            new PadDef { id = "stationB", cost = 200, pos = new Vector3(SlotX[2], 0, PickZ + 0.2f), label = p.bName, build = () => { BuildProducer(2, p.b, false); Toast("NEW ON THE MENU: " + Items.Name(p.b).ToUpper() + "!"); } },
            new PadDef { id = "table4", cost = 260, pos = V(TablePos[3]), label = "NEW TABLE", build = () => BuildTable(3) },
            new PadDef { id = "cashier", cost = 400, pos = new Vector3(3.3f, 0, 6.2f), label = "HIRE CASHIER", build = () => Hire(Bot.Job.Cashier) },
            new PadDef { id = "stationC", cost = 550, pos = new Vector3(SlotX[3], 0, PickZ + 0.2f), label = p.cName, build = () => { BuildProducer(3, p.c, false); Toast("NEW ON THE MENU: " + Items.Name(p.c).ToUpper() + "!"); } },
            new PadDef { id = "table5", cost = 700, pos = V(TablePos[4]), label = "NEW TABLE", build = () => BuildTable(4) },
            new PadDef { id = "runner", cost = 900, pos = new Vector3(-0.1f, 0, 5.2f), label = "HIRE RUNNER", build = () => Hire(Bot.Job.Runner) },
            new PadDef { id = "table6", cost = 1100, pos = V(TablePos[5]), label = "NEW TABLE", build = () => BuildTable(5) },
            new PadDef { id = "cleaner", cost = 1300, pos = new Vector3(-6.9f, 0, 3.6f), label = "HIRE CLEANER", build = () => Hire(Bot.Job.Cleaner) },
            new PadDef { id = "delivery", cost = 650, pos = new Vector3(7.9f, 0, 2.3f), label = "DRONE DELIVERY", build = BuildDelivery },
            new PadDef { id = "jukebox", cost = 800, pos = new Vector3(-8.2f, 0, -2.8f), label = "JUKEBOX", build = BuildJukebox },
            new PadDef { id = "table7", cost = 1500, pos = V(TablePos[6]), label = "PATIO TABLE", build = () => BuildTable(6) },
            new PadDef { id = "table8", cost = 1800, pos = V(TablePos[7]), label = "PATIO TABLE", build = () => BuildTable(7) },
            new PadDef { id = "plants", cost = 2000, pos = new Vector3(-8.2f, 0, -6.4f), label = "SPACE GARDEN", build = BuildPlants },
            new PadDef { id = "table9", cost = 2300, pos = V(TablePos[8]), label = "PATIO TABLE", build = () => BuildTable(8) },
            new PadDef { id = "table10", cost = 2700, pos = V(TablePos[9]), label = "PATIO TABLE", build = () => BuildTable(9) },
            new PadDef { id = "rocket", cost = 4000, pos = RocketPos + new Vector3(-2.6f, 0, 2.2f), label = "LAUNCH TO " + Planets[(Save.planet + 1) % Planets.Length].name, build = Launch },
        };
        padDefs.Sort((a, b) => a.id == "rocket" ? 1 : b.id == "rocket" ? -1 : a.cost.CompareTo(b.cost));
        for (int i = 0; i < padDefs.Count; i++) { var d = padDefs[i]; d.cost *= m; padDefs[i] = d; }

        restoring = true;
        foreach (var d in padDefs) if (Save.done.Contains(d.id) && d.id != "rocket") d.build();
        restoring = false;
        RefreshPads();
        StaticDecor(p);

        if (Dev && Application.absoluteURL.Contains("debug=1"))
            foreach (Transform ch in world)
            {
                var b = Kit.WorldBounds(ch.gameObject);
                Debug.Log($"[DBG] {ch.name} pos={ch.position} scale={ch.localScale.x:F2} bounds={b.size} center={b.center}");
            }
    }

    bool restoring;
    static Vector3 V(Vector2 v) => new Vector3(v.x, 0, v.y);

    void RefreshPads()
    {
        int shown = 0;
        foreach (var d in padDefs)
        {
            if (Save.done.Contains(d.id)) continue;
            if (shown++ >= 2) break;
            if (pads.Exists(x => x && x.Id == d.id)) continue;
            var go = new GameObject("pad_" + d.id);
            go.transform.SetParent(world, false);
            go.transform.localPosition = d.pos;
            var pad = go.AddComponent<Pad>();
            pad.Id = d.id; pad.Cost = d.cost; pad.Paid = PadPaid(d.id); pad.Label = d.label;
            var dd = d;
            pad.OnDone = dd.build;
            pad.Zone = Zone.Make(go.transform, Vector3.zero, 0.95f, Kit.Hex("#FFC93C"), "padzone");
            pad.Tag = UI.I.Tag(go.transform, Vector3.up * 0.4f, false);
            pads.Add(pad);
            if (!restoring) StartCoroutine(PopIn(go.transform));
        }
    }

    public void CompletePad(Pad pad)
    {
        Save.done.Add(pad.Id);
        pads.Remove(pad);
        pad.Tag.Destroy();
        var pos = pad.transform.position;
        Destroy(pad.gameObject);
        Sfx.I.Unlock();
        Burst(pos, Kit.Hex("#FFC93C"));
        pad.OnDone?.Invoke();
        Tutorial(TutStep.Buy);
        NavGrid.MarkDirty();
        WebBridge.Event("unlock_" + pad.Id, (int)Math.Min(int.MaxValue, pad.Cost));
        RefreshPads();
        Persist();
    }

    void BuildGround(Planet p)
    {
        var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer));   // no CreatePrimitive: Plane wants a MeshCollider
        ground.GetComponent<MeshFilter>().sharedMesh = Kit.Quad;
        ground.transform.SetParent(world, false);
        ground.transform.localPosition = new Vector3(0, -0.02f, 10f);
        ground.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ground.transform.localScale = new Vector3(160, 160, 1);
        var gm = new Material(Shader.Find("Standard")) { mainTexture = Kit.Noise(256, p.ground, p.ground2, 0.035f, Save.planet + 5, 14) };
        gm.mainTextureScale = new Vector2(10, 10);
        gm.SetFloat("_Glossiness", 0.05f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = gm;

        // Diner floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(world, false);
        floor.transform.localPosition = new Vector3(0, -0.1f, -1f);
        floor.transform.localScale = new Vector3(19.4f, 0.2f, 21.4f);
        var fm = new Material(Shader.Find("Standard")) { mainTexture = Kit.Tiles(64, p.floorA, p.floorB, p.line) };
        fm.mainTextureScale = new Vector2(19.4f / 2.4f, 21.4f / 2.4f);
        fm.SetFloat("_Glossiness", 0.3f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = fm;

        // Glowing rim with an entrance gap
        var rimMat = new Material(Shader.Find("Standard")) { color = p.rim };
        rimMat.EnableKeyword("_EMISSION");
        rimMat.SetColor("_EmissionColor", p.rim * 0.6f);
        void Rim(Vector3 pos, Vector3 size)
        {
            var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.transform.SetParent(world, false);
            r.transform.localPosition = pos; r.transform.localScale = size;
            r.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
            Destroy(r.GetComponent<Collider>());
        }
        Rim(new Vector3(-9.7f, 0.06f, -1f), new Vector3(0.2f, 0.12f, 21.4f));
        Rim(new Vector3(9.7f, 0.06f, -1f), new Vector3(0.2f, 0.12f, 21.4f));
        Rim(new Vector3(-3.1f, 0.06f, -11.7f), new Vector3(13.2f, 0.12f, 0.2f));
        Rim(new Vector3(8.35f, 0.06f, -11.7f), new Vector3(2.7f, 0.12f, 0.2f));

        // Physics: floor + invisible walls
        void Wall(Vector3 c, Vector3 s) { var w = new GameObject("wall"); w.transform.SetParent(world, false); w.transform.localPosition = c; var b = w.AddComponent<BoxCollider>(); b.size = s; }
        Wall(new Vector3(0, -0.5f, -1f), new Vector3(60, 1f, 60));
        Wall(new Vector3(-10f, 1f, -1f), new Vector3(0.5f, 3, 23));
        Wall(new Vector3(10f, 1f, -1f), new Vector3(0.5f, 3, 23));
        Wall(new Vector3(0, 1f, -12f), new Vector3(21, 3, 0.5f));
        Wall(new Vector3(0, 1f, 9.8f), new Vector3(21, 3, 0.5f));

        var gate = Kit.Spawn("Space/gate_simple", 3.2f, world, new Vector3(5f, 0, -11.7f), 0);
        foreach (var r in gate.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void BuildKitchenShell(Planet p)
    {
        // Back wall with a coloured stripe
        var wallMat = new Material(Shader.Find("Standard")) { color = Kit.Hex("#e9edf5") };
        wallMat.SetFloat("_Glossiness", 0.2f);
        var stripeMat = new Material(Shader.Find("Standard")) { color = p.rim };
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(world, false);
        wall.transform.localPosition = new Vector3(0, 1.3f, 9.55f);
        wall.transform.localScale = new Vector3(19.4f, 2.6f, 0.3f);
        wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.transform.SetParent(world, false);
        stripe.transform.localPosition = new Vector3(0, 1.9f, 9.38f);
        stripe.transform.localScale = new Vector3(19.4f, 0.22f, 0.06f);
        stripe.GetComponent<MeshRenderer>().sharedMaterial = stripeMat;
        Destroy(stripe.GetComponent<Collider>());

        Kit.Spawn("Furniture/kitchenFridgeLarge", Kit.FurnScale * 1.25f, world, new Vector3(4.6f, 0, 8.9f), 0, true);
        Kit.Spawn("Furniture/kitchenFridgeLarge", Kit.FurnScale * 1.25f, world, new Vector3(5.8f, 0, 8.9f), 0, true);
        Kit.Spawn("Furniture/pottedPlant", Kit.FurnScale * 1.4f, world, new Vector3(8.9f, 0, 8.8f));
        Kit.Spawn("Furniture/pottedPlant", Kit.FurnScale * 1.4f, world, new Vector3(-9f, 0, -11f));
        Kit.Spawn("Furniture/pottedPlant", Kit.FurnScale * 1.4f, world, new Vector3(-9f, 0, 1.6f));
        Kit.Spawn("Furniture/lampSquareFloor", Kit.FurnScale * 1.3f, world, new Vector3(9f, 0, -3f));
        Kit.Spawn("Furniture/speaker", Kit.FurnScale * 1.2f, world, new Vector3(-9.1f, 0, 8.9f));
    }

    Counter BuildCounter()
    {
        var root = new GameObject("Counter");
        root.transform.SetParent(world, false);
        root.transform.localPosition = new Vector3(0, 0, CounterZ);
        var c = root.AddComponent<Counter>();
        for (int i = 0; i < 8; i++)
            Kit.Spawn("Furniture/kitchenCabinet", Kit.FurnScale, root.transform, new Vector3(-2.9f + i * 0.69f, 0, 0), 0, true);
        Kit.Spawn("Furniture/computerScreen", Kit.FurnScale * 1.1f, root.transform, new Vector3(1.2f, 0.72f, 0.05f), 180);
        c.StockOrigin = new Vector3(-0.35f, 0.72f, 0);
        c.DropZone = Zone.Make(world, new Vector3(-1.4f, 0, CounterZ + 1.0f), 1.05f, Kit.Hex("#6fd3ff"), "drop");
        c.CashierZone = Zone.Make(world, new Vector3(1.2f, 0, CounterZ + 0.95f), 0.72f, Kit.Hex("#8cff9e"), "cashier");
        c.QueueHead = new Vector3(1.2f, 0, CounterZ - 1.05f);
        return c;
    }

    Producer BuildProducer(int slot, Item product, bool core)
    {
        float x = SlotX[slot];
        var root = new GameObject("Producer_" + product);
        root.transform.SetParent(world, false);
        root.transform.localPosition = new Vector3(x, 0, SlotZ);
        var pr = root.AddComponent<Producer>();
        pr.Product = product;
        GameObject model;
        if (slot == 3)
        {
            Kit.Spawn("Furniture/kitchenCabinet", Kit.FurnScale * 1.55f, root.transform, Vector3.zero, 180, true);
            model = Kit.Spawn("Furniture/kitchenCoffeeMachine", Kit.FurnScale * 2.6f, root.transform, new Vector3(0, 1.1f, 0.05f), 180);
            pr.Interval = 1.7f;
        }
        else
        {
            model = Kit.Spawn(slot == 2 ? "Furniture/kitchenStoveElectric" : "Furniture/kitchenStove", Kit.FurnScale * 1.55f, root.transform, Vector3.zero, 180, true);
            Kit.Spawn("Furniture/hoodModern", Kit.FurnScale * 1.4f, root.transform, new Vector3(0, 1.9f, 0.3f), 180);
            pr.Interval = slot == 2 ? 2.8f : 2.2f;
        }
        pr.SetModel(model.transform);
        pr.SpawnLocal = new Vector3(0, 1.1f, -0.2f);
        pr.Cap = 8;

        // pass-through shelf with the output pile
        var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stand.transform.SetParent(root.transform, false);
        stand.transform.localPosition = new Vector3(0, 0.33f, StandZ - SlotZ);
        stand.transform.localScale = new Vector3(1.05f, 0.66f, 0.62f);
        stand.GetComponent<MeshRenderer>().sharedMaterial = SteelMat;
        var so = new GameObject("Output");
        so.transform.SetParent(root.transform, false);
        so.transform.localPosition = new Vector3(0, 0.66f, StandZ - SlotZ);
        pr.Output = so.AddComponent<ItemStack>();
        pr.Output.Cols = 2; pr.Output.Rows = 2; pr.Output.Cell = new Vector2(0.44f, 0.3f); pr.Output.Capacity = 99; pr.Output.AllowMixed = false;
        pr.Zone = Zone.Make(world, new Vector3(x, 0, PickZ), 0.8f, Kit.Hex("#FFB25C"), "pick");

        Producers.Add(pr);
        Counter.AddStock(product);
        if (!restoring && !core) { StartCoroutine(PopIn(root.transform)); }
        return pr;
    }

    static Material steel;
    static Material SteelMat
    {
        get
        {
            if (!steel) { steel = new Material(Shader.Find("Standard")) { color = Kit.Hex("#c9ced8") }; steel.SetFloat("_Glossiness", 0.45f); }
            return steel;
        }
    }

    void BuildTable(int i)
    {
        var tp = TablePos[i];
        var root = new GameObject("Table" + i);
        root.transform.SetParent(world, false);
        root.transform.localPosition = new Vector3(tp.x, 0, tp.y);
        var t = root.AddComponent<Table>();
        Kit.Spawn("Furniture/tableRound", Kit.FurnScale * 1.15f, root.transform, Vector3.zero, 90, true);
        Kit.Spawn("Furniture/rugRound", Kit.FurnScale * 0.42f, root.transform, new Vector3(0, 0.005f, 0));
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1 : 1;
            var seatPos = new Vector3(tp.x + side * 1.02f, 0, tp.y);
            Kit.Spawn("Furniture/chairRounded", Kit.FurnScale * 1.15f, root.transform, new Vector3(side * 1.1f, 0, 0), side < 0 ? -90 : 90);   // seat faces the table
            var plate = new GameObject("Plate" + s);
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(side * 0.3f, 0.68f, 0);
            var ps = plate.AddComponent<ItemStack>();
            ps.Cols = 1; ps.Rows = 1; ps.FlyTime = 0.3f; ps.ArcHeight = 0.6f;
            t.Seats.Add(new Seat { Table = t, Pos = seatPos, Face = new Vector3(tp.x, 0, tp.y), Plate = ps, Approach = new Vector3(tp.x + side * 1.55f, 0, tp.y - 0.6f) });
        }
        t.Zone = Zone.Make(world, new Vector3(tp.x, 0, tp.y + 1.35f), 0.85f, Kit.Hex("#b7a6ff"), "clean");
        var tips = new GameObject("Tips"); tips.transform.SetParent(root.transform, false); tips.transform.localPosition = new Vector3(0, 0.68f, 0.28f);
        t.Tips = tips.AddComponent<ItemStack>();
        t.Tips.Cols = 2; t.Tips.Rows = 1; t.Tips.Cell = new Vector2(0.3f, 0.2f); t.Tips.FlyTime = 0.35f; t.Tips.ArcHeight = 0.8f;
        Tables.Add(t);
        if (!restoring) StartCoroutine(PopIn(root.transform));
    }

    TrashCan BuildTrash(Vector3 pos)
    {
        var root = new GameObject("Trash");
        root.transform.SetParent(world, false);
        root.transform.localPosition = pos;
        var tc = root.AddComponent<TrashCan>();
        Kit.Spawn("Furniture/trashcan", Kit.FurnScale * 2.2f, root.transform, Vector3.zero, 0, true);
        var mouth = new GameObject("mouth"); mouth.transform.SetParent(root.transform, false); mouth.transform.localPosition = new Vector3(0, 1.1f, 0);
        tc.Mouth = mouth.transform;
        tc.Zone = Zone.Make(world, pos + new Vector3(0.9f, 0, -0.9f), 0.8f, Kit.Hex("#ff7a7a"), "trash");
        return tc;
    }

    CashPile BuildCash(Vector3 pos)
    {
        var root = new GameObject("Cash");
        root.transform.SetParent(world, false);
        root.transform.localPosition = pos;
        var cp = root.AddComponent<CashPile>();
        var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stand.transform.SetParent(root.transform, false);
        stand.transform.localPosition = new Vector3(0, 0.3f, 0);
        stand.transform.localScale = new Vector3(1.2f, 0.6f, 0.8f);
        stand.GetComponent<MeshRenderer>().sharedMaterial = SteelMat;
        var pile = new GameObject("Pile"); pile.transform.SetParent(root.transform, false); pile.transform.localPosition = new Vector3(0, 0.6f, 0);
        cp.Pile = pile.AddComponent<ItemStack>();
        cp.Pile.Cols = 3; cp.Pile.Rows = 2; cp.Pile.Cell = new Vector2(0.44f, 0.26f); cp.Pile.FlyTime = 0.35f; cp.Pile.ArcHeight = 1.4f;
        cp.Zone = Zone.Make(world, pos + new Vector3(0, 0, 1.1f), 0.8f, Kit.Hex("#8cff9e"), "cash");
        return cp;
    }

    void BuildTerminal()
    {
        var root = new GameObject("Terminal");
        root.transform.SetParent(world, false);
        root.transform.localPosition = new Vector3(6.4f, 0, 6.9f);
        var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.transform.SetParent(root.transform, false);
        desk.transform.localPosition = new Vector3(0, 0.4f, 0); desk.transform.localScale = new Vector3(1.4f, 0.8f, 0.7f);
        desk.GetComponent<MeshRenderer>().sharedMaterial = SteelMat;
        Kit.Spawn("Furniture/computerScreen", Kit.FurnScale * 1.6f, root.transform, new Vector3(0, 0.8f, 0.05f), 180);
        var term = root.AddComponent<Terminal>();
        term.Zone = Zone.Make(world, new Vector3(6.4f, 0, 5.8f), 0.8f, Kit.Hex("#6fd3ff"), "upgrades");
        TerminalZone = term.Zone;
        var tag = UI.I.Tag(root.transform, Vector3.up * 2.1f, false);
        tag.Set(null, "UPGRADES");
        if (!restoring) StartCoroutine(PopIn(root.transform));
    }

    void Hire(Bot.Job job)
    {
        Vector3 home = job switch
        {
            Bot.Job.Cashier => new Vector3(1.9f, 0, CounterZ + 1.3f),
            Bot.Job.Runner => new Vector3(-0.1f, 0, 5.2f),
            _ => new Vector3(-6.9f, 0, 4.6f),
        };
        Color tint = job switch { Bot.Job.Cashier => Kit.Hex("#b6ff7a"), Bot.Job.Runner => Kit.Hex("#ffb36b"), _ => Kit.Hex("#7ae9ff") };
        var b = Bot.Create(world, job, home, tint);
        Bots.Add(b);
        if (!restoring) { StartCoroutine(PopIn(b.transform)); Toast(job switch { Bot.Job.Cashier => "CASHIER HIRED! They'll serve for you.", Bot.Job.Runner => "RUNNER HIRED! Food goes to the counter.", _ => "CLEANER HIRED! Tables stay spotless." }); }
    }

    Transform rocket;
    void BuildRocket()
    {
        var root = new GameObject("Rocket").transform;
        root.SetParent(world, false);
        root.localPosition = RocketPos;
        float s = 1.9f;
        Kit.Spawn("Space/platform_large", 2.6f, root, Vector3.zero, 0, true);
        float y = 0.26f;
        foreach (var part in new[] { "rocket_finsA", "rocket_baseA", "rocket_fuelA", "rocket_sidesA", "rocket_topA" })
        {
            var go = Kit.Spawn("Space/" + part, s, root, new Vector3(0, y, 0));
            y += Kit.WorldBounds(go).size.y;
        }
        rocket = root;
    }

    void StaticDecor(Planet p)
    {
        var rng = new System.Random(42 + Save.planet);
        float R() => (float)rng.NextDouble();
        string[] big = { "Space/hangar_largeA", "Space/hangar_roundA", "Space/hangar_smallA", "Space/satelliteDish_large", "Space/machine_generator", "Space/machine_wireless", "Space/structure_detailed" };
        string[] small = { "Space/rock", "Space/rock_largeA", "Space/rock_largeB", "Space/rock_crystals", "Space/rock_crystalsLargeA", "Space/rocks_smallA", "Space/meteor_detailed", "Space/barrels", "Space/rover", "Space/turret_single" };
        string[] flat = { "Space/crater", "Space/craterLarge" };

        // backdrop buildings behind the kitchen and along the sides (never between camera and diner)
        for (int i = 0; i < 16; i++)
        {
            float side = i % 2 == 0 ? -1 : 1;
            Vector3 pos = i < 6 ? new Vector3(-16 + i * 6.4f + R() * 2, 0, 13.5f + R() * 7)
                                : new Vector3(side * (13 + R() * 9), 0, -10 + R() * 22);
            KeepOut(Kit.Spawn(big[rng.Next(big.Length)], 2.6f + R() * 1.6f, world, pos, R() * 360f, true));
        }
        for (int i = 0; i < 45; i++)
        {
            float a = R() * Mathf.PI * 2, r = 12.5f + R() * 26f;
            var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * 1.1f - 1f);
            if (pos.z < -14f && Mathf.Abs(pos.x) < 12f) pos.z = -22f - R() * 8f;   // keep the camera line clear
            KeepOut(Kit.Spawn(small[rng.Next(small.Length)], 1.8f + R() * 2.6f, world, pos, R() * 360f));
        }
        for (int i = 0; i < 18; i++)
        {
            float a = R() * Mathf.PI * 2, r = 12f + R() * 30f;
            var go = Kit.Spawn(flat[rng.Next(flat.Length)], 4f + R() * 6f, world, new Vector3(Mathf.Cos(a) * r, -0.04f, Mathf.Sin(a) * r), R() * 360f);
            KeepOut(go);
            foreach (var rr in go.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Kit.Spawn("Space/craft_speederA", 2.2f, world, new Vector3(-13f, 0, -7f), 40);
        Kit.Spawn("Space/rover", 3f, world, new Vector3(12.5f, 0, 3f), -70);
    }

    // Decor must never intrude on the diner (or the camera's view of it): push it out by its real footprint.
    static void KeepOut(GameObject go)
    {
        var b = Kit.WorldBounds(go);
        var zone = new Bounds(new Vector3(0, 0, -1.5f), new Vector3(22.5f, 50f, 25.5f));
        if (!zone.Intersects(b)) return;
        float left = b.max.x - zone.min.x, right = zone.max.x - b.min.x, back = zone.max.z - b.min.z, front = b.max.z - zone.min.z;
        float m = Mathf.Min(Mathf.Min(left, right), back);   // never push toward the camera side
        var t = go.transform;
        if (m == back) t.position += Vector3.forward * (back + 0.5f);
        else if (m == left) t.position += Vector3.left * (left + 0.5f);
        else t.position += Vector3.right * (right + 0.5f);
    }

    IEnumerator Shuttle()
    {
        var ship = Kit.Spawn("Space/craft_cargoA", 2.4f, world, Spawn + new Vector3(2.8f, 0, -1.5f), -20).transform;
        var basePos = ship.localPosition;
        var ownWorld = world;
        while (ship && world == ownWorld)
        {
            ship.localPosition = basePos + Vector3.up * (0.25f + Mathf.Sin(Time.time * 1.5f) * 0.12f);
            ship.localRotation = Quaternion.Euler(Mathf.Sin(Time.time) * 2f, -20f, Mathf.Sin(Time.time * 1.3f) * 2f);
            yield return null;
        }
    }


    // ======================================================================
    // Expansion: delivery window, jukebox, garden
    void BuildDelivery()
    {
        var root = new GameObject("Delivery");
        root.transform.SetParent(world, false);
        root.transform.localPosition = new Vector3(9.0f, 0, 2.3f);
        var dw = root.AddComponent<DeliveryWindow>();
        var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.transform.SetParent(root.transform, false);
        shelf.transform.localPosition = new Vector3(0, 0.35f, 0);
        shelf.transform.localScale = new Vector3(0.8f, 0.7f, 1.4f);
        shelf.GetComponent<MeshRenderer>().sharedMaterial = SteelMat;
        var crate = new GameObject("Crate"); crate.transform.SetParent(root.transform, false); crate.transform.localPosition = new Vector3(0, 0.7f, 0);
        dw.Crate = crate.AddComponent<ItemStack>();
        dw.Crate.Cols = 2; dw.Crate.Rows = 3; dw.Crate.Cell = new Vector2(0.3f, 0.36f); dw.Crate.AllowMixed = true;
        var drone = Kit.Spawn("Space/craft_speederA", 1.3f, null, Vector3.zero, -90).transform;
        drone.SetParent(root.transform, true);
        dw.Drone = drone;
        dw.DockPos = root.transform.position + new Vector3(1.8f, 1.6f, 0);
        dw.AwayPos = root.transform.position + new Vector3(16f, 7f, 4f);
        drone.position = dw.AwayPos;
        drone.gameObject.SetActive(false);
        var cashRoot = new GameObject("DeliveryCash");
        cashRoot.transform.SetParent(world, false);
        cashRoot.transform.localPosition = new Vector3(9.0f, 0, 0.6f);
        var cp = cashRoot.AddComponent<CashPile>();
        var pile = new GameObject("Pile"); pile.transform.SetParent(cashRoot.transform, false); pile.transform.localPosition = new Vector3(0, 0.02f, 0);
        cp.Pile = pile.AddComponent<ItemStack>();
        cp.Pile.Cols = 2; cp.Pile.Rows = 2; cp.Pile.Cell = new Vector2(0.44f, 0.26f); cp.Pile.FlyTime = 0.4f; cp.Pile.ArcHeight = 1.6f;
        cp.Zone = Zone.Make(world, new Vector3(8.2f, 0, 0.6f), 0.7f, Kit.Hex("#8cff9e"), "dcash");
        dw.Cash = cp;
        dw.Zone = Zone.Make(world, new Vector3(7.9f, 0, 2.3f), 0.8f, Kit.Hex("#6fd3ff"), "delivery");
        dw.Tag = UI.I.Tag(root.transform, new Vector3(0.8f, 2.9f, 0), true);
        dw.Tag.Visible = false;
        Delivery = dw;
        if (!restoring) { StartCoroutine(PopIn(root.transform)); Toast("DRONE DELIVERY OPEN! Bulk orders pay double."); }
    }

    void BuildJukebox()
    {
        var root = new GameObject("Jukebox");
        root.transform.SetParent(world, false);
        root.transform.localPosition = new Vector3(-8.9f, 0, -2.8f);
        Kit.Spawn("Furniture/speaker", Kit.FurnScale * 1.6f, root.transform, new Vector3(0, 0, -0.5f), 90, true);
        Kit.Spawn("Furniture/speaker", Kit.FurnScale * 1.6f, root.transform, new Vector3(0, 0, 0.5f), 90, true);
        Kit.Spawn("Furniture/rugRound", Kit.FurnScale * 0.5f, root.transform, new Vector3(0.6f, 0.005f, 0));
        root.AddComponent<Bouncer>();
        Popularity += 0.25f;
        Sfx.I.MusicBoost();
        if (!restoring) { StartCoroutine(PopIn(root.transform)); Toast("JUKEBOX! +25% customers"); }
    }

    void BuildPlants()
    {
        var root = new GameObject("Garden");
        root.transform.SetParent(world, false);
        Vector3[] spots = { new Vector3(-9.0f, 0, -6.4f), new Vector3(-9.0f, 0, -9.8f), new Vector3(-0.8f, 0, -11.0f), new Vector3(9.0f, 0, -6.2f), new Vector3(-9.0f, 0, 4.8f), new Vector3(2.6f, 0, -11.0f) };
        foreach (var sp in spots) Kit.Spawn("Furniture/pottedPlant", Kit.FurnScale * 1.5f, root.transform, sp, UnityEngine.Random.Range(0, 360f), true);
        foreach (var sp in new[] { new Vector3(-8.9f, 0, -4.9f), new Vector3(-8.9f, 0, -0.7f) })
            Kit.Spawn("Furniture/plantSmall2", 0.9f, root.transform, sp);
        Popularity += 0.2f;
        if (!restoring) { StartCoroutine(PopIn(root.transform)); Toast("SPACE GARDEN! +20% customers"); }
    }

    // ======================================================================
    // Rush hour: short, loud, lucrative
    void UpdateRush(float dt)
    {
        if (Save.tut < (int)TutStep.Done || launching) return;
        if (RushLeft > 0)
        {
            RushLeft -= dt;
            if (RushLeft <= 0) { RushLeft = 0; Sfx.I.RushEnd(); }
            return;
        }
        rushT -= dt;
        if (rushT <= 0 && Tables.Count >= 3)
        {
            rushT = UnityEngine.Random.Range(150f, 220f);
            RushLeft = 40f;
            spawnT = 0;
            Sfx.I.Rush();
            UI.I.FloatText(Player.transform.position + Vector3.up * 3f, "RUSH HOUR!  ×1.5 CASH", Kit.Hex("#FF7A59"), 60);
            WebBridge.Event("rush_hour");
        }
    }

    // ======================================================================
    // Quest chain (per planet)
    struct Quest { public string text; public Func<double> value; public double goal; public double reward; }
    List<Quest> quests;
    public string QuestText; public float QuestProgress; public bool QuestVisible;

    List<Quest> Quests() => quests ??= new List<Quest>
    {
        new Quest { text = "Serve {0} customers", value = () => Save.pServed, goal = 10, reward = 40 },
        new Quest { text = "Own {0} tables", value = () => Tables.Count, goal = 3, reward = 60 },
        new Quest { text = "Earn {0}", value = () => Save.pEarned, goal = 400, reward = 80 },
        new Quest { text = "Serve {0} VIP guests", value = () => Save.pVip, goal = 3, reward = 200 },
        new Quest { text = "Collect {0} tips", value = () => Save.pTips, goal = 10, reward = 250 },
        new Quest { text = "Complete {0} drone deliveries", value = () => Save.pDeliveries, goal = 2, reward = 400 },
        new Quest { text = "Serve {0} customers", value = () => Save.pServed, goal = 150, reward = 700 },
        new Quest { text = "Own {0} tables", value = () => Tables.Count, goal = 10, reward = 1200 },
        new Quest { text = "Earn {0}", value = () => Save.pEarned, goal = 12000, reward = 2000 },
    };

    void UpdateQuest()
    {
        var q = Quests();
        if (Save.quest >= q.Count || Save.tut < (int)TutStep.Buy) { QuestVisible = false; return; }
        var cur = q[Save.quest];
        bool money = cur.text.StartsWith("Earn");
        double goal = cur.goal * (money ? PlanetMult : 1);
        double v = cur.value();
        QuestVisible = true;
        QuestText = string.Format(cur.text, money ? Kit.Money(goal) : goal.ToString());
        QuestProgress = (float)Math.Min(1, v / goal);
        if (v >= goal)
        {
            double reward = cur.reward * PlanetMult;
            Save.quest++;
            Cash.Deposit(reward, Player.transform.position + Vector3.up * 3f);
            Sfx.I.LevelUp();
            UI.I.FloatText(Player.transform.position + Vector3.up * 2.8f, "QUEST COMPLETE!  +" + Kit.Money(reward), Kit.Hex("#FFC93C"), 52);
            UI.I.PunchQuest();
            WebBridge.Event("quest_" + Save.quest);
            dirty = true;
        }
    }

    // ======================================================================
    // Customers
    void UpdateCustomers(float dt)
    {
        spawnT -= dt;
        int maxQueue = 4 + Mathf.Min(Tables.Count / 2, 3) + (RushLeft > 0 ? 2 : 0);
        if (spawnT <= 0 && Counter.Queue.Count < maxQueue && Customers.Count < 28 && !launching)
        {
            spawnT = UnityEngine.Random.Range(2.2f, 4.2f) / ((Tables.Count >= 4 ? 1.3f : 1f) * Popularity * (RushLeft > 0 ? 2.2f : 1f));
            var menu = new List<Item>(Counter.Stock.Keys);
            var order = menu[UnityEngine.Random.Range(0, menu.Count)];
            int maxNeed = 2 + Mathf.Min(Tables.Count / 2, 2);
            bool vip = Tables.Count >= 3 && UnityEngine.Random.value < 0.12f;
            int need = vip ? UnityEngine.Random.Range(3, maxNeed + 3) : UnityEngine.Random.Range(1, maxNeed + 1);
            var c = Customer.Create(world, Spawn + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0, 0), order, need, vip);
            Customers.Add(c);
            Counter.Queue.Add(c);
            c.JoinQueue(Counter.Queue.Count - 1);
        }
    }

    public Seat FindSeat()
    {
        Seat best = null; float bd = float.MaxValue;
        foreach (var t in Tables)
            foreach (var s in t.Seats)
                if (s.Free)
                {
                    float d = (s.Pos - Counter.QueueHead).sqrMagnitude + UnityEngine.Random.value * 6f;
                    if (d < bd) { bd = d; best = s; }
                }
        return best;
    }

    public List<Vector3> SeatPath(Seat s, Vector3 from)
    {
        var list = NavGrid.Path(new Vector3(AisleX, 0, from.z - 0.6f), s.Approach);
        list.Insert(0, new Vector3(AisleX, 0, from.z - 0.6f));   // step out of the queue line first
        list.Add(s.Pos);
        return list;
    }

    public List<Vector3> ExitPath(Seat s, Vector3 from)
    {
        var list = new List<Vector3> { s.Approach };
        list.AddRange(NavGrid.Path(s.Approach, new Vector3(5f, 0, -10.9f)));   // leave through the gate
        list.Add(Exit);
        return list;
    }

    public List<Vector3> TablePath(Table t, Vector3 from)
    {
        var c0 = t.transform.position;
        return NavGrid.Path(from, new Vector3(c0.x, 0, c0.z + 1.25f));
    }

    public List<Vector3> HomePath(Vector3 from, Vector3 to) => NavGrid.Path(from, to);

    // ======================================================================
    // Tutorial + guidance
    public void Tutorial(TutStep s)
    {
        if (Save.tut != (int)s) return;
        Save.tut++;
        dirty = true;
        if (Save.tut == (int)TutStep.Done) { Toast("YOU'RE A NATURAL! Keep expanding."); Persist(); }
    }

    void UpdateGuidance()
    {
        ArrowTarget = null; Hint = null;
        var step = (TutStep)Save.tut;
        switch (step)
        {
            case TutStep.Move: Hint = "Drag anywhere to move"; break;
            case TutStep.PickUp: Hint = "Grab food from the " + P.aName.ToLower(); ArrowTarget = Producers[0].Zone.transform.position; break;
            case TutStep.Drop: Hint = "Stack it on the counter"; ArrowTarget = Counter.DropZone.transform.position; break;
            case TutStep.Serve: Hint = "Stand at the register to serve"; ArrowTarget = Counter.CashierZone.transform.position; break;
            case TutStep.Collect: Hint = "Grab your cash!"; ArrowTarget = Cash.Zone.transform.position; break;
            case TutStep.Buy:
                var pad = pads.Count > 0 ? pads[0] : null;
                if (pad) { Hint = "Stand on the pad to build"; ArrowTarget = pad.transform.position; }
                break;
            case TutStep.Clean:
                var dt = Tables.Find(t => t.Dirty);
                if (dt) { Hint = "Clear the dirty plates"; ArrowTarget = dt.Zone.transform.position; }
                break;
            case TutStep.Trash: Hint = "Toss plates in the trash"; ArrowTarget = Trash.Zone.transform.position; break;
            default:
                // After the tutorial: point at a pad you can afford, or nudge about blockers.
                Pad afford = null;
                foreach (var p in pads) if (p && Money >= p.Cost - p.Paid) { afford = p; break; }
                if (afford) ArrowTarget = afford.transform.position;
                else if (Delivery && Delivery.WantsFood && Player.Stack.HasFood())
                {
                    foreach (var it in Player.Stack.Types()) if (Delivery.Needs(it)) { Hint = "The drone wants your food!"; ArrowTarget = Delivery.Zone.transform.position; break; }
                }
                else if (Counter.Queue.Count > 0 && Counter.Queue[0].State == Customer.St.WaitSeat && !Bots.Exists(b => b.Kind == Bot.Job.Cleaner))
                {
                    var d = Tables.Find(t => t.Dirty);
                    bool canDrop = false;
                    foreach (var kv in Counter.Stock) if (Player.Stack.Has(kv.Key) && !kv.Value.Full) canDrop = true;
                    // food in your hands blocks picking up plates; if the counter is full too, the trash is the way out
                    if (d && Player.Stack.HasFood() && !canDrop) { Hint = "Hands full! Toss extra food in the trash"; ArrowTarget = Trash.Zone.transform.position; }
                    else if (d && Player.Stack.HasFood()) { Hint = "Drop your food on the counter first"; ArrowTarget = Counter.DropZone.transform.position; }
                    else if (d) { Hint = "Customers need a clean table!"; ArrowTarget = d.Zone.transform.position; }
                }
                break;
        }
    }

    // ======================================================================
    void Update()
    {
        float dt = Time.deltaTime;
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown) Sfx.I.StartMusic();
        UpdateCustomers(dt);
        UpdateGuidance();
        if (AutoPlay && TerminalZone && TerminalZone.PlayerIn && (autoBuyT -= dt) <= 0)
        {
            autoBuyT = 0.4f;
            double c = CheapestUpgrade(out int k);
            if (k >= 0 && Money >= c)
            {
                AddMoney(-c, false);
                if (k == 0) Save.lvSpeed++; else if (k == 1) Save.lvCap++; else if (k == 2) Save.lvProfit++; else Save.lvStaff++;
                Sfx.I.LevelUp();
            }
        }

        if (BoostLeft > 0) BoostLeft = Mathf.Max(0, BoostLeft - dt);
        else boostCooldown -= dt;
        UpdateRush(dt);
        UpdateQuest();

        AdaptQuality();
        rateT += dt;
        if (rateT >= 1f)
        {
            Save.rate = Save.rate * 0.97 + earnedThisSecond / rateT * 0.03;
            earnedThisSecond = 0; rateT = 0;
        }
        saveT += dt;
        if (saveT > (dirty ? 3f : 6f)) { saveT = 0; Persist(); }   // food and cash move constantly: snapshot often

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.M)) AddMoney(1000 * PlanetMult, true);
#endif
        if (Dev && Application.absoluteURL.Contains("cheat=1") && Input.GetKeyDown(KeyCode.M)) AddMoney(1000 * PlanetMult, true);
    }

    // Weak phones: after warm-up, if we average under ~40 fps drop shadows + MSAA, then render scale.
    float qT, qFrames; int qLevel;
    void AdaptQuality()
    {
        if (qLevel >= 2 || AutoPlay && Dev) return;
        qT += Time.unscaledDeltaTime; qFrames++;
        if (qT < (qLevel == 0 ? 6f : 4f)) return;
        float fps = qFrames / qT;
        qT = 0; qFrames = 0;
        if (fps >= 40f) { qLevel = 2; return; }            // healthy: stop measuring
        qLevel++;
        if (qLevel == 1) { sun.shadows = LightShadows.None; QualitySettings.antiAliasing = 0; }
        else WebBridge.Event("low_quality", (int)fps);
        WebBridge.Event("quality_drop", (int)fps);
    }

    void LateUpdate()
    {
        if (!Player || launching) return;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float dist = Mathf.Lerp(1.72f, 1.05f, Mathf.InverseLerp(0.46f, 1.3f, aspect));
        var focus = Player.transform.position;
        // Portrait screens are narrow: stay centred on the action, drift gently toward the player.
        float follow = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(0.5f, 1.2f, aspect));
        focus.x = Mathf.Clamp(Mathf.Lerp(-2.2f, focus.x, follow), -5.5f, 5.5f);
        focus.z = Mathf.Clamp(focus.z, -9f, 6.5f);
        var want = focus + new Vector3(0, 11.5f, -8.6f) * dist;
        Cam.transform.position = Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-Time.deltaTime * 6f));
        Cam.transform.rotation = Quaternion.Euler(53f, 0, 0);
    }

    // ======================================================================
    // Launch to the next planet
    void Launch()
    {
        if (restoring) return;
        StartCoroutine(LaunchSeq());
    }

    IEnumerator LaunchSeq()
    {
        launching = true;
        WebBridge.Event("launch", Save.planet + 1);
        Toast("3... 2... 1... LIFTOFF!");
        Sfx.I.Whoosh();
        var cam0 = Cam.transform.position;
        float t = 0;
        var start = rocket.position;
        while (t < 3.2f)
        {
            t += Time.deltaTime;
            float lift = t < 0.8f ? 0 : Mathf.Pow(t - 0.8f, 2.2f) * 4f;
            rocket.position = start + Vector3.up * lift + new Vector3(Mathf.Sin(t * 60f), 0, Mathf.Cos(t * 53f)) * 0.04f * Mathf.Clamp01(t);
            if (UnityEngine.Random.value < 0.6f) Burst(rocket.position + Vector3.up * 0.3f, Kit.Hex("#FFB25C"));
            Cam.transform.position = Vector3.Lerp(Cam.transform.position, new Vector3(rocket.position.x, rocket.position.y + 8f, rocket.position.z - 14f), Time.deltaTime * 1.5f);
            Cam.transform.LookAt(rocket.position + Vector3.up * 2f);
            if (t > 1.5f && t < 1.55f) Sfx.I.Whoosh();
            yield return null;
        }
        // advance
        Save.planet++;
        Save.launches++;
        Save.money = 0;
        Save.done.Clear(); Save.padIds.Clear(); Save.padPaid.Clear();
        Save.lvSpeed = Save.lvCap = Save.lvProfit = Save.lvStaff = 0;
        Save.pServed = Save.pDeliveries = Save.pVip = Save.pTips = Save.quest = 0; Save.pEarned = 0;
        RushLeft = 0;
        Persist();
        launching = false;
        BuildWorld(true);
        Cam.transform.position = Player.transform.position + new Vector3(0, 30, -20);
        Sfx.I.Unlock();
        UI.I.Confirm("WELCOME TO " + P.name + "!", "New planet, new menu: " + Items.Name(P.a) + ", " + Items.Name(P.b) + " & " + Items.Name(P.c) + ".\nEverything earns 4× more here.", "LET'S COOK!", null);
    }

    // ======================================================================
    // FX
    public void Toast(string msg)
    {
        if (restoring || !Player) return;
        UI.I.FloatText(Player.transform.position + Vector3.up * 2.8f, msg, Color.white, 40);
    }

    public void Burst(Vector3 pos, Color c) => StartCoroutine(BurstRing(pos, c));

    IEnumerator BurstRing(Vector3 pos, Color c)
    {
        var ring = Kit.FloorQuad("burst", Kit.Ring, c, 0.5f, null, pos, 0.05f);
        float k = 0;
        while (k < 1f)
        {
            k += Time.deltaTime / 0.45f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 4f, 1f - (1f - k) * (1f - k));
            ring.sharedMaterial.color = Kit.A(c, 1f - k);
            yield return null;
        }
        Destroy(ring.sharedMaterial);
        Destroy(ring.gameObject);
    }

    IEnumerator PopIn(Transform t)
    {
        if (!t) yield break;
        var s = t.localScale;
        float k = 0;
        while (k < 1f && t)
        {
            k += Time.deltaTime / 0.45f;
            t.localScale = s * Kit.EaseOutBack(Mathf.Clamp01(k));
            yield return null;
        }
        if (t) t.localScale = s;
        NavGrid.MarkDirty();   // colliders reach full size only after the pop-in
    }
}
