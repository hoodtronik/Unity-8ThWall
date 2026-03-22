# Unity-8ThWall — WebAR Toolkit

Unity WebGL toolkit for **WebAR experiences** powered by [8th Wall's self-hosted engine](https://github.com/8thwall/engine).

> Point your phone at an image → AR content appears. No app required.

---

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
| **Scene Templates** | `XR8 WebAR > Scene Templates` | 5+ pre-built AR scene types |
| **Import Gaussian Splat** | `XR8 WebAR > Import Gaussian Splat` | .ply/.splat → ready prefab |
| **Image Trackability Analyzer** | `XR8 WebAR > Image Trackability Analyzer` | Score images 0-100 |
| **Build WebGL** | `XR8 WebAR > Build WebGL` | Quick build shortcut |
| **Optimize Scene** | Editor script | Mesh Baker + Mantis LOD batch optimization |

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
- **AR Tween FX** — DOTween-powered reveal, float, pulse, rotate effects (coroutine fallback)
- **AR Crowds** — Spawn + manage animated crowds via Mesh Animator VAT + GPU Instancer
- **Convai AI Characters** — AI-powered talking characters with ARKit lip-sync
- **Screen Capture** — In-AR screenshot utility

---

## 🚀 Quick Start

### Prerequisites
- **Unity 6** (6000.x) or Unity 2022.3+
- **Node.js** (for local serving and image target processing)
- A web server with **HTTPS** (required for mobile camera access)
- A modern mobile browser (Chrome / Safari)

### Step 1 — Clone & Open
```bash
git clone https://github.com/hoodtronik/Unity-8ThWall.git
cd Unity-8ThWall
```
Open the project in Unity 6. Wait for package imports to complete.

### Step 2 — Setup Wizard (Fastest Path)
1. Go to **`XR8 WebAR > Quick Setup Wizard`**
2. **Setup tab** — Check your tracking modes (Image / World / Face), enter a target ID
3. Click **⚡ Run Setup** — automatically creates camera, manager, trackers, targets
4. **Validate tab** — Verify all checklist items show ✅
5. Hit **Play** to test with Desktop Preview

### Step 3 — Or Use Scene Templates
1. Go to **`XR8 WebAR > Scene Templates`**
2. Pick a template (Image+Video, Gallery, Portal, Face, World)
3. Click **Create Scene** — everything is auto-wired and ready

### Step 4 — Build & Deploy
```bash
# Build in Unity: XR8 WebAR > Build WebGL

# Serve locally with HTTPS (required for camera access)
cd Build/
npx serve .

# Open the URL on your phone (same WiFi network)
# The URL will be something like https://192.168.x.x:3000
```

> **Tip:** For HTTPS without certificates, use `npx serve --ssl-cert ... --ssl-key ...` or deploy to any static hosting provider (Netlify, Vercel, GitHub Pages).

---

## 🎯 AR Feature Guides

### Image Tracking Setup

Image tracking lets you detect and track printed images (posters, business cards, paintings, etc.) and overlay AR content on top of them.

1. **Prepare your target image:**
   - Use `XR8 WebAR > Image Trackability Analyzer` to score your image (aim for 60+)
   - Higher contrast, more unique features = better tracking
   - Avoid symmetric or repetitive patterns

2. **Add to scene:**
   - Setup Wizard creates an `XR8ImageTracker` component automatically
   - Or manually: Create empty GameObject → Add `XR8ImageTracker` component
   - Set the **Target ID** field to match your image target data filename

3. **Configure quality:**
   - Inspector → Tracker Settings → Tracking Quality:
     - **Performance** — Raw poses, no smoothing (fast-moving targets, lowest latency)
     - **Balanced** — Moderate smoothing (default, good for most use cases)
     - **Quality** — Heavy smoothing (stationary targets, buttery-smooth result)
     - **Manual** — Set position/rotation smoothing values independently

4. **Assign AR content:**
   - Child any 3D objects to the tracker GameObject
   - They'll appear/hide automatically when the target is found/lost
   - Add `XR8TweenFX` for animated reveal effects (scale-up, pulse, float)

### World Tracking Setup

World tracking uses SLAM to understand the real-world environment, detect surfaces, and allow tap-to-place interactions.

1. **Enable World Tracking** in the Setup Wizard or add `XR8WorldTracker` to a GameObject
2. **Surface Detection** — The tracker automatically detects horizontal/vertical surfaces
3. **Tap-to-Place:**
   - The world tracker performs hit tests against detected surfaces
   - Use the provided events to spawn content at hit positions
4. **Scale + Rotation** — Use pinch/rotate gestures for placed content manipulation

### Face Tracking Setup

Face tracking detects human faces and provides landmark positions, expression coefficients, and attachment points for filters/effects.

1. **Enable Face Tracking** in the Setup Wizard or add `XR8FaceTracker` component
2. **Attachment Points** — Predefined face regions (forehead, nose, chin, left eye, right eye, etc.)
3. **Expression Data:**
   - 52 ARKit-compatible blendshape values at 60fps
   - Values: browDownLeft, browDownRight, jawOpen, mouthSmile, eyeBlinkLeft, etc.
4. **Assign face content:**
   - Child objects to the face tracker for automatic positioning
   - Use attachment point transforms for accessories (glasses, hats, masks)

### Combined Tracking (Gallery / Museum Mode)

Combine image tracking + world tracking for rich experiences where users can scan artwork and also explore with world-anchored information.

1. Add `XR8CombinedTracker` component
2. Configure image targets AND surface detection simultaneously
3. Image content appears on scan, world content persists in the environment

### AR Portal Setup

Create walk-through portals to hidden worlds using stencil buffer masking.

1. Use the **AR Portal** scene template, or set up manually:
   - Create portal frame → assign `XR8WebAR/PortalMask` shader (stencil-only, invisible)
   - Create interior environment → assign `XR8WebAR/PortalInterior` shader
   - Both shaders must share the same **Stencil Reference** value (default: `1`)
2. The mask renders no color — it's the "window" through which the interior is visible
3. Interior objects are only visible through the portal opening

### Gaussian Splat Rendering

Render photorealistic 3D Gaussian Splat scenes in WebGL without compute shaders.

1. **Prepare a .ply file** — Use [Mobile-GS](https://github.com/xiaobiaodu/Mobile-GS) or any 3DGS pipeline
2. Go to **`XR8 WebAR > Import Gaussian Splat`**
3. Browse or drag your `.ply` or `.splat` file
4. Auto-creates: `.bytes` data asset + material + prefab with `GaussianSplatRenderer`
5. Drop the prefab into your scene — done
6. `GaussianSplatLoader` handles parsing; `GaussianSplatRenderer` handles WebGL rendering

### AR Tween Effects (XR8TweenFX)

Pre-built DOTween-powered effects for common AR scenarios. Falls back to coroutine-based animations if DOTween is not installed.

| Effect | Method | Description |
|--------|--------|-------------|
| **Reveal** | `Reveal()` / `Hide()` | Scale-up + overshoot entrance when tracking found |
| **Float** | `StartFloat()` / `StopFloat()` | Gentle bobbing animation |
| **Pulse** | `Pulse()` / `StartPulsing()` | Attention-grabbing scale pulse |
| **Rotate** | `StartRotation(speed)` | Smooth auto-rotation (product displays) |
| **Billboard** | `SmoothLookAtCamera()` | Smoothly face the camera |
| **Track Toggle** | `SetTracked(bool)` | Auto reveal/hide based on tracking state |

```csharp
// Example: Connect to tracker events
imageTracker.OnTargetFound.AddListener(() => tweenFX.Reveal());
imageTracker.OnTargetLost.AddListener(() => tweenFX.Hide());
```

### AR Crowds (XR8ARCrowd)

Spawn and manage animated character crowds in AR using Mesh Animator VAT (Vertex Animation Textures) + GPU Instancer for maximum WebGL performance.

1. **Bake characters** with Mesh Animator (shader-animated mode)
2. Add `XR8ARCrowd` component to a GameObject
3. Assign the baked prefab to `Crowd Prefab`
4. Configure: crowd size (1–500), spawn radius, spacing, scale variation
5. Call `SpawnCrowd()` or enable **Auto Spawn**
6. Supports: object pooling, GPU instancing, multiple prefab variants, iClone FBX import

### Convai AI Characters

AI-powered talking characters in WebAR with real-time lip sync.

**Architecture:**
```
Unity C# (XR8ConvaiCharacter)  ←→  ConvaiBridge.jslib  ←→  convai-bridge.js  ←→  Convai Web SDK (CDN)
```

**Setup:**
1. Create character on [Convai.com](https://convai.com) — get Character ID + API Key
2. Prepare a 3D character model with **ARKit blendshapes** (52 values) for lip sync
3. Set up Animator Controller: Idle ↔ Talking states, `IsTalking` Bool parameter
4. Add `XR8ConvaiCharacter` component → paste Character ID + API Key
5. Lip sync runs at 60fps via WebRTC

**Recommended Character Pipeline (Reallusion):**
1. Create/customize character in Character Creator 4 (CC4)
2. Use InstaLOD Remesher → optimize to 10K–15K triangles for WebAR
3. Enable ARKit facial expressions (Facial Profile > ARKit)
4. Export FBX with "Unity" preset
5. Install **Auto Setup for Unity** plugin (free, from Reallusion)
6. Drag FBX into Unity → Auto Setup handles shader assignment

**Poly Budget for WebAR:**
| Asset | Triangle Budget |
|-------|----------------|
| Character | 5K–15K (ideal), 30K max |
| Total scene | Under 100K |
| Optimization tools | Mantis LOD, Mesh Baker, InstaLOD |

---

## 🎮 Desktop Preview Controls

Enable `Desktop Preview` on the `XR8Manager` inspector, then enter Play Mode:

| Key | Action |
|-----|--------|
| `T` | Toggle tracking on/off |
| `Tab` | Cycle through image targets |
| `Mouse Drag` | Move target position |
| `Scroll` | Adjust target distance |
| `R` | Reset position |
| `Esc` | Lose tracking |
| `F` | Toggle face tracking |
| `1-5` | Expression presets (neutral/smile/surprise/wink/talk) |
| `Right-Click Drag` | Move face position |

---

## 🔧 Architecture

```
C# Scripts  ←→  .jslib (DllImport)  ←→  xr8-bridge.js  ←→  8th Wall Engine (xr8.js + xr-slam.js)
```

### JS Bridge Files (Runtime/Plugins/)
| File | Purpose |
|------|---------|
| `XR8TrackerLib.jslib` | Image tracking interop |
| `XR8CameraLib.jslib` | Camera feed control |
| `XR8FaceTrackerLib.jslib` | Face tracking interop |
| `XR8WorldTrackerLib.jslib` | World tracking interop |
| `ConvaiBridge.jslib` | Convai AI character interop |
| `TransparentBackground.jslib` | WebGL transparent background |
| `DownloadTexture.jslib` | Texture download utility |
| `Helpers.jslib` | General JS helper functions |

### WebGL Template (WebGLTemplates/8thWallTracker/)
| File | Purpose |
|------|---------|
| `index.html` | WebGL entry point, loads engine + bridge |
| `xr8-bridge.js` | Open-source XR8 ↔ Unity bridge |
| `xr8.js` | 8th Wall engine binary (~1 MB) |
| `xr-slam.js` | SLAM engine binary (~5.3 MB) |
| `convai-bridge.js` | Convai Web SDK bridge |

---

## 📁 Project Structure

```
Unity-8ThWall/
├── Assets/
│   ├── XR8WebAR/                           ← The core addon (Unity package)
│   │   ├── Runtime/
│   │   │   ├── Scripts/
│   │   │   │   ├── XR8Manager.cs           ← Unified controller
│   │   │   │   ├── XR8Camera.cs            ← Camera feed rendering
│   │   │   │   ├── XR8ImageTracker.cs      ← Image tracking
│   │   │   │   ├── XR8WorldTracker.cs      ← SLAM / surfaces
│   │   │   │   ├── XR8FaceTracker.cs       ← Face tracking
│   │   │   │   ├── XR8CombinedTracker.cs   ← Image + World combined
│   │   │   │   ├── XR8TrackerSettings.cs   ← Quality presets
│   │   │   │   ├── XR8VideoController.cs   ← Video playback on targets
│   │   │   │   ├── XR8TweenFX.cs           ← DOTween AR effects
│   │   │   │   ├── XR8ARCrowd.cs           ← Crowd spawning + management
│   │   │   │   ├── XR8ConvaiCharacter.cs   ← AI character integration
│   │   │   │   ├── XR8MeshOptimizer.cs     ← Mesh optimization utilities
│   │   │   │   ├── XR8ScreenCapture.cs     ← AR screenshot utility
│   │   │   │   ├── XR8EngineStatus.cs      ← Engine status monitoring
│   │   │   │   └── GaussianSplat/          ← 3DGS rendering
│   │   │   │       ├── GaussianSplatRenderer.cs
│   │   │   │       └── GaussianSplatLoader.cs
│   │   │   ├── Shaders/
│   │   │   │   ├── GaussianSplat.shader    ← WebGL splat rendering
│   │   │   │   ├── PortalMask.shader       ← Stencil mask (invisible window)
│   │   │   │   └── PortalInterior.shader   ← Stencil-tested interior
│   │   │   └── Plugins/                    ← .jslib bridge files (8 files)
│   │   └── Editor/
│   │       ├── XR8SetupWizard.cs           ← Setup wizard
│   │       ├── XR8SceneTemplates.cs        ← Scene templates
│   │       ├── XR8SceneGenerator.cs        ← Scene generation engine
│   │       ├── XR8MenuItems.cs             ← Menu bar items
│   │       ├── XR8ManagerEditor.cs         ← Custom inspector for XR8Manager
│   │       ├── XR8ImageTrackerEditor.cs    ← Custom inspector for tracker
│   │       ├── XR8ImageTargetGizmos.cs     ← Scene view gizmos
│   │       ├── XR8OptimizeScene.cs         ← Scene optimization tool
│   │       ├── GaussianSplatImporter.cs    ← Splat import tool
│   │       ├── ImageTrackabilityAnalyzer.cs← Image scoring tool
│   │       └── WebGLBuilder.cs             ← Build automation
│   ├── WebGLTemplates/
│   │   └── 8thWallTracker/
│   │       ├── index.html                  ← WebGL entry point
│   │       ├── xr8-bridge.js              ← Open-source XR8↔Unity bridge
│   │       ├── xr8.js                     ← 8th Wall engine binary
│   │       ├── xr-slam.js                ← SLAM engine binary
│   │       └── convai-bridge.js           ← Convai Web SDK bridge
│   └── [Optimization Plugins]/             ← Pro version only
├── .agents/
│   ├── skills/unity-xr8-webar/            ← Agent skill guide
│   └── workflows/                         ← Agent workflows
├── .brain/                                ← Agent memory / task state
└── README.md
```

---

## 🔌 Optimization Plugins (Pro Version Only)

The **Pro version** (`Unity-8ThWall-Pro`, private repo) includes these premium Unity Asset Store plugins for WebGL optimization:

| Plugin | Purpose | C# API |
|--------|---------|--------|
| **Mesh Baker** | Mesh combining + texture atlasing → fewer draw calls | `MB3_MeshBaker`, `MB3_TextureBaker` |
| **Mantis LOD Editor Pro** | Auto-generates LOD levels for any mesh | `Mantis.LODEditor` namespace |
| **Mesh Animator** | Bakes skeletal animations to Vertex Animation Textures (VAT) | `MeshAnimator` namespace |
| **GPU Instancer (Crowd)** | GPU instancing for repeated/animated objects | `GPUInstancer` namespace |
| **Amplify Shader Editor** | Visual shader graph for Built-in pipeline | Visual editor |
| **DOTween Pro** | Tweening library for smooth animations | `DG.Tweening` namespace |
| **Animation Converter** | Animation retargeting and compression | Editor tool |
| **NaughtyAttributes** | Better Unity inspector UX | `[Button]`, `[ShowIf]` attributes |

> These plugins are **not included** in the open-source version. Purchase them separately from the Unity Asset Store if needed. The XR8WebAR addon uses `#if` preprocessor guards so it works with or without them.

---

## 📋 WebGL Build Checklist

Before building, verify these settings:

- [ ] **Player Settings → WebGL Template** set to `8thWallTracker`
- [ ] **Color Space** set to `Linear` (recommended) or `Gamma`
- [ ] **Compression** set to `Disabled` or `Gzip` (Brotli can cause issues with some hosts)
- [ ] **Exception Handling** set to `None` or `Explicitly Thrown` for smaller builds
- [ ] **Strip Engine Code** enabled for smaller builds
- [ ] **Target images** are in `Assets/image-targets/` with proper data files
- [ ] All materials use **Built-in** pipeline shaders (not URP/HDRP)
- [ ] Test with **Desktop Preview** first before building

---

## 🌐 Deployment Options

| Platform | Setup | Notes |
|----------|-------|-------|
| **Local (dev)** | `npx serve Build/` | Need HTTPS for camera; use `--ssl-*` flags |
| **Netlify** | Drag & drop `Build/` folder | Free tier, auto-HTTPS |
| **Vercel** | `vercel Build/` | Free tier, auto-HTTPS |
| **GitHub Pages** | Push `Build/` to `gh-pages` branch | Free, auto-HTTPS |
| **AWS S3 + CloudFront** | Upload to S3, set up CloudFront | Scalable, pay-per-use |

---

## ⚠️ Important Notes

- **No App Keys Required** — 8th Wall engine is self-hosted. No cloud project IDs, no subscriptions needed.
- **WebGL Only** — This is designed for WebGL builds, not native iOS/Android.
- **HTTPS Required** — Camera access requires HTTPS on all mobile browsers.
- **Built-in Render Pipeline** — Use Built-in shaders, not URP or HDRP.
- **No Compute Shaders** — WebGL doesn't support compute shaders (Gaussian Splat works around this).

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit changes: `git commit -m "Add my feature"`
4. Push: `git push origin feature/my-feature`
5. Open a Pull Request

---

## 📄 License

- **XR8WebAR addon code**: MIT License
- **8th Wall engine binary** (`xr8.js`, `xr-slam.js`): [8th Wall Engine Binary License](https://www.8thwall.com/docs/migration/faq/)
- **Optimization plugins** (Pro version): Respective Unity Asset Store licenses
