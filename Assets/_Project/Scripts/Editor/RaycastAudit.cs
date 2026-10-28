#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RaycastAudit
{
    [MenuItem("Tools/Auditar RaycastTarget")]
    static void Run()
    {
        int count = 0;

        // Cena aberta
        foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            count += Check(g, "Cena");

        // Prefabs do projeto
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var g in prefab.GetComponentsInChildren<Graphic>(true))
                count += Check(g, path);
        }

        Debug.Log($"Auditoria de RaycastTarget: {count} item(ns) com Raycast Target ligado fora de botões. " +
                  "Clique em cada aviso amarelo para localizar o objeto.");
    }

    static int Check(Graphic g, string origin)
    {
        if (!g.raycastTarget) return 0;
        if (g.GetComponent<Selectable>() != null) return 0;   // botões precisam receber toque

        Debug.LogWarning($"[{origin}] {PathOf(g.transform)}", g);
        return 1;
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}
#endif