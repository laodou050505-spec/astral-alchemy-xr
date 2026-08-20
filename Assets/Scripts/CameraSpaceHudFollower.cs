using UnityEngine;

namespace AstralAlchemy
{
    public class CameraSpaceHudFollower : MonoBehaviour
    {
        public Camera targetCamera;
        // 2.2 m is far enough for comfortable stereo convergence in PICO while
        // preserving the same apparent HUD size as the previous 1.72 m / .0015
        // desktop authoring layout.  The canvas scale is calibrated by the scene
        // builder, so do not compensate for this value in child panel positions.
        [Min(.5f)] public float distance = 2.2f;
        public Vector3 localOffset = Vector3.zero;

        private void Awake() => AttachToCamera();
        private void OnEnable() => AttachToCamera();

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            if (transform.parent != targetCamera.transform) transform.SetParent(targetCamera.transform, false);
            transform.localPosition = new Vector3(localOffset.x, localOffset.y, distance + localOffset.z);
            transform.localRotation = Quaternion.identity;
        }

        private void AttachToCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;
            transform.SetParent(targetCamera.transform, false);
            transform.localPosition = new Vector3(localOffset.x, localOffset.y, distance + localOffset.z);
            transform.localRotation = Quaternion.identity;
        }
    }
}
