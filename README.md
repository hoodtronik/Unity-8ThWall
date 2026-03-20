# AR Image Template — 8th Wall Edition

Unity WebGL template for **WebAR image tracking** powered by [8th Wall's self-hosted engine](https://github.com/8thwall/engine).

> Point your phone at a target image → AR content appears on top of it.

## 🎯 What's Different from the Original?

| Feature | Original (Imagine WebAR) | This Fork (8th Wall) |
|---------|--------------------------|---------------------|
| Image tracking engine | OpenCV.js (obfuscated) | 8th Wall XR8 (SLAM-based) |
| Cloud dependency | None | None |
| App key required | No | No |
| Addon license | Paid (Unity Asset Store) | Free (MIT + engine binary) |
| Camera bridge | Obfuscated JS | Open-source `xr8-bridge.js` |
| Additional AR features | Image tracking only | Image + World + Face + Sky (expandable) |

## 🚀 Quick Start

### Prerequisites
- **Unity 6** (or Unity 2022.3+)
- **Node.js** (for image target processing)
- A web server with HTTPS (for mobile camera access)

### 1. Open in Unity
```
File → Open Project → select Ar-Image-Template-8thWall/
```

### 2. Set the WebGL Template
```
Edit → Project Settings → Player → WebGL tab
→ Resolution and Presentation → WebGL Template → 8thWallTracker
```

### 3. Set Up Your Scene
The scene needs these GameObjects:

```
Scene Hierarchy:
├── Main Camera          ← Add XR8Camera component
├── Light                ← Directional light for AR
└── XR8ImageTracker      ← Add XR8ImageTracker component (root level!)
    └── YourContent      ← 3D model, video plane, etc.
```

### 4. Configure the Tracker
On the **XR8ImageTracker** GameObject:
1. Set `Tracker Cam` → drag in your Main Camera
2. Add an entry to `Image Targets` list:
   - `ID`: must match the name in your target JSON (e.g., `gallery-target`)
   - `Transform`: drag in the content you want to track

### 5. Process Image Targets
```bash
# Install the CLI
npm install -g @8thwall/image-target-cli

# Process your image
image-target-cli process --image my-painting.jpg --name my-target
```

This creates a JSON file and luminance image. Place them in `Assets/image-targets/`.

### 6. Build & Deploy
```
File → Build Profiles → WebGL → Build
```

Upload the build folder to any HTTPS web server. Open on your phone and point at the target image!

## 📁 Project Structure

```
Ar-Image-Template-8thWall/
├── Assets/
│   ├── XR8WebAR/                       ← The addon (UPM-ready)
│   │   ├── package.json                ← Unity Package manifest
│   │   └── Runtime/
│   │       ├── Scripts/
│   │       │   ├── XR8Camera.cs        ← Camera bridge
│   │       │   ├── XR8ImageTracker.cs  ← Image tracker
│   │       │   └── XR8TrackerSettings.cs
│   │       └── Plugins/
│   │           ├── XR8CameraLib.jslib
│   │           ├── XR8TrackerLib.jslib
│   │           ├── TransparentBackground.jslib
│   │           ├── Helpers.jslib
│   │           └── DownloadTexture.jslib
│   ├── WebGLTemplates/
│   │   └── 8thWallTracker/
│   │       ├── index.html              ← WebGL entry point
│   │       ├── xr8-bridge.js           ← Open-source XR8↔Unity bridge
│   │       ├── xr.js                   ← 8th Wall engine (binary)
│   │       └── xr-slam.js             ← SLAM chunk (binary)
│   ├── Scenes/
│   │   └── SampleScene.unity
│   └── image-targets/                  ← Sample target data
│       └── gallery-target.json
└── README.md
```

## 🔧 Installing as a Unity Package

You can also install XR8WebAR in any Unity project via the Package Manager:

1. Copy the `Assets/XR8WebAR/` folder to your project's `Assets/` directory
2. Copy the `Assets/WebGLTemplates/8thWallTracker/` to your project's `Assets/WebGLTemplates/`
3. Or install via git URL: `https://github.com/hoodtronik/Ar-Image-Template-8thWall.git?path=Assets/XR8WebAR`

## 📜 Scripts Reference

### `XR8Camera.cs`
Attach to the **Camera** entity. Initializes the 8th Wall engine, manages camera feed, video background, and FOV.

### `XR8ImageTracker.cs`
Attach to a **root-level** GameObject. Receives tracking events from XR8, updates tracked entity positions/rotations.

**Settings:**
- `Tracker Origin` — `CAMERA_ORIGIN` (camera stays at origin, targets move) or `FIRST_TARGET_ORIGIN` (first target at origin, camera moves)
- `Disable World Tracking` — `true` for image-only ode (better performance)
- `Use Extra Smoothing` — Lerp/Slerp for smoother tracking

### `xr8-bridge.js`
Open-source JavaScript bridge. Converts XR8 pipeline events to Unity SendMessage calls.

## 📄 License

- **XR8WebAR addon code**: MIT License
- **8th Wall engine binary** (`xr.js`, `xr-slam.js`): [8th Wall Engine Binary License](https://www.8thwall.com/docs/migration/faq/#distributed-engine-binary-license-and-permitted-use) — free to use and distribute
