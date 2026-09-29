using System.Collections.Generic;
using UnityEngine;

// A visual pile of items. Items fly into place along an arc — the core "juice" of the genre.
public class ItemStack : MonoBehaviour
{
    public int Capacity = 999;
    public int Cols = 1, Rows = 1;           // grid footprint per layer
    public Vector2 Cell = new Vector2(0.45f, 0.45f);
    public bool AllowMixed = true;
    public float FlyTime = 0.28f;
    public float ArcHeight = 1.1f;
    public Vector3 Sway;                     // set by movers for the wobbly-tower effect
    public bool Spin;                        // slowly spin items (cash piles look alive)

    class Entry { public Item type; public Transform t; public Vector3 from; public float k = 1f; public Quaternion fromRot; }
    readonly List<Entry> items = new List<Entry>();

    public int Count => items.Count;
    public bool Full => items.Count >= Capacity;
    public bool Empty => items.Count == 0;
    public Item TopType => items[items.Count - 1].type;

    public int CountOf(Item i) { int n = 0; foreach (var e in items) if (e.type == i) n++; return n; }
    public bool Has(Item i) { foreach (var e in items) if (e.type == i) return true; return false; }
    public bool HasFood() { foreach (var e in items) if (Items.IsFood(e.type)) return true; return false; }

    public bool CanAccept(Item i)
    {
        if (Full) return false;
        if (items.Count == 0) return true;
        if (!AllowMixed) return items[0].type == i;
        // mixed food is fine, but never food + trash
        return Items.IsFood(items[0].type) == Items.IsFood(i);
    }

    public void Push(Item type, Transform t, float delay = 0f)
    {
        var e = new Entry { type = type, t = t, from = t.position, fromRot = t.rotation, k = -delay / FlyTime };
        t.SetParent(transform, true);
        items.Add(e);
    }

    // Spawn a fresh item at a world position and fly it in.
    public void Add(Item type, Vector3 fromWorld, float delay = 0f)
    {
        var t = ItemPool.Get(type);
        t.position = fromWorld;
        Push(type, t, delay);
    }

    // Restore from a save: appear in place, no flight.
    public void AddInstant(Item type)
    {
        var t = ItemPool.Get(type);
        t.position = transform.position;
        t.rotation = transform.rotation;
        t.SetParent(transform, true);
        items.Add(new Entry { type = type, t = t, from = t.position, fromRot = t.rotation, k = 1f });
        t.localPosition = Slot(items.Count - 1);
        t.localScale = Vector3.one * Items.Scale(type) / Mathf.Max(0.001f, transform.lossyScale.x);
    }

    public Transform Pop(out Item type)
    {
        var e = items[items.Count - 1];
        items.RemoveAt(items.Count - 1);
        type = e.type;
        e.t.SetParent(null, true);
        return e.t;
    }

    public Transform PopType(Item type)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].type != type) continue;
            var e = items[i];
            items.RemoveAt(i);
            e.t.SetParent(null, true);
            return e.t;
        }
        return null;
    }

    public void Clear()
    {
        foreach (var e in items) ItemPool.Release(e.type, e.t);
        items.Clear();
    }

    // Move the top item into another stack. Returns true if something moved.
    public bool TransferTo(ItemStack dst, Item? only = null)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var e = items[i];
            if (only.HasValue && e.type != only.Value) continue;
            if (!dst.CanAccept(e.type)) return false;
            items.RemoveAt(i);
            dst.Push(e.type, e.t);
            return true;
        }
        return false;
    }

    public IEnumerable<Item> Types() { foreach (var e in items) yield return e.type; }

    Vector3 Slot(int index)
    {
        int per = Cols * Rows;
        int layer = index / per, inLayer = index % per;
        int c = inLayer % Cols, r = inLayer / Cols;
        float x = (c - (Cols - 1) * 0.5f) * Cell.x;
        float z = (r - (Rows - 1) * 0.5f) * Cell.y;
        float y = 0;
        if (per == 1)
        {
            for (int i = 0; i < index; i++) y += Items.Height(items[i].type);
        }
        else if (index < items.Count) y = layer * Items.Height(items[index].type);
        return new Vector3(x, y, z);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i];
            var local = Slot(i);
            if (Cols * Rows == 1) local += Sway * local.y;
            if (e.k < 1f)
            {
                e.k += dt / FlyTime;
                float k = Mathf.Clamp01(e.k);
                if (e.k <= 0f) { e.t.position = e.from; continue; }
                var target = transform.TransformPoint(local);
                float ek = 1f - (1f - k) * (1f - k);
                e.t.position = Vector3.Lerp(e.from, target, ek) + Vector3.up * Mathf.Sin(k * Mathf.PI) * ArcHeight;
                e.t.rotation = Quaternion.Slerp(e.fromRot, transform.rotation, ek);
                float s = Items.Scale(e.type) * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f);
                e.t.localScale = Vector3.one * s / Mathf.Max(0.001f, transform.lossyScale.x);
            }
            else
            {
                e.t.localPosition = Vector3.Lerp(e.t.localPosition, local, 1f - Mathf.Exp(-dt * 25f));
                e.t.localRotation = Spin ? Quaternion.Euler(0, Time.time * 40f + i * 17f, 0) : Quaternion.identity;
                e.t.localScale = Vector3.one * Items.Scale(e.type) / Mathf.Max(0.001f, transform.lossyScale.x);
            }
        }
    }
}
