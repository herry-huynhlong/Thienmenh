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

        CreateDefaultIntroIfEmpty();

        if (bodyText != null)
        {
            bodyText.text = "";
        }

        RefreshButtons();
    }

    public void ShowIntro()
    {
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
            if (pageTitles != null && currentPage < pageTitles.Length)
            {
                titleText.text = pageTitles[currentPage];
            }
            else
            {
                titleText.text = "Thiên Mệnh Chi Tử";
            }
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

        if (pageText != null)
        {
            pageText.text =
                (currentPage + 1).ToString() +
                " / " +
                pageBodies.Length.ToString();
        }

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
            FindFirstObjectByType<MainMenuManager>();

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
        if (pageBodies != null && pageBodies.Length > 0)
        {
            return;
        }

        pageTitles = new string[]
        {
            "Thiên Đạo Vỡ Nát",
            "Vùng Rìa Hoang Cổ Đại Lục",
            "Quyền Chưởng Khống",
            "Ban Phát Cơ Duyên",
            "Không Có Đường Lui"
        };

        pageBodies = new string[]
        {
            "Chào mừng ngươi đến với Hoang Cổ Đại Lục.\n\nĐây từng là một thế giới hoàn chỉnh, nơi Thiên Đạo bao phủ vạn vật, sinh linh sinh ra, tu luyện, tranh đấu rồi hóa thành bụi đất.\n\nNhưng hiện tại, Thiên Đạo đã không còn nguyên vẹn.\nThần thức của ngươi chỉ còn có thể bao phủ một vùng rìa nhỏ bé của đại lục này.",

            "Trong vùng đất còn nằm dưới ánh nhìn của ngươi, vạn vật vẫn đang tự vận hành.\n\nNgươi có thể nhìn thấy tu sĩ, yêu thú, bảo vật, linh thảo, cây cối và những sinh linh đang cố gắng sinh tồn.\n\nMỗi ngày trôi qua, sẽ có người trưởng thành, có kẻ đột phá, có người ngã xuống trong rừng sâu, cũng có đời sau tiếp tục sinh ra.",

            "Ngươi không phải một phàm nhân bước vào thế giới này.\n\nNgươi là ý chí còn sót lại của Thiên Đạo.\nNgươi có thể quan sát vận mệnh của từng sinh linh, nhìn bọn họ tranh đoạt cơ duyên, kết thù, kết bạn, tu luyện, săn giết và chết đi.\n\nNhưng quyền chưởng khống của ngươi hiện tại vẫn còn rất yếu.",

            "Để khôi phục Thiên Đạo đã vỡ nát, ngươi cần từng bước mở rộng quyền năng của mình.\n\nNgươi có thể ban phát bảo vật cho một tu sĩ mà ngươi để mắt tới, cũng có thể trao cơ duyên cho yêu thú, hoặc thả bảo vật xuống một khu vực bất kỳ.\n\nNếu món bảo vật đủ hấp dẫn, nó có thể khiến cả vùng đất nổi lên tranh đoạt, thậm chí dẫn đến một trận đại chiến.",

            "Hãy nhớ kỹ.\n\nMột khi thế giới này bắt đầu vận hành, vận mệnh sẽ không còn quay đầu.\nSinh linh đã chết sẽ không thể sống lại.\nCơ duyên đã rơi xuống sẽ tạo ra nhân quả.\nMỗi lựa chọn của ngươi đều có thể thay đổi tương lai của Hoang Cổ Đại Lục.\n\nChúc may mắn, Thiên Đạo Chi Chủ."
        };
    }
}
