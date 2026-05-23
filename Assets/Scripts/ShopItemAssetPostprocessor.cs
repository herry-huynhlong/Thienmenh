#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ShopItemAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ContainsItemAssetChange(importedAssets) &&
            !ContainsItemAssetChange(deletedAssets) &&
            !ContainsItemAssetChange(movedAssets) &&
            !ContainsItemAssetChange(movedFromAssetPaths))
        {
            return;
        }

        SimpleItemShop[] shops =
            Object.FindObjectsByType<SimpleItemShop>(
                FindObjectsSortMode.None);

        foreach (SimpleItemShop shop in shops)
        {
            if (shop == null ||
                !shop.autoLoadItemsFromAssets)
            {
                continue;
            }

            shop.RefreshItemsFromAssets();
        }
    }

    static bool ContainsItemAssetChange(string[] paths)
    {
        foreach (string path in paths)
        {
            if (path.StartsWith("Assets/Item"))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
