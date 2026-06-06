using UnityEditor;
using UnityEngine;

public static class StatItemBalanceEditor
{
    const string ItemRoot = "Assets/Item";

    [MenuItem("Tools/ThienMenh/Items/Balance Selected Stat Items")]
    static void BalanceSelected()
    {
        int count = 0;

        foreach (Object selected in Selection.objects)
        {
            StatItemData item = selected as StatItemData;
            if (item == null)
            {
                continue;
            }

            Undo.RecordObject(item, "Balance Stat Item");
            ItemStatBalanceUtility.ApplyBalancedStats(item);
            EditorUtility.SetDirty(item);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Balanced selected stat items: " + count);
    }

    [MenuItem("Tools/ThienMenh/Items/Balance All Stat Items")]
    static void BalanceAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:StatItemData", new[] { ItemRoot });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatItemData item = AssetDatabase.LoadAssetAtPath<StatItemData>(path);
            if (item == null)
            {
                continue;
            }

            Undo.RecordObject(item, "Balance Stat Item");
            ItemStatBalanceUtility.ApplyBalancedStats(item);
            EditorUtility.SetDirty(item);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Balanced all stat items: " + count);
    }
}
