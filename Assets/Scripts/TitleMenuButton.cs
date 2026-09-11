using UnityEngine;
using UnityEngine.UI;

namespace AstralAlchemy
{
    [RequireComponent(typeof(BoxCollider))]
    public class TitleMenuButton : MonoBehaviour
    {
        public enum Command { Start, Help, Back, Quit, CancelQuit, ConfirmQuit }
        public Command command;
        public Image background;
        public Text label;
        public Color idleColor = new Color(.85f, .88f, .93f);
        public Color focusColor = new Color(1f, .82f, .43f);
        public bool IsFocused { get; private set; }
        private float pressUntil;
        public void SetFocused(bool value) => IsFocused = value;
        public void Press() => pressUntil = Time.unscaledTime + .16f;
        private void Update()
        {
            var pressed = Time.unscaledTime < pressUntil;
            var amount = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * (pressed ? .98f : IsFocused ? 1.025f : 1f), amount);
            if (label != null) label.color = Color.Lerp(label.color, pressed ? Color.white : IsFocused ? focusColor : idleColor, amount);
            if (background != null) background.color = Color.Lerp(background.color,
                new Color(.24f, .18f, .075f, pressed ? .8f : IsFocused ? .62f : .12f), amount);
        }
        private void OnDisable() { IsFocused = false; transform.localScale = Vector3.one; }
    }
}
