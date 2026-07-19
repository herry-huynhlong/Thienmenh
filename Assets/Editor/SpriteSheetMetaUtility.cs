using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static class SpriteSheetMetaUtility
{
    public struct FrameSliceData
    {
        public string name;
        public Rect rect;
        public Vector2 pivot;
    }

    public static List<FrameSliceData> LoadFrameSlicesFromMeta(
        string metaPath,
        string namePrefix)
    {
        List<FrameSliceData> slices = new List<FrameSliceData>();
        if (!File.Exists(metaPath))
        {
            return slices;
        }

        string[] lines = File.ReadAllLines(metaPath);
        FrameSliceData current = default;
        bool insideSpriteSheet = false;
        bool hasCurrent = false;
        float rectX = 0f;
        float rectY = 0f;
        float rectWidth = 0f;
        float rectHeight = 0f;
        float pivotX = 0.5f;
        float pivotY = 0.5f;
        string expectedPrefix = "name: " + (namePrefix ?? string.Empty);

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i] ?? string.Empty;
            string line = rawLine.Trim();

            if (!insideSpriteSheet)
            {
                if (line == "sprites:")
                {
                    insideSpriteSheet = true;
                }

                continue;
            }

            if (line.StartsWith("- serializedVersion:"))
            {
                TryAppendCurrentSlice(
                    slices,
                    ref current,
                    ref hasCurrent,
                    rectX,
                    rectY,
                    rectWidth,
                    rectHeight,
                    pivotX,
                    pivotY);
                rectX = 0f;
                rectY = 0f;
                rectWidth = 0f;
                rectHeight = 0f;
                pivotX = 0.5f;
                pivotY = 0.5f;
                continue;
            }

            if (line.StartsWith(expectedPrefix))
            {
                current.name = line.Substring("name: ".Length).Trim();
                hasCurrent = true;
                continue;
            }

            if (!hasCurrent)
            {
                continue;
            }

            if (line.StartsWith("x: "))
            {
                rectX = ParseFloat(line.Substring(3));
                continue;
            }

            if (line.StartsWith("y: "))
            {
                rectY = ParseFloat(line.Substring(3));
                continue;
            }

            if (line.StartsWith("width: "))
            {
                rectWidth = ParseFloat(line.Substring(7));
                continue;
            }

            if (line.StartsWith("height: "))
            {
                rectHeight = ParseFloat(line.Substring(8));
                continue;
            }

            if (line.StartsWith("pivot: "))
            {
                pivotX = ParseMapFloat(line, "x", 0.5f);
                pivotY = ParseMapFloat(line, "y", 0.5f);
            }
        }

        TryAppendCurrentSlice(
            slices,
            ref current,
            ref hasCurrent,
            rectX,
            rectY,
            rectWidth,
            rectHeight,
            pivotX,
            pivotY);

        return slices
            .Where(slice => !string.IsNullOrWhiteSpace(slice.name))
            .OrderByDescending(slice => slice.rect.y)
            .ThenBy(slice => slice.rect.x)
            .ToList();
    }

    static void TryAppendCurrentSlice(
        List<FrameSliceData> slices,
        ref FrameSliceData current,
        ref bool hasCurrent,
        float rectX,
        float rectY,
        float rectWidth,
        float rectHeight,
        float pivotX,
        float pivotY)
    {
        if (!hasCurrent ||
            string.IsNullOrWhiteSpace(current.name) ||
            rectWidth <= 0f ||
            rectHeight <= 0f)
        {
            current = default;
            hasCurrent = false;
            return;
        }

        current.rect = new Rect(rectX, rectY, rectWidth, rectHeight);
        current.pivot = new Vector2(pivotX, pivotY);
        slices.Add(current);
        current = default;
        hasCurrent = false;
    }

    static float ParseFloat(string value)
    {
        return float.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float parsed)
            ? parsed
            : 0f;
    }

    static float ParseMapFloat(string line, string key, float fallback)
    {
        string token = key + ":";
        int keyIndex = line.IndexOf(token);
        if (keyIndex < 0)
        {
            return fallback;
        }

        int valueStart = keyIndex + token.Length;
        int valueEnd = line.IndexOfAny(new[] { ',', '}' }, valueStart);
        if (valueEnd < 0)
        {
            valueEnd = line.Length;
        }

        string value = line.Substring(valueStart, valueEnd - valueStart);
        return float.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float parsed)
            ? parsed
            : fallback;
    }
}
