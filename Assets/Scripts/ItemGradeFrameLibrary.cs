using System.Collections.Generic;
using UnityEngine;

public static class ItemGradeFrameLibrary
{
    static readonly Dictionary<ItemGrade, string> ResourcePathByGrade =
        new Dictionary<ItemGrade, string>
        {
            { ItemGrade.Ha, "UI/ItemFrame/HaPham" },
            { ItemGrade.Trung, "UI/ItemFrame/ThuongPham" },
            { ItemGrade.Thuong, "UI/ItemFrame/ThuongPham 1" },
            { ItemGrade.Tien, "UI/ItemFrame/TienPham" }
        };

    static readonly Dictionary<ItemGrade, Sprite> Cache =
        new Dictionary<ItemGrade, Sprite>();

    public static Sprite GetFrame(ItemGrade grade)
    {
        if (Cache.TryGetValue(grade, out Sprite cached))
        {
            return cached;
        }

        if (!ResourcePathByGrade.TryGetValue(grade, out string path) ||
            string.IsNullOrEmpty(path))
        {
            Cache[grade] = null;
            return null;
        }

        Sprite frame =
            Resources.Load<Sprite>(path);
        Cache[grade] = frame;
        return frame;
    }
}
