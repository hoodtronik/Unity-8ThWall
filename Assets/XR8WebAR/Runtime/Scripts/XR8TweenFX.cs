using UnityEngine;
using System.Collections;

#if DOTWEEN
using DG.Tweening;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8 Tween FX — DOTween-powered AR effects library.
    /// 
    /// Pre-built effects for common AR scenarios:
    ///   - Content reveal (scale + fade in when tracking found)
    ///   - Floating/bobbing animation
    ///   - Pulse/attention effects
    ///   - Smooth transitions between tracked states
    ///   - Stop-motion style animations
    ///   - UI text character animations
    /// 
    /// Usage:
    ///   Add to any AR content GameObject.
    ///   Call effects from XR8ImageTracker events or your own scripts.
    /// 
    /// Requires: DOTween (DG.Tweening namespace)
    /// If DOTween is not installed, falls back to coroutine-based animations.
    /// </summary>
    public class XR8TweenFX : MonoBehaviour
    {
        [Header("Reveal Effect")]
        [Tooltip("Duration of the reveal animation")]
        public float revealDuration = 0.6f;

        [Tooltip("Scale to punch to before settling (1.0 = no overshoot)")]
        public float revealOvershoot = 1.1f;

        [Tooltip("Start scale for reveal")]
        public float revealStartScale = 0f;

        [Header("Floating Effect")]
        [Tooltip("Floating amplitude (world units)")]
        public float floatAmplitude = 0.05f;

        [Tooltip("Floating speed (cycles per second)")]
        public float floatSpeed = 1f;

        [Tooltip("Auto-start floating on enable")]
        public bool autoFloat = false;

        [Header("Pulse Effect")]
        [Tooltip("Pulse scale multiplier")]
        public float pulseScale = 1.15f;

        [Tooltip("Pulse duration")]
        public float pulseDuration = 0.8f;

        [Header("Stop Motion")]
        [Tooltip("Simulated FPS for stop-motion style")]
        public int stopMotionFPS = 8;

        // Internal state
        private Vector3 _originalScale;
        private Vector3 _originalPosition;
        private bool _isRevealed = false;
        private Coroutine _floatCoroutine;
        private Coroutine _pulseCoroutine;

        private void Awake()
        {
            _originalScale = transform.localScale;
            _originalPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            if (autoFloat)
                StartFloat();
        }

        private void OnDisable()
        {
            StopAllEffects();
        }

        // =============================================
        // REVEAL EFFECT — Content appears when tracking found
        // =============================================

        /// <summary>
        /// Play reveal animation (scale up + optional overshoot).
        /// Call when tracking is found.
        /// </summary>
        public void Reveal()
        {
            if (_isRevealed) return;
            _isRevealed = true;

            #if DOTWEEN
            transform.localScale = Vector3.one * revealStartScale;
            transform.DOScale(_originalScale * revealOvershoot, revealDuration * 0.6f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    transform.DOScale(_originalScale, revealDuration * 0.4f)
                        .SetEase(Ease.InOutSine);
                });
            #else
            StartCoroutine(RevealCoroutine());
            #endif
        }

        /// <summary>
        /// Play hide animation (scale down + fade).
        /// Call when tracking is lost.
        /// </summary>
        public void Hide()
        {
            if (!_isRevealed) return;
            _isRevealed = false;

            #if DOTWEEN
            transform.DOScale(Vector3.zero, revealDuration * 0.5f)
                .SetEase(Ease.InBack);
            #else
            StartCoroutine(HideCoroutine());
            #endif
        }

        /// <summary>
        /// Toggle reveal/hide based on tracking state.
        /// </summary>
        public void SetTracked(bool tracked)
        {
            if (tracked) Reveal();
            else Hide();
        }

        // =============================================
        // FLOATING EFFECT — Gentle bobbing animation
        // =============================================

        /// <summary>
        /// Start floating/bobbing animation.
        /// </summary>
        public void StartFloat()
        {
            StopFloat();

            #if DOTWEEN
            transform.DOLocalMoveY(
                _originalPosition.y + floatAmplitude,
                1f / floatSpeed * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
            #else
            _floatCoroutine = StartCoroutine(FloatCoroutine());
            #endif
        }

        /// <summary>
        /// Stop floating animation.
        /// </summary>
        public void StopFloat()
        {
            #if DOTWEEN
            transform.DOKill();
            #endif

            if (_floatCoroutine != null)
            {
                StopCoroutine(_floatCoroutine);
                _floatCoroutine = null;
            }
            transform.localPosition = _originalPosition;
        }

        // =============================================
        // PULSE EFFECT — Attention-grabbing pulse
        // =============================================

        /// <summary>
        /// Play a single pulse animation.
        /// </summary>
        public void Pulse()
        {
            #if DOTWEEN
            transform.DOPunchScale(
                Vector3.one * (pulseScale - 1f),
                pulseDuration,
                vibrato: 1,
                elasticity: 0.5f);
            #else
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseCoroutine());
            #endif
        }

        /// <summary>
        /// Start continuous pulsing.
        /// </summary>
        public void StartPulsing()
        {
            #if DOTWEEN
            transform.DOScale(_originalScale * pulseScale, pulseDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
            #else
            _pulseCoroutine = StartCoroutine(ContinuousPulseCoroutine());
            #endif
        }

        /// <summary>
        /// Stop pulsing.
        /// </summary>
        public void StopPulsing()
        {
            #if DOTWEEN
            transform.DOKill();
            transform.localScale = _originalScale;
            #else
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
            transform.localScale = _originalScale;
            #endif
        }

        // =============================================
        // ROTATION — Smooth auto-rotation
        // =============================================

        /// <summary>
        /// Start smooth rotation (great for product displays).
        /// </summary>
        public void StartRotation(float degreesPerSecond = 30f, Vector3? axis = null)
        {
            Vector3 rotAxis = axis ?? Vector3.up;

            #if DOTWEEN
            transform.DORotate(rotAxis * 360f, 360f / degreesPerSecond, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1);
            #else
            StartCoroutine(RotateCoroutine(degreesPerSecond, rotAxis));
            #endif
        }

        // =============================================
        // LOOK AT CAMERA — Smooth billboard
        // =============================================

        /// <summary>
        /// Smoothly rotate to face the camera (billboard effect).
        /// </summary>
        public void SmoothLookAtCamera(float duration = 0.3f)
        {
            if (Camera.main == null) return;

            Vector3 lookDir = Camera.main.transform.position - transform.position;
            lookDir.y = 0; // Keep upright
            Quaternion targetRot = Quaternion.LookRotation(-lookDir);

            #if DOTWEEN
            transform.DORotateQuaternion(targetRot, duration)
                .SetEase(Ease.OutSine);
            #else
            StartCoroutine(SmoothLookCoroutine(targetRot, duration));
            #endif
        }

        // =============================================
        // STOP ALL
        // =============================================

        /// <summary>
        /// Stop all running effects and reset to original state.
        /// </summary>
        public void StopAllEffects()
        {
            #if DOTWEEN
            transform.DOKill();
            #endif

            StopAllCoroutines();
            _floatCoroutine = null;
            _pulseCoroutine = null;

            transform.localScale = _originalScale;
            transform.localPosition = _originalPosition;
        }

        // =============================================
        // COROUTINE FALLBACKS (when DOTween not available)
        // =============================================

        private IEnumerator RevealCoroutine()
        {
            transform.localScale = Vector3.one * revealStartScale;
            float elapsed = 0;

            while (elapsed < revealDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / revealDuration;
                // Overshoot curve
                float scale = t < 0.6f
                    ? Mathf.Lerp(revealStartScale, revealOvershoot, t / 0.6f)
                    : Mathf.Lerp(revealOvershoot, 1f, (t - 0.6f) / 0.4f);
                transform.localScale = _originalScale * scale;
                yield return null;
            }

            transform.localScale = _originalScale;
        }

        private IEnumerator HideCoroutine()
        {
            float elapsed = 0;
            float duration = revealDuration * 0.5f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                yield return null;
            }

            transform.localScale = Vector3.zero;
        }

        private IEnumerator FloatCoroutine()
        {
            while (true)
            {
                float y = _originalPosition.y + Mathf.Sin(Time.time * floatSpeed * Mathf.PI * 2f) * floatAmplitude;
                transform.localPosition = new Vector3(_originalPosition.x, y, _originalPosition.z);
                yield return null;
            }
        }

        private IEnumerator PulseCoroutine()
        {
            float elapsed = 0;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseDuration;
                float scale = 1f + (pulseScale - 1f) * Mathf.Sin(t * Mathf.PI);
                transform.localScale = _originalScale * scale;
                yield return null;
            }
            transform.localScale = _originalScale;
        }

        private IEnumerator ContinuousPulseCoroutine()
        {
            while (true)
            {
                float scale = 1f + (pulseScale - 1f) * (Mathf.Sin(Time.time * Mathf.PI * 2f / pulseDuration) * 0.5f + 0.5f);
                transform.localScale = _originalScale * scale;
                yield return null;
            }
        }

        private IEnumerator RotateCoroutine(float speed, Vector3 axis)
        {
            while (true)
            {
                transform.Rotate(axis, speed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator SmoothLookCoroutine(Quaternion target, float duration)
        {
            Quaternion start = transform.rotation;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, target, elapsed / duration);
                yield return null;
            }
            transform.rotation = target;
        }
    }
}
