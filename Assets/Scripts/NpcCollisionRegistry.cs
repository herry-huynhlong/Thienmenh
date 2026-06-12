using System.Collections.Generic;
using UnityEngine;

public static class NpcCollisionRegistry
{
    private sealed class Entry
    {
        public Object owner;
        public readonly List<Collider2D> colliders = new List<Collider2D>();
    }

    static readonly List<Entry> entries = new List<Entry>();

    public static void Register(Object owner, Collider2D[] colliders)
    {
        if (owner == null || colliders == null || colliders.Length == 0)
        {
            return;
        }

        Unregister(owner);
        CleanupDestroyedEntries();

        Entry entry = new Entry
        {
            owner = owner
        };

        foreach (Collider2D collider in colliders)
        {
            if (collider != null &&
                !collider.isTrigger)
            {
                entry.colliders.Add(collider);
            }
        }

        if (entry.colliders.Count == 0)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            Entry other = entries[i];
            if (other == null || other.owner == null)
            {
                continue;
            }

            ApplyIgnore(entry.colliders, other.colliders, true);
        }

        entries.Add(entry);
    }

    public static void Unregister(Object owner)
    {
        if (owner == null)
        {
            return;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (entry == null || entry.owner != owner)
            {
                continue;
            }

            entries.RemoveAt(i);
            ApplyIgnore(entry.colliders, GetAllCollidersExcept(entry), false);
            return;
        }
    }

    static void CleanupDestroyedEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (entry == null || entry.owner == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            RemoveNullColliders(entry.colliders);
            if (entry.colliders.Count == 0)
            {
                entries.RemoveAt(i);
            }
        }
    }

    static List<Collider2D> GetAllCollidersExcept(Entry excluded)
    {
        List<Collider2D> result = new List<Collider2D>();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || entry == excluded || entry.owner == null)
            {
                continue;
            }

            RemoveNullColliders(entry.colliders);
            for (int j = 0; j < entry.colliders.Count; j++)
            {
                Collider2D collider = entry.colliders[j];
                if (collider != null)
                {
                    result.Add(collider);
                }
            }
        }

        return result;
    }

    static void RemoveNullColliders(List<Collider2D> colliders)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = colliders.Count - 1; i >= 0; i--)
        {
            if (colliders[i] == null)
            {
                colliders.RemoveAt(i);
            }
        }
    }

    static void ApplyIgnore(
        List<Collider2D> first,
        List<Collider2D> second,
        bool ignore)
    {
        if (first == null || second == null)
        {
            return;
        }

        for (int i = 0; i < first.Count; i++)
        {
            Collider2D a = first[i];
            if (a == null)
            {
                continue;
            }

            for (int j = 0; j < second.Count; j++)
            {
                Collider2D b = second[j];
                if (b == null || a == b)
                {
                    continue;
                }

                if (a.isTrigger || b.isTrigger)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
