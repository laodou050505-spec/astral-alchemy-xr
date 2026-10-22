using System.Collections;
using UnityEngine;

namespace AstralAlchemy
{
    /// <summary>
    /// 轻量级相机震动工具。在任何游戏事件中调用 <see cref="Shake"/> 即可向 Camera.main
    /// 施加短促的位移冲击感。
    /// 在 XR 中，相机通常是追踪装置的子对象；震动偏移作用于局部位置，
    /// 结束后完全还原，不影响 XR 追踪。
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        private static CameraShaker _instance;
        private const bool AllowCameraShakeInXR = false;

        private Vector3 _originLocalPos;
        private Coroutine _active;

        private void Awake()
        {
            _instance = this;
            _originLocalPos = transform.localPosition;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 触发相机震动。若场景相机上尚未挂载 CameraShaker，会自动附加。
        /// </summary>
        /// <param name="magnitude">最大位移幅度（米）。</param>
        /// <param name="duration">震动持续时间（秒）。</param>
        public static void Shake(float magnitude = 0.022f, float duration = 0.20f)
        {
            // 总控审查：VR 中相机位姿由头部追踪每帧写入，脚本晃动相机会与追踪冲突并引起晕动，
            // 属于舒适度红线。保留接口但不执行；受伤反馈由手柄震动（TriggerHaptic）与星核闪光承担。
            if (!AllowCameraShakeInXR) return;
            EnsureInstance();
            if (_instance == null) return;
            _instance.DoShake(magnitude, duration);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var cam = Camera.main;
            if (cam == null) return;
            _instance = cam.gameObject.GetComponent<CameraShaker>();
            if (_instance == null)
            {
                _instance = cam.gameObject.AddComponent<CameraShaker>();
                _instance._originLocalPos = _instance.transform.localPosition;
            }
        }

        private void DoShake(float magnitude, float duration)
        {
            if (_active != null) StopCoroutine(_active);
            _active = StartCoroutine(ShakeRoutine(magnitude, duration));
        }

        private IEnumerator ShakeRoutine(float magnitude, float duration)
        {
            var origin = _originLocalPos;
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var decay = 1f - t / duration;
                // 水平偏移减半——XR 下细微的位移比大幅度震动更有沉浸感；
                // 向下偏移更能传递"冲击感"。
                var offset = new Vector3(
                    Random.Range(-1f, 1f) * magnitude * decay * 0.50f,
                    Random.Range(-1f, 0f) * magnitude * decay * 0.75f,
                    0f);
                transform.localPosition = origin + offset;
                yield return null;
            }
            transform.localPosition = origin;
            _active = null;
        }
    }
}
