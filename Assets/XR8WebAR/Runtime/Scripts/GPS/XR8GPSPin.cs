using UnityEngine;
using UnityEngine.Events;

namespace XR8WebAR
{
    /// <summary>
    /// XR8GPSPin — marks a world-space AR object at a specific GPS coordinate.
    /// Place this on any GameObject. The GPS tracker will move it to the correct
    /// position relative to the camera based on real-world lat/lon.
    /// 
    /// Inspired by Imaginary Labs' GPSPin system.
    /// </summary>
    public class XR8GPSPin : MonoBehaviour
    {
        [Header("GPS Coordinates")]
        [SerializeField] public string pinId;
        [SerializeField] public double latitude;
        [SerializeField] public double longitude;
        [SerializeField] public double altitude = 0;

        [Header("Events")]
        public UnityEvent OnEnteredRange;
        public UnityEvent OnExitedRange;
        public UnityEvent OnEnteredPin;
        public UnityEvent OnExitedPin;

        /// <summary>Target position calculated from GPS data.</summary>
        [HideInInspector] public Vector3 targetPosition;

        /// <summary>Whether this pin is within the activation radius.</summary>
        [HideInInspector] public bool inRange = false;

        /// <summary>Whether the user is within the pin radius (close enough to "enter").</summary>
        [HideInInspector] public bool entered = false;

        private void Awake()
        {
            if (string.IsNullOrEmpty(pinId))
                pinId = gameObject.name;
            targetPosition = transform.position;
        }

        /// <summary>
        /// Converts this pin's GPS coordinates to a local Cartesian position
        /// relative to the given reference (camera) GPS coordinates.
        /// Uses a flat-earth approximation (accurate within ~50km).
        /// </summary>
        public Vector3 GPSToCartesian(double refLatitude, double refLongitude, double refAltitude)
        {
            const double earthRadius = 6371000.0;

            double latRad = latitude * Mathf.Deg2Rad;

            // Meters per degree at this latitude
            double metersPerDegreeLat = 111132.92
                - 559.82 * Mathf.Cos(2f * (float)latRad)
                + 1.175 * Mathf.Cos(4f * (float)latRad)
                - 0.0023 * Mathf.Cos(6f * (float)latRad);

            double metersPerDegreeLon = Mathf.PI * earthRadius * Mathf.Cos((float)latRad) / 180.0;

            double z = (latitude - refLatitude) * metersPerDegreeLat;
            double y = altitude - refAltitude;
            double x = (longitude - refLongitude) * metersPerDegreeLon;

            return new Vector3((float)x, (float)y, (float)z);
        }
    }
}
