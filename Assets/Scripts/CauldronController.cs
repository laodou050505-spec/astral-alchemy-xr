using System.Collections;
using UnityEngine;

namespace AstralAlchemy
{
    public class CauldronController : MonoBehaviour
    {
        public Transform visualRoot;
        public Renderer liquidRenderer;
        public ParticleSystem correctBurst;
        public ParticleSystem rampageSteam;
        public Light cauldronLight;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Coroutine rampageRoutine;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            homePosition = visualRoot.localPosition;
            homeRotation = visualRoot.localRotation;
        }

        public void Correct(ElementKind a, ElementKind b)
        {
            StopRampage();
            var color = Color.Lerp(ElementPalette.ColorOf(a), ElementPalette.ColorOf(b), .5f);
            SetLiquid(color, 3.3f);
            if (correctBurst != null)
            {
                var main = correctBurst.main;
                main.startColor = color;
                correctBurst.Play(true);
            }
            StartCoroutine(SettleLiquid());
        }

        public void Rampage(ElementKind a, ElementKind b)
        {
            StopRampage();
            rampageRoutine = StartCoroutine(RampageDance(a, b));
        }

        public void StopRampage()
        {
            if (rampageRoutine != null) StopCoroutine(rampageRoutine);
            rampageRoutine = null;
            if (rampageSteam != null) rampageSteam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visualRoot != null)
            {
                visualRoot.localPosition = homePosition;
                visualRoot.localRotation = homeRotation;
            }
        }

        private IEnumerator RampageDance(ElementKind a, ElementKind b)
        {
            var color = Color.Lerp(ElementPalette.ColorOf(a), ElementPalette.ColorOf(b), .5f);
            SetLiquid(color, 5f);
            if (rampageSteam != null)
            {
                var main = rampageSteam.main;
                main.startColor = color;
                rampageSteam.Play(true);
            }
            var duration = 3.4f;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var beat = time * 13f;
                var side = Mathf.Sin(beat) * .21f;
                var jump = Mathf.Abs(Mathf.Sin(beat * .5f)) * .26f;
                visualRoot.localPosition = homePosition + new Vector3(side, jump, 0f);
                visualRoot.localRotation = homeRotation * Quaternion.Euler(0f, 0f, -side * 65f);
                yield return null;
            }
            StopRampage();
            yield return SettleLiquid();
        }

        private IEnumerator SettleLiquid()
        {
            yield return new WaitForSeconds(1.2f);
            SetLiquid(new Color(.38f, .08f, .7f), 1.5f);
        }

        private void SetLiquid(Color color, float intensity)
        {
            if (liquidRenderer != null)
            {
                liquidRenderer.material.color = color;
                liquidRenderer.material.SetColor("_EmissionColor", color * intensity);
            }
            if (cauldronLight != null)
            {
                cauldronLight.color = color;
                cauldronLight.intensity = Mathf.Clamp(intensity * .34f, .18f, 1.35f);
            }
        }
    }
}
