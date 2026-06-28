using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private string myID;
    
    void Start()
    {
        if (PlayerPrefs.GetString("SpawnPoint") == myID)
        {
            // знаходимо гравця і переміщуємо сюди
            GameObject player = GameObject.FindWithTag("Player");
            player.transform.position = transform.position;
        }
    }
}