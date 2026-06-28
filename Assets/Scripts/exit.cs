using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionTrigger : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    [SerializeField] private string spawnPointID;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerPrefs.SetString("SpawnPoint", spawnPointID);
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}