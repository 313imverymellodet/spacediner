using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------- tiny tween for things flying into hands/bins
public class Fly : MonoBehaviour
{
    static Fly inst;
    class F { public Transform t, target; public Vector3 off, from; public float k, dur; public Action done; public Vector3 scale; }
    readonly List<F> list = new List<F>();

    public static void To(Transform t, Transform target, Vector3 offset, float dur, Action done)
    {
        if (!inst) inst = new GameObject("Fly").AddComponent<Fly>();
        t.SetParent(null, true);
        inst.list.Add(new F { t = t, target = target, off = offset, from = t.position, dur = dur, done = done, scale = t.localScale });
    }

    void Update()
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var f = list[i];
            f.k += Time.deltaTime / f.dur;
            float k = Mathf.Clamp01(f.k), e = k * k;
            var to = f.target ? f.target.position + f.off : f.from;
            f.t.position = Vector3.Lerp(f.from, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
            f.t.localScale = f.scale * Mathf.Lerp(1f, 0.3f, e);
            if (f.k >= 1f) { list.RemoveAt(i); f.done?.Invoke(); }
        }
    }
}

// ---------------------------------------------------------------- shared character rig helpers
public static class Rig
{
    public static Animation Setup(GameObject root)
    {
        var anim = root.GetComponentInChildren<Animation>();
        if (!anim) return null;
        anim.cullingType = AnimationCullingType.AlwaysAnimate;
        foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
        var hold = anim["holding-both"];
        if (hold != null)
        {
            hold.layer = 1;
            hold.weight = 0;
            foreach (var t in anim.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("arm-")) hold.AddMixingTransform(t, true);
            hold.enabled = true;
        }
        anim.Play("idle");
        return anim;
    }

    public static void Loco(Animation anim, float speed01, bool carrying, float walkRate = 1.4f)
    {
        if (!anim) return;
        string clip = speed01 > 0.08f ? "walk" : "idle";
        if (!anim.IsPlaying(clip)) anim.CrossFade(clip, 0.15f);
        if (clip == "walk") anim["walk"].speed = Mathf.Max(0.6f, speed01) * walkRate;
        var hold = anim["holding-both"];
        if (hold != null)
        {
            hold.enabled = true;
            hold.weight = Mathf.MoveTowards(hold.weight, carrying ? 1f : 0f, Time.deltaTime * 8f);
        }
    }
}

// ---------------------------------------------------------------- Player
public class Player : MonoBehaviour
{
    public ItemStack Stack;
    CharacterController cc;
    Animation anim;
    Transform model;
    float nextTick, nextCash, yaw;
    Vector3 vel;
    WorldTag maxTag;

    public static Player Create(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Player");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;
        var p = root.AddComponent<Player>();
        var m = Kit.Spawn("Characters/character-male-e", Kit.CharScale, root.transform);
        p.model = m.transform;
        p.anim = Rig.Setup(m);
        root.layer = NavGrid.IgnoreLayer;
        p.cc = root.AddComponent<CharacterController>();
        p.cc.radius = 0.32f; p.cc.height = 1.2f; p.cc.center = new Vector3(0, 0.62f, 0); p.cc.stepOffset = 0.2f; p.cc.skinWidth = 0.03f;
        var s = new GameObject("Stack");
        s.transform.SetParent(root.transform, false);
        s.transform.localPosition = new Vector3(0, 0.62f, 0.42f);
        p.Stack = s.AddComponent<ItemStack>();
        p.Stack.FlyTime = 0.22f; p.Stack.ArcHeight = 0.8f;
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.25f), 0.9f, root.transform, Vector3.zero, 0.015f);
        p.maxTag = UI.I.Tag(root.transform, Vector3.up * 2.4f, false);
        p.maxTag.Set("MAX", null);
        return p;
    }

    public bool Tick()
    {
        if (Time.time < nextTick) return false;
        nextTick = Time.time + 0.075f;
        return true;
    }

    public bool CashTick()
    {
        if (Time.time < nextCash) return false;
        nextCash = Time.time + 0.035f;
        return true;
    }

    public Vector3 Velocity => vel;

    // CharacterController ignores direct position writes unless disabled first.
    public void Teleport(Vector3 pos) { cc.enabled = false; transform.position = pos; cc.enabled = true; }

    // ---- ?bot=1 autoplay: a simple priority brain, used for testing and trailer capture.
    Vector3? autoTarget; float autoT;
    List<Vector3> autoPath = new List<Vector3>();

    Vector2 AutoInput()
    {
        var g = Game.I;
        autoT -= Time.deltaTime;
        var pos = transform.position;
        if (autoT <= 0 || autoTarget == null)
        {
            autoT = 0.35f;
            var t = ChooseTarget(g);
            if (t.HasValue && (autoTarget == null || (t.Value - autoTarget.Value).sqrMagnitude > 0.01f || autoPath.Count == 0))
                autoPath = NavGrid.Path(pos, t.Value);
            autoTarget = t;
        }
        if (autoTarget == null) return Vector2.zero;
        if (autoPath.Count == 0) autoPath.Add(autoTarget.Value);   // no grid route (e.g. standing in a blocked cell): head straight there
        var to = autoPath[0];
        var d = to - pos; d.y = 0;
        if (d.magnitude < 0.3f)
        {
            if (autoPath.Count > 1) { autoPath.RemoveAt(0); to = autoPath[0]; d = to - pos; d.y = 0; }
            else
            {
                // grid route ended short of the goal (goal sits in a blocked cell): finish the last stretch directly
                var rest = autoTarget.Value - pos; rest.y = 0;
                if (rest.magnitude > 0.5f) d = rest;
                else if (d.magnitude < 0.2f) return Vector2.zero;
            }
        }
        var n = d.normalized * Mathf.Clamp01(d.magnitude * 2f + 0.3f);
        return new Vector2(n.x, n.z);
    }

    Vector3? ChooseTarget(Game g)
    {
        if (!Stack.Empty && Stack.TopType == Item.Trash) return Why("r1", g.Trash.Zone.transform.position);
        foreach (var pad in FindObjectsByType<Pad>(FindObjectsSortMode.None))   // building never needs empty hands
            if (!pad.Done && g.Money >= pad.Cost - pad.Paid) return Why("r2", pad.transform.position);
        Producer best = null;
        foreach (var p in g.Producers)
            if (!p.Output.Empty && Stack.CanAccept(p.Product) && (best == null || p.Output.Count > best.Output.Count)) best = p;
        if (g.Delivery && g.Delivery.WantsFood)
            foreach (var it in Stack.Types()) if (g.Delivery.Needs(it)) return Why("r3", g.Delivery.Zone.transform.position);
        bool canDrop = false;
        foreach (var kv in g.Counter.Stock) if (Stack.Has(kv.Key) && !kv.Value.Full) canDrop = true;
        if (Stack.HasFood() && canDrop)
            return Why("r4", (Stack.Full || best == null) ? g.Counter.DropZone.transform.position : best.Zone.transform.position);
        // counter can't take what we hold and plates are piling up: empty our hands completely (don't refill)
        if (Stack.HasFood() && g.Tables.Exists(t => t.Dirty)) return Why("r6", g.Trash.Zone.transform.position);
        if (Stack.HasFood() && !Stack.Full && best != null) return Why("r5", best.Zone.transform.position);
        if (g.Cash.Pile.Count >= 4) return Why("r7", g.Cash.Zone.transform.position);
        if (g.Delivery && g.Delivery.Cash.Pile.Count > 0) return Why("r8", g.Delivery.Cash.Zone.transform.position);
        var tipped = g.Tables.Find(t => t.HasTips);
        if (tipped && Stack.Empty) return Why("r9", tipped.Zone.transform.position);
        foreach (var pad in FindObjectsByType<Pad>(FindObjectsSortMode.None))
            if (!pad.Done && g.Money >= pad.Cost - pad.Paid) return Why("r10", pad.transform.position);
        if (g.TerminalZone && g.Money >= g.CheapestUpgrade(out _) * 1.5) return Why("r11", g.TerminalZone.transform.position);
        var front = g.Counter.Queue.Count > 0 ? g.Counter.Queue[0] : null;
        if (front != null && front.WantsService && !g.Counter.HasCashierBot && g.Counter.Stock.TryGetValue(front.Order, out var st) && st.Count > 0)
            return Why("r12", g.Counter.CashierZone.transform.position);
        var dirty = g.Tables.Find(t => t.Dirty);
        if (dirty && !g.Bots.Exists(b => b.Kind == Bot.Job.Cleaner)) return Why("r13", dirty.Zone.transform.position);
        if (best != null) return Why("r14", best.Zone.transform.position);
        if (g.Cash.Pile.Count > 0) return Why("r15", g.Cash.Zone.transform.position);
        return Why("r16", g.Counter.CashierZone.transform.position);
    }

    string lastWhy;
    Vector3? Why(string why, Vector3? t)
    {
        if (Game.I.Dev && why != lastWhy) { lastWhy = why; Debug.Log("[BOT] " + why + " -> " + t + " full=" + Stack.Full + " n=" + Stack.Count + "/" + Stack.Capacity); }
        return t;
    }

    void Update()
    {
        Stack.Capacity = Game.I.Capacity;
        var input = UI.I.Joy;
        float kx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        float ky = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        if (kx != 0 || ky != 0) input = new Vector2(kx, ky).normalized;
        if (Game.I.AutoPlay) input = AutoInput();
        if (UI.I.Blocking) input = Vector2.zero;

        var dir = new Vector3(input.x, 0, input.y);
        float mag = Mathf.Clamp01(dir.magnitude);
        var target = dir.normalized * Game.I.MoveSpeed * mag;
        vel = Vector3.Lerp(vel, target, 1f - Mathf.Exp(-Time.deltaTime * 14f));
        cc.SimpleMove(vel);
        if (mag > 0.05f)
        {
            float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, want, Time.deltaTime * 900f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Game.I.Tutorial(TutStep.Move);
        }
        Rig.Loco(anim, vel.magnitude / Game.I.MoveSpeed, !Stack.Empty);

        // wobbly tower: lag opposite to local velocity
        var lv = transform.InverseTransformDirection(vel);
        Stack.Sway = Vector3.Lerp(Stack.Sway, new Vector3(-lv.x, 0, -lv.z) * 0.035f, 1f - Mathf.Exp(-Time.deltaTime * 6f));
        maxTag.Visible = Stack.Full;
    }
}

// ---------------------------------------------------------------- Customer
public class Customer : MonoBehaviour
{
    public enum St { ToQueue, Queue, Ordering, WaitSeat, ToSeat, Eating, Leaving }
    public St State;
    public Item Order;
    public int Need, Got;
    public bool Vip;
    public ItemStack Hands;
    Seat seat;
    Animation anim;
    readonly List<Vector3> path = new List<Vector3>();
    float speed, eatT, eatTotal, waitT, yaw;
    int slot = -1;
    WorldTag bubble;
    bool sitting;

    public bool WantsService => State == St.Ordering && Got < Need && Arrived;
    bool Arrived => path.Count == 0;

    public static Customer Create(Transform parent, Vector3 spawn, Item order, int need, bool vip = false)
    {
        string[] looks = { "female-a", "female-b", "female-c", "female-d", "female-e", "female-f", "male-a", "male-b", "male-c", "male-d", "male-f" };
        var root = new GameObject("Customer");
        root.transform.SetParent(parent, false);
        root.transform.position = spawn;
        var c = root.AddComponent<Customer>();
        var m = Kit.Spawn("Characters/character-" + looks[UnityEngine.Random.Range(0, looks.Length)], Kit.CharScale, root.transform);
        c.anim = Rig.Setup(m);
        c.speed = UnityEngine.Random.Range(2.3f, 2.9f);
        c.Order = order; c.Need = need; c.Vip = vip;
        if (vip)
        {
            m.transform.localScale *= 1.12f;
            Kit.FloorQuad("vipring", Kit.Ring, Kit.Hex("#FFC93C"), 1.2f, root.transform, Vector3.zero, 0.02f);
            Kit.FloorQuad("vipglow", Kit.Glow, Kit.A(Kit.Hex("#FFC93C"), 0.5f), 1.8f, root.transform, Vector3.zero, 0.018f);
        }
        var h = new GameObject("Hands");
        h.transform.SetParent(root.transform, false);
        h.transform.localPosition = new Vector3(0, 0.62f, 0.4f);
        c.Hands = h.AddComponent<ItemStack>();
        c.Hands.FlyTime = 0.22f; c.Hands.ArcHeight = 0.7f;
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.22f), 0.85f, root.transform, Vector3.zero, 0.015f);
        c.bubble = UI.I.Tag(root.transform, Vector3.up * 2.05f, true);
        c.bubble.Visible = false;
        return c;
    }

    public void GoTo(params Vector3[] pts) { path.Clear(); path.AddRange(pts); }

    public void JoinQueue(int i)
    {
        State = St.ToQueue;
        slot = i;
        var s = Game.I.Counter.QueueSlot(i);
        GoTo(new Vector3(s.x + 1.6f, 0, s.z - 0.6f), s);
    }

    public void SetQueueSlot(int i)
    {
        slot = i;
        if (State == St.Queue || State == St.ToQueue || State == St.Ordering || State == St.WaitSeat)
            GoTo(Game.I.Counter.QueueSlot(i));
    }

    public void OnServed()
    {
        State = St.WaitSeat;
        waitT = 0;
        bubble.Visible = false;
        TryTakeSeat();
    }

    void TryTakeSeat()
    {
        seat = Game.I.FindSeat();
        if (seat == null)
        {
            bubble.Visible = true;
            bubble.SetIcon(null);
            bubble.Set(null, "NEED TABLE");
            return;
        }
        seat.Occupant = this;
        bubble.Visible = false;
        Game.I.Counter.Leave(this);
        State = St.ToSeat;
        GoTo(Game.I.SeatPath(seat, transform.position).ToArray());
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float sp = 0;
        if (path.Count > 0 && !sitting)
        {
            var to = path[0]; to.y = 0;
            var d = to - transform.position; d.y = 0;
            float step = speed * dt;
            if (d.magnitude <= step) { transform.position = to; path.RemoveAt(0); }
            else
            {
                var dir = d.normalized;
                // crowd separation: slide around anyone standing in our way
                foreach (var o in Game.I.Customers)
                {
                    if (o == this || !o) continue;
                    var off = transform.position - o.transform.position; off.y = 0;
                    float dist = off.magnitude;
                    if (dist < 0.6f && dist > 0.001f && Vector3.Dot(-off, dir) > 0)
                        dir = (dir + off.normalized * (0.6f - dist) * 2.2f).normalized;
                }
                transform.position += dir * step;
                sp = 1f;
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 720f);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
        }
        if (!sitting) Rig.Loco(anim, sp, !Hands.Empty, 1.2f);

        switch (State)
        {
            case St.ToQueue:
                if (Arrived) State = St.Queue;
                break;
            case St.Queue:
                if (slot == 0 && Arrived)
                {
                    State = St.Ordering;
                    bubble.Visible = true;
                    bubble.SetIcon(UI.Icon(Order));
                    if (Vip) bubble.Bg.color = Kit.Hex("#FFE9A8");
                }
                FaceCounter();
                break;
            case St.Ordering:
                bubble.Set(Vip ? "VIP" : null, (Need - Got).ToString());
                FaceCounter();
                break;
            case St.WaitSeat:
                waitT += dt;
                if (waitT > 0.4f) { waitT = 0; TryTakeSeat(); }
                FaceCounter();
                break;
            case St.ToSeat:
                if (Arrived) Sit();
                break;
            case St.Eating:
                eatT -= dt;
                int left = Mathf.CeilToInt(eatT / eatTotal * Need);
                while (seat.Plate.Count > 0 && seat.Plate.Count > left && Items.IsFood(seat.Plate.TopType))
                {
                    var t = seat.Plate.Pop(out var type);
                    ItemPool.Release(type, t);
                }
                if (eatT <= 0) StandUp();
                break;
            case St.Leaving:
                if (Arrived) { bubble.Destroy(); Game.I.Customers.Remove(this); Destroy(gameObject); }
                break;
        }
    }

    void FaceCounter()
    {
        if (!Arrived) return;
        yaw = Mathf.MoveTowardsAngle(yaw, 0, Time.deltaTime * 540f);
        transform.rotation = Quaternion.Euler(0, yaw, 0);
    }

    void Sit()
    {
        State = St.Eating;
        sitting = true;
        transform.position = seat.Pos;
        var f = seat.Face - seat.Pos; f.y = 0;
        yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        anim["holding-both"].weight = 0;
        anim.CrossFade("sit", 0.15f);
        while (!Hands.Empty) Hands.TransferTo(seat.Plate);
        eatTotal = eatT = UnityEngine.Random.Range(3.5f, 5.5f);
    }

    void StandUp()
    {
        sitting = false;
        int plates = Mathf.Clamp((Need + 1) / 2, 1, 3);
        for (int i = 0; i < plates; i++) seat.Plate.Add(Item.Trash, seat.Plate.transform.position + Vector3.up * 0.6f, i * 0.08f);
        seat.Occupant = null;
        State = St.Leaving;
        // happy customers tip; VIPs always do
        if (Vip || UnityEngine.Random.value < 0.3f)
        {
            double tip = Items.BasePrice(Order) * Need * Game.I.PriceMult * (Vip ? 1.5 : 0.5);
            seat.Table.AddTip(tip, transform.position + Vector3.up * 1.2f);
            UI.I.Emote(transform, Vip ? "BIG TIP!" : "TIP!", Kit.Hex("#8cff9e"));
        }
        else if (UnityEngine.Random.value < 0.4f) UI.I.Emote(transform, "YUM!", Kit.Hex("#FF5C8A"));
        GoTo(Game.I.ExitPath(seat, transform.position).ToArray());
    }
}

// ---------------------------------------------------------------- Staff bots (hovering aliens)
public class Bot : MonoBehaviour
{
    public enum Job { Cashier, Runner, Cleaner }
    public Job Kind;
    public ItemStack Stack;
    readonly List<Vector3> path = new List<Vector3>();
    Transform body;
    Vector3 home;
    float yaw, workT, idleT;
    int state;          // job-specific state machine
    Producer src;
    Table table;

    public static Bot Create(Transform parent, Job job, Vector3 home, Color tint)
    {
        var root = new GameObject("Bot_" + job);
        root.transform.SetParent(parent, false);
        root.transform.position = home;
        var b = root.AddComponent<Bot>();
        b.Kind = job; b.home = home;
        var m = Kit.Spawn("Space/alien", Kit.SpaceScale * 1.25f, root.transform);
        Kit.Tint(m, tint);
        b.body = m.transform;
        var s = new GameObject("Stack");
        s.transform.SetParent(root.transform, false);
        s.transform.localPosition = new Vector3(0, 0.75f, 0.4f);
        b.Stack = s.AddComponent<ItemStack>();
        b.Stack.Capacity = 4; b.Stack.FlyTime = 0.22f; b.Stack.ArcHeight = 0.7f;
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.25f), 0.8f, root.transform, Vector3.zero, 0.015f);
        Kit.FloorQuad("jobring", Kit.Ring, Kit.A(tint, 0.9f), 1.05f, root.transform, Vector3.zero, 0.02f);
        if (job == Job.Cashier) Game.I.Counter.HasCashierBot = true;
        return b;
    }

    bool Arrived => path.Count == 0;
    void GoTo(params Vector3[] pts) { path.Clear(); path.AddRange(NavGrid.Path(transform.position, pts[pts.Length - 1])); }

    void Update()
    {
        float dt = Time.deltaTime;
        body.localPosition = new Vector3(0, 0.25f + Mathf.Sin(Time.time * 3f + home.x) * 0.08f, 0);
        if (path.Count > 0)
        {
            var d = path[0] - transform.position; d.y = 0;
            float step = 2.4f * Game.I.StaffSpeed * dt;
            if (d.magnitude <= step) { transform.position = new Vector3(path[0].x, 0, path[0].z); path.RemoveAt(0); }
            else
            {
                transform.position += d.normalized * step;
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 600f);
            }
        }
        body.localRotation = Quaternion.Euler(0, 0, Arrived ? 0 : Mathf.Sin(Time.time * 10f) * 4f);
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        Stack.Capacity = 3 + Game.I.Save.lvStaff;

        switch (Kind)
        {
            case Job.Cashier: yaw = Mathf.MoveTowardsAngle(yaw, 180, dt * 300f); break;
            case Job.Runner: Runner(dt); break;
            case Job.Cleaner: Cleaner(dt); break;
        }
    }

    bool Tick(float dt, float rate) { workT += dt; if (workT < rate / Game.I.StaffSpeed) return false; workT = 0; return true; }

    void Runner(float dt)
    {
        var counter = Game.I.Counter;
        switch (state)
        {
            case 0: // pick the fullest producer whose product the counter needs
                src = null; float best = 0.12f;
                foreach (var p in Game.I.Producers)
                {
                    if (p.Output.Empty || !counter.Stock.ContainsKey(p.Product)) continue;
                    if (p.Fullness > best) { best = p.Fullness; src = p; }
                }
                if (src != null) { GoTo(src.Zone.transform.position); state = 1; }
                else if (Arrived && (transform.position - home).sqrMagnitude > 0.1f) GoTo(home);
                break;
            case 1:
                if (!Arrived) break;
                idleT += dt;
                if (Tick(dt, 0.15f) && !src.Output.Empty && Stack.CanAccept(src.Product)) { src.Output.TransferTo(Stack); idleT = 0; }
                if (Stack.Full || (idleT > 1.2f && !Stack.Empty) || (idleT > 2f && Stack.Empty))
                {
                    idleT = 0;
                    if (Stack.Empty) state = 0;
                    else { GoTo(counter.DropZone.transform.position); state = 2; }
                }
                break;
            case 2:
                if (!Arrived) break;
                if (Tick(dt, 0.12f))
                {
                    if (Stack.Empty) { state = 0; break; }
                    var top = Stack.TopType;
                    if (counter.Stock.TryGetValue(top, out var st)) { Stack.TransferTo(st); Sfx.I.Drop(); }
                    else { var t = Stack.Pop(out var ty); ItemPool.Release(ty, t); }
                }
                break;
        }
    }

    void Cleaner(float dt)
    {
        switch (state)
        {
            case 0:
                table = null;
                foreach (var t in Game.I.Tables) if (t.Dirty) { table = t; break; }
                if (table != null) { GoTo(Game.I.TablePath(table, transform.position).ToArray()); state = 1; }
                else if (Arrived && (transform.position - home).sqrMagnitude > 0.1f) GoTo(Game.I.HomePath(transform.position, home).ToArray());
                break;
            case 1:
                if (!Arrived) break;
                if (Tick(dt, 0.15f) && !table.TakeTrash(Stack))
                {
                    if (Stack.Empty) state = 0;
                    else { GoTo(Game.I.HomePath(transform.position, Game.I.Trash.Zone.transform.position).ToArray()); state = 2; }
                }
                break;
            case 2:
                if (!Arrived) break;
                if (Tick(dt, 0.1f))
                {
                    if (Stack.Empty) state = 0;
                    else Game.I.Trash.Dump(Stack);
                }
                break;
        }
    }
}

// Makes the jukebox speakers thump to the music.
public class Bouncer : MonoBehaviour
{
    Vector3 baseScale;
    void Start() => baseScale = transform.localScale;
    void Update()
    {
        float beat = (Time.time % (60f / 84f)) / (60f / 84f);
        float k = Mathf.Exp(-beat * 6f) * 0.06f;
        transform.localScale = new Vector3(baseScale.x * (1 + k), baseScale.y * (1 - k * 0.5f), baseScale.z * (1 + k));
    }
}
