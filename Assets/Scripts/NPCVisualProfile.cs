using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NPCVisualProfile",
    menuName = "Thien Menh/NPC Visual Profile")]
public class NPCVisualProfile : ScriptableObject
{
    [Serializable]
    public class VisualEntry
    {
        public Gender gender = Gender.Male;
        public LifeStage lifeStage = LifeStage.Youth;
        public RuntimeAnimatorController controller;
    }

    public string profileId;
    public RuntimeAnimatorController fallbackController;
    public List<VisualEntry> entries = new List<VisualEntry>();

    public RuntimeAnimatorController GetController(
        Gender gender,
        LifeStage lifeStage)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                VisualEntry entry = entries[i];
                if (entry != null &&
                    entry.gender == gender &&
                    entry.lifeStage == lifeStage &&
                    entry.controller != null)
                {
                    return entry.controller;
                }
            }
        }

        return fallbackController;
    }
}
