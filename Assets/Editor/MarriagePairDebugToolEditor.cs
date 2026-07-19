using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MarriagePairDebugTool))]
public class MarriagePairDebugToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MarriagePairDebugTool tool =
            target as MarriagePairDebugTool;
        if (tool == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Test", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("Force Marriage Test Pair"))
        {
            tool.DebugForceMarriageTestPair();
            EditorUtility.SetDirty(tool);
        }
        EditorGUI.EndDisabledGroup();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Vao Play Mode, gan 1 NPC nam va 1 NPC nu, roi bam nut de test cuoi va nha tan hon.",
                MessageType.Info);
        }
    }
}
