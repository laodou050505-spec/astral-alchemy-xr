using UnityEngine;
using UnityEngine.InputSystem;

namespace AstralAlchemy
{
    public class DesktopPlayerController : MonoBehaviour
    {
        public Camera playerCamera;
        public DesignPlayerStart designStart;
        public float moveSpeed = 2.7f;
        public float lookSpeed = 1.7f;
        public LayerMask interactLayer = ~0;

        private float pitch;
        private ArenaGameController game;
        private float chargeStart;
        private bool charging;
        private ElementOrb focusedOrb;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            game = FindFirstObjectByType<ArenaGameController>();
        }

        private void Start()
        {
            if (designStart == null) designStart = GetComponent<DesignPlayerStart>();
            pitch = playerCamera != null ? NormalizePitch(playerCamera.transform.localEulerAngles.x) : 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static float NormalizePitch(float angle) => angle > 180f ? angle - 360f : angle;

        private void Update()
        {
            if (game != null && game.IsEnded)
            {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) game.Restart();
                return;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                transform.Rotate(0f, delta.x * lookSpeed * .08f, 0f);
                pitch = Mathf.Clamp(pitch - delta.y * lookSpeed * .08f, -55f, 55f);
                playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
            var move = Vector3.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed) move.x += 1f;
                if (Keyboard.current.sKey.isPressed) move.z -= 1f;
                if (Keyboard.current.wKey.isPressed) move.z += 1f;
                move.Normalize();
            }
            transform.position += transform.TransformDirection(move) * moveSpeed * Time.deltaTime;
            UpdateElementFocus();
            if ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)) ClickElement();
            var chargePressed = (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
            var chargeReleased = (Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame) || (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame);
            if (chargePressed) { charging = true; chargeStart = Time.time; }
            if (charging && chargeReleased) { Cast(Mathf.Clamp(Time.time - chargeStart, 0f, 1.55f)); charging = false; }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && game != null) game.RecenterDesktop();
        }

        private void ClickElement()
        {
            if (playerCamera == null) return;
            var ray = PointerRay();
            if (Physics.Raycast(ray, out var hit, 50f, interactLayer))
            {
                var orb = hit.collider.GetComponentInParent<ElementOrb>();
                if (orb != null) { orb.Select(); return; }
            }
            ElementOrb.FindBestTarget(ray)?.Select();
        }

        private Ray PointerRay()
        {
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null)
                return playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return playerCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        }

        private void UpdateElementFocus()
        {
            if (playerCamera == null) return;
            var next = ElementOrb.FindBestTarget(PointerRay());
            if (next == focusedOrb) return;
            if (focusedOrb != null) focusedOrb.SetFocused(false);
            focusedOrb = next;
            if (focusedOrb != null) focusedOrb.SetFocused(true);
        }

        private void Cast(float charge)
        {
            if (game == null || playerCamera == null) return;
            game.CastAtRay(playerCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0)), charge);
        }
    }
}
