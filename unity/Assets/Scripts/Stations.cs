using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------- Zone
// A glowing floor circle. Everything interactive in the diner is "stand here".
public class Zone : MonoBehaviour
{
    public float Radius = 0.9f;
    public Color Color = Color.white;
    MeshRenderer ring, fill;
    public float Progress = -1f;   // >=0 shows a fill disc (pads)
    float pulse;

    public static Zone Make(Transform parent, Vector3 pos, float radius, Color c, string name = "zone")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var z = go.AddComponent<Zone>();
        z.Radius = radius; z.Color = c;
        z.ring = Kit.FloorQuad("ring", Kit.Ring, c, radius * 2f, go.transform, Vector3.zero, 0.03f);
        z.fill = Kit.FloorQuad("fill", Kit.Disc, Kit.A(c, 0.45f), 0.01f, go.transform, Vector3.zero, 0.035f);
        return z;
    }

    public bool Has(Vector3 p)
    {
        var d = p - transform.position; d.y = 0;
        return d.sqrMagnitude < Radius * Radius;
    }

    public bool PlayerIn => Game.I.Player && Has(Game.I.Player.transform.position);

    void Update()
    {
        bool inside = PlayerIn;
        pulse = Mathf.MoveTowards(pulse, inside ? 1 : 0, Time.deltaTime * 6f);
        float s = Radius * 2f * (1f + pulse * 0.08f + Mathf.Sin(Time.time * 4f) * 0.015f);
        ring.transform.localScale = Vector3.one * s;
        ring.sharedMaterial.color = Kit.A(Color, 0.65f + pulse * 0.35f);
        if (Progress >= 0)
        {
            fill.enabled = true;
            fill.transform.localScale = Vector3.one * Radius * 1.75f * Mathf.Clamp01(Progress);
        }
        else fill.enabled = false;
    }
}

// ---------------------------------------------------------------- Producer
public class Producer : MonoBehaviour
{
    public Item Product;
    public float Interval = 2.4f;
    public int Cap = 8;
    public ItemStack Output;
    public Zone Zone;
    public Transform Model;
    public Vector3 SpawnLocal = new Vector3(0, 0.8f, 0);
    float t, squash;

    void Update()
    {
        t += Time.deltaTime * Game.I.CookBoost;
        if (t >= Interval)
        {
            t = 0;
            if (Output.Count < Cap)
            {
                Output.Add(Product, transform.TransformPoint(SpawnLocal));
                squash = 1f;
            }
        }
        squash = Mathf.MoveTowards(squash, 0, Time.deltaTime * 4f);
        if (Model) Model.localScale = new Vector3(1 + squash * 0.06f, 1 - squash * 0.08f, 1 + squash * 0.06f) * baseScale;

        var p = Game.I.Player;
        if (Zone.PlayerIn && !Output.Empty && p.Stack.CanAccept(Product) && p.Tick())
        {
            Output.TransferTo(p.Stack);
            Sfx.I.Pop();
            Game.I.Tutorial(TutStep.PickUp);
        }
    }

    float baseScale = 1f;
    public void SetModel(Transform m) { Model = m; baseScale = m.localScale.x; }

    public float Fullness => Output.Count / (float)Cap;
}

// ---------------------------------------------------------------- Counter + register + queue
public class Counter : MonoBehaviour
{
    public readonly Dictionary<Item, ItemStack> Stock = new Dictionary<Item, ItemStack>();
    public Zone DropZone, CashierZone;
    public Vector3 QueueHead;           // world
    public readonly List<Customer> Queue = new List<Customer>();
    public bool HasCashierBot;
    public Vector3 StockOrigin;         // local, first stock slot
    float serveT;

    public ItemStack AddStock(Item i)
    {
        if (Stock.ContainsKey(i)) return Stock[i];
        var go = new GameObject("stock_" + i);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = StockOrigin + new Vector3(-Stock.Count * 1.1f, 0, 0);
        var s = go.AddComponent<ItemStack>();
        s.Cols = 3; s.Rows = 2; s.Cell = new Vector2(0.3f, 0.3f); s.Capacity = 24; s.AllowMixed = false;
        Stock[i] = s;
        return s;
    }

    public Vector3 QueueSlot(int i) => QueueHead + new Vector3(0, 0, -1.05f * i);

    void Update()
    {
        var p = Game.I.Player;
        // drop food into matching stock
        if (DropZone.PlayerIn && p.Stack.HasFood() && p.Tick())
        {
            foreach (var kv in Stock)
                if (p.Stack.Has(kv.Key) && p.Stack.TransferTo(kv.Value, kv.Key)) { Sfx.I.Drop(); Game.I.Tutorial(TutStep.Drop); break; }
        }

        // serve the front customer
        bool staffed = CashierZone.PlayerIn || HasCashierBot;
        var front = Queue.Count > 0 ? Queue[0] : null;
        if (staffed && front != null && front.WantsService)
        {
            serveT += Time.deltaTime;
            float rate = CashierZone.PlayerIn ? 0.12f : 0.3f / Game.I.StaffSpeed;
            if (serveT >= rate)
            {
                serveT = 0;
                if (Stock.TryGetValue(front.Order, out var st) && !st.Empty)
                {
                    st.TransferTo(front.Hands);
                    front.Got++;
                    Sfx.I.Pop();
                    if (front.Got >= front.Need)
                    {
                        double pay = Items.BasePrice(front.Order) * front.Need * Game.I.PriceMult * (front.Vip ? 3 : 1);
                        Game.I.Cash.Deposit(pay, front.transform.position + Vector3.up);
                        Sfx.I.Register();
                        Game.I.Save.pServed++;
                        if (front.Vip) { Game.I.Save.pVip++; UI.I.FloatText(front.transform.position + Vector3.up * 2.4f, "VIP ×3!", Kit.Hex("#FFC93C"), 52); }
                        front.OnServed();
                        Game.I.Tutorial(TutStep.Serve);
                    }
                }
            }
        }
    }

    public void Leave(Customer c)
    {
        Queue.Remove(c);
        for (int i = 0; i < Queue.Count; i++) Queue[i].SetQueueSlot(i);
    }
}

// ---------------------------------------------------------------- Cash pile
public class CashPile : MonoBehaviour
{
    public ItemStack Pile;
    public Zone Zone;
    double value;
    const int MaxBills = 48;

    public void Deposit(double amount, Vector3 from)
    {
        value += amount;
        int bills = Mathf.Clamp(Mathf.CeilToInt((float)(amount / Game.I.BillValue)), 1, 6);
        for (int i = 0; i < bills && Pile.Count < MaxBills; i++) Pile.Add(Item.Cash, from, i * 0.05f);
        if (Pile.Empty) Pile.Add(Item.Cash, from);
    }

    void Update()
    {
        var p = Game.I.Player;
        if (Zone.PlayerIn && !Pile.Empty && p.CashTick())
        {
            int n = Pile.Count;
            double share = value / n;
            value -= share;
            var t = Pile.Pop(out var type);
            Fly.To(t, p.transform, new Vector3(0, 0.8f, 0), 0.25f, () => ItemPool.Release(Item.Cash, t));
            UI.I.CashFly(p.transform.position);
            WebBridge.Vibrate(8);
            Game.I.AddMoney(share, true);
            Sfx.I.Coin();
            Game.I.Tutorial(TutStep.Collect);
            if (Pile.Empty) value = 0;
        }
    }

    public double Value => value;
}

// ---------------------------------------------------------------- Tables
public class Seat
{
    public Table Table;
    public Vector3 Pos, Face, Approach;
    public Customer Occupant;
    public ItemStack Plate;
    public bool Dirty => !Plate.Empty && Plate.TopType == Item.Trash;
    public bool Free => Occupant == null && Plate.Empty;
}

public class Table : MonoBehaviour
{
    public readonly List<Seat> Seats = new List<Seat>();
    public Zone Zone;
    public ItemStack Tips;          // happy customers leave cash on the table
    double tipValue;

    public bool Dirty { get { foreach (var s in Seats) if (s.Dirty) return true; return false; } }
    public bool HasTips => Tips && !Tips.Empty;
    public double TipValue => tipValue;

    public void AddTip(double amount, Vector3 from)
    {
        tipValue += amount;
        int bills = Mathf.Clamp(Mathf.CeilToInt((float)(amount / Game.I.BillValue)), 1, 3);
        for (int i = 0; i < bills && Tips.Count < 12; i++) Tips.Add(Item.Cash, from, i * 0.06f);
    }

    void Update()
    {
        var p = Game.I.Player;
        if (!Zone.PlayerIn) return;
        // tips are collected even with full hands
        if (HasTips && p.CashTick())
        {
            double share = tipValue / Tips.Count;
            tipValue -= share;
            var t = Tips.Pop(out _);
            Fly.To(t, p.transform, new Vector3(0, 0.8f, 0), 0.25f, () => ItemPool.Release(Item.Cash, t));
            Game.I.AddMoney(share, true);
            Game.I.Save.pTips++;
            Sfx.I.Coin();
            if (Tips.Empty) tipValue = 0;
        }
        if (!p.Stack.CanAccept(Item.Trash) || !p.Tick()) return;
        foreach (var s in Seats)
            if (s.Dirty) { s.Plate.TransferTo(p.Stack); Sfx.I.Pop(); Game.I.Tutorial(TutStep.Clean); break; }
    }

    public bool TakeTrash(ItemStack into)
    {
        foreach (var s in Seats) if (s.Dirty && into.CanAccept(Item.Trash)) { s.Plate.TransferTo(into); Game.I.Tutorial(TutStep.Clean); return true; }
        return false;
    }
}

// ---------------------------------------------------------------- Delivery drone window
// Drones dock outside with a bulk, mixed order. Fill the crate, get paid double.
public class DeliveryWindow : MonoBehaviour
{
    public Zone Zone;
    public ItemStack Crate;
    public Transform Drone;
    public CashPile Cash;
    public WorldTag Tag;
    public Vector3 DockPos, AwayPos;
    readonly Dictionary<Item, int> need = new Dictionary<Item, int>();
    float stateT = 4f;
    int state;          // 0 away, 1 arriving, 2 docked (order open), 3 leaving
    double orderValue;

    public bool WantsFood => state == 2;
    public bool Needs(Item i) => state == 2 && need.TryGetValue(i, out var n) && n > 0;

    void NewOrder()
    {
        need.Clear(); orderValue = 0;
        var menu = new List<Item>(Game.I.Counter.Stock.Keys);
        int kinds = Mathf.Min(menu.Count, UnityEngine.Random.Range(1, 3));
        for (int k = 0; k < kinds; k++)
        {
            var it = menu[UnityEngine.Random.Range(0, menu.Count)];
            menu.Remove(it);
            int n = UnityEngine.Random.Range(3, 5 + Game.I.Tables.Count / 2);
            need[it] = n;
            orderValue += Items.BasePrice(it) * n;
        }
        RefreshTag();
    }

    void RefreshTag()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in need)
            if (kv.Value > 0) { if (sb.Length > 0) sb.Append("  +  "); sb.Append(kv.Value).Append(' ').Append(Items.Name(kv.Key).ToUpper()); }
        Tag.Set("DELIVERY  ×2 PAY", sb.ToString());
        Item? first = null;
        foreach (var kv in need) if (kv.Value > 0) { first = kv.Key; break; }
        Tag.SetIcon(first.HasValue ? UI.Icon(first.Value) : null);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        stateT -= dt;
        var bob = Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;
        switch (state)
        {
            case 0: // away
                Tag.Visible = false;
                if (stateT <= 0) { state = 1; stateT = 1.6f; Drone.gameObject.SetActive(true); Sfx.I.Whoosh(); }
                break;
            case 1: // flying in
                Drone.position = Vector3.Lerp(AwayPos, DockPos, 1f - Mathf.Pow(Mathf.Clamp01(stateT / 1.6f), 2)) + bob;
                if (stateT <= 0) { state = 2; NewOrder(); }
                break;
            case 2: // docked: take food
                Drone.position = DockPos + bob;
                Tag.Visible = true;
                var p = Game.I.Player;
                if (Zone.PlayerIn && p.Stack.HasFood() && p.Tick())
                {
                    foreach (var kv in need)
                    {
                        if (kv.Value <= 0 || !p.Stack.Has(kv.Key)) continue;
                        p.Stack.TransferTo(Crate, kv.Key);
                        need[kv.Key] = kv.Value - 1;
                        Sfx.I.Drop();
                        RefreshTag();
                        break;
                    }
                    bool done = true;
                    foreach (var kv in need) if (kv.Value > 0) done = false;
                    if (done)
                    {
                        Cash.Deposit(orderValue * 2 * Game.I.PriceMult, Drone.position);
                        Sfx.I.Register();
                        Game.I.Save.pDeliveries++;
                        UI.I.FloatText(transform.position + Vector3.up * 2f, "DELIVERED!", Kit.Hex("#8cff9e"), 56);
                        state = 3; stateT = 1.4f;
                        Sfx.I.Whoosh();
                    }
                }
                break;
            case 3: // leaving with the crate
                Tag.Visible = false;
                Drone.position = Vector3.Lerp(AwayPos, DockPos, Mathf.Clamp01(stateT / 1.4f)) + bob;
                if (!Crate.Empty && stateT < 1.2f) { var t = Crate.Pop(out var ty); Fly.To(t, Drone, Vector3.zero, 0.2f, () => ItemPool.Release(ty, t)); }
                if (stateT <= 0)
                {
                    Crate.Clear();
                    state = 0; stateT = UnityEngine.Random.Range(18f, 32f);
                    Drone.gameObject.SetActive(false);
                }
                break;
        }
    }
}

// ---------------------------------------------------------------- Trash can
public class TrashCan : MonoBehaviour
{
    public Zone Zone;
    public Transform Mouth;

    void Update()
    {
        var p = Game.I.Player;
        // Accepts anything: tossing surplus food is the escape hatch when the counter is full.
        if (Zone.PlayerIn && !p.Stack.Empty && p.Tick()) Dump(p.Stack);
    }

    public void Dump(ItemStack s)
    {
        var t = s.Pop(out var type);
        Fly.To(t, Mouth, Vector3.zero, 0.25f, () => ItemPool.Release(type, t));
        Sfx.I.Trash();
        Game.I.Tutorial(TutStep.Trash);
    }
}

// ---------------------------------------------------------------- Unlock pad
public class Pad : MonoBehaviour
{
    public string Id;
    public double Cost, Paid;
    public string Label;
    public Action OnDone;
    public Zone Zone;
    public WorldTag Tag;
    float stay, coinT;
    public bool Done;

    void Update()
    {
        if (Done) return;
        Zone.Progress = (float)(Paid / Cost);
        if (Tag != null) { Tag.Set(Label, Kit.Money(Math.Ceiling(Cost - Paid))); Tag.Progress = (float)(Paid / Cost); }
        if (!Zone.PlayerIn) { stay = 0; return; }
        stay += Time.deltaTime;
        if (stay < 0.25f || Game.I.Money < 1) return;

        double rate = Math.Max(Cost * Time.deltaTime / 1.4, 1.0);
        double pay = Math.Min(Math.Min(rate, Game.I.Money), Cost - Paid);
        Game.I.AddMoney(-pay, false);
        Paid += pay;
        coinT -= Time.deltaTime;
        if (coinT <= 0)
        {
            coinT = 0.06f;
            var t = ItemPool.Get(Item.Cash);
            t.position = Game.I.Player.transform.position + Vector3.up * 1.2f;
            Fly.To(t, transform, Vector3.up * 0.1f, 0.3f, () => ItemPool.Release(Item.Cash, t));
            Sfx.I.Coin();
        }
        Game.I.SavePad(Id, Paid);
        if (Paid >= Cost - 0.001)
        {
            Done = true;
            Game.I.CompletePad(this);
        }
    }
}

// ---------------------------------------------------------------- Upgrade terminal
public class Terminal : MonoBehaviour
{
    public Zone Zone;
    float stay; bool opened;
    void Update()
    {
        if (Zone.PlayerIn)
        {
            stay += Time.deltaTime;
            if (stay > 0.35f && !opened) { opened = true; UI.I.OpenUpgrades(); }
        }
        else
        {
            if (opened) UI.I.CloseUpgrades();
            stay = 0; opened = false;
        }
    }
}
