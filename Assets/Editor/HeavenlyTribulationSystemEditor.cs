using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HeavenlyTribulationSystem))]
public class HeavenlyTribulationSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HeavenlyTribulationSystem system =
            target as HeavenlyTribulationSystem;
        if (system == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Test", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("Test Loi Kiep Visual"))
        {
            system.DebugPlayLoiKiepVisual();
        }
        EditorGUI.EndDisabledGroup();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Vao Play Mode roi bam nut de test loi kiep ngoai world.",
                MessageType.Info);
        }
    }
}
