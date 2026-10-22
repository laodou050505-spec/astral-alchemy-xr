using UnityEngine;
using UnityEngine.UI;

namespace AstralAlchemy
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(AudioSource))]
    public class TitleMenuButton : MonoBehaviour
    {
        public enum Command { Start, Help, Back, Quit, CancelQuit, ConfirmQuit }
        public Command command;
        public Image background;
        public Image glowOverlay;
        public Text label;
        public AudioClip clickClip;
        public AudioClip hoverClip;
        public Color idleColor  = new Color(1f, 1f, 1f, 1f);
        public Color focusColor = new Color(1.12f, 1.06f, .88f, 1f);
        public Color labelIdleColor  = new Color(.92f, .88f, .78f, 1f);
        public Color labelFocusColor = new Color(1f, .91f, .55f, 1f);

        private AudioSource audioSource;
        private float pressUntil;

        public bool IsFocused { get; private set; }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void SetFocused(bool value)
        {
            IsFocused = value;
        }

        public void Press()
        {
            pressUntil = Time.unscaledTime + .18f;
            if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip, .85f);
        }

        // Called by TitleMenuController when this button first gains focus;
        // a separate method avoids re-triggering the sound every frame.
        public void OnGainFocus()
        {
            if (audioSource != null && hoverClip != null) audioSource.PlayOneShot(hoverClip, .55f);
        }

        private void Update()
        {
            var pressed  = Time.unscaledTime < pressUntil;
            var lerp     = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);

            // Scale: 1.05x hover, 0.97x press, 1.0x idle
            var targetScale = pressed ? .97f : IsFocused ? 1.05f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, lerp);

            // Background tint (works even without a sprite)
            if (background != null)
            {
                var targetBg = pressed
                    ? new Color(1.4f, 1.2f, .8f, .95f)
                    : IsFocused ? new Color(1.08f, 1.02f, .84f, .78f) : new Color(.9f, .82f, .65f, .38f);
                background.color = Color.Lerp(background.color, targetBg, lerp);
            }

            // Glow overlay alpha
            if (glowOverlay != null)
            {
                var targetAlpha = pressed ? .55f : IsFocused ? .35f : 0f;
                var c = glowOverlay.color;
                c.a = Mathf.Lerp(c.a, targetAlpha, lerp);
                glowOverlay.color = c;
            }

            // Label color
            if (label != null)
            {
                var targetLabel = pressed ? Color.white : IsFocused ? labelFocusColor : labelIdleColor;
                label.color = Color.Lerp(label.color, targetLabel, lerp);
            }
        }

        private void OnDisable()
        {
            IsFocused = false;
            transform.localScale = Vector3.one;
        }
    }
}
