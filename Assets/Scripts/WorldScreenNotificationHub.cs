using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldScreenNotificationHub : MonoBehaviour
{
    public static WorldScreenNotificationHub Instance { get; private set; }

    [Header("Roots")]
    public GameObject normalRoot;
    public GameObject originRoot;

    [Header("Auto Bind")]
    public string normalRootName = "UI_StoryMarquee";
    public string originRootName = "UI_OriginMarquee";
    public bool duplicateNormalRootForOrigin = true;

    [Header("Behaviour")]
    public bool showStoryLogs = true;
    public bool showNonStoryLogs = true;
    public bool showOriginRewards = true;
    public float normalScrollSpeed = 220f;
    public float originScrollSpeed = 260f;
    public float normalPauseAfterRun = 0.12f;
    public float originPauseAfterRun = 0.2f;
    public float fadeInTime = 0.15f;
    public float fadeOutTime = 0.2f;
    public float horizontalPadding = 28f;
    public float normalDuration = 2.4f;
    public float originDuration = 3.2f;

    [Header("Fallback Colors")]
    public Color normalTextColor = new Color(1f, 0.96f, 0.86f, 1f);
    public Color normalAccentColor = new Color(0.82f, 0.71f, 0.54f, 1f);
    public Color originTextColor = new Color(0.94f, 0.98f, 1f, 1f);
    public Color originAccentColor = new Color(0.25f, 0.78f, 1f, 1f);

    [Header("Fallback Prefix")]
    public string originPrefix = "Thiên Đạo";
    public string normalPrefix = "";
    public bool useRunningText = true;
    public bool disableAnimatorsWhileMarquee = true;
    public bool marqueeRightToLeft = true;
    public bool forceRunningTextLeftAligned = true;
    public bool reverseWordOrder = false;

    NoticeView normalView;
    NoticeView originView;
    readonly Queue<NoticeRequest> normalQueue = new Queue<NoticeRequest>();
    readonly Queue<NoticeRequest> originQueue = new Queue<NoticeRequest>();
    Coroutine normalRoutine;
    Coroutine originRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AutoBind();
    }

    void OnEnable()
    {
        WorldEventManager.LogAdded += HandleWorldLogAdded;
    }

    void Start()
    {
        HideView(normalView);
        HideView(originView);
    }

    void OnDisable()
    {
        WorldEventManager.LogAdded -= HandleWorldLogAdded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ShowNormal(string message)
    {
        if (Instance != null)
        {
            Instance.EnqueueNormal(message);
        }
    }

    public static void ShowOrigin(string message, int originAmount = 0)
    {
        if (Instance != null)
        {
            Instance.EnqueueOrigin(message, originAmount);
        }
    }

    public void EnqueueNormal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        normalQueue.Enqueue(new NoticeRequest
        {
            message = message.Trim(),
            kind = NoticeKind.Normal
        });

        if (normalRoutine == null)
        {
            normalRoutine = StartCoroutine(PlayQueue(normalQueue, normalView, false));
        }
    }

    public void EnqueueOrigin(string message, int originAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        originQueue.Enqueue(new NoticeRequest
        {
            message = message.Trim(),
            kind = NoticeKind.Origin,
            originAmount = originAmount
        });

        if (originRoutine == null)
        {
            originRoutine = StartCoroutine(PlayQueue(originQueue, originView, true));
        }
    }

    void HandleWorldLogAdded(LogEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.content))
        {
            return;
        }

        if (entry.isStoryLog)
        {
            if (!showStoryLogs)
            {
                return;
            }

            if (HeavenDaoSystem.Instance != null &&
                HeavenDaoSystem.Instance.TryGetStoryReward(entry.content, out HeavenDaoStoryReward reward) &&
                reward != null &&
                showOriginRewards)
            {
                EnqueueOrigin(
                    BuildOriginMessage(entry.content, reward),
                    reward.originReward);
                return;
            }
        }
        else if (!showNonStoryLogs)
        {
            return;
        }

        EnqueueNormal(BuildNormalMessage(entry));
    }

    string BuildNormalMessage(LogEntry entry)
    {
        if (entry == null)
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(normalPrefix))
        {
            return entry.content;
        }

        return normalPrefix + ": " + entry.content;
    }

    string BuildOriginMessage(string content, HeavenDaoStoryReward reward)
    {
        string label =
            reward != null &&
            !string.IsNullOrWhiteSpace(reward.displayLabel)
            ? reward.displayLabel
            : "origin";

        if (reward != null && reward.originReward > 0)
        {
            return originPrefix + " +" + reward.originReward + " " + label + "\n" + content;
        }

        return originPrefix + "\n" + content;
    }

    IEnumerator PlayQueue(
        Queue<NoticeRequest> queue,
        NoticeView view,
        bool isOrigin)
    {
        while (queue.Count > 0)
        {
            NoticeRequest request = queue.Dequeue();
            if (!view.IsValid)
            {
                AutoBind();
                view = isOrigin ? originView : normalView;
            }

            if (!view.IsValid)
            {
                continue;
            }

            yield return PlayRequest(view, request, isOrigin);
        }

        if (isOrigin)
        {
            originRoutine = null;
        }
        else
        {
            normalRoutine = null;
        }
    }

    IEnumerator PlayRequest(
        NoticeView view,
        NoticeRequest request,
        bool isOrigin)
    {
        if (view.root != null)
        {
            view.root.SetActive(true);
        }

        if (disableAnimatorsWhileMarquee)
        {
            view.SetAnimatorsEnabled(false);
        }

        string displayMessage = reverseWordOrder
            ? ReverseWordOrder(request.message)
            : request.message;

        view.SetText(displayMessage);
        view.SetColors(
            isOrigin ? originTextColor : normalTextColor,
            isOrigin ? originAccentColor : normalAccentColor);

        if (useRunningText && view.runningTextRect != null && view.maskRect != null)
        {
            yield return PlayMarquee(view, isOrigin);
            yield break;
        }

        CanvasGroup group = view.group;
        if (group != null)
        {
            group.alpha = 0f;
        }

        yield return Fade(view, 0f, 1f, fadeInTime);

        float holdTime =
            isOrigin
            ? Mathf.Max(1f, originDuration)
            : Mathf.Max(0.8f, normalDuration);

        yield return new WaitForSeconds(holdTime);

        yield return Fade(view, 1f, 0f, fadeOutTime);

        HideView(view);
    }

    IEnumerator PlayMarquee(
        NoticeView view,
        bool isOrigin)
    {
        CanvasGroup group = view.group;
        if (group != null)
        {
            group.alpha = 0f;
        }

        if (view.root != null)
        {
            view.root.SetActive(true);
        }

        if (view.text != null)
        {
            view.text.enableWordWrapping = false;
            view.text.overflowMode = TextOverflowModes.Overflow;
            view.text.alignment = TextAlignmentOptions.Left;
            view.text.isRightToLeftText = false;
            view.text.rectTransform.localScale = Vector3.one;
            view.text.rectTransform.localRotation = Quaternion.identity;
            view.text.ForceMeshUpdate(true, true);
        }

        if (forceRunningTextLeftAligned && view.runningTextRect != null)
        {
            Vector2 anchorMin = view.runningTextRect.anchorMin;
            Vector2 anchorMax = view.runningTextRect.anchorMax;
            Vector2 pivot = view.runningTextRect.pivot;

            anchorMin.x = 0f;
            anchorMax.x = 0f;
            pivot.x = 0f;

            view.runningTextRect.anchorMin = anchorMin;
            view.runningTextRect.anchorMax = anchorMax;
            view.runningTextRect.pivot = pivot;
            view.runningTextRect.localScale = Vector3.one;
            view.runningTextRect.localRotation = Quaternion.identity;
        }

        Canvas.ForceUpdateCanvases();
        yield return null;

        LayoutRebuilder.ForceRebuildLayoutImmediate(view.runningTextRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(view.maskRect);
        Canvas.ForceUpdateCanvases();

        float maskWidth = Mathf.Max(1f, view.maskRect.rect.width);
        float textWidth = Mathf.Max(1f, view.runningTextRect.rect.width);
        if (view.text != null)
        {
            textWidth = Mathf.Max(textWidth, view.text.preferredWidth);
        }
        float speed = Mathf.Max(1f, isOrigin ? originScrollSpeed : normalScrollSpeed);
        float offscreenX = maskWidth * 0.5f + horizontalPadding;
        if (!forceRunningTextLeftAligned)
        {
            offscreenX += textWidth * 0.5f;
        }

        float startX = marqueeRightToLeft ? offscreenX : -offscreenX;
        float endX = marqueeRightToLeft ? -offscreenX : offscreenX;
        float distance = Mathf.Abs(startX - endX);
        float duration = Mathf.Max(0.5f, distance / speed);

        Vector2 startPos = view.runningTextRect.anchoredPosition;
        startPos.x = startX;
        view.runningTextRect.anchoredPosition = startPos;

        yield return Fade(view, 0f, 1f, fadeInTime);

        float elapsed = 0f;
        Vector2 current = startPos;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            current.x = Mathf.Lerp(startX, endX, t);
            view.runningTextRect.anchoredPosition = current;
            yield return null;
        }

        float pause = Mathf.Max(0f, isOrigin ? originPauseAfterRun : normalPauseAfterRun);
        if (pause > 0f)
        {
            yield return new WaitForSecondsRealtime(pause);
        }

        yield return Fade(view, 1f, 0f, fadeOutTime);
        HideView(view);

        if (disableAnimatorsWhileMarquee)
        {
            view.SetAnimatorsEnabled(true);
        }
    }

    IEnumerator Fade(
        NoticeView view,
        float from,
        float to,
        float duration)
    {
        if (view.group == null || duration <= 0.01f)
        {
            if (view.group != null)
            {
                view.group.alpha = to;
            }

            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            view.group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        view.group.alpha = to;
    }

    void HideView(NoticeView view)
    {
        if (!view.IsValid)
        {
            return;
        }

        if (view.group != null)
        {
            view.group.alpha = 0f;
        }

        if (disableAnimatorsWhileMarquee)
        {
            view.SetAnimatorsEnabled(true);
        }

        if (view.root != null)
        {
            view.root.SetActive(false);
        }
    }

    void AutoBind()
    {
        if (normalRoot == null)
        {
            normalRoot = FindNamedObject(normalRootName);
        }

        if (originRoot == null && duplicateNormalRootForOrigin && normalRoot != null)
        {
            originRoot = Instantiate(normalRoot, normalRoot.transform.parent);
            originRoot.name = originRootName;
        }

        if (originRoot == null)
        {
            originRoot = FindNamedObject(originRootName);
        }

        normalView = BuildView(normalRoot);
        originView = BuildView(originRoot);

        if (!originView.IsValid && normalView.IsValid && duplicateNormalRootForOrigin)
        {
            originRoot = Instantiate(normalView.root, normalView.root.transform.parent);
            originRoot.name = originRootName;
            originView = BuildView(originRoot);
        }
    }

    NoticeView BuildView(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return default;
        }

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = rootObject.transform as RectTransform;
        }

        CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = rootObject.AddComponent<CanvasGroup>();
        }

        TMP_Text text = rootObject.GetComponentInChildren<TMP_Text>(true);
        TMP_Text runningText = FindText(rootObject, "Story_RunningText");
        RectTransform maskRect = FindRect(rootObject, "Story_TextMask");
        RectTransform runningTextRect = FindRect(rootObject, "Story_RunningText");
        Image background = rootObject.transform.Find("Story_Background") != null
            ? rootObject.transform.Find("Story_Background").GetComponent<Image>()
            : rootObject.GetComponentInChildren<Image>(true);
        Image icon = rootObject.transform.Find("Story_SpeakerIcon") != null
            ? rootObject.transform.Find("Story_SpeakerIcon").GetComponent<Image>()
            : null;
        Animator[] animators = rootObject.GetComponentsInChildren<Animator>(true);

        return new NoticeView(
            rootObject,
            rootRect,
            group,
            runningText != null ? runningText : text,
            background,
            icon,
            maskRect,
            runningTextRect,
            animators);
    }

    GameObject FindNamedObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject direct = GameObject.Find(objectName);
        if (direct != null)
        {
            return direct;
        }

        Transform[] roots = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < roots.Length; i++)
        {
            Transform candidate = roots[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    TMP_Text FindText(GameObject rootObject, string objectName)
    {
        Transform target = FindChild(rootObject, objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    RectTransform FindRect(GameObject rootObject, string objectName)
    {
        Transform target = FindChild(rootObject, objectName);
        return target != null ? target as RectTransform : null;
    }

    string ReverseWordOrder(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        string[] lines = message.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] words = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            System.Array.Reverse(words);
            lines[i] = string.Join(" ", words);
        }

        return string.Join("\n", lines);
    }

    Transform FindChild(GameObject rootObject, string objectName)
    {
        if (rootObject == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform direct = rootObject.transform.Find(objectName);
        if (direct != null)
        {
            return direct;
        }

        foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    struct NoticeRequest
    {
        public string message;
        public NoticeKind kind;
        public int originAmount;
    }

    enum NoticeKind
    {
        Normal,
        Origin
    }

    struct NoticeView
    {
        public readonly GameObject root;
        public readonly RectTransform rect;
        public readonly CanvasGroup group;
        public readonly TMP_Text text;
        public readonly Image background;
        public readonly Image icon;
        public readonly RectTransform maskRect;
        public readonly RectTransform runningTextRect;
        public readonly Animator[] animators;

        public bool IsValid => root != null;

        public NoticeView(
            GameObject root,
            RectTransform rect,
            CanvasGroup group,
            TMP_Text text,
            Image background,
            Image icon,
            RectTransform maskRect,
            RectTransform runningTextRect,
            Animator[] animators)
        {
            this.root = root;
            this.rect = rect;
            this.group = group;
            this.text = text;
            this.background = background;
            this.icon = icon;
            this.maskRect = maskRect;
            this.runningTextRect = runningTextRect;
            this.animators = animators;
        }

        public void SetText(string value)
        {
            if (text != null)
            {
                text.text = value ?? "";
            }
        }

        public void SetRunningText(string value)
        {
            if (text != null)
            {
                text.text = value ?? "";
            }
        }

        public void SetAnimatorsEnabled(bool enabled)
        {
            if (animators == null)
            {
                return;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].enabled = enabled;
                }
            }
        }

        public void SetColors(Color textColor, Color accentColor)
        {
            if (text != null)
            {
                text.color = textColor;
            }

            if (background != null)
            {
                background.color = new Color(
                    accentColor.r,
                    accentColor.g,
                    accentColor.b,
                    background.color.a);
            }

            if (icon != null)
            {
                icon.color = accentColor;
            }
        }
    }
}
