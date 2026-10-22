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

            // Background: stay near-white so the alchemical frame texture always shows.
            // Hover adds a very subtle warm tint; press flashes bright without hiding the frame.
            if (background != null)
            {
                var targetBg = pressed
                    ? new Color(1.38f, 1.28f, .82f, 1f)   // Bright warm flash on press
                    : IsFocused ? new Color(1.05f, 1.02f, .90f, 1f) : new Color(1f, 1f, 1f, 1f);
                background.color = Color.Lerp(background.color, targetBg, lerp);
            }

            // Glow overlay: uses the same 9-sliced frame sprite (set in scene builder) so the
            // gold halo follows the copper frame shape rather than flooding the button interior.
            if (glowOverlay != null)
            {
                var targetAlpha = pressed ? .72f : IsFocused ? .50f : 0f;
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
