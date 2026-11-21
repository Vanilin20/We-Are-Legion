using UnityEngine;
using UnityEngine.SceneManagement;

// Екран «Колонія загинула» з кнопкою «Нова гра».
// Вішається на панель (вимкнену за замовчуванням); кнопка викликає NewGame.
public class GameOverScreen : MonoBehaviour
{
    [Tooltip("Яку сцену завантажити для нової гри")]
    [SerializeField] private string gameScene = "SampleScene";

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void NewGame()
    {
        SceneManager.LoadScene(gameScene);
    }
}
