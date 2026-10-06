using System.Collections.Generic;
using UnityEngine;

// ZERO-G TOSS: low gravity means you don't have to walk your food over. Hold TOSS (Space / the button):
// a power meter sweeps and an arc + landing ring aim at the counter. Let go when the ring is on the counter
// and the whole stack sails over, tumbling. Long tosses earn a tip; misses drift on the floor until you grab them.
public class Toss : MonoBehaviour
{
    public static Toss I;
    Player player;
    LineRenderer arc;
    MeshRenderer ring;
    bool holding, autoHold;
    float power, sweepDir = 1f, coachT;
    int coached;
    public bool Available { get; private set; }
    public bool Aiming => holding;
    public bool OnTarget { get; private set; }
    public float Power => power;

    const float MinDist = 2.4f, MaxDist = 15f, Sweep = 1.0f;   // seconds for the meter to go 0 -> 1

    class Flyer { public Item type; public Transform t; public Vector3 from, to; public float k, dur; public Vector3 axis; public Quaternion rot0; public bool hit; public ItemStack dst; public float dist; public bool tipped; }
    class Drift { public Item type; public Transform t; public Vector3 pos, vel; public float bob, life; }
    readonly List<Flyer> flyers = new List<Flyer>();
    readonly List<Drift> drifts = new List<Drift>();
    readonly Dictionary<ItemStack, int> pending = new Dictionary<ItemStack, int>();

    void Awake()
    {
        I = this;
        player = GetComponent<Player>();
        var go = new GameObject("TossArc");
        go.transform.SetParent(transform.parent, false);   // lives and dies with the diner world
        arc = go.AddComponent<LineRenderer>();
        arc.sharedMaterial = Kit.UnlitAlpha; arc.positionCount = 28; arc.widthMultiplier = 0.12f;
        arc.textureMode = LineTextureMode.Stretch; arc.numCapVertices = 4;
        arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arc.enabled = false;
        ring = Kit.FloorQuad("TossRing", Kit.Ring, Color.white, 1.6f, transform.parent, Vector3.zero, 0.05f);
        ring.enabled = false;
    }

    Counter C => Game.I.Counter;

    // What we'd throw: carried food the counter stocks and still has room for.
    bool HasThrowable()
    {
        if (!player.Stack.HasFood()) return false;
        foreach (var kv in C.Stock)
            if (player.Stack.Has(kv.Key) && Room(kv.Value) > 0) return true;
        return false;
    }
    int Room(ItemStack s) => s.Capacity - s.Count - (pending.TryGetValue(s, out var n) ? n : 0);

    void Update()
    {
        var g = Game.I;
        if (!g || !C || UI.I == null) return;
        float dt = Time.deltaTime;
        var zone = C.DropZone;
        var me = transform.position;
        var to = zone.transform.position - me; to.y = 0;
        float dist = to.magnitude;
        Available = !UI.I.Blocking && HasThrowable() && dist > zone.Radius + 0.6f && dist > MinDist && dist < MaxDist;

        bool held = Input.GetKey(KeyCode.Space) || UI.I.TossHeld || autoHold;
        if (Available && held)
        {
            if (!holding) { holding = true; power = 0; sweepDir = 1; Sfx.I.Click(); }
            power += sweepDir * dt / Sweep;
            if (power >= 1f) { power = 1f; sweepDir = -1; }
            if (power <= 0f) { power = 0f; sweepDir = 1; }
        }
        float maxReach = Mathf.Min(MaxDist + 2f, dist * 1.45f + 1.2f);
        var dir = dist > 0.01f ? to / dist : transform.forward;
        float land = power * maxReach;
        var landAt = me + dir * land; landAt.y = 0;
        var off = landAt - zone.transform.position; off.y = 0;
        OnTarget = off.magnitude <= zone.Radius + 0.3f;

        if (holding && (!held || !Available))
        {
            holding = false;
            if (Available && !held) Throw(landAt, OnTarget, off.magnitude, land);
        }

        // aim preview: a floaty low-gravity arc and a ring where it lands
        arc.enabled = ring.enabled = holding;
        if (holding)
        {
            var from = player.Stack.transform.position + Vector3.up * 0.4f;
            float h = 1.4f + land * 0.28f;
            for (int i = 0; i < arc.positionCount; i++)
            {
                float k = i / (float)(arc.positionCount - 1);
                arc.SetPosition(i, Vector3.Lerp(from, landAt + Vector3.up * 0.3f, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * h);
            }
            var col = OnTarget ? Kit.Hex("#7CFF8A") : Kit.Hex("#FF6B6B");
            arc.startColor = Kit.A(col, 0.2f); arc.endColor = Kit.A(col, 0.95f);
            ring.transform.position = landAt + Vector3.up * 0.05f;
            ring.transform.localScale = Vector3.one * (1.2f + Mathf.Sin(Time.time * 12f) * 0.08f);
            ring.sharedMaterial.color = Kit.A(col, 0.95f);
        }

        // ?bot=1: toss from range, letting go when the ring is on the counter
        if (g.AutoPlay)
        {
            if (!holding && Available && dist > 4.5f && Random.value < dt * 2f) autoHold = true;
            if (autoHold && holding && OnTarget && off.magnitude < zone.Radius * 0.5f) autoHold = false;
            if (!Available) autoHold = false;
        }

        // coach the hook once the tutorial gets to the counter (and a few times after)
        if (Available && !holding && coached < 3 && (coachT -= dt) <= 0 && dist > 4f)
        {
            coachT = 9f; coached++;
            UI.I.FloatText(me + Vector3.up * 2.6f, "HOLD TOSS!", Kit.Hex("#B69CFF"), 44);
        }

        UpdateFlyers(dt);
        UpdateDrifts(dt, me);
        UI.I.SetToss(Available || holding, holding, power, OnTarget);
    }

    void Throw(Vector3 landAt, bool hit, float miss, float dist)
    {
        int n = 0;
        // throw everything the counter can take, one food type after another
        foreach (var kv in C.Stock)
        {
            int room = Room(kv.Value);
            while (room > 0 && player.Stack.Has(kv.Key))
            {
                var t = player.Stack.PopType(kv.Key);
                if (!t) break;
                t.SetParent(transform.parent, true);
                var f = new Flyer
                {
                    type = kv.Key, t = t, from = t.position, rot0 = t.rotation,
                    to = landAt + new Vector3(Random.Range(-0.25f, 0.25f), 0.35f, Random.Range(-0.25f, 0.25f)),
                    dur = 0.85f + dist * 0.07f, k = -n * 0.06f / (0.85f + dist * 0.07f),
                    axis = Random.onUnitSphere, hit = hit, dst = kv.Value, dist = dist,
                };
                flyers.Add(f);
                if (hit) pending[kv.Value] = (pending.TryGetValue(kv.Value, out var p) ? p : 0) + 1;
                room--; n++;
            }
        }
        if (n == 0) return;
        Sfx.I.Whoosh();
        WebBridge.Event(hit ? "toss_hit" : "toss_miss", Mathf.RoundToInt(dist));
        if (hit)
        {
            Game.I.Tutorial(TutStep.Drop);
            bool bull = miss < 0.4f;
            if (dist >= 5f || bull)
            {
                // tip: long or dead-centre tosses pay extra
                double tip = 0;
                foreach (var f in flyers) if (f.hit && !f.tipped && f.k <= 0) { f.tipped = true; tip += Items.BasePrice(f.type); }
                tip *= Game.I.PriceMult * 0.3 * Mathf.Clamp(dist / 5f, 1f, 3f) * (bull ? 2 : 1);
                var label = bull && dist >= 5f ? "BULLSEYE LONG TOSS!" : bull ? "BULLSEYE!" : "LONG TOSS!";
                StartCoroutine(TipLater(landAt, tip, label, 0.85f + dist * 0.07f));
                WebBridge.Event(bull ? "toss_bullseye" : "toss_long", Mathf.RoundToInt(dist));
            }
        }
    }

    System.Collections.IEnumerator TipLater(Vector3 at, double tip, string label, float wait)
    {
        yield return new WaitForSeconds(wait);
        if (tip > 0) Game.I.Cash.Deposit(tip, at + Vector3.up);
        UI.I.FloatText(at + Vector3.up * 2.2f, label + "  +" + Kit.Money(tip), Kit.Hex("#7CFF8A"), 46);
        Sfx.I.Coin();
    }

    void UpdateFlyers(float dt)
    {
        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            var f = flyers[i];
            f.k += dt / f.dur;
            if (f.k <= 0f) continue;
            float k = Mathf.Clamp01(f.k);
            // floaty: lingers near the top of the arc
            float ease = k + Mathf.Sin(k * Mathf.PI * 2f) * -0.06f;
            float h = 1.4f + f.dist * 0.28f;
            f.t.position = Vector3.Lerp(f.from, f.to, ease) + Vector3.up * Mathf.Sin(k * Mathf.PI) * h;
            f.t.rotation = Quaternion.AngleAxis(k * 560f, f.axis) * f.rot0;
            if (f.k < 1f) continue;
            flyers.RemoveAt(i);
            if (f.hit && pending.ContainsKey(f.dst)) pending[f.dst] = Mathf.Max(0, pending[f.dst] - 1);
            if (f.hit && f.dst.CanAccept(f.type)) { f.dst.Push(f.type, f.t); Sfx.I.Drop(); }
            else
            {
                // missed: it drifts in the low gravity until someone grabs it
                drifts.Add(new Drift { type = f.type, t = f.t, pos = NavGrid.Snap(new Vector3(f.to.x, 0, f.to.z)), vel = new Vector3(Random.Range(-0.4f, 0.4f), 0, Random.Range(-0.4f, 0.4f)), bob = Random.value * 6f, life = 40f });
                Sfx.I.Deny();
            }
        }
    }

    void UpdateDrifts(float dt, Vector3 me)
    {
        for (int i = drifts.Count - 1; i >= 0; i--)
        {
            var d = drifts[i];
            d.vel *= Mathf.Exp(-dt * 0.6f);
            var next = d.pos + d.vel * dt;
            if (NavGrid.Walkable(next)) d.pos = next; else d.vel = -d.vel * 0.5f;
            d.life -= dt;
            d.t.position = d.pos + Vector3.up * (0.55f + Mathf.Sin(Time.time * 2.2f + d.bob) * 0.15f);
            d.t.rotation = Quaternion.Euler(Time.time * 35f + d.bob * 40f, Time.time * 50f, d.bob * 30f);
            var to = d.pos - me; to.y = 0;
            if (to.magnitude < 1.0f && player.Stack.CanAccept(d.type))
            {
                player.Stack.Push(d.type, d.t);
                Sfx.I.Pop();
                drifts.RemoveAt(i);
                continue;
            }
            if (d.life <= 0) { ItemPool.Release(d.type, d.t); drifts.RemoveAt(i); }
        }
    }

    public void Clear()
    {
        foreach (var f in flyers) ItemPool.Release(f.type, f.t);
        foreach (var d in drifts) ItemPool.Release(d.type, d.t);
        flyers.Clear(); drifts.Clear(); pending.Clear();
        holding = autoHold = false;
    }
}
