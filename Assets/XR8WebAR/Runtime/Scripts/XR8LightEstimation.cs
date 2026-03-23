using UnityEngine;
using System;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8 Light Estimation — Auto-adjusts scene lighting from 8th Wall camera analysis.
    ///
    /// Features:
    ///   - Auto-rotate directional light to match estimated real-world light direction
    ///   - Auto-set ambient intensity from camera feed brightness
    ///   - Auto-set ambient color temperature from camera warmth
    ///   - Smooth transitions to prevent flickering
    ///
    /// Usage:
    ///   1. Add to any GameObject in scene
    ///   2. Assign your main Directional Light to 'sceneLight'
    ///   3. Component auto-updates lighting each frame from 8th Wall data
    ///
    /// Works with: XR8LightEstimationLib.jslib
    /// </summary>
    public class XR8LightEstimation : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebGLStartLightEstimation(string goName);
        [DllImport("__Internal")] private static extern void WebGLStopLightEstimation();
#endif

        [Header("Light References")]
        [Tooltip("Main directional light to adjust. Auto-finds one if not set.")]
        public Light sceneLight;

        [Header("Features")]
        [Tooltip("Auto-adjust light intensity from camera brightness")]
        public bool adjustIntensity = true;

        [Tooltip("Auto-adjust light color from camera color temperature")]
        public bool adjustColorTemperature = true;

        [Tooltip("Auto-rotate light to match estimated direction")]
        public bool adjustDirection = true;

        [Tooltip("Auto-adjust ambient light intensity")]
        public bool adjustAmbient = true;

        [Header("Tuning")]
        [Tooltip("How fast lighting adapts (higher = faster, more flickery)")]
        [Range(0.5f, 15f)]
        public float adaptSpeed = 3f;

        [Tooltip("Intensity multiplier")]
        [Range(0.1f, 3f)]
        public float intensityMultiplier = 1f;

        [Tooltip("Minimum light intensity")]
        [Range(0f, 1f)]
        public float minIntensity = 0.2f;

        [Tooltip("Maximum light intensity")]
        [Range(0.5f, 3f)]
        public float maxIntensity = 2f;

        // Current estimated values
        private float _targetIntensity = 1f;
        private Color _targetColor = Color.white;
        private Vector3 _targetDirection = new Vector3(50f, -30f, 0f);
        private float _targetAmbient = 0.5f;

        // Events
        public event Action<float> OnIntensityEstimated;
        public event Action<Color> OnColorEstimated;
        public event Action<Vector3> OnDirectionEstimated;

        private void Start()
        {
            if (sceneLight == null)
            {
                sceneLight = FindFirstObjectByType<Light>();
                if (sceneLight != null && sceneLight.type != LightType.Directional)
                    sceneLight = null;
            }

            if (sceneLight != null)
            {
                _targetIntensity = sceneLight.intensity;
                _targetColor = sceneLight.color;
                _targetDirection = sceneLight.transform.eulerAngles;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStartLightEstimation(gameObject.name);
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStopLightEstimation();
#endif
        }

        private void Update()
        {
            if (sceneLight == null) return;

            float dt = Time.deltaTime * adaptSpeed;

            if (adjustIntensity)
            {
                float clamped = Mathf.Clamp(_targetIntensity * intensityMultiplier, minIntensity, maxIntensity);
                sceneLight.intensity = Mathf.Lerp(sceneLight.intensity, clamped, dt);
            }

            if (adjustColorTemperature)
            {
                sceneLight.color = Color.Lerp(sceneLight.color, _targetColor, dt);
            }

            if (adjustDirection)
            {
                Quaternion targetRot = Quaternion.Euler(_targetDirection);
                sceneLight.transform.rotation = Quaternion.Slerp(
                    sceneLight.transform.rotation, targetRot, dt);
            }

            if (adjustAmbient)
            {
                float targetVal = Mathf.Clamp(_targetAmbient * intensityMultiplier, 0.05f, 1.5f);
                RenderSettings.ambientIntensity = Mathf.Lerp(
                    RenderSettings.ambientIntensity, targetVal, dt);
            }
        }

        // =============================================
        // SendMessage callbacks from JS bridge
        // =============================================

        /// <summary>
        /// Receives light data from JS: "intensity,colorR,colorG,colorB,dirX,dirY,dirZ,ambient"
        /// </summary>
        public void OnLightEstimation(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 8) return;

            if (float.TryParse(parts[0], out float intensity))
            {
                _targetIntensity = intensity;
                OnIntensityEstimated?.Invoke(intensity);
            }

            if (float.TryParse(parts[1], out float r) &&
                float.TryParse(parts[2], out float g) &&
                float.TryParse(parts[3], out float b))
            {
                _targetColor = new Color(r, g, b);
                OnColorEstimated?.Invoke(_targetColor);
            }

            if (float.TryParse(parts[4], out float dx) &&
                float.TryParse(parts[5], out float dy) &&
                float.TryParse(parts[6], out float dz))
            {
                _targetDirection = new Vector3(dx, dy, dz);
                OnDirectionEstimated?.Invoke(_targetDirection);
            }

            if (float.TryParse(parts[7], out float ambient))
            {
                _targetAmbient = ambient;
            }
        }
    }
}
