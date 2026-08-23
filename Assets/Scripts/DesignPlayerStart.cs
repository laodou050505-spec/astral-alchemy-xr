using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AstralAlchemy
{
    /// <summary>
    /// Single source of truth for the opening composition.  The desktop camera
    /// and the tracked PICO head both resolve to the same world-space eye pose.
    /// </summary>
    public class DesignPlayerStart : MonoBehaviour
    {
        [Header("Authoritative design start")]
        // Gameplay opens close enough to read the cauldron and the floating
        // star core, instead of from the former distant arena overview.
        public Vector3 designEyePosition = new Vector3(0f, 2.65f, -4.7f);
        [Range(-180f, 180f)] public float designYaw = 0f;
        [Tooltip("First gameplay landmark the player faces at launch.")]
        public Transform firstLandmark;
        public Camera eyeCamera;

        private Coroutine pendingPose;

        private void Awake()
        {
            if (eyeCamera == null) eyeCamera = Camera.main;
        }

        private void Start() => ResetToDesignStart(false);

        public void ResetToDesignStart(bool requestTrackingRecenter = true)
        {
            if (requestTrackingRecenter)
            {
                var subsystems = new List<XRInputSubsystem>();
                SubsystemManager.GetInstances(subsystems);
                foreach (var subsystem in subsystems)
                    if (subsystem.running) subsystem.TryRecenter();
            }

            if (pendingPose != null) StopCoroutine(pendingPose);
            pendingPose = StartCoroutine(ApplyAfterTrackingUpdate());
        }

        private IEnumerator ApplyAfterTrackingUpdate()
        {
            // One frame lets the XR provider apply its local tracked pose first.
            yield return null;
            if (eyeCamera == null) eyeCamera = Camera.main;
            var yaw = Quaternion.Euler(0f, designYaw, 0f);
            transform.rotation = yaw;
            var cameraOffset = eyeCamera != null ? eyeCamera.transform.localPosition : Vector3.zero;
            transform.position = designEyePosition - yaw * cameraOffset;

            // With no XR device the authoring camera retains the exact, shared eye height.
            if (!XRSettings.isDeviceActive && eyeCamera != null)
                eyeCamera.transform.localPosition = new Vector3(0f, designEyePosition.y, 0f);
        }

        public string StartDescription => $"Eye {designEyePosition:F2}, yaw {designYaw:F0}°, landmark {(firstLandmark != null ? firstLandmark.name : "Cauldron")}";
    }
}
