using UnityEngine;

namespace AstralAlchemy
{
    public class AstralFloatRotate : MonoBehaviour
    {
        [Min(0f)] public float floatAmplitude = .065f;
        [Min(.01f)] public float floatFrequency = .78f;
        [Tooltip("Local position sway amplitude. When zero, floatAmplitude keeps legacy vertical motion.")]
        public Vector3 positionSway;
        public float rotationSpeed = 60f;
        [Tooltip("Low-speed XYZ rotation; keeps the original authored orientation as the base.")]
        public Vector3 rotationAxisSpeed = new Vector3(0f, 1f, 0f);
        [Tooltip("When positive, completes this many degrees over rotationCycleSeconds before repeating.")]
        [Min(0f)] public float rotationDegreesPerCycle;
        [Min(0f)] public float rotationCycleSeconds;
        [Range(0f, .08f)] public float breatheAmount;
        public float phaseOffset;

        private Vector3 homePosition;
        private Vector3 homeScale;
        private Quaternion homeRotation;

        private void Awake() => CaptureHome();
        private void OnEnable() => CaptureHome();

        private void CaptureHome()
        {
            homePosition = transform.localPosition;
            homeScale = transform.localScale;
            homeRotation = transform.localRotation;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            var wave = Mathf.Sin((Time.time + phaseOffset) * floatFrequency * Mathf.PI * 2f);
            var sway = positionSway == Vector3.zero ? Vector3.up * floatAmplitude : positionSway;
            transform.localPosition = homePosition + sway * wave;
            if (rotationDegreesPerCycle > 0f && rotationCycleSeconds > .01f)
            {
                var axis = rotationAxisSpeed.sqrMagnitude > .0001f ? rotationAxisSpeed.normalized : Vector3.up;
                var angle = Mathf.Repeat(Time.time / rotationCycleSeconds * rotationDegreesPerCycle, rotationDegreesPerCycle);
                transform.localRotation = homeRotation * Quaternion.Euler(axis * angle);
            }
            else transform.localRotation = homeRotation * Quaternion.Euler(rotationAxisSpeed * (Time.time * rotationSpeed));
            transform.localScale = homeScale * (1f + wave * breatheAmount);
        }
    }
}
