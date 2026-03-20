using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XR8WebAR.GaussianSplat
{
    /// <summary>
    /// Loads Gaussian Splat data from .ply or .splat files.
    /// Compatible with standard 3DGS output and Mobile-GS compressed output.
    /// 
    /// Mobile-GS (https://github.com/xiaobiaodu/Mobile-GS) produces optimized
    /// .ply files with fewer splats and quantized attributes — this loader
    /// handles both full and compressed formats.
    /// </summary>
    public static class GaussianSplatLoader
    {
        /// <summary>Raw splat data for one Gaussian.</summary>
        public struct SplatData
        {
            public Vector3 position;
            public Vector3 scale;       // log-scale (exp applied at render)
            public Quaternion rotation;
            public Color color;         // SH degree 0 → RGB + opacity
            public Matrix4x4 cov3D;     // precomputed if available
        }

        /// <summary>
        /// Load a .splat file (binary format used by antimatter15/splat viewer).
        /// Each splat = 32 bytes: px,py,pz (f32), sx,sy,sz (f32*0.01), r,g,b,a (u8), qx,qy,qz,qw (u8 normalized)
        /// Wait — actually the standard .splat format is different. Let me use the correct one.
        /// </summary>
        public static SplatData[] LoadSplatFile(byte[] data)
        {
            // Standard .splat binary: 32 bytes per splat
            // [0-11]  position xyz (3x float32)
            // [12-23] scale xyz (3x float32) 
            // [24-27] color rgba (4x uint8)
            // [28-31] rotation wxyz (4x uint8, normalized to [-1,1])
            int splatSize = 32;
            int count = data.Length / splatSize;
            var splats = new SplatData[count];

            for (int i = 0; i < count; i++)
            {
                int offset = i * splatSize;

                // Position
                float px = BitConverter.ToSingle(data, offset + 0);
                float py = BitConverter.ToSingle(data, offset + 4);
                float pz = BitConverter.ToSingle(data, offset + 8);

                // Scale
                float sx = BitConverter.ToSingle(data, offset + 12);
                float sy = BitConverter.ToSingle(data, offset + 16);
                float sz = BitConverter.ToSingle(data, offset + 20);

                // Color (uint8 → float)
                float r = data[offset + 24] / 255f;
                float g = data[offset + 25] / 255f;
                float b = data[offset + 26] / 255f;
                float a = data[offset + 27] / 255f;

                // Rotation quaternion (uint8 → [-1, 1])
                float qw = (data[offset + 28] - 128f) / 128f;
                float qx = (data[offset + 29] - 128f) / 128f;
                float qy = (data[offset + 30] - 128f) / 128f;
                float qz = (data[offset + 31] - 128f) / 128f;

                splats[i] = new SplatData
                {
                    position = new Vector3(px, py, pz),
                    scale = new Vector3(sx, sy, sz),
                    rotation = new Quaternion(qx, qy, qz, qw).normalized,
                    color = new Color(r, g, b, a)
                };
            }

            Debug.Log("[GaussianSplatLoader] Loaded " + count + " splats from .splat file");
            return splats;
        }

        /// <summary>
        /// Load a PLY file (standard or Mobile-GS compressed output).
        /// Reads the PLY header to find property names and offsets dynamically,
        /// so it works with any PLY variant.
        /// </summary>
        public static SplatData[] LoadPlyFile(byte[] data)
        {
            // Find end of header
            string headerStr = "";
            int headerEnd = -1;
            for (int i = 0; i < Math.Min(data.Length, 8192); i++)
            {
                if (i >= 9 &&
                    data[i - 9] == 'e' && data[i - 8] == 'n' && data[i - 7] == 'd' &&
                    data[i - 6] == '_' && data[i - 5] == 'h' && data[i - 4] == 'e' &&
                    data[i - 3] == 'a' && data[i - 2] == 'd' && data[i - 1] == 'e' &&
                    data[i] == 'r')
                {
                    // Skip past "end_header\n"
                    headerEnd = i + 1;
                    while (headerEnd < data.Length && data[headerEnd] != '\n') headerEnd++;
                    headerEnd++; // skip the newline
                    headerStr = System.Text.Encoding.ASCII.GetString(data, 0, headerEnd);
                    break;
                }
            }

            if (headerEnd < 0)
            {
                Debug.LogError("[GaussianSplatLoader] Could not find PLY header end!");
                return new SplatData[0];
            }

            // Parse header
            int vertexCount = 0;
            var properties = new List<PlyProperty>();
            bool isBinary = false;

            string[] lines = headerStr.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("element vertex"))
                {
                    vertexCount = int.Parse(line.Split(' ')[2]);
                }
                else if (line.StartsWith("property"))
                {
                    string[] parts = line.Split(' ');
                    if (parts.Length >= 3)
                    {
                        properties.Add(new PlyProperty
                        {
                            type = parts[1],
                            name = parts[2],
                            offset = 0 // computed below
                        });
                    }
                }
                else if (line.StartsWith("format binary"))
                {
                    isBinary = true;
                }
            }

            if (!isBinary)
            {
                Debug.LogError("[GaussianSplatLoader] Only binary PLY files are supported!");
                return new SplatData[0];
            }

            // Compute byte offsets for each property
            int stride = 0;
            for (int i = 0; i < properties.Count; i++)
            {
                var p = properties[i];
                p.offset = stride;
                p.byteSize = GetPlyTypeSize(p.type);
                properties[i] = p;
                stride += p.byteSize;
            }

            // Build lookup: property name → index
            var propMap = new Dictionary<string, int>();
            for (int i = 0; i < properties.Count; i++)
                propMap[properties[i].name] = i;

            // Parse vertices
            var splats = new SplatData[vertexCount];
            int dataStart = headerEnd;

            for (int i = 0; i < vertexCount; i++)
            {
                int baseOffset = dataStart + i * stride;
                if (baseOffset + stride > data.Length) break;

                // Position (always x, y, z)
                float px = ReadPlyFloat(data, baseOffset, properties, propMap, "x");
                float py = ReadPlyFloat(data, baseOffset, properties, propMap, "y");
                float pz = ReadPlyFloat(data, baseOffset, properties, propMap, "z");

                // Scale — try scale_0/1/2 first, then sx/sy/sz
                float sx = ReadPlyFloat(data, baseOffset, properties, propMap, "scale_0", "sx", 0.01f);
                float sy = ReadPlyFloat(data, baseOffset, properties, propMap, "scale_1", "sy", 0.01f);
                float sz = ReadPlyFloat(data, baseOffset, properties, propMap, "scale_2", "sz", 0.01f);

                // Rotation — try rot_0/1/2/3 first, then quaternion fields
                float qw = ReadPlyFloat(data, baseOffset, properties, propMap, "rot_0", "qw", 1f);
                float qx = ReadPlyFloat(data, baseOffset, properties, propMap, "rot_1", "qx", 0f);
                float qy = ReadPlyFloat(data, baseOffset, properties, propMap, "rot_2", "qy", 0f);
                float qz = ReadPlyFloat(data, baseOffset, properties, propMap, "rot_3", "qz", 0f);

                // Color — SH degree 0 coefficients (f_dc_0/1/2) or red/green/blue
                float r, g, b;
                if (propMap.ContainsKey("f_dc_0"))
                {
                    // Spherical harmonics DC component → RGB
                    // SH DC to color: c = 0.5 + SH_C0 * val, where SH_C0 = 0.28209479
                    float sh0 = ReadPlyFloat(data, baseOffset, properties, propMap, "f_dc_0");
                    float sh1 = ReadPlyFloat(data, baseOffset, properties, propMap, "f_dc_1");
                    float sh2 = ReadPlyFloat(data, baseOffset, properties, propMap, "f_dc_2");
                    r = Mathf.Clamp01(0.5f + 0.28209479f * sh0);
                    g = Mathf.Clamp01(0.5f + 0.28209479f * sh1);
                    b = Mathf.Clamp01(0.5f + 0.28209479f * sh2);
                }
                else
                {
                    r = ReadPlyFloat(data, baseOffset, properties, propMap, "red", "r", 1f) / 255f;
                    g = ReadPlyFloat(data, baseOffset, properties, propMap, "green", "g", 1f) / 255f;
                    b = ReadPlyFloat(data, baseOffset, properties, propMap, "blue", "b", 1f) / 255f;
                }

                // Opacity (stored as logit in standard 3DGS)
                float opacity;
                if (propMap.ContainsKey("opacity"))
                {
                    float rawOpacity = ReadPlyFloat(data, baseOffset, properties, propMap, "opacity");
                    // Sigmoid activation: 1 / (1 + exp(-x))
                    opacity = 1f / (1f + Mathf.Exp(-rawOpacity));
                }
                else
                {
                    opacity = ReadPlyFloat(data, baseOffset, properties, propMap, "alpha", "a", 1f);
                }

                // Apply exp to scale (stored as log-scale in standard 3DGS)
                if (propMap.ContainsKey("scale_0"))
                {
                    sx = Mathf.Exp(sx);
                    sy = Mathf.Exp(sy);
                    sz = Mathf.Exp(sz);
                }

                splats[i] = new SplatData
                {
                    position = new Vector3(px, py, pz),
                    scale = new Vector3(sx, sy, sz),
                    rotation = new Quaternion(qx, qy, qz, qw).normalized,
                    color = new Color(r, g, b, opacity)
                };
            }

            Debug.Log("[GaussianSplatLoader] Loaded " + vertexCount + " splats from PLY (" + 
                properties.Count + " properties, stride=" + stride + "B)");
            return splats;
        }

        // --- Helpers ---

        struct PlyProperty
        {
            public string type;
            public string name;
            public int offset;
            public int byteSize;
        }

        static int GetPlyTypeSize(string type)
        {
            switch (type)
            {
                case "float":
                case "float32":
                case "int":
                case "int32":
                case "uint":
                case "uint32":
                    return 4;
                case "double":
                case "float64":
                    return 8;
                case "short":
                case "int16":
                case "uint16":
                case "ushort":
                    return 2;
                case "char":
                case "uchar":
                case "int8":
                case "uint8":
                    return 1;
                default:
                    Debug.LogWarning("[GaussianSplatLoader] Unknown PLY type: " + type + ", assuming 4 bytes");
                    return 4;
            }
        }

        static float ReadPlyFloat(byte[] data, int baseOff, List<PlyProperty> props, 
            Dictionary<string, int> map, string name, string altName = null, float defaultVal = 0f)
        {
            string useName = name;
            if (!map.ContainsKey(useName))
            {
                if (altName != null && map.ContainsKey(altName))
                    useName = altName;
                else
                    return defaultVal;
            }

            var prop = props[map[useName]];
            int off = baseOff + prop.offset;

            switch (prop.type)
            {
                case "float":
                case "float32":
                    return BitConverter.ToSingle(data, off);
                case "double":
                case "float64":
                    return (float)BitConverter.ToDouble(data, off);
                case "uchar":
                case "uint8":
                    return data[off];
                case "char":
                case "int8":
                    return (sbyte)data[off];
                case "short":
                case "int16":
                    return BitConverter.ToInt16(data, off);
                case "ushort":
                case "uint16":
                    return BitConverter.ToUInt16(data, off);
                case "int":
                case "int32":
                    return BitConverter.ToInt32(data, off);
                default:
                    return BitConverter.ToSingle(data, off);
            }
        }
    }
}
