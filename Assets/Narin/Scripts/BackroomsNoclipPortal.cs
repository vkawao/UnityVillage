using UnityEngine;

namespace NarinVillage
{
    /// <summary>
    /// Doorway trigger on House 5 exterior that noclips/teleports the player into the Backrooms (House 5, Action 2).
    /// </summary>
    public class BackroomsNoclipPortal : MonoBehaviour
    {
        private float _lastTeleportTime = -10f;

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - _lastTeleportTime < 3f) return;

            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponentInParent<Camera>() != null ||
                            other.name.Contains("XR") ||
                            other.name.Contains("Camera") ||
                            other.name.Contains("Character");

            if (isPlayer && VillageActionManager.Instance != null)
            {
                _lastTeleportTime = Time.time;
                VillageActionManager.Instance.CompleteAction(5, 2);
                VillageActionManager.Instance.TeleportToBackrooms();
            }
        }
    }
}
