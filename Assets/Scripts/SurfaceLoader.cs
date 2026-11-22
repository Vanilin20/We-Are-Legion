using UnityEngine;
using UnityEngine.SceneManagement;

// Догружає сцену поверхні поруч із мурашником (адитивно), щоб обидві локації жили одночасно:
// робітник носить їжу з поверхні, навіть коли гравець дивиться в мурашник.
public class SurfaceLoader : MonoBehaviour
{
    [SerializeField] private string surfaceScene = "Outdoor";

    void Awake()
    {
        // Сцена вже є в списку (наприклад, відкрита поруч у редакторі) — навіть якщо ще вантажиться.
        // Перевіряти лише isLoaded мало: тоді догружалась друга копія поверхні.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == surfaceScene) return;

        SceneManager.LoadScene(surfaceScene, LoadSceneMode.Additive);
    }
}
