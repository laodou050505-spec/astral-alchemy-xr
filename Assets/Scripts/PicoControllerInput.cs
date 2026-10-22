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

        // 蓄力过程中右手射线的基础宽度（米）。
        // 射线随蓄力程度从 1x 增宽到 2.4x，颜色从橙色渐变为金色，
        // 让玩家直观感受到蓄力进度。
        private const float BaseRayWidth = 0.006f;

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
            {
                var charge = Mathf.Clamp(Time.time - rightChargeStart, 0f, 1.55f);
                game.CastAtRay(new Ray(rightController.position, rightController.forward), charge);
                // 施法结束震动反馈由 ArenaGameController.CastAtRay 内部根据命中结果决定。
            }
            leftWasPressed = leftPressed;
            rightWasPressed = rightPressed;

            // ── 射线渲染（左手：蓝色；右手：根据蓄力进度从橙变金，线宽随之增加）──
            var chargeProgress = rightWasPressed
                ? Mathf.Clamp01((Time.time - rightChargeStart) / 1.55f)
                : 0f;
            var chargeRayColor = Color.Lerp(new Color(1f, .62f, .18f), new Color(1f, .94f, .42f), chargeProgress);
            var chargeWidthScale = 1f + chargeProgress * 1.45f;
            DrawRay(leftRay, leftController, new Color(.18f, .85f, 1f), leftDevice.isValid, 1f);
            DrawRay(rightRay, rightController, chargeRayColor, rightDevice.isValid, chargeWidthScale);
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

        /// <summary>
        /// 向左手和/或右手控制器发送触觉冲动（震动反馈）。
        /// 由 ArenaGameController 在关键游戏事件中调用。
        /// </summary>
        public void TriggerHaptic(bool leftHand, bool rightHand, float amplitude, float duration)
        {
            if (leftHand) SendHaptic(leftDevice, amplitude, duration);
            if (rightHand) SendHaptic(rightDevice, amplitude, duration);
        }

        private static void SendHaptic(InputDevice device, float amplitude, float duration)
        {
            if (!device.isValid) return;
            if (device.TryGetHapticCapabilities(out var cap) && cap.supportsImpulse)
                device.SendHapticImpulse(0, amplitude, duration);
        }

        private void DrawRay(LineRenderer line, Transform origin, Color color, bool deviceActive, float widthScale)
        {
            if (line == null || origin == null) return;
            line.enabled = deviceActive;
            if (!deviceActive) return;
            line.positionCount = 2;
            line.SetPosition(0, origin.position);
            line.SetPosition(1, origin.position + origin.forward * rayLength);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.startWidth = BaseRayWidth * widthScale;
            line.endWidth = 0f;
        }
    }
}
