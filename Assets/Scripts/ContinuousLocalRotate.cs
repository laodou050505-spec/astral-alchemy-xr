using UnityEngine;

namespace AstralAlchemy
{
    public class ContinuousLocalRotate : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 72f;

        private void Update()
        {
            if (!Application.isPlaying) return;
            transform.Rotate(axis.normalized, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
