using UnityEngine;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// Captures AR screenshots by compositing Unity's render and camera feed.
    /// The screenshot is downloaded directly to the user's device.
    /// </summary>
    public class XR8ScreenCapture : MonoBehaviour
    {
        [DllImport("__Internal")] private static extern void ShowWebGLScreenshot(string dataUrl);
        [DllImport("__Internal")] private static extern void DownloadWebGLTexture(byte[] img, int size, string name, string ext);

        [SerializeField] private Camera captureCamera;
        [SerializeField] private string filenamePrefix = "ar-screenshot";

        /// <summary>
        /// Capture a screenshot and show it in the browser overlay.
        /// </summary>
        public void CaptureAndShow()
        {
            StartCoroutine(CaptureCoroutine(false));
        }

        /// <summary>
        /// Capture a screenshot and download it as a PNG.
        /// </summary>
        public void CaptureAndDownload()
        {
            StartCoroutine(CaptureCoroutine(true));
        }

        private System.Collections.IEnumerator CaptureCoroutine(bool download)
        {
            yield return new WaitForEndOfFrame();

            if (captureCamera == null)
                captureCamera = Camera.main;

            int width = Screen.width;
            int height = Screen.height;

            RenderTexture rt = new RenderTexture(width, height, 24);
            captureCamera.targetTexture = rt;
            captureCamera.Render();

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            captureCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);

#if UNITY_WEBGL && !UNITY_EDITOR
            if (download)
            {
                byte[] pngBytes = tex.EncodeToPNG();
                string filename = filenamePrefix + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                DownloadWebGLTexture(pngBytes, pngBytes.Length, filename, ".png");
            }
            else
            {
                byte[] pngBytes = tex.EncodeToPNG();
                string dataUrl = "data:image/png;base64," + System.Convert.ToBase64String(pngBytes);
                ShowWebGLScreenshot(dataUrl);
            }
#else
            // Editor: save to desktop
            byte[] bytes = tex.EncodeToPNG();
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                filenamePrefix + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"
            );
            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log("[XR8ScreenCapture] Saved to: " + path);
#endif

            Destroy(tex);
        }
    }
}
