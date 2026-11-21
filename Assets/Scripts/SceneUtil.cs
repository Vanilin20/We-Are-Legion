using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneUtil
{
    // Об'єкти, створені кодом, потрапляють в активну сцену. Переносимо в сцену потрібної локації.
    public static void MoveToSceneOf(GameObject go, GameObject owner)
    {
        if (owner != null && go.scene != owner.scene)
            SceneManager.MoveGameObjectToScene(go, owner.scene);
    }
}
