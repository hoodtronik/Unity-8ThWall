using UnityEngine;
using UnityEditor;
using System.IO;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Auto-fixes EXIF orientation on JPEG import + provides right-click rotate.
    /// 
    /// EXIF Orientation Values:
    ///   1 = Normal
    ///   2 = Flipped horizontal
    ///   3 = Rotated 180°
    ///   4 = Flipped vertical
    ///   5 = Rotated 270° + flipped horizontal
    ///   6 = Rotated 90° CW  ← most common phone portrait
    ///   7 = Rotated 90° CW + flipped horizontal
    ///   8 = Rotated 270° CW (= 90° CCW)
    /// 
    /// Access manual rotate via: Right-click texture → XR8 → Rotate 90° CW/CCW
    /// </summary>
    public class XR8TextureOrientationFixer : AssetPostprocessor
    {
        // =====================================================
        // AUTO-FIX ON IMPORT (EXIF detection)
        // =====================================================

        private void OnPreprocessTexture()
        {
            // Only process JPEGs — PNGs don't have EXIF orientation issues
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg") return;

            // Read EXIF orientation from the raw file
            string fullPath = Path.GetFullPath(assetPath);
            int orientation = ReadExifOrientation(fullPath);

            if (orientation > 1)
            {
                // Store orientation in userData so OnPostprocessTexture can use it
                var importer = assetImporter as TextureImporter;
                if (importer != null)
                {
                    importer.userData = "exif_orientation:" + orientation;
                    importer.isReadable = true; // Need readable to rotate pixels
                }
            }
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            var importer = assetImporter as TextureImporter;
            if (importer == null || !importer.userData.StartsWith("exif_orientation:")) return;

            int orientation = int.Parse(importer.userData.Replace("exif_orientation:", ""));
            if (orientation <= 1) return;

            Debug.Log($"[XR8 Texture Fixer] Fixing EXIF orientation {orientation} for: {assetPath}");

            // Apply rotation to the actual source file so it's permanent
            string fullPath = Path.GetFullPath(assetPath);
            RotateJpegFile(fullPath, orientation);

            // Clear the userData flag and reimport
            importer.userData = "";
            // Note: We modify the source file, so Unity will reimport automatically
        }

        // =====================================================
        // RIGHT-CLICK CONTEXT MENU
        // =====================================================

        [MenuItem("Assets/XR8/Rotate 90° Clockwise", false, 1200)]
        private static void RotateCW()
        {
            RotateSelectedTextures(1); // 1 = CW
        }

        [MenuItem("Assets/XR8/Rotate 90° Counter-Clockwise", false, 1201)]
        private static void RotateCCW()
        {
            RotateSelectedTextures(3); // 3 = CCW (270° CW)
        }

        [MenuItem("Assets/XR8/Rotate 180°", false, 1202)]
        private static void Rotate180()
        {
            RotateSelectedTextures(2); // 2 = 180°
        }

        [MenuItem("Assets/XR8/Fix EXIF Orientation", false, 1210)]
        private static void FixExifOrientation()
        {
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg")
                {
                    Debug.LogWarning($"[XR8 Texture Fixer] Skipping non-JPEG: {path}");
                    continue;
                }

                string fullPath = Path.GetFullPath(path);
                int orientation = ReadExifOrientation(fullPath);

                if (orientation <= 1)
                {
                    Debug.Log($"[XR8 Texture Fixer] {Path.GetFileName(path)} — orientation is already correct (EXIF={orientation})");
                    continue;
                }

                Debug.Log($"[XR8 Texture Fixer] Fixing {Path.GetFileName(path)} — EXIF orientation {orientation}");
                RotateJpegFile(fullPath, orientation);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        // Validation — only show menu items when textures are selected
        [MenuItem("Assets/XR8/Rotate 90° Clockwise", true)]
        [MenuItem("Assets/XR8/Rotate 90° Counter-Clockwise", true)]
        [MenuItem("Assets/XR8/Rotate 180°", true)]
        private static bool ValidateRotate()
        {
            foreach (var obj in Selection.objects)
                if (obj is Texture2D) return true;
            return false;
        }

        [MenuItem("Assets/XR8/Fix EXIF Orientation", true)]
        private static bool ValidateFixExif()
        {
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg") return true;
            }
            return false;
        }

        // =====================================================
        // ROTATION ENGINE
        // =====================================================

        private static void RotateSelectedTextures(int rotationType)
        {
            foreach (var obj in Selection.objects)
            {
                if (!(obj is Texture2D)) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                string fullPath = Path.GetFullPath(path);

                // Make texture readable temporarily
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool wasReadable = importer.isReadable;
                if (!wasReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                // Load the texture and rotate
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var rotated = RotateTexture(tex, rotationType);

                // Save back to file
                string ext = Path.GetExtension(path).ToLowerInvariant();
                byte[] bytes;
                if (ext == ".png")
                    bytes = rotated.EncodeToPNG();
                else
                    bytes = rotated.EncodeToJPG(95);

                File.WriteAllBytes(fullPath, bytes);
                Object.DestroyImmediate(rotated);

                // Restore readable state
                if (!wasReadable)
                {
                    importer.isReadable = false;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                string dir = rotationType == 1 ? "90° CW" : rotationType == 3 ? "90° CCW" : "180°";
                Debug.Log($"[XR8 Texture Fixer] Rotated {Path.GetFileName(path)} — {dir}");
            }

            AssetDatabase.Refresh();
        }

        private static Texture2D RotateTexture(Texture2D source, int rotationType)
        {
            int w = source.width;
            int h = source.height;
            Color32[] srcPixels = source.GetPixels32();
            Color32[] dstPixels;
            int dstW, dstH;

            switch (rotationType)
            {
                case 1: // 90° CW
                    dstW = h; dstH = w;
                    dstPixels = new Color32[dstW * dstH];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            dstPixels[(w - 1 - x) * dstW + y] = srcPixels[y * w + x];
                    break;

                case 2: // 180°
                    dstW = w; dstH = h;
                    dstPixels = new Color32[dstW * dstH];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            dstPixels[(h - 1 - y) * w + (w - 1 - x)] = srcPixels[y * w + x];
                    break;

                case 3: // 90° CCW (270° CW)
                    dstW = h; dstH = w;
                    dstPixels = new Color32[dstW * dstH];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            dstPixels[x * dstW + (h - 1 - y)] = srcPixels[y * w + x];
                    break;

                default:
                    return Object.Instantiate(source);
            }

            var result = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
            result.SetPixels32(dstPixels);
            result.Apply();
            return result;
        }

        // =====================================================
        // EXIF PARSER (minimal — orientation tag only)
        // =====================================================

        private static void RotateJpegFile(string fullPath, int exifOrientation)
        {
            // Read the image, rotate, write back — strips EXIF in the process
            byte[] fileBytes = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(fileBytes);

            int rotationType;
            bool flipH = false;

            switch (exifOrientation)
            {
                case 2: flipH = true; rotationType = 0; break;
                case 3: rotationType = 2; break; // 180°
                case 4: flipH = true; rotationType = 2; break;
                case 5: flipH = true; rotationType = 3; break;
                case 6: rotationType = 1; break; // 90° CW
                case 7: flipH = true; rotationType = 1; break;
                case 8: rotationType = 3; break; // 90° CCW
                default: Object.DestroyImmediate(tex); return;
            }

            Texture2D result = tex;

            if (flipH)
            {
                result = FlipHorizontal(result);
                if (result != tex) Object.DestroyImmediate(tex);
            }

            if (rotationType > 0)
            {
                var rotated = RotateTexture(result, rotationType);
                if (rotated != result) Object.DestroyImmediate(result);
                result = rotated;
            }

            byte[] outBytes = result.EncodeToJPG(95);
            File.WriteAllBytes(fullPath, outBytes);
            Object.DestroyImmediate(result);
        }

        private static Texture2D FlipHorizontal(Texture2D source)
        {
            int w = source.width;
            int h = source.height;
            var src = source.GetPixels32();
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    dst[y * w + (w - 1 - x)] = src[y * w + x];

            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.SetPixels32(dst);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Reads the EXIF orientation tag from a JPEG file.
        /// Returns 1 (normal) if no EXIF data or not a JPEG.
        /// </summary>
        private static int ReadExifOrientation(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    // Check JPEG SOI marker
                    if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
                        return 1;

                    // Find APP1 (EXIF) marker
                    while (stream.Position < stream.Length - 2)
                    {
                        byte marker1 = reader.ReadByte();
                        if (marker1 != 0xFF) return 1;

                        byte marker2 = reader.ReadByte();

                        if (marker2 == 0xE1) // APP1 = EXIF
                        {
                            int segLength = ReadUInt16BE(reader);
                            long segEnd = stream.Position + segLength - 2;

                            // Check "Exif\0\0" header
                            byte[] exifHeader = reader.ReadBytes(6);
                            if (exifHeader[0] != (byte)'E' || exifHeader[1] != (byte)'x' ||
                                exifHeader[2] != (byte)'i' || exifHeader[3] != (byte)'f')
                                return 1;

                            long tiffStart = stream.Position;

                            // Read byte order
                            byte[] bo = reader.ReadBytes(2);
                            bool bigEndian = (bo[0] == 0x4D && bo[1] == 0x4D); // "MM"
                            // "II" = little endian

                            // Skip TIFF magic (0x002A)
                            reader.ReadBytes(2);

                            // Read IFD0 offset
                            uint ifdOffset = ReadUInt32(reader, bigEndian);
                            stream.Position = tiffStart + ifdOffset;

                            // Read IFD0 entries
                            int entryCount = ReadUInt16(reader, bigEndian);
                            for (int i = 0; i < entryCount; i++)
                            {
                                if (stream.Position + 12 > segEnd) break;

                                int tag = ReadUInt16(reader, bigEndian);
                                int type = ReadUInt16(reader, bigEndian);
                                int count = (int)ReadUInt32(reader, bigEndian);
                                int valueOffset = (int)ReadUInt32(reader, bigEndian);

                                if (tag == 0x0112) // Orientation tag
                                {
                                    // For SHORT type (3), value is in the offset field
                                    if (type == 3)
                                        return bigEndian ? (valueOffset >> 16) & 0xFFFF : valueOffset & 0xFFFF;
                                    return valueOffset;
                                }
                            }
                            return 1; // No orientation tag found
                        }
                        else if (marker2 == 0xDA) // Start of scan — no more markers
                        {
                            return 1;
                        }
                        else
                        {
                            // Skip this segment
                            int len = ReadUInt16BE(reader);
                            stream.Position += len - 2;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XR8 Texture Fixer] Could not read EXIF from {filePath}: {e.Message}");
            }

            return 1;
        }

        private static int ReadUInt16BE(BinaryReader r)
        {
            byte a = r.ReadByte();
            byte b = r.ReadByte();
            return (a << 8) | b;
        }

        private static int ReadUInt16(BinaryReader r, bool bigEndian)
        {
            byte a = r.ReadByte();
            byte b = r.ReadByte();
            return bigEndian ? (a << 8) | b : (b << 8) | a;
        }

        private static uint ReadUInt32(BinaryReader r, bool bigEndian)
        {
            byte a = r.ReadByte();
            byte b = r.ReadByte();
            byte c = r.ReadByte();
            byte d = r.ReadByte();
            if (bigEndian)
                return (uint)((a << 24) | (b << 16) | (c << 8) | d);
            else
                return (uint)((d << 24) | (c << 16) | (b << 8) | a);
        }
    }
}
