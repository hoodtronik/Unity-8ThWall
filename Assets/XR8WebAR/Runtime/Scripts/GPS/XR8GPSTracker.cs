using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace XR8WebAR
{
    /// <summary>
    /// XR8GPSTracker — manages GPS-positioned AR content via XR8GPSPin components.
    /// 
    /// Receives GPS position updates from the JS bridge and repositions all GPSPin
    /// objects relative to the camera. Handles activation/deactivation based on proximity.
    /// 
    /// Must be on a root-level GameObject named "XR8GPSTracker" to receive SendMessage.
    /// Inspired by Imaginary Labs' WorldTracker_GPS system.
    /// </summary>
    public class XR8GPSTracker : MonoBehaviour
    {
        [DllImport("__Internal")] private static extern void WebGLSubscribeToGPS();
        [DllImport("__Internal")] private static extern void WebGLUnsubscribeFromGPS();

        [Header("Settings")]
        [Tooltip("Radius in meters — pins beyond this are hidden")]
        [SerializeField] private float activationRadius = 50f;
        [Tooltip("Radius in meters — entering this triggers 'entered pin' event")]
        [SerializeField] private float pinRadius = 5f;
        [Tooltip("Lerp speed for smoothing pin position updates")]
        [SerializeField] private float positionLerpSpeed = 2.5f;

        [Header("Debug (Editor Only)")]
        [SerializeField] private double debugStartLatitude = 39.1031;
        [SerializeField] private double debugStartLongitude = -84.5120;

        [Header("Events")]
        public UnityEvent<GPSData> OnGPSPositionUpdated;
        public UnityEvent<XR8GPSPin> OnPinInRange;
        public UnityEvent<XR8GPSPin> OnPinOutOfRange;
        public UnityEvent OnAllPinsOutOfRange;
        public UnityEvent<XR8GPSPin> OnEnteredPin;
        public UnityEvent<XR8GPSPin> OnExitedPin;

        private List<XR8GPSPin> pins = new List<XR8GPSPin>();
        private GPSData currentPosition;
        private Camera trackerCam;
        private bool anyPinInRange = false;

        /// <summary>Current GPS data.</summary>
        public GPSData CurrentPosition => currentPosition;
        /// <summary>All discovered GPS pins.</summary>
        public IReadOnlyList<XR8GPSPin> Pins => pins;

        private void Start()
        {
            trackerCam = Camera.main;
            pins = FindObjectsByType<XR8GPSPin>(FindObjectsSortMode.None).ToList();
            Debug.Log($"[XR8GPSTracker] Found {pins.Count} GPS pins");

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSubscribeToGPS();
#endif

#if UNITY_EDITOR
            // Seed with debug coordinates
            ProcessGPSData($"0,0,0,0,{debugStartLatitude.ToString(CultureInfo.InvariantCulture)},{debugStartLongitude.ToString(CultureInfo.InvariantCulture)},0,0");
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLUnsubscribeFromGPS();
#endif
        }

        private void Update()
        {
            if (currentPosition == null) return;

            // Smoothly lerp pin positions and check proximity
            int nearbyCount = 0;

            foreach (var pin in pins)
            {
                // Smooth position update
                pin.transform.position = Vector3.Lerp(
                    pin.transform.position,
                    pin.targetPosition,
                    Time.deltaTime * positionLerpSpeed
                );

                float distance = Vector3.Distance(pin.transform.position, trackerCam.transform.position);

                // Activation radius check
                if (distance > activationRadius && pin.inRange)
                {
                    pin.inRange = false;
                    pin.gameObject.SetActive(false);
                    OnPinOutOfRange?.Invoke(pin);
                    pin.OnExitedRange?.Invoke();
                }
                else if (distance <= activationRadius)
                {
                    nearbyCount++;
                    if (!pin.inRange)
                    {
                        pin.inRange = true;
                        pin.gameObject.SetActive(true);
                        OnPinInRange?.Invoke(pin);
                        pin.OnEnteredRange?.Invoke();
                    }
                }

                // Pin radius check (close proximity)
                if (distance <= pinRadius && !pin.entered)
                {
                    pin.entered = true;
                    OnEnteredPin?.Invoke(pin);
                    pin.OnEnteredPin?.Invoke();
                }
                else if (distance > pinRadius && pin.entered)
                {
                    pin.entered = false;
                    OnExitedPin?.Invoke(pin);
                    pin.OnExitedPin?.Invoke();
                }
            }

            if (anyPinInRange && nearbyCount <= 0)
                OnAllPinsOutOfRange?.Invoke();

            anyPinInRange = nearbyCount > 0;
        }

        // --- Called from JS via SendMessage ---

        /// <summary>
        /// Receives GPS position from JS bridge.
        /// CSV format: accuracy,altitude,altitudeAccuracy,heading,latitude,longitude,speed,alpha
        /// </summary>
        void OnGPSPosition(string csv)
        {
            ProcessGPSData(csv);
        }

        void OnGPSPositionError(string error)
        {
            Debug.LogError($"[XR8GPSTracker] GPS error: {error}");
        }

        private void ProcessGPSData(string csv)
        {
            csv = csv.Replace("null", "0").Replace("NaN", "0");
            var vals = csv.Split(',');
            if (vals.Length < 8) return;

            currentPosition = new GPSData
            {
                accuracy = double.Parse(vals[0], CultureInfo.InvariantCulture),
                altitude = double.Parse(vals[1], CultureInfo.InvariantCulture),
                altitudeAccuracy = double.Parse(vals[2], CultureInfo.InvariantCulture),
                heading = double.Parse(vals[3], CultureInfo.InvariantCulture),
                latitude = double.Parse(vals[4], CultureInfo.InvariantCulture),
                longitude = double.Parse(vals[5], CultureInfo.InvariantCulture),
                speed = double.Parse(vals[6], CultureInfo.InvariantCulture),
                alpha = double.Parse(vals[7], CultureInfo.InvariantCulture)
            };

            // Reposition all pins relative to current GPS
            foreach (var pin in pins)
            {
                pin.targetPosition = pin.GPSToCartesian(
                    currentPosition.latitude,
                    currentPosition.longitude,
                    currentPosition.altitude
                );
            }

            OnGPSPositionUpdated?.Invoke(currentPosition);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: simulate GPS movement with WASD keys.</summary>
        private float debugOffsetX, debugOffsetZ;

        void OnGUI()
        {
            // Simple WASD debug movement for GPS
            if (Event.current.type == EventType.KeyDown)
            {
                float sensitivity = 0.0002f;
                switch (Event.current.keyCode)
                {
                    case KeyCode.W: debugOffsetX += sensitivity; break;
                    case KeyCode.S: debugOffsetX -= sensitivity; break;
                    case KeyCode.D: debugOffsetZ += sensitivity; break;
                    case KeyCode.A: debugOffsetZ -= sensitivity; break;
                }

                var lat = debugStartLatitude + debugOffsetX;
                var lon = debugStartLongitude + debugOffsetZ;
                ProcessGPSData($"0,0,0,0,{lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)},0,0");
            }
        }
#endif
    }

    /// <summary>
    /// GPS position data received from the browser Geolocation API.
    /// </summary>
    [System.Serializable]
    public class GPSData
    {
        public double accuracy;
        public double altitude;
        public double altitudeAccuracy;
        public double heading;
        public double latitude;
        public double longitude;
        public double speed;
        public double alpha;
    }
}
