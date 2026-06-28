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
                titleText.text = "Trùng Tu Thiên Đạo";
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
        if (pageBodies != null && pageBodies.Length > 0)
        {
            return;
        }

        pageTitles = new string[]
        {
            " Thiên Đạo Hoàn Chỉnh",
            " Hạo Kiếp Diệt Giới",
            " Bản Nguyên Vỡ Nát",
            " Trùng Tu Thiên Đạo"
        };

        pageBodies = new string[]
        {
            "Thuở thiên địa còn nguyên vẹn, Hoang Cổ Đại Lục từng là một thế giới phồn thịnh.\n\nThiên Đạo bao phủ vạn vật, nhật nguyệt vận hành có thứ tự, linh khí luân chuyển không dứt.\n\nSinh linh sinh ra, tu luyện, tranh đấu, truyền thừa rồi hóa thành một phần của thiên địa.\n\nTông môn hưng thịnh, yêu thú tung hoành, linh thảo sinh trưởng khắp núi sông. Mỗi sinh mệnh đều có quỹ tích riêng, mỗi cơ duyên đều nằm trong đại đạo tuần hoàn.",

            "Nhưng vào một kỷ nguyên xa xưa, hư không bỗng rạn nứt.\n\nMột thế lực đến từ ngoài thiên địa xâm nhập Hoang Cổ Đại Lục.\n\nChúng không tuân theo Thiên Đạo, không nhập luân hồi, chỉ muốn nuốt lấy linh khí, bản nguyên và sinh cơ của cả thế giới.\n\nTrận hạo kiếp ấy khiến núi sông sụp đổ, tông môn tiêu vong, vô số sinh linh hóa thành tro bụi. Trật tự thiên địa bắt đầu tan rã.",

            "Để giữ lại một tia sinh cơ cuối cùng, Thiên Đạo đã cưỡng ép thiêu đốt bản nguyên của chính mình.\n\nSức mạnh ấy đánh lui ngoại địch, phong bế vết nứt hư không, cứu lấy phần còn sót lại của Hoang Cổ Đại Lục.\n\nNhưng cái giá phải trả quá lớn.\n\nBản nguyên Thiên Đạo vỡ nát, quyền năng thất lạc khắp nơi. Linh khí suy yếu, pháp tắc hỗn loạn, thế giới chỉ còn lại những vùng đất rời rạc đang tự chống chọi với thời gian.",

            "Hiện tại, một tia tàn ý của Thiên Đạo đã thức tỉnh.\n\nĐó chính là ngươi.\n\nNgươi chưa thể chưởng khống toàn bộ thế giới, chỉ có thể quan sát một vùng đất nhỏ bé nơi rìa Hoang Cổ Đại Lục.\n\nNhưng từ nơi này, Thiên Đạo có thể bắt đầu trùng tu.\n\nHãy dẫn dắt sinh linh, ban phát cơ duyên, thúc đẩy tu luyện, mở rộng vùng đất, khôi phục linh khí và từng bước thu hồi bản nguyên đã mất.\n\nKhi Thiên Đạo đủ mạnh, Hoang Cổ Đại Lục sẽ một lần nữa nghênh đón thời đại mới."
        };
    }
}
