using System;
using UnityEngine;

public enum HeavenTargetType
{
    Cultivator,
    Monster
}

[Serializable]
public class HeavenNurtureTargetData
{
    public string id;
    public string displayName;
    public string portraitResource;
    public HeavenTargetType targetType = HeavenTargetType.Cultivator;
    public string realm;
    public int aptitude;
    public int fear;
    public string title;
    public string origin;
    public string fateState;
    public string description;
    public bool hasReceivedFate;

    [NonSerialized] public Sprite portrait;
    [NonSerialized] public GameObject runtimeObject;
    [NonSerialized] public NpcFavorite favoriteComponent;
    [NonSerialized] public bool fromJsonOnly;
}

[Serializable]
public class HeavenNurtureTargetDatabase
{
    public HeavenNurtureTargetData[] targets;
}
