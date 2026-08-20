using UnityEngine;

namespace AstralAlchemy
{
    /// <summary>Gentle offset bob and head-like sway around the object's authored bottom pivot.</summary>
    public class BaseSway : MonoBehaviour
    {
        public float height = .08f;
        public float frequency = .42f;
        public float pitch = 1.35f;
        public float yaw = 1.85f;
        public float phase;

        private Vector3 homePosition;
        private Quaternion homeRotation;

        private void Awake() { homePosition = transform.localPosition; homeRotation = transform.localRotation; }
        private void OnEnable() { homePosition = transform.localPosition; homeRotation = transform.localRotation; }

        private void Update()
        {
            if (!Application.isPlaying) return;
            var t = Time.time * frequency + phase;
            transform.localPosition = homePosition + Vector3.up * Mathf.Sin(t) * height;
            transform.localRotation = homeRotation * Quaternion.Euler(Mathf.Sin(t * .91f) * pitch, Mathf.Sin(t * 1.17f) * yaw, Mathf.Sin(t * .73f) * pitch * .45f);
        }
    }
}
