using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class CharacterCreateManager : MonoBehaviour
{
    public TMP_InputField nameInput;

    public GameObject inputFieldObject;
    public GameObject confirmButton;

    public GameObject maleButton;
    public GameObject femaleButton;

    public Image backgroundImage;

    public Sprite defaultBG;
    public Sprite maleBG;
    public Sprite femaleBG;

    public string selectedGender;

    public string firstGameScene = "PersistentScene";

    public void SelectMale()
    {
        selectedGender = "Male";

        // đổi ảnh
        backgroundImage.sprite = maleBG;

        ShowNameInput();
    }

    public void SelectFemale()
    {
        selectedGender = "Female";

        // đổi ảnh
        backgroundImage.sprite = femaleBG;

        ShowNameInput();
    }

    void ShowNameInput()
    {
        inputFieldObject.SetActive(true);
        confirmButton.SetActive(true);
    }

    public void ConfirmName()
    {
        string playerName = nameInput.text;

        if (playerName == "")
        {
            playerName = "Vô Danh";
        }

        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetString("Gender", selectedGender);

        PlayerPrefs.Save();

        SceneManager.LoadScene(firstGameScene);
    }
}