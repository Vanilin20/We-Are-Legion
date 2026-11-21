using UnityEngine;
using UnityEngine.SceneManagement;

// Догружає сцену поверхні поруч із мурашником (адитивно), щоб обидві локації жили одночасно:
// робітник носить їжу з поверхні, навіть коли гравець дивиться в мурашник.
public class SurfaceLoader : MonoBehaviour
{
    [SerializeField] private string surfaceScene = "Outdoor";

    void Awake()
    {
        if (!SceneManager.GetSceneByName(surfaceScene).isLoaded)
            SceneManager.LoadScene(surfaceScene, LoadSceneMode.Additive);
    }
}
