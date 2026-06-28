using UnityEngine;
using UnityEngine.SceneManagement;

public class Button_Function : MonoBehaviour
{
    public string sceneName;
    public GameObject ObjectWhichActivity;
    public void Button_Scene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Button_Activity(GameObject ObjectWhichActivity)
    {
      ObjectWhichActivity.SetActive(!ObjectWhichActivity.activeSelf);
    }
}
