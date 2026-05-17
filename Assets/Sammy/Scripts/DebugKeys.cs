using UnityEngine;
using UnityEngine.InputSystem;

public class DebugKey : MonoBehaviour
{
    [SerializeField] private PlayerResources playerResources;

    void Update()
    {
        if (Keyboard.current.nKey.wasPressedThisFrame)
            playerResources.AddGold(25, Resources.GOLD);

        if (Keyboard.current.mKey.wasPressedThisFrame)
            playerResources.DecreaseGold(25, Resources.GOLD);

        if (Keyboard.current.vKey.wasPressedThisFrame)
            playerResources.AddScrap(25, Resources.SCRAP);

        if (Keyboard.current.bKey.wasPressedThisFrame)
            playerResources.DecreaseScrap(25, Resources.SCRAP);
    }
}
