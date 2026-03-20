using UnityEngine;
using UnityEditor;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Image Trackability Analyzer — rate your tracking images before uploading to 8th Wall.
    /// 
    /// Analyzes key visual features that affect AR image tracking quality:
    ///   - Edge density (feature richness)
    ///   - Contrast distribution (tonal range)
    ///   - Detail uniformity (features spread across the image)
    ///   - Color variation (distinct visual regions)
    ///   - Aspect ratio (square-ish is better)
    ///   - Resolution (minimum thresholds)
    /// 
    /// Gives a 0-100 score with specific improvement suggestions.
    /// Access via: XR8 WebAR > Image Trackability Analyzer
    /// </summary>
    public class ImageTrackabilityAnalyzer : EditorWindow
    {
        private Texture2D targetImage;
        private Texture2D lastAnalyzedImage;

        // Results
        private bool hasResults = false;
        private float overallScore;
        private float edgeScore;
        private float contrastScore;
        private float uniformityScore;
        private float colorScore;
        private float aspectScore;
        private float resolutionScore;
        private string[] suggestions;
        private string ratingLabel;
        private Color ratingColor;

        private Vector2 scrollPos;

        [MenuItem("XR8 WebAR/Image Trackability Analyzer", false, 101)]
        public static void ShowWindow()
        {
            var win = GetWindow<ImageTrackabilityAnalyzer>("Trackability Analyzer");
            win.minSize = new Vector2(400, 520);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("🎯 Image Trackability Analyzer", titleStyle);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Drag an image to check how well it will work as an AR tracking target.\n" +
                "This gives you a local preview score — 8th Wall also rates images when uploaded.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // Image field
            targetImage = (Texture2D)EditorGUILayout.ObjectField("Target Image", targetImage, typeof(Texture2D), false);

            EditorGUILayout.Space(8);

            // Analyze button
            GUI.enabled = targetImage != null;
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("🔍  Analyze Trackability", GUILayout.Height(34)))
            {
                AnalyzeImage(targetImage);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // Preview
            if (targetImage != null)
            {
                EditorGUILayout.Space(4);
                float previewSize = Mathf.Min(position.width - 40, 200);
                float aspect = (float)targetImage.height / targetImage.width;
                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize * aspect);
                previewRect.x = (position.width - previewSize) / 2f;
                previewRect.width = previewSize;
                GUI.DrawTexture(previewRect, targetImage, ScaleMode.ScaleToFit);
            }

            // Results
            if (hasResults)
            {
                EditorGUILayout.Space(12);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                DrawResults();
                EditorGUILayout.EndScrollView();
            }
        }

        private void AnalyzeImage(Texture2D img)
        {
            // Make readable copy
            Texture2D readable = MakeReadable(img);
            if (readable == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not read image pixels. Check texture import settings.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Analyzing", "Reading pixels...", 0.1f);

            try
            {
                Color[] pixels = readable.GetPixels();
                int w = readable.width;
                int h = readable.height;

                EditorUtility.DisplayProgressBar("Analyzing", "Computing edge density...", 0.3f);
                edgeScore = ComputeEdgeScore(pixels, w, h);

                EditorUtility.DisplayProgressBar("Analyzing", "Computing contrast...", 0.4f);
                contrastScore = ComputeContrastScore(pixels);

                EditorUtility.DisplayProgressBar("Analyzing", "Computing uniformity...", 0.5f);
                uniformityScore = ComputeUniformityScore(pixels, w, h);

                EditorUtility.DisplayProgressBar("Analyzing", "Computing color variation...", 0.6f);
                colorScore = ComputeColorScore(pixels);

                EditorUtility.DisplayProgressBar("Analyzing", "Computing aspect ratio...", 0.7f);
                aspectScore = ComputeAspectScore(w, h);

                EditorUtility.DisplayProgressBar("Analyzing", "Computing resolution...", 0.8f);
                resolutionScore = ComputeResolutionScore(w, h);

                // Weighted overall score
                overallScore = (edgeScore * 0.30f +
                               contrastScore * 0.20f +
                               uniformityScore * 0.20f +
                               colorScore * 0.10f +
                               aspectScore * 0.10f +
                               resolutionScore * 0.10f);

                // Rating label
                if (overallScore >= 80) { ratingLabel = "⭐ EXCELLENT"; ratingColor = new Color(0.2f, 0.9f, 0.3f); }
                else if (overallScore >= 60) { ratingLabel = "👍 GOOD"; ratingColor = new Color(0.5f, 0.9f, 0.2f); }
                else if (overallScore >= 40) { ratingLabel = "⚠ FAIR"; ratingColor = new Color(1f, 0.8f, 0.2f); }
                else if (overallScore >= 20) { ratingLabel = "👎 POOR"; ratingColor = new Color(1f, 0.5f, 0.2f); }
                else { ratingLabel = "❌ BAD"; ratingColor = new Color(1f, 0.2f, 0.2f); }

                // Generate suggestions
                suggestions = GenerateSuggestions(w, h);

                hasResults = true;
                lastAnalyzedImage = img;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (readable != img) DestroyImmediate(readable);
            }
        }

        private void DrawResults()
        {
            // Overall score header
            var scoreStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            scoreStyle.normal.textColor = ratingColor;
            EditorGUILayout.LabelField(ratingLabel + "  " + Mathf.RoundToInt(overallScore) + "/100", scoreStyle);

            EditorGUILayout.Space(8);

            // Individual scores
            DrawScoreBar("Edge Density", edgeScore, "Rich in visual features?");
            DrawScoreBar("Contrast", contrastScore, "Good tonal range?");
            DrawScoreBar("Detail Uniformity", uniformityScore, "Features spread evenly?");
            DrawScoreBar("Color Variation", colorScore, "Distinct color regions?");
            DrawScoreBar("Aspect Ratio", aspectScore, "Close to square?");
            DrawScoreBar("Resolution", resolutionScore, "Enough pixel detail?");

            // Suggestions
            if (suggestions != null && suggestions.Length > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Suggestions", EditorStyles.boldLabel);
                foreach (var s in suggestions)
                {
                    EditorGUILayout.HelpBox(s, MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("This image should work well as a tracking target! 🎉", MessageType.Info);
            }
        }

        private void DrawScoreBar(string label, float score, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(130));

            Rect barRect = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
            float barWidth = barRect.width;

            // Background
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));

            // Fill
            Color barColor;
            if (score >= 70) barColor = new Color(0.2f, 0.8f, 0.3f, 0.8f);
            else if (score >= 40) barColor = new Color(1f, 0.8f, 0.2f, 0.8f);
            else barColor = new Color(1f, 0.3f, 0.2f, 0.8f);

            var fillRect = new Rect(barRect.x, barRect.y, barRect.width * (score / 100f), barRect.height);
            EditorGUI.DrawRect(fillRect, barColor);

            // Score text
            var textStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            textStyle.normal.textColor = Color.white;
            EditorGUI.LabelField(barRect, Mathf.RoundToInt(score).ToString(), textStyle);

            EditorGUILayout.EndHorizontal();
        }

        // =============================================
        // ANALYSIS ALGORITHMS
        // =============================================

        /// <summary>Edge detection using Sobel-like gradient magnitude.</summary>
        private float ComputeEdgeScore(Color[] pixels, int w, int h)
        {
            float totalGradient = 0;
            int samples = 0;
            int step = Mathf.Max(1, (w * h) / 10000); // sample ~10k pixels

            for (int i = step; i < pixels.Length - step; i += step)
            {
                int x = i % w;
                int y = i / w;
                if (x <= 0 || x >= w - 1 || y <= 0 || y >= h - 1) continue;

                // Horizontal gradient
                float gx = Luminance(pixels[y * w + x + 1]) - Luminance(pixels[y * w + x - 1]);
                // Vertical gradient
                float gy = Luminance(pixels[(y + 1) * w + x]) - Luminance(pixels[(y - 1) * w + x]);

                float mag = Mathf.Sqrt(gx * gx + gy * gy);
                totalGradient += mag;
                samples++;
            }

            if (samples == 0) return 0;
            float avgGradient = totalGradient / samples;

            // Map: 0.02 = very flat (score 0), 0.15+ = very detailed (score 100)
            return Mathf.Clamp01((avgGradient - 0.02f) / 0.13f) * 100f;
        }

        /// <summary>Contrast: histogram spread of luminance values.</summary>
        private float ComputeContrastScore(Color[] pixels)
        {
            float minLum = 1f, maxLum = 0f;
            int step = Mathf.Max(1, pixels.Length / 5000);

            // Also compute standard deviation
            float sumLum = 0, sumLumSq = 0;
            int samples = 0;

            for (int i = 0; i < pixels.Length; i += step)
            {
                float lum = Luminance(pixels[i]);
                if (lum < minLum) minLum = lum;
                if (lum > maxLum) maxLum = lum;
                sumLum += lum;
                sumLumSq += lum * lum;
                samples++;
            }

            float range = maxLum - minLum;
            float mean = sumLum / samples;
            float variance = (sumLumSq / samples) - (mean * mean);
            float stdDev = Mathf.Sqrt(Mathf.Max(variance, 0));

            // Combine range and std dev
            float rangeScore = Mathf.Clamp01(range / 0.7f); // 70% range = full score
            float devScore = Mathf.Clamp01(stdDev / 0.2f);  // 0.2 stddev = full score

            return (rangeScore * 0.4f + devScore * 0.6f) * 100f;
        }

        /// <summary>Uniformity: are features spread across the image or concentrated?</summary>
        private float ComputeUniformityScore(Color[] pixels, int w, int h)
        {
            // Divide image into a 4x4 grid and check edge density in each cell
            int gridSize = 4;
            float[] cellEdge = new float[gridSize * gridSize];
            int cellW = w / gridSize;
            int cellH = h / gridSize;

            for (int gy = 0; gy < gridSize; gy++)
            {
                for (int gx = 0; gx < gridSize; gx++)
                {
                    float cellGradient = 0;
                    int cellSamples = 0;

                    int startX = gx * cellW + 1;
                    int startY = gy * cellH + 1;
                    int endX = Mathf.Min(startX + cellW - 2, w - 1);
                    int endY = Mathf.Min(startY + cellH - 2, h - 1);
                    int cellStep = Mathf.Max(1, (cellW * cellH) / 200);

                    for (int y = startY; y < endY; y += cellStep)
                    {
                        for (int x = startX; x < endX; x += cellStep)
                        {
                            float gxVal = Luminance(pixels[y * w + x + 1]) - Luminance(pixels[y * w + x - 1]);
                            float gyVal = Luminance(pixels[(y + 1) * w + x]) - Luminance(pixels[(y - 1) * w + x]);
                            cellGradient += Mathf.Sqrt(gxVal * gxVal + gyVal * gyVal);
                            cellSamples++;
                        }
                    }

                    cellEdge[gy * gridSize + gx] = cellSamples > 0 ? cellGradient / cellSamples : 0;
                }
            }

            // Compute coefficient of variation (lower = more uniform = better)
            float mean = 0;
            foreach (float v in cellEdge) mean += v;
            mean /= cellEdge.Length;

            if (mean < 0.001f) return 0; // totally flat image

            float variance = 0;
            foreach (float v in cellEdge) variance += (v - mean) * (v - mean);
            variance /= cellEdge.Length;
            float cv = Mathf.Sqrt(variance) / mean;

            // CV of 0 = perfectly uniform (100), CV of 1.5+ = very concentrated (0)
            return Mathf.Clamp01(1f - cv / 1.5f) * 100f;
        }

        /// <summary>Color variation: how many distinct color regions exist.</summary>
        private float ComputeColorScore(Color[] pixels)
        {
            // Simple: count how many of 8 hue bins have significant presence
            int[] hueBins = new int[8];
            int chromatic = 0;
            int step = Mathf.Max(1, pixels.Length / 5000);

            for (int i = 0; i < pixels.Length; i += step)
            {
                Color.RGBToHSV(pixels[i], out float h, out float s, out float v);
                if (s > 0.15f && v > 0.1f) // skip grays
                {
                    int bin = Mathf.Clamp((int)(h * 8f), 0, 7);
                    hueBins[bin]++;
                    chromatic++;
                }
            }

            if (chromatic < 10) return 30f; // mostly grayscale — not great but not terrible

            int activeBins = 0;
            int threshold = chromatic / 16; // at least 6% in a bin
            foreach (int count in hueBins)
                if (count > threshold) activeBins++;

            // 1 bin = 20, 2 = 40, 3 = 60, 4+ = 80-100
            return Mathf.Clamp(activeBins * 20f + 10f, 0f, 100f);
        }

        /// <summary>Aspect ratio: square-ish is best for tracking.</summary>
        private float ComputeAspectScore(int w, int h)
        {
            float ratio = (float)Mathf.Max(w, h) / Mathf.Min(w, h);
            // 1:1 = 100, up to 2:1 = decent, 3:1+ = poor
            if (ratio <= 1.2f) return 100f;
            if (ratio <= 1.5f) return 85f;
            if (ratio <= 2f) return 65f;
            if (ratio <= 3f) return 40f;
            return 20f;
        }

        /// <summary>Resolution: minimum needed for good tracking.</summary>
        private float ComputeResolutionScore(int w, int h)
        {
            int minDim = Mathf.Min(w, h);
            // Under 200px = bad, 300 = okay, 500+ = great, 1000+ = excellent
            if (minDim >= 1000) return 100f;
            if (minDim >= 500) return 90f;
            if (minDim >= 300) return 70f;
            if (minDim >= 200) return 45f;
            return 20f;
        }

        private string[] GenerateSuggestions(int w, int h)
        {
            var list = new System.Collections.Generic.List<string>();

            if (edgeScore < 40)
                list.Add("Low edge density — the image lacks visual detail. Use images with text, illustrations, or complex patterns.");
            if (contrastScore < 40)
                list.Add("Low contrast — the image looks flat. Increase brightness/contrast or use an image with more tonal range.");
            if (uniformityScore < 40)
                list.Add("Features are concentrated in one area. Use an image with detail spread across the whole surface.");
            if (colorScore < 30)
                list.Add("Limited color variation. Multi-colored images track better than monochrome ones.");
            if (aspectScore < 50)
                list.Add("Extreme aspect ratio. Images closer to square (1:1 to 3:2) track more reliably.");
            if (resolutionScore < 50)
                list.Add("Low resolution (" + w + "×" + h + "). Use at least 300×300px, ideally 500×500px or larger.");
            if (edgeScore < 25 && contrastScore < 25)
                list.Add("⚠ This image will likely fail to track. Consider a completely different image with more visual complexity.");

            return list.ToArray();
        }

        // =============================================
        // HELPERS
        // =============================================

        private float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private Texture2D MakeReadable(Texture2D source)
        {
            // If already readable, return as-is
            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch { }

            // Create readable copy via RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }
    }
}
