using UnityEngine;
using UnityEditor;

public class FindMissingScripts : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts")]
    static void FindAll()
    {
        int count = 0;

        // Cherche dans la scène
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    Debug.LogWarning($"[MISSING SCRIPT] GO: {go.name} | Scene: {go.scene.name}", go);
                    count++;
                }
            }
        }

        // Cherche dans les prefabs du projet
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            foreach (Component c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null)
                {
                    Debug.LogWarning($"[MISSING SCRIPT] Prefab: {path}", prefab);
                    count++;
                }
            }
        }

        Debug.Log($"[MISSING SCRIPT] Total trouvés : {count}");
    }
}