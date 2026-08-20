using UnityEngine;

namespace AstralAlchemy
{
    public class StarTwinkle : MonoBehaviour
    {
        public Renderer starRenderer;
        public Color color = Color.white;
        public float minIntensity = .38f;
        public float maxIntensity = 2.4f;
        public float speed = 1.4f;
        public float phase;
        [Range(0f, 1f)] public float scalePulse = .22f;
        private MaterialPropertyBlock block;
        private Vector3 homeScale;

        private void Awake()
        {
            InitializeRuntimeState();
        }

        private void OnEnable() => InitializeRuntimeState();

        private void InitializeRuntimeState()
        {
            if (starRenderer == null) starRenderer = GetComponent<Renderer>();
            block ??= new MaterialPropertyBlock();
            if (homeScale == Vector3.zero) homeScale = transform.localScale;
        }

        private void Update()
        {
            if (starRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            var wave = Mathf.Pow(Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.time * speed + phase)), 2.1f);
            starRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", color * Mathf.Lerp(minIntensity, maxIntensity, wave));
            starRenderer.SetPropertyBlock(block);
            transform.localScale = homeScale * Mathf.Lerp(1f - scalePulse, 1f + scalePulse, wave);
        }
    }
}
