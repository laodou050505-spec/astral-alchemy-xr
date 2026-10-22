using UnityEngine;

namespace AstralAlchemy
{
    [RequireComponent(typeof(ElementOrb))]
    public class ElementOrbVisualFX : MonoBehaviour
    {
        public Renderer coreRenderer;
        public Renderer innerCoreRenderer;
        public Renderer shellRenderer;
        public float pulseSpeed = 2.1f;
        public float pulseAmount = .24f;

        private ElementOrb orb;
        private MaterialPropertyBlock block;
        private ParticleSystem motes;
        private float phase;
        private float nextVisualUpdate;

        private void Awake()
        {
            orb = GetComponent<ElementOrb>();
            block ??= new MaterialPropertyBlock();
            phase = (int)orb.element * .87f;
            ConfigureShell();
            if (motes == null) CreateMotes();
        }

        private void OnEnable()
        {
            orb ??= GetComponent<ElementOrb>();
            block ??= new MaterialPropertyBlock();
            if (motes != null) motes.Play(true);
        }

        private void Update()
        {
            if (orb == null || coreRenderer == null) return;
            // Twenty-four element bubbles do not need to rebuild two material
            // property blocks at 72 Hz. Staggered 30 Hz updates preserve the pulse
            // while reducing CPU/render-thread churn on standalone XR hardware.
            if (Time.unscaledTime < nextVisualUpdate) return;
            nextVisualUpdate = Time.unscaledTime + (1f / 30f);
            block ??= new MaterialPropertyBlock();
            var baseColor = ElementPalette.ColorOf(orb.element);
            var pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
            coreRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", baseColor * (3.6f * pulse));
            block.SetColor("_Color", baseColor);
            block.SetColor("_BaseColor", baseColor);
            coreRenderer.SetPropertyBlock(block);
            if (innerCoreRenderer != null)
            {
                var innerPulse = 1.08f + Mathf.Sin(Time.time * pulseSpeed * 1.17f + phase + 1.3f) * .18f;
                innerCoreRenderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", baseColor * (5.4f * innerPulse));
                block.SetColor("_Color", baseColor * 1.15f);
                block.SetColor("_BaseColor", baseColor * 1.15f);
                innerCoreRenderer.SetPropertyBlock(block);
            }
        }

        private void ConfigureShell()
        {
            if (shellRenderer == null) return;
            var color = ElementPalette.ColorOf(orb.element);
            shellRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(color.r, color.g, color.b, .055f));
            block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, .055f));
            block.SetColor("_EmissionColor", color * .8f);
            block.SetFloat("_Metallic", 0f);
            block.SetFloat("_Glossiness", .08f);
            block.SetFloat("_Smoothness", 0f);
            shellRenderer.SetPropertyBlock(block);
        }

        private void CreateMotes()
        {
            var go = new GameObject("ElementParticle_" + ElementPalette.DisplayName(orb.element));
            go.transform.SetParent(transform, false);
            motes = go.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.loop = true;
            main.duration = 2.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.025f, .11f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .058f);
            main.maxParticles = 8;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = ElementPalette.ColorOf(orb.element);
            var emission = motes.emission;
            emission.rateOverTime = orb.element == ElementKind.Void ? 3.2f : 5f;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .27f;
            var velocity = motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = orb.element == ElementKind.Storm ? .4f : .12f;
            velocity.radial = orb.element == ElementKind.Void ? -.06f : .03f;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = Resources.Load<Material>("M_SoftMagicParticle");
            motes.Play(true);
        }
    }
}
