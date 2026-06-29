using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private string myID;

    void Start()
    {
        if (PlayerPrefs.GetString("SpawnPoint") == myID)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = transform.position;
            }
        }
    }
}