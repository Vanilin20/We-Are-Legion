using UnityEngine;

public class CollectButton : MonoBehaviour
{
    public static bool IsHeld { get; private set; }

    public void StartHold() => IsHeld = true;
    public void StopHold() => IsHeld = false;
}