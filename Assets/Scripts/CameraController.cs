using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Ціль")]
    [SerializeField] private Transform target;

    [Header("Слідування")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);
    [SerializeField] private float followSpeed = 5f;

    [Header("Ручне керування")]
    [SerializeField] private float dragSensitivity = 0.05f;
    [SerializeField] private float returnDelay = 1.5f;
    [SerializeField] private float returnSpeed = 3f;

    [Header("Зона джойстика")]
    [SerializeField] private float joystickZonePercent = 0.5f;

    private Vector3 manualOffset = Vector3.zero;
    private float lastDragTime = -999f;
    private int cameraTouchId = -1;
    private bool isDragging = false;

    void LateUpdate()
    {
        HandleDrag();

        if (!isDragging && Time.time - lastDragTime > returnDelay)
        {
            manualOffset = Vector3.Lerp(manualOffset, Vector3.zero, returnSpeed * Time.deltaTime);
        }

        if (target != null)
        {
            Vector3 desired = target.position + offset + manualOffset;
            transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
        }
    }

    void HandleDrag()
    {
        Vector2 delta = Vector2.zero;
        isDragging = false;

        if (Input.touchCount > 0)
        {
            foreach (Touch t in Input.touches)
            {
                if (t.fingerId == cameraTouchId)
                {
                    if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                    {
                        delta = t.deltaPosition * dragSensitivity;
                        isDragging = true;
                        lastDragTime = Time.time;
                    }

                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        cameraTouchId = -1;
                        lastDragTime = Time.time;
                    }

                    break;
                }

                if (t.phase == TouchPhase.Began && cameraTouchId == -1)
                {
                    bool isJoystickZone = t.position.x < Screen.width * joystickZonePercent;
                    bool joystickActive = FixedJoystick.IsActive; // ✅ нова перевірка

                    if (!isJoystickZone && !joystickActive)
                    {
                        cameraTouchId = t.fingerId;
                    }
                }
            }
        }
        
        else
        {
            cameraTouchId = -1;

            if (Input.GetMouseButton(0))
            {
                bool isJoystick = Input.mousePosition.x < Screen.width * joystickZonePercent;
                if (!isJoystick)
                {
                    delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * dragSensitivity * 10f;
                    isDragging = true;
                    lastDragTime = Time.time;
                }
            }

            if (Input.GetMouseButtonUp(0))
                lastDragTime = Time.time;
        }

        if (delta != Vector2.zero)
        {
            manualOffset += new Vector3(-delta.x, -delta.y, 0);
        }
    }
}