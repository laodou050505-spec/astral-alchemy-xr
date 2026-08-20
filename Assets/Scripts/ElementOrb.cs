using UnityEngine;

namespace AstralAlchemy
{
    [RequireComponent(typeof(Collider))]
    public class ElementOrb : MonoBehaviour
    {
        public ElementKind element;
        public Renderer coreRenderer;
        public Renderer shellRenderer;
        public GameObject demandHalo;
        public float floatHeight = 0.16f;
        public float floatSpeed = 1.7f;
        [Range(0f, .8f)] public float gatherHorizontal = .18f;
        public float gatherForward = 1.15f;
        [HideInInspector] public float motionPhase;

        private Vector3 home;
        private bool available = true;
        private MaterialPropertyBlock propertyBlock;
        private float forcedVisibleUntil;
        private Vector3 homeScale;
        private bool focused;
        private float focusBlend;

        public bool IsAvailable => available && gameObject.activeInHierarchy;

        private void Awake()
        {
            home = transform.localPosition;
            homeScale = transform.localScale;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            var gathered = home + new Vector3(-home.x * gatherHorizontal, 0f, -gatherForward);
            transform.localPosition = gathered + Vector3.up * Mathf.Sin(Time.time * floatSpeed + motionPhase) * floatHeight;
            transform.Rotate(0f, 38f * Time.deltaTime, 0f, Space.Self);
            if (available)
            {
                focusBlend = Mathf.MoveTowards(focusBlend, focused ? 1f : 0f, Time.deltaTime * 8f);
                transform.localScale = homeScale * Mathf.Lerp(1f, 1.13f, focusBlend);
            }
        }

        public static ElementOrb FindBestTarget(Ray ray, float maxDistance = 50f, float aimAssistDegrees = 8.5f)
        {
            ElementOrb best = null;
            var bestScore = float.MaxValue;
            var cosineLimit = Mathf.Cos(aimAssistDegrees * Mathf.Deg2Rad);
            foreach (var orb in FindObjectsByType<ElementOrb>(FindObjectsSortMode.None))
            {
                if (orb == null || !orb.IsAvailable) continue;
                var toOrb = orb.transform.position - ray.origin;
                var distance = toOrb.magnitude;
                if (distance < .05f || distance > maxDistance) continue;
                var alignment = Vector3.Dot(ray.direction.normalized, toOrb / distance);
                if (alignment < cosineLimit) continue;
                var angularError = Mathf.Acos(Mathf.Clamp(alignment, -1f, 1f)) * Mathf.Rad2Deg;
                var score = angularError + distance * .018f;
                if (score < bestScore) { best = orb; bestScore = score; }
            }
            return best;
        }

        public void SetFocused(bool value)
        {
            focused = value && IsAvailable;
            if (shellRenderer == null) return;
            shellRenderer.GetPropertyBlock(propertyBlock);
            var color = ElementPalette.ColorOf(element);
            propertyBlock.SetColor("_EmissionColor", color * (focused ? 3.1f : 1.35f));
            shellRenderer.SetPropertyBlock(propertyBlock);
        }

        public void Select()
        {
            if (!available) return;
            var game = FindFirstObjectByType<ArenaGameController>();
            if (game != null) game.ReceiveElement(this);
        }

        public void SetDemand(bool demanded)
        {
            if (demandHalo != null) demandHalo.SetActive(demanded);
            if (coreRenderer == null) return;
            coreRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", demanded ? ElementPalette.ColorOf(element) * 5.2f : ElementPalette.ColorOf(element) * 2.35f);
            coreRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetAvailable(bool value)
        {
            available = value;
            gameObject.SetActive(value);
        }

        public void Supply(float highlightSeconds = 3.2f)
        {
            StopAllCoroutines();
            available = true;
            transform.localPosition = home;
            transform.localScale = homeScale * 1.18f;
            gameObject.SetActive(true);
            forcedVisibleUntil = Time.time + highlightSeconds;
            if (demandHalo != null) demandHalo.SetActive(true);
        }

        public void PulseToCauldron(Transform cauldron)
        {
            available = false;
            StartCoroutine(FlyTo(cauldron));
        }

        private System.Collections.IEnumerator FlyTo(Transform target)
        {
            var start = transform.position;
            var duration = .28f;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = time / duration;
                transform.position = Vector3.Lerp(start, target.position + Vector3.up * .35f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .8f;
                transform.localScale = homeScale * Mathf.Lerp(1f, .18f, t);
                yield return null;
            }
            transform.localScale = homeScale;
            gameObject.SetActive(false);
            yield return new WaitForSeconds(.14f);
            if (this != null)
            {
                transform.localPosition = home;
                gameObject.SetActive(true);
                available = true;
                if (demandHalo != null && Time.time > forcedVisibleUntil) demandHalo.SetActive(false);
            }
        }
    }
}
