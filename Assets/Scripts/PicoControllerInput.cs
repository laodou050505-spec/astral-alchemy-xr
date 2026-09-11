using UnityEngine;
using UnityEngine.XR;

namespace AstralAlchemy
{
    public class PicoControllerInput : MonoBehaviour
    {
        public Transform leftController;
        public Transform rightController;
        public LineRenderer leftRay;
        public LineRenderer rightRay;
        public float rayLength = 25f;

        private InputDevice leftDevice;
        private InputDevice rightDevice;
        private bool leftWasPressed;
        private bool rightWasPressed;
        private ArenaGameController game;
        private float rightChargeStart;
        private ElementOrb focusedOrb;

        private void Start()
        {
            game = FindFirstObjectByType<ArenaGameController>();
            RefreshDevices();
        }

        private void Update()
        {
            if (!leftDevice.isValid || !rightDevice.isValid) RefreshDevices();
            UpdatePose(leftDevice, leftController);
            UpdatePose(rightDevice, rightController);
            var leftPressed = ReadTrigger(leftDevice);
            var rightPressed = ReadTrigger(rightDevice);
            if (game != null && game.IsEnded && ((leftPressed && !leftWasPressed) || (rightPressed && !rightWasPressed)))
            {
                game.Restart();
                leftWasPressed = leftPressed;
                rightWasPressed = rightPressed;
                return;
            }
            UpdateElementFocus();
            if (leftPressed && !leftWasPressed) SelectElement();
            if (rightPressed && !rightWasPressed) rightChargeStart = Time.time;
            if (!rightPressed && rightWasPressed && game != null && rightController != null)
                game.CastAtRay(new Ray(rightController.position, rightController.forward), Mathf.Clamp(Time.time - rightChargeStart, 0f, 1.55f));
            leftWasPressed = leftPressed;
            rightWasPressed = rightPressed;
            DrawRay(leftRay, leftController, new Color(.18f, .85f, 1f), leftDevice.isValid);
            DrawRay(rightRay, rightController, new Color(1f, .62f, .18f), rightDevice.isValid);
        }

        private void RefreshDevices()
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private static void UpdatePose(InputDevice device, Transform target)
        {
            if (!device.isValid || target == null) return;
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out var position)) target.localPosition = position;
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation)) target.localRotation = rotation;
        }

        private static bool ReadTrigger(InputDevice device)
        {
            return device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out var pressed) && pressed;
        }

        private void SelectElement()
        {
            if (leftController == null) return;
            if (Physics.Raycast(leftController.position, leftController.forward, out var hit, rayLength))
            {
                var orb = hit.collider.GetComponentInParent<ElementOrb>();
                if (orb != null) { orb.Select(); return; }
            }
            ElementOrb.FindBestTarget(new Ray(leftController.position, leftController.forward), rayLength, 10f)?.Select();
        }

        private void UpdateElementFocus()
        {
            if (leftController == null) return;
            var next = ElementOrb.FindBestTarget(new Ray(leftController.position, leftController.forward), rayLength, 10f);
            if (next == focusedOrb) return;
            if (focusedOrb != null) focusedOrb.SetFocused(false);
            focusedOrb = next;
            if (focusedOrb != null) focusedOrb.SetFocused(true);
        }

        private void DrawRay(LineRenderer line, Transform origin, Color color, bool deviceActive)
        {
            if (line == null || origin == null) return;
            line.enabled = deviceActive;
            if (!deviceActive) return;
            line.positionCount = 2;
            line.SetPosition(0, origin.position);
            line.SetPosition(1, origin.position + origin.forward * rayLength);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
