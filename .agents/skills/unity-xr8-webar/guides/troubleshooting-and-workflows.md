---
description: Troubleshooting common issues and specific workflows in XR8WebAR (Video Overlays, EXIF, Previews)
---

# XR8WebAR Troubleshooting & Workflows

This guide covers specific edge cases, common issues, and specialized workflows you might encounter while building XR8WebAR projects in Unity.

## 1. Image Targets & EXIF Orientation

**Issue:** You import an image target (e.g., a landscape photo), but in the Unity Editor and when tracking, it displays sideways (portrait).
**Cause:** Modern smartphones embed an invisible EXIF orientation tag in JPEG metadata instead of physically rotating the pixels. Unity's default texture importer completely ignores EXIF data.

**Solution:**
Starting with the `XR8TextureOrientationFixer` update, this is handled automatically:
- **Auto-Fix on Import:** Any new `.jpg` or `.jpeg` file dropped into the `Assets` folder is automatically parsed. If an EXIF rotation tag is found, the actual pixel data is rotated on disk and re-saved, destroying the EXIF tag so Unity imports it exactly as it looks.
- **Manual Fix:** If you have legacy images that are sideways, right-click the image in the `Assets` folder -> `XR8 -> Fix EXIF Orientation`.
- **Manual Rotation:** If an image is just wrong, right-click -> `XR8 -> Rotate 90° Clockwise` (or Counter-Clockwise).

## 2. Image to Video Quick Setup (Overlay Sizing)

**Workflow:** You want a video to play exactly over a tracked image poster.
**Tool:** Use `XR8 WebAR -> Image -> Video Quick Setup`.

**Common Issue:** The video quad doesn't match the image dimensions (e.g., one is a landscape rectangle, one is a square, or the video is clipped).
**Why This Matters:** In augmented reality, you want the overlay plane to EXACTLY match the physical poster's aspect ratio.

**The Fix / Best Practices:**
1. **Never scale the video mesh blindly.** The video object should be a standard 1x1 Unity `Quad` primitive.
2. Set the `TargetPreview` object (the visual image placeholder in the editor) to ALSO be a 1x1 Unity `Quad`.
3. Apply the exact same `Transform.localScale` (e.g., `[width, height, 1]`) to both the `TargetPreview` and the `VideoContent` child objects.
4. The video's specific aspect ratio (16:9 vs 4:3) doesn't matter for the geometry. The VideoPlayer will project the video onto the geometry. If the physical poster is 4:3, the video quad must be 4:3 so it locks to the poster correctly.
5. Provide video files at exactly **1080p (1920x1080)**. 4K overloads mobile Safari memory limits during WebAR decoding.

## 3. Desktop Preview Modes & Multi-Mode Sytem

**Issue:** You want to test how the AR tracking feels when the phone moves, or if tracking gets lost, without building to a phone.
**Workflow:**
The `XR8Manager` GameObject contains a dropdown for **Preview Mode**. 

*Note: This dropdown is purely an editor workflow tool and is stripped from WebGL builds, so it will never accidentally break production.*

**Modes Available:**
*   `Static`: The camera doesn't move. You drag the target plane using the mouse to simulate the camera moving around it.
*   `FlyThrough`: The camera flies forward continuously at `Fly Speed`. Great for testing "enter the zone" triggers or looping gallery layouts.
*   `RecordedPlayback`: Not fully implemented without external position files, but acts as a stub for playing back JSON positional matrices captured from a real device.
*   `SimulatedNoise`: The most important testing mode. Automatically jitters and shifts the camera using `Noise Intensity` and `Drift Speed` to simulate poor lighting or a shaky hand holding the phone. **If your AR UI is usable under SimulatedNoise, it's ready for production.**

To change modes mid-flight, you must stop Play mode, change the dropdown, and hit Play again.

## 4. Scene Templates vs Scene Generator

**Issue:** Doing the same setup repeatedly takes too long.
**Workflow:**
*   **Scene Templates:** `XR8 WebAR -> Scene Templates`. Lets you click a card to instantly build a specific type of AR scene. 
*   **Generate All:** `XR8 WebAR -> Generate All Scene Templates`. Batch-mode.
*   **The Toggle:** Always pay attention to the `New Scene / Add to Scene` toggle at the top of these windows. By default, templates spawn in a brand new empty scene. If you switch to `Add to Scene`, it appends the AR rig to your currently open level without destroying it.
