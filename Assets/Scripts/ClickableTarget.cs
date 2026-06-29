using UnityEngine;

public class ClickableTarget : MonoBehaviour
{
    private CameraFollow cameraFollow;

    void Start()
    {
        cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void Update()
    {
        // Мобільний тап
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TrySelect(Input.GetTouch(0).position);
        }

        // ПК клік
        if (Input.GetMouseButtonDown(0))
        {
            TrySelect(Input.mousePosition);
        }
    }

    void TrySelect(Vector2 screenPos)
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            cameraFollow.SetTarget(transform);
        }
    }
}