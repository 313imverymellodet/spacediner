using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Probe
{
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var dir in new[] { "Characters", "Food", "Furniture", "Space" })
        {
            foreach (var path in Directory.GetFiles("Assets/Resources/Kenney/" + dir, "*.fbx"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(Path.DirectorySeparatorChar, '/'));
                var inst = (GameObject)Object.Instantiate(go);
                var rs = inst.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                string clips = "";
                var an = inst.GetComponent<Animation>();
                if (an) clips = string.Join(",", an.Cast<AnimationState>().Select(s => s.name));
                var mats = string.Join("|", rs.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name + ":" + m.shader.name + ":" + (m.mainTexture ? m.mainTexture.name : "-")).Distinct());
                sb.AppendLine($"{dir}/{Path.GetFileNameWithoutExtension(path)} size={b.size.x:F2}x{b.size.y:F2}x{b.size.z:F2} center={b.center.x:F2},{b.center.y:F2},{b.center.z:F2} scale={inst.transform.localScale.x:F2} rot={inst.transform.eulerAngles} clips=[{clips}] mats=[{mats}]");
                Object.DestroyImmediate(inst);
            }
        }
        File.WriteAllText("../probe.txt", sb.ToString());
    }
}
