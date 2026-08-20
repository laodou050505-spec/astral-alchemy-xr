using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace AstralAlchemy
{
    /// <summary>Dedicated startup stage. No gameplay scene object exists until Start is chosen.</summary>
    public class TitleMenuController : MonoBehaviour
    {
        public string gameplaySceneName = "AstralAlchemyArena";
        public DesignPlayerStart designStart;
        [Tooltip("World-space START target. Mouse input must hit this object; a window-focus click cannot skip the title stage.")]
        public Collider startTarget;
        private bool loading;
        private bool inputArmed;
        private bool previousXrPressed;
        private float titleReadyTime;

        private void Start()
        {
            if (designStart == null) designStart = FindFirstObjectByType<DesignPlayerStart>();
            if (designStart != null) designStart.ResetToDesignStart(false);
            // Some desktop/XR runtimes surface an initial controller state while
            // the player window comes to the foreground. Do not accept a start
            // action during the fade-in period.
            titleReadyTime = Time.unscaledTime + 1.25f;
        }

        private void Update()
        {
            if (loading) return;
            var keyboardStart = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
            var mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            // Mouse remains a useful desktop fallback, but only a deliberate ray hit
            // on the START panel is accepted after the neutral arming phase below.
            var mouseStart = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && IsMouseOverStart();
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            // PICO's desktop shim can return a persistent trigger state despite no
            // active HMD. Never let that state bypass the independent title scene.
            var xrPressed = XRSettings.isDeviceActive && (ReadTrigger(left) || ReadTrigger(right));
            var xrStart = xrPressed && !previousXrPressed;

            // Launching/focusing a standalone desktop player can arrive with a stale
            // pressed frame. Arm only after every start input has returned to neutral.
            // The next deliberate input is then the only one allowed to leave Title.
            if (!inputArmed)
            {
                if (Time.unscaledTime >= titleReadyTime && !mouseHeld && !keyboardStart && !xrPressed) inputArmed = true;
                previousXrPressed = xrPressed;
                return;
            }
            if (keyboardStart || mouseStart || xrStart) StartGame();
            previousXrPressed = xrPressed;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ExitGame();
        }

        private static bool ReadTrigger(UnityEngine.XR.InputDevice device) =>
            device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var pressed) && pressed;

        private bool IsMouseOverStart()
        {
            if (startTarget == null || Camera.main == null || Mouse.current == null) return false;
            var point = Mouse.current.position.ReadValue();
            var ray = Camera.main.ScreenPointToRay(point);
            return startTarget.Raycast(ray, out _, 30f);
        }

        public void StartGame()
        {
            if (loading) return;
            loading = true;
            SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
        }

        public void ExitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
