using UnityEngine;
using UnityEngine.EventSystems;

public class FixedJoystick : Joystick
{
    public static bool IsActive = false;

    public override void OnPointerDown(PointerEventData eventData)
    {
        IsActive = true;
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        IsActive = false;
        base.OnPointerUp(eventData);
    }
}