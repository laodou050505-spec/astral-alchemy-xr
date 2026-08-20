using UnityEngine;

namespace AstralAlchemy
{
    public class StarCoreController : MonoBehaviour
    {
        public float maxHealth = 100f;
        public Renderer coreRenderer;
        public Light coreLight;
        public Transform orbitRoot;
        public Transform coreAnchor;
        public float Health { get; private set; }

        private void Awake()
        {
            Health = maxHealth;
        }

        private void Update()
        {
            if (orbitRoot != null) orbitRoot.Rotate(0f, 42f * Time.deltaTime, 0f);
            if (coreRenderer != null && coreRenderer.sharedMaterial != null && coreRenderer.sharedMaterial.HasProperty("_EmissionColor"))
            {
                var c = Color.Lerp(new Color(.9f, .03f, .08f), new Color(1f, .68f, .12f), Health / maxHealth);
                coreRenderer.material.SetColor("_EmissionColor", c * (1.2f + Mathf.Sin(Time.time * 4f) * .25f));
            }
        }

        public Transform TargetTransform => coreAnchor != null ? coreAnchor : transform;

        public void Damage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            if (coreLight != null) coreLight.intensity = .45f + Health / maxHealth * 1.05f;
            var game = FindFirstObjectByType<ArenaGameController>();
            if (game != null) game.RefreshHud();
            if (Health <= 0f && game != null) game.EndGame(false);
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(maxHealth, Health + amount);
            var game = FindFirstObjectByType<ArenaGameController>();
            if (game != null) game.RefreshHud();
        }
    }
}
