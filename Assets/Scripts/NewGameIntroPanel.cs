using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NewGameIntroPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject introPanel;

    [Header("Texts")]
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text pageText;

    [Header("Buttons")]
    public Button backButton;
    public Button nextButton;
    public Button enterWorldButton;
    public Button closeButton;

    [Header("Scene")]
    public string firstGameScene = "PersistentScene";

    [Header("Typing Effect")]
    public bool useTypingEffect = true;
    public float charDelay = 0.035f;
    public float lineDelay = 0.25f;

    [Header("Intro Pages")]
    [TextArea(2, 5)]
    public string[] pageTitles;

    [TextArea(5, 12)]
    public string[] pageBodies;

    private int currentPage;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private string currentFullBodyText;

    private void Awake()
    {
        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(BackPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextPage);
        }

        if (enterWorldButton != null)
        {
            enterWorldButton.onClick.AddListener(EnterWorld);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideIntro);
        }

        ReloadLocalizedIntroContent();

        if (bodyText != null)
        {
            bodyText.text = "";
        }

        RefreshButtons();
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    public void ShowIntro()
    {
        ReloadLocalizedIntroContent();
        currentPage = 0;

        if (introPanel != null)
        {
            introPanel.SetActive(true);
        }

        RefreshPage();
    }

    public void HideIntro()
    {
        StopTyping();

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }
    }

    public void NextPage()
    {
        if (isTyping)
        {
            FinishTypingImmediately();
            return;
        }

        if (pageBodies == null || pageBodies.Length == 0)
        {
            return;
        }

        currentPage++;

        if (currentPage >= pageBodies.Length)
        {
            currentPage = pageBodies.Length - 1;
        }

        RefreshPage();
    }

    public void BackPage()
    {
        if (isTyping)
        {
            FinishTypingImmediately();
            return;
        }

        currentPage--;

        if (currentPage < 0)
        {
            currentPage = 0;
        }

        RefreshPage();
    }

    private void RefreshPage()
    {
        if (pageBodies == null || pageBodies.Length == 0)
        {
            return;
        }

        StopTyping();

        if (titleText != null)
        {
            titleText.text = GetLocalizedPageTitle();
        }

        currentFullBodyText = pageBodies[currentPage];

        if (bodyText != null)
        {
            if (useTypingEffect)
            {
                bodyText.text = "";
                typingCoroutine = StartCoroutine(TypeBodyText(currentFullBodyText));
            }
            else
            {
                bodyText.text = currentFullBodyText;
            }
        }

        ApplyLocalizedPageIndicator();
        RefreshButtons();
    }

    private IEnumerator TypeBodyText(string fullText)
    {
        isTyping = true;

        if (bodyText != null)
        {
            bodyText.text = "";
        }

        string[] lines = fullText.Split('\n');

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];

            for (int charIndex = 0; charIndex < line.Length; charIndex++)
            {
                if (bodyText != null)
                {
                    bodyText.text += line[charIndex];
                }

                yield return new WaitForSeconds(charDelay);
            }

            if (lineIndex < lines.Length - 1)
            {
                if (bodyText != null)
                {
                    bodyText.text += "\n";
                }

                yield return new WaitForSeconds(lineDelay);
            }
        }

        isTyping = false;
        typingCoroutine = null;

        RefreshButtons();
    }

    private void FinishTypingImmediately()
    {
        StopTyping();

        if (bodyText != null)
        {
            bodyText.text = currentFullBodyText;
        }

        RefreshButtons();
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
    }

    private void RefreshButtons()
    {
        if (pageBodies == null || pageBodies.Length == 0)
        {
            return;
        }

        bool isFirstPage = currentPage <= 0;
        bool isLastPage = currentPage >= pageBodies.Length - 1;

        if (backButton != null)
        {
            backButton.gameObject.SetActive(!isFirstPage);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(!isLastPage);
        }

        if (enterWorldButton != null)
        {
            enterWorldButton.gameObject.SetActive(isLastPage);
        }
    }

    private void EnterWorld()
    {
        if (isTyping)
        {
            FinishTypingImmediately();
            return;
        }

        MainMenuManager mainMenuManager =
            FindAnyObjectByType<MainMenuManager>();

        if (mainMenuManager != null)
        {
            mainMenuManager.StartNewGameNow();
            return;
        }

        PlayerPrefs.SetInt("ThienMenh_NewGame", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene(firstGameScene);
    }

    private void CreateDefaultIntroIfEmpty()
    {
        if (TryLoadIntroFromJson())
        {
            return;
        }

        if (pageBodies != null && pageBodies.Length > 0)
        {
            return;
        }

        pageTitles = UiText.Lines("intro", "defaultPageTitles");
        pageBodies = UiText.Lines("intro", "defaultPageBodies");
    }

    private void ReloadLocalizedIntroContent()
    {
        pageTitles = null;
        pageBodies = null;
        CreateDefaultIntroIfEmpty();
    }

    private void HandleLanguageChanged()
    {
        ReloadLocalizedIntroContent();

        if (introPanel != null && introPanel.activeSelf)
        {
            currentPage = Mathf.Clamp(
                currentPage,
                0,
                pageBodies != null && pageBodies.Length > 0
                    ? pageBodies.Length - 1
                    : 0);
            RefreshPage();
        }
    }

    private string GetLocalizedPageTitle()
    {
        if (pageTitles != null &&
            currentPage < pageTitles.Length &&
            !string.IsNullOrWhiteSpace(pageTitles[currentPage]))
        {
            return pageTitles[currentPage];
        }

        return UiText.Get("intro", "fallbackTitle");
    }

    private void ApplyLocalizedPageIndicator()
    {
        if (pageText == null ||
            pageBodies == null ||
            pageBodies.Length == 0)
        {
            return;
        }

        pageText.text = UiText.Format(
            "intro",
            "pageIndicatorFormat",
            currentPage + 1,
            pageBodies.Length);
    }

    private bool TryLoadIntroFromJson()
    {
        string[] loadedBodies =
            UiText.Lines("intro", "pageBodies");
        if (loadedBodies == null ||
            loadedBodies.Length == 0)
        {
            return false;
        }

        string[] loadedTitles =
            UiText.Lines("intro", "pageTitles");

        pageBodies = loadedBodies;
        pageTitles =
            loadedTitles != null &&
            loadedTitles.Length > 0
                ? loadedTitles
                : pageTitles;
        return true;
    }
}
