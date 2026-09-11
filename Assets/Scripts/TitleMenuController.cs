using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace AstralAlchemy
{
    /// <summary>World-space menu: every action requires its own focused target.</summary>
    public class TitleMenuController : MonoBehaviour
    {
        public string gameplaySceneName = "AstralAlchemyArena";
        public DesignPlayerStart designStart;
        public Collider startTarget;
        public GameObject mainMenu, helpPage, quitPage;
        public TitleMenuButton[] buttons;
        public Transform leftController, rightController;
        public LineRenderer leftRay, rightRay;
        public bool IsLoading => loading;
        private bool loading, inputArmed, previousLeft, previousRight;
        private float titleReadyTime;
        private TitleMenuButton focused, keyboardSelection;
        private void Start()
        {
            if (designStart != null) designStart.ResetToDesignStart(false);
            titleReadyTime = Time.unscaledTime + 1.25f;
            ShowMain();
        }
        private void Update()
        {
            if (loading) return;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var xr = XRSettings.isDeviceActive;
            var leftTracked = xr && UpdatePose(left, leftController);
            var rightTracked = xr && UpdatePose(right, rightController);
            var leftHeld = leftTracked && ReadTrigger(left);
            var rightHeld = rightTracked && ReadTrigger(right);
            var leftHit = leftTracked ? ButtonAtRay(new Ray(leftController.position, leftController.forward)) : null;
            var rightHit = rightTracked ? ButtonAtRay(new Ray(rightController.position, rightController.forward)) : null;
            DrawRay(leftRay, leftController, leftTracked, leftHit);
            DrawRay(rightRay, rightController, rightTracked, rightHit);
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            var keyHeld = keyboard != null && (keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed);
            var mouseHeld = mouse != null && mouse.leftButton.isPressed;
            var pointerHit = xr ? rightHit ?? leftHit : mouse != null && Camera.main != null
                ? ButtonAtRay(Camera.main.ScreenPointToRay(mouse.position.ReadValue())) : null;
            if (keyboard != null && (keyboard.downArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
            {
                var visible = buttons.Where(b => b != null && b.gameObject.activeInHierarchy).ToArray();
                if (visible.Length > 0)
                {
                    var index = System.Array.IndexOf(visible, keyboardSelection);
                    keyboardSelection = visible[(index + (keyboard.downArrowKey.wasPressedThisFrame ? 1 : visible.Length - 1) + visible.Length) % visible.Length];
                }
            }
            if (!xr && mouse != null && mouse.delta.ReadValue().sqrMagnitude > .1f) keyboardSelection = null;
            SetFocus(xr ? pointerHit : keyboardSelection ?? pointerHit);
            if (!inputArmed)
            {
                inputArmed = Time.unscaledTime >= titleReadyTime && !mouseHeld && !keyHeld && !leftHeld && !rightHeld;
                previousLeft = leftHeld; previousRight = rightHeld;
                return;
            }
            // Each hand must point at the button it invokes; blank trigger presses do nothing.
            if (rightHeld && !previousRight && rightHit != null) { Activate(rightHit); Haptic(right); }
            else if (leftHeld && !previousLeft && leftHit != null) { Activate(leftHit); Haptic(left); }
            else if (!xr && mouse != null && mouse.leftButton.wasPressedThisFrame && pointerHit != null) Activate(pointerHit);
            else if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) Activate(focused);
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (helpPage.activeSelf || quitPage.activeSelf) ShowMain(); else ShowQuit();
            }
            previousLeft = leftHeld; previousRight = rightHeld;
        }
        public TitleMenuButton ButtonAtRay(Ray ray)
        {
            TitleMenuButton nearest = null;
            var distance = 30f;
            if (buttons == null) return null;
            foreach (var button in buttons)
                if (button != null && button.gameObject.activeInHierarchy && button.GetComponent<Collider>().Raycast(ray, out var hit, distance))
                { nearest = button; distance = hit.distance; }
            return nearest;
        }
        public bool ActivateAtRay(Ray ray)
        {
            var button = ButtonAtRay(ray);
            if (button == null || loading) return false;
            Activate(button);
            return true;
        }
        private void Activate(TitleMenuButton button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || loading) return;
            button.Press();
            switch (button.command)
            {
                case TitleMenuButton.Command.Start: StartGame(); break;
                case TitleMenuButton.Command.Help: ShowPage(helpPage); break;
                case TitleMenuButton.Command.Back:
                case TitleMenuButton.Command.CancelQuit: ShowMain(); break;
                case TitleMenuButton.Command.Quit: ShowQuit(); break;
                case TitleMenuButton.Command.ConfirmQuit: ExitGame(); break;
            }
        }
        public void ShowMain() => ShowPage(mainMenu);
        public void ShowQuit() => ShowPage(quitPage);
        private void ShowPage(GameObject page)
        {
            mainMenu.SetActive(page == mainMenu); helpPage.SetActive(page == helpPage); quitPage.SetActive(page == quitPage);
            SetFocus(null);
            keyboardSelection = buttons.FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy);
        }
        private void SetFocus(TitleMenuButton next)
        {
            if (focused == next) return;
            if (focused != null) focused.SetFocused(false);
            focused = next;
            if (focused != null) focused.SetFocused(true);
        }
        private static bool UpdatePose(UnityEngine.XR.InputDevice device, Transform target)
        {
            if (!device.isValid || target == null || !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position)
                || !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation)) return false;
            target.localPosition = position; target.localRotation = rotation; return true;
        }
        private static bool ReadTrigger(UnityEngine.XR.InputDevice device) => device.TryGetFeatureValue(CommonUsages.triggerButton, out var pressed) && pressed;
        private static void Haptic(UnityEngine.XR.InputDevice device)
        {
            if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse) device.SendHapticImpulse(0, .25f, .045f);
        }
        private static void DrawRay(LineRenderer line, Transform origin, bool tracked, TitleMenuButton hit)
        {
            if (line == null) return;
            line.enabled = tracked;
            if (!tracked) return;
            line.SetPosition(0, origin.position);
            line.SetPosition(1, origin.position + origin.forward * (hit != null ? Vector3.Distance(origin.position, hit.transform.position) : 12f));
            line.startColor = line.endColor = hit != null ? new Color(1f, .82f, .43f) : new Color(.3f, .7f, .85f, .55f);
        }
        public void StartGame()
        {
            if (loading || !mainMenu.activeSelf) return;
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
