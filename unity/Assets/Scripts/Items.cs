using System.Collections.Generic;
using UnityEngine;

public enum Item { Burger, Fries, Soda, HotDog, Pizza, IceCream, Taco, Donut, Coffee, Trash, Cash }

public static class Items
{
    public static bool IsFood(Item i) => i < Item.Trash;

    public static string Model(Item i) => i switch
    {
        Item.Burger => "Food/burger-cheese",
        Item.Fries => "Food/fries",
        Item.Soda => "Food/soda",
        Item.HotDog => "Food/hot-dog",
        Item.Pizza => "Food/pizza",
        Item.IceCream => "Food/ice-cream",
        Item.Taco => "Food/taco",
        Item.Donut => "Food/donut-sprinkles",
        Item.Coffee => "Food/cup-coffee",
        Item.Trash => "Food/plate",
        _ => null,
    };

    public static string Icon(Item i) => i switch
    {
        Item.Burger => "burger", Item.Fries => "fries", Item.Soda => "soda", Item.HotDog => "hot-dog",
        Item.Pizza => "pizza", Item.IceCream => "ice-cream", Item.Taco => "taco", Item.Donut => "donut-sprinkles",
        Item.Coffee => "cup-coffee", Item.Trash => "plate", _ => null,
    };

    public static float Scale(Item i) => i switch
    {
        Item.Pizza => 0.55f, Item.Trash => 0.5f, Item.HotDog => 0.8f, _ => 1f,
    };

    // Vertical spacing when stacked.
    public static float Height(Item i) => i switch
    {
        Item.Burger => 0.22f, Item.Fries => 0.4f, Item.Soda => 0.43f, Item.HotDog => 0.13f, Item.Pizza => 0.05f,
        Item.IceCream => 0.46f, Item.Taco => 0.26f, Item.Donut => 0.1f, Item.Coffee => 0.14f, Item.Trash => 0.05f,
        Item.Cash => 0.06f, _ => 0.2f,
    };

    public static int BasePrice(Item i) => i switch
    {
        Item.Burger => 5, Item.Fries => 8, Item.Soda => 4,
        Item.HotDog => 6, Item.Pizza => 10, Item.IceCream => 5,
        Item.Taco => 7, Item.Donut => 9, Item.Coffee => 5,
        _ => 0,
    };

    public static string Name(Item i) => i switch
    {
        Item.HotDog => "Hot Dog", Item.IceCream => "Ice Cream", _ => i.ToString(),
    };
}

// Pools item visuals so the stacking juggle never allocates.
public static class ItemPool
{
    static readonly Dictionary<Item, Stack<Transform>> free = new Dictionary<Item, Stack<Transform>>();
    static Transform bin;
    static Mesh cashMesh; static Material cashMat;

    public static Transform Get(Item i)
    {
        if (!bin) { bin = new GameObject("ItemPool").transform; bin.gameObject.SetActive(false); }
        if (free.TryGetValue(i, out var s) && s.Count > 0)
        {
            var t = s.Pop();
            t.SetParent(null, true);
            t.localScale = Vector3.one * Items.Scale(i);
            t.rotation = Quaternion.identity;
            return t;
        }
        return Create(i);
    }

    public static void Release(Item i, Transform t)
    {
        if (!t) return;
        if (!free.TryGetValue(i, out var s)) free[i] = s = new Stack<Transform>();
        t.SetParent(bin, false);
        s.Push(t);
    }

    static Transform Create(Item i)
    {
        if (i == Item.Cash)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "cash";
            if (!cashMat) cashMat = new Material(Shader.Find("Standard")) { mainTexture = Kit.Cash };
            cashMat.SetFloat("_Glossiness", 0.1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = cashMat;
            var root = new GameObject("Cash").transform;
            go.transform.SetParent(root, false);
            go.transform.localScale = new Vector3(0.42f, 0.055f, 0.22f);
            go.transform.localPosition = new Vector3(0, 0.0275f, 0);
            return root;
        }
        var t = Kit.Spawn(Items.Model(i), Items.Scale(i)).transform;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }
}
