using UnityEngine;
using System.Collections.Generic;

public class DontDestroy : MonoBehaviour
{
    static readonly Dictionary<string, DontDestroy> instances =
        new Dictionary<string, DontDestroy>();

    public string uniqueKey = "";
    public bool destroyDuplicates = false;

    void Awake()
    {
        string key =
            GetKey();

        if (ShouldDestroyDuplicate(key) &&
            instances.TryGetValue(key, out DontDestroy existing) &&
            existing != null &&
            existing != this)
        {
            Destroy(gameObject);
            return;
        }

        instances[key] = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        string key =
            GetKey();

        if (instances.TryGetValue(key, out DontDestroy existing) &&
            existing == this)
        {
            instances.Remove(key);
        }
    }

    string GetKey()
    {
        if (!string.IsNullOrEmpty(uniqueKey))
        {
            return uniqueKey;
        }

        return gameObject.name;
    }

    bool ShouldDestroyDuplicate(string key)
    {
        return key == "Systems" ||
            (destroyDuplicates &&
            !string.IsNullOrEmpty(uniqueKey));
    }
}
