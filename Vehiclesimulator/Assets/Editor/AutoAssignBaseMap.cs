using UnityEngine;
using UnityEditor;

public class AutoAssignBaseMap_Real
{
    [MenuItem("Tools/Auto Assign BaseMap (Real)")]
    static void Assign()
    {
        string[] matGuids = AssetDatabase.FindAssets("t:Material");

        int count = 0;

        foreach (string guid in matGuids)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null) continue;

            string[] texGuids = AssetDatabase.FindAssets(mat.name + " t:Texture");

            if (texGuids.Length == 0) continue;

            string texPath = AssetDatabase.GUIDToAssetPath(texGuids[0]);
            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
    }
}