using UnityEngine;

namespace AstralAlchemy
{
    /// <summary>Short-lived elemental bolt used by every completed recipe.</summary>
    public class ElementSpellProjectile : MonoBehaviour
    {
        public Vector3 destination;
        public Color color;
        public ElementKind primary;
        public ElementKind secondary;
        public float duration = .24f;
        public ParticleSystem trail;

        private Vector3 origin;
        private float elapsed;

        public void Launch(Vector3 from, Vector3 to, ElementKind a, ElementKind b)
        {
            origin = from;
            destination = to;
            primary = a;
            secondary = b;
            color = Color.Lerp(ElementPalette.ColorOf(a), ElementPalette.ColorOf(b), .38f);
            transform.position = from;
            transform.localScale = Vector3.one * .17f;
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                if (shader != null)
                {
                    var material = new Material(shader);
                    material.EnableKeyword("_EMISSION");
                    material.color = color;
                    material.SetColor("_EmissionColor", color * 5.2f);
                    material.SetFloat("_Metallic", .05f);
                    material.SetFloat("_Glossiness", .25f);
                    renderer.material = material;
                }
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color * 5.2f);
                renderer.SetPropertyBlock(block);
            }
            var light = GetComponent<Light>();
            if (light == null) light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.6f;
            light.intensity = 1.1f;
            light.color = color;
            trail = CreateTrail();
        }

        private ParticleSystem CreateTrail()
        {
            var go = new GameObject("ElementSpellTrail");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .26f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .1f);
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            var emission = ps.emission; emission.rateOverTime = primary == ElementKind.Storm ? 125f : 85f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .08f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = primary == ElementKind.Void ? -.35f : -.09f;
            velocity.orbitalY = primary == ElementKind.Storm ? 3f : 1.1f;
            var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = Resources.Load<Material>("M_SoftMagicParticle");
            ps.Play(true);
            return ps;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(origin, destination, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .18f;
            transform.localScale = Vector3.one * Mathf.Lerp(.16f, .3f, Mathf.Sin(t * Mathf.PI));
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
