using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    private static Player instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
        if (cam != null)
        {
            cam.SetTarget(transform);
        }

        // Знаходимо джойстик у новій сцені
        Joystick joystick = FindObjectOfType<Joystick>();
        if (joystick != null)
        {
            GetComponent<AntMovement>().joystick = joystick;
            GetComponent<PlayerRotation>().joystick = joystick;
        }
    }
}