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

        private ArenaGameController gameController;

        private void Awake()
        {
            Health = maxHealth;
        }

        private void Start()
        {
            // 缓存 ArenaGameController 引用，避免每帧 FindFirstObjectByType 的开销。
            gameController = FindFirstObjectByType<ArenaGameController>();
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

            if (gameController == null) gameController = FindFirstObjectByType<ArenaGameController>();
            if (gameController != null) gameController.RefreshHud();

            // 受伤视觉反馈：相机轻震 + 控制器震动（通过 ArenaGameController 路由）
            if (amount >= 3f)
            {
                CameraShaker.Shake(
                    Mathf.Clamp(0.010f + amount * 0.0015f, 0.010f, 0.038f),
                    0.20f);
                gameController?.NotifyCoreDamaged(amount);
            }

            if (Health <= 0f && gameController != null) gameController.EndGame(false);
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(maxHealth, Health + amount);
            if (gameController == null) gameController = FindFirstObjectByType<ArenaGameController>();
            if (gameController != null) gameController.RefreshHud();
        }
    }
}
