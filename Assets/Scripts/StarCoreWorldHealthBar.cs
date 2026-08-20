using UnityEngine;
using UnityEngine.UI;

namespace AstralAlchemy
{
    public class StarCoreWorldHealthBar : MonoBehaviour
    {
        public Transform coreTarget;
        public StarCoreController core;
        public Image fill;
        public Image damageTrail;
        public Text valueLabel;
        public Vector3 worldOffset = new Vector3(0f, .74f, 0f);
        public bool stayUprightToCamera = true;
        public Color healthyColor = new Color(1f, .18f, .1f, 1f);
        public Color criticalColor = new Color(1f, .035f, .06f, 1f);
        private float displayedHealth = 1f;
        private float trailingHealth = 1f;
        private float lastHealth = 1f;
        private float damageFlash;
        private float trailDelay;

        private void LateUpdate()
        {
            if (coreTarget != null) transform.position = coreTarget.position + worldOffset;
            var camera = Camera.main;
            if (camera != null && stayUprightToCamera)
            {
                var direction = transform.position - camera.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
            if (core != null && fill != null)
            {
                var target = core.Health / Mathf.Max(.01f, core.maxHealth);
                if (target < lastHealth - .001f)
                {
                    damageFlash = 1f;
                    trailDelay = .28f;
                }
                lastHealth = target;
                displayedHealth = Mathf.MoveTowards(displayedHealth, target, Time.deltaTime * 2.6f);
                fill.fillAmount = displayedHealth;
                if (damageTrail != null)
                {
                    if (trailDelay > 0f) trailDelay -= Time.deltaTime;
                    else trailingHealth = Mathf.MoveTowards(trailingHealth, target, Time.deltaTime * .24f);
                    trailingHealth = Mathf.Max(trailingHealth, displayedHealth);
                    damageTrail.fillAmount = trailingHealth;
                }
                if (valueLabel != null)
                    valueLabel.text = Mathf.CeilToInt(core.Health) + " / " + Mathf.CeilToInt(core.maxHealth);
                damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.deltaTime * 2.8f);
                var healthColor = Color.Lerp(criticalColor, healthyColor, displayedHealth);
                fill.color = Color.Lerp(healthColor, Color.white, damageFlash * .72f);
                fill.transform.localScale = new Vector3(1f, 1f + Mathf.Sin(Time.time * 18f) * damageFlash * .16f, 1f);
            }
        }
    }
}
