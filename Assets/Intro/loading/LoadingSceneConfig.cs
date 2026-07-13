using UnityEngine;

[CreateAssetMenu(
    fileName = "LoadingSceneConfig",
    menuName = "ThienMenh/Loading Scene Config")]
public class LoadingSceneConfig : ScriptableObject
{
    public string loadingSceneName = "Loading";
    public string defaultTargetScene = "MainMenu";
    public float minimumLoadingSeconds = 10f;
    public float backgroundCrossFadeSeconds = 0.22f;
    public float loadingTextFrameSeconds = 0.2f;
    public float loadingBounceAmplitude = 8f;
    public float loadingBounceSpeed = 4f;
    public Sprite[] backgroundFrames;
}
