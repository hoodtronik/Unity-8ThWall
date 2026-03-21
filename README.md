# Unity-8ThWall — WebAR Toolkit

Unity WebGL toolkit for **WebAR experiences** powered by [8th Wall's self-hosted engine](https://github.com/8thwall/engine).

> Point your phone at an image → AR content appears. No app required.

## ✨ Features

### Core AR Tracking
- **Image Tracking** — Track posters, paintings, business cards with SLAM-based accuracy
- **World Tracking** — SLAM surface detection, hit testing, tap-to-place
- **Face Tracking** — Face detection with landmarks, expressions, attachment points
- **Combined Tracking** — Image + World simultaneously (gallery/museum mode)

### Editor Tools
| Tool | Menu Path | What It Does |
|------|-----------|-------------|
| **Quick Setup Wizard** | `XR8 WebAR > Quick Setup Wizard` | One-click scene setup, validation, build |
| **Scene Templates** | `XR8 WebAR > Scene Templates` | 5 pre-built AR scene types |
| **Import Gaussian Splat** | `XR8 WebAR > Import Gaussian Splat` | .ply/.splat → ready prefab |
| **Image Trackability Analyzer** | `XR8 WebAR > Image Trackability Analyzer` | Score images 0-100 |
| **Build WebGL** | `XR8 WebAR > Build WebGL` | Quick build shortcut |

### Scene Templates
1. 🖼 **Image + Video** — Track image, play video overlay
2. 🏛 **Gallery Mode** — Image tracking + floor detection (museums, exhibitions)
3. 🚪 **AR Portal** — Walk-through portal with stencil buffer masking
4. 😊 **Face Filter** — Face tracking with attachment points
5. 📍 **World Placement** — Tap-to-place objects on surfaces

### Advanced Features
- **Tracking Quality Presets** — Performance / Balanced / Quality (client-side smoothing)
- **Gaussian Splat Rendering** — WebGL-compatible (no compute shaders), Mobile-GS compatible
- **Desktop Preview** — Full interactive simulation in Unity Editor (T/Tab/R/Esc/mouse)
- **Multi-Target Tracking** — Track multiple images simultaneously
- **Portal Shaders** — Stencil-based PortalMask + PortalInterior shaders

## 🚀 Quick Start

### Prerequisites
- **Unity 6** (6000.x) or Unity 2022.3+
- **Node.js** (for image target processing)
- A web server with HTTPS (for mobile camera access)

### Fastest Path (Setup Wizard)
1. Open project in Unity
2. Go to `XR8 WebAR > Quick Setup Wizard`
3. Check your tracking modes, enter target ID
4. Click **⚡ Run Setup**
5. Hit Play to test with Desktop Preview

### Or Use Scene Templates
1. Go to `XR8 WebAR > Scene Templates`
2. Pick a template (Image+Video, Gallery, Portal, Face, World)
3. Click **Create Scene** — everything is auto-wired

### Build & Deploy
```bash
# Build in Unity: XR8 WebAR > Build WebGL

# Serve locally
cd Build/
npx serve .

# Open URL on phone (same WiFi network)
```

## 📁 Project Structure

```
Unity-8ThWall/
├── Assets/
│   ├── XR8WebAR/                           ← The core addon
│   │   ├── Runtime/
│   │   │   ├── Scripts/
│   │   │   │   ├── XR8Manager.cs           ← Unified controller
│   │   │   │   ├── XR8Camera.cs            ← Camera feed
│   │   │   │   ├── XR8ImageTracker.cs      ← Image tracking
│   │   │   │   ├── XR8WorldTracker.cs      ← SLAM / surfaces
│   │   │   │   ├── XR8FaceTracker.cs       ← Face tracking
│   │   │   │   ├── XR8CombinedTracker.cs   ← Image + World combined
│   │   │   │   ├── XR8TrackerSettings.cs   ← Quality presets
│   │   │   │   ├── XR8VideoController.cs   ← Video playback
│   │   │   │   └── GaussianSplat/          ← 3DGS rendering
│   │   │   ├── Shaders/
│   │   │   │   ├── GaussianSplat.shader    ← WebGL splat rendering
│   │   │   │   ├── PortalMask.shader       ← Stencil mask (invisible)
│   │   │   │   └── PortalInterior.shader   ← Stencil-tested interior
│   │   │   └── Plugins/                    ← .jslib bridge files
│   │   └── Editor/
│   │       ├── XR8SetupWizard.cs           ← Setup wizard
│   │       ├── XR8SceneTemplates.cs        ← Scene templates
│   │       ├── GaussianSplatImporter.cs    ← Splat import tool
│   │       ├── ImageTrackabilityAnalyzer.cs← Image scoring
│   │       └── WebGLBuilder.cs             ← Build automation
│   ├── WebGLTemplates/
│   │   └── 8thWallTracker/
│   │       ├── index.html                  ← WebGL entry point
│   │       ├── xr8-bridge.js              ← Open-source XR8↔Unity bridge
│   │       └── xr8.js                     ← 8th Wall engine (binary)
│   └── image-targets/                     ← Target data
└── README.md
```

## 🔌 Optimization Plugins (Pro Version Only)

The **Pro version** (`Unity-8ThWall-Pro`, private repo) includes these premium Unity Asset Store plugins for WebGL optimization:

| Plugin | Purpose |
|--------|---------|
| **Mesh Baker** | Mesh combining + texture atlasing → fewer draw calls |
| **Mantis LOD Editor Pro** | Auto-generates LOD levels for any mesh |
| **Mesh Animator** | Bakes skeletal animations to Vertex Animation Textures (VAT) |
| **GPU Instancer (Crowd)** | GPU instancing for repeated/animated objects |
| **Amplify Shader Editor** | Visual shader graph for Built-in pipeline |
| **DOTween Pro** | Tweening library for smooth animations |
| **Animation Converter** | Animation retargeting and compression |
| **NaughtyAttributes** | Better Unity inspector UX (free) |

> These plugins are **not included** in the open-source version. Purchase them separately from the Unity Asset Store if needed.

## 🎮 Desktop Preview Controls

Enable `Desktop Preview` on XR8Manager, then enter Play Mode:

| Key | Action |
|-----|--------|
| `T` | Toggle tracking on/off |
| `Tab` | Cycle through image targets |
| `Mouse Drag` | Move target position |
| `Scroll` | Adjust target distance |
| `R` | Reset position |
| `F` | Toggle face tracking |
| `1-5` | Expression presets |

## ⚠️ Important Notes

- **No App Keys Required** — 8th Wall engine is self-hosted. No cloud project IDs needed.
- **WebGL Only** — This is designed for WebGL builds, not native mobile.
- **HTTPS Required** — Camera access requires HTTPS on mobile browsers.

## 📄 License

- **XR8WebAR addon code**: MIT License
- **8th Wall engine binary** (`xr8.js`): [8th Wall Engine Binary License](https://www.8thwall.com/docs/migration/faq/)
- **Optimization plugins** (Pro version): Respective Unity Asset Store licenses
