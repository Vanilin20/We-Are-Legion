using UnityEngine;
using UnityEngine.Tilemaps;

public class AntDigging : MonoBehaviour
{
    public Tilemap groundTilemap;
    public float digTime = 5f;

    bool touchingWall;
    bool digButtonHeld;
    Vector3Int wallCell;
    float digTimer;

    void Start()
    {
        if (groundTilemap == null)
        {
            Debug.LogError("groundTilemap не призначено в інспекторі на AntDigging!");
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("На мурасі немає Collider2D!");
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("На мурасі немає Rigidbody2D!");
        }
        else if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning("Rigidbody2D мурахи не Dynamic, зараз: " + rb.bodyType);
        }
    }

    void OnCollisionStay2D(Collision2D col)
    {
        Tilemap tm = col.collider.GetComponent<Tilemap>();
        if (tm == null)
        {
            Debug.Log("зіткнення з чимось без Tilemap: " + col.collider.name);
            return;
        }

        if (tm != groundTilemap)
        {
            Debug.LogWarning("зіткнення з Tilemap, але не з тим, що призначений в groundTilemap. Знайдено: " + tm.name);
            return;
        }

        touchingWall = true;
        Vector2 contact = col.GetContact(0).point - col.GetContact(0).normal * 0.1f;
        wallCell = groundTilemap.WorldToCell(contact);
        Debug.Log("touching wall, cell = " + wallCell);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.collider.GetComponent<Tilemap>() == groundTilemap)
        {
            touchingWall = false;
            digTimer = 0f;
            Debug.Log("left wall");
        }
    }

    void Update()
    {
        if (touchingWall && digButtonHeld)
        {
            digTimer += Time.deltaTime;
            Debug.Log("digging... " + digTimer);
            if (digTimer >= digTime)
            {
                TileBase tileBefore = groundTilemap.GetTile(wallCell);
                if (tileBefore == null)
                {
                    Debug.LogWarning("в клітинці " + wallCell + " вже немає тайла до видалення");
                }
                groundTilemap.SetTile(wallCell, null);
                Debug.Log("tile removed at " + wallCell);
                digTimer = 0f;
                touchingWall = false;
            }
        }
        else
        {
            digTimer = 0f;
        }
    }

    public void OnDigButtonDown()
    {
        digButtonHeld = true;
        Debug.Log("button down");
    }

    public void OnDigButtonUp()
    {
        digButtonHeld = false;
        Debug.Log("button up");
    }
}