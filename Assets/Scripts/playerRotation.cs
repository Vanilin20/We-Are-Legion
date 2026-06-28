using UnityEngine;

public class PlayerRotation : MonoBehaviour
{
    // Перетягни сюди компонент джойстика з пакету
    public Joystick joystick; // Fixed/Floating/Dynamic Joystick

    void Update()
    {
        float horizontal = joystick.Horizontal;
        float vertical = joystick.Vertical;

        // Перевіряємо чи є рух (щоб не скидати поворот коли відпустили)
        if (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f)
        {
            float angle = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            // -90f бо Unity рахує кут від правої осі, а спрайт зазвичай дивиться вгору
        }
    }
}