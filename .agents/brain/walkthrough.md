# XR8WebAR Unity Addon — Agent Handoff

> **Last updated:** 2026-03-22 13:10 EDT  
> **Branch:** `main` (all features merged)  
> **Project:** `G:\_AR_Projects\Unity-8ThWall`

## Architecture

Unity WebGL + 8th Wall self-hosted engine. The addon lives at `Assets/XR8WebAR/` as a UPM-style local package.

```
C# Scripts  ←→  .jslib (DllImport)  ←→  xr8-bridge.js  ←→  8th Wall Engine
```

Data flows: JS bridge classes receive 8th Wall events → serialize to CSV strings → `SendMessage()` to Unity C# scripts.

## File Map

### Runtime (`Assets/XR8WebAR/Runtime/Scripts/`)
| File | Purpose |
|---|---|
| `XR8Manager.cs` | Unified AR controller: tracking modes, engine init, desktop preview |
| `XR8Camera.cs` | Camera feed → Unity texture (background rendering) |
| `XR8ImageTracker.cs` | Image target tracking with anchor system + quality presets |
| `XR8FaceTracker.cs` | Face tracking: pose, expressions, 15 attachment points, multi-face |
| `XR8WorldTracker.cs` | SLAM world tracking — 6DOF, 3DOF, Orbit modes, PlaneMode, 3DOF fallback, tracking confidence |
| `XR8CombinedTracker.cs` | Image + World combined tracking (gallery/museum mode) |
| `XR8VideoController.cs` | Video playback on tracked targets |
| `XR8TextureExtractor.cs` | **NEW** — Extract de-warped texture from tracked images (ported from IL) |
| `XR8ConvaiCharacter.cs` | Convai AI character component (lip-sync, animation, events) |
| `XR8ARCrowd.cs` | GPU Instancer crowd integration |
| `XR8MeshOptimizer.cs` | Mesh Baker / Mantis LOD optimization wrapper |
| `XR8TweenFX.cs` | DOTween Pro FX utilities |
| `XR8EngineStatus.cs` | Engine lifecycle monitoring |
| `XR8ScreenCapture.cs` | Screenshot/share functionality |

### Interaction Scripts (`Assets/XR8WebAR/Runtime/Scripts/Interaction/`)
| File | Purpose |
|---|---|
| `XR8PinchToScale.cs` | Two-finger pinch-to-scale (InputSystem dual support) |
| `XR8SwipeToRotate.cs` | Single-finger swipe-to-rotate (InputSystem dual support) |
| `XR8TapToReposition.cs` | Tap-to-reposition via raycast (InputSystem dual support) |
| `XR8TwoFingerPan.cs` | Two-finger pan (InputSystem dual support) |
| `XR8PlacementIndicator.cs` | Visual reticle for tap-to-place workflow |

### JS Bridge (`Assets/WebGLTemplates/8thWallTracker/`)
| File | Purpose |
|---|---|
| `xr8-bridge.js` | All bridge classes: Camera, Tracker, World, Face |
| `convai-bridge.js` | Convai Web SDK bridge |
| `index.html` | WebGL template, loads 8th Wall engine |

### Plugins (`Assets/XR8WebAR/Runtime/Plugins/`)
| File | Purpose |
|---|---|
| `XR8CameraLib.jslib` | Camera C#↔JS interop |
| `XR8TrackerLib.jslib` | Image + world tracker C#↔JS interop (includes stubs for new features) |
| `XR8FaceTrackerLib.jslib` | Face tracker C#↔JS interop |
| `ConvaiBridge.jslib` | Convai C#↔JS interop |
| `XR8GPSLib.jslib` | GPS tracker C#↔JS interop |

### Editor (`Assets/XR8WebAR/Editor/`)
| File | Purpose |
|---|---|
| `XR8ImageTrackerEditor.cs` | Custom inspector: auto-discovery, thumbnails, drag-drop |
| `XR8ManagerEditor.cs` | Custom inspector: colored tracking toggles |
| `XR8ImageTargetGizmos.cs` | Scene gizmos: renders target images as textured quads |
| `XR8ImageTargetFactory.cs` | **NEW** — One-click image target creator |
| `XR8MenuItems.cs` | Menu bar items |
| `XR8SetupWizard.cs` | Quick Setup Wizard (3 tabs) |
| `XR8SceneTemplates.cs` | 5 pre-configured scene templates |
| `XR8SceneGenerator.cs` | Scene generation utilities |
| `XR8OptimizeScene.cs` | Scene optimization tools |
| `ImageTrackabilityAnalyzer.cs` | Score images 0-100 for tracking quality |

### Scenes
- `Assets/Scenes/SampleScene.unity` — **Test scene** with XR8Manager + WorldTracker + interaction scripts on colored test objects
- `Assets/Scenes/AR Scene.unity` — Original AR scene with image tracker
- `Assets/Scenes/GapFixTest.unity` — Additional gap fix test scene
- `Assets/image-targets/` — Target JSON + images

## What Works
- ✅ Image tracking (C# ↔ JS ↔ 8th Wall pipeline complete)
- ✅ Face tracking (expressions, landmarks, multi-face)
- ✅ World tracking (6DOF, 3DOF, Orbit, surface detection)
- ✅ Combined image + world tracking (XR8CombinedTracker)
- ✅ InputSystem dual support (legacy Input + new InputSystem via #if guards)
- ✅ Tracking confidence events
- ✅ 3DOF fallback when SLAM surfaces lost
- ✅ Vertical plane mode (experimental wall tracking)
- ✅ Texture extraction from tracked images
- ✅ Custom inspectors, scene gizmos, menu items
- ✅ Desktop preview mode (keyboard simulation)
- ✅ AG Bridge for remote phone control (cloned, installed)

## Known Issues / Pinned
- ⚠️ **JS bridge stubs** — `GetXR8WarpedTexture` and `OnTrackingConfidenceReceived` are jslib stubs. Full JS implementation needed in `xr8-bridge.js` when actively used.
- ⚠️ **Desktop preview video** — events fire but video doesn't play visually.
- ⚠️ **Vertical plane mode** — Experimental, depends on 8th Wall engine support.

## AG Bridge (Remote Phone Control)
- Cloned to `ag_bridge/` (gitignored)
- `launch_ag_bridge.bat` — double-click to launch Antigravity with `--remote-debugging-port=9000` + AG Bridge
- Tailscale recommended for remote access (auto-detected)
- Download: https://tailscale.com/download/windows

## Next Steps
1. **WebGL build + phone test** — the real validation
2. Install Tailscale, test AG Bridge remote connection
3. Implement JS bridge for `GetXR8WarpedTexture` and `OnTrackingConfidenceReceived`
4. Import Reallusion characters + lip-sync test
5. Test Convai AI conversation on phone
6. Polish Gaussian splat support for mobile WebGL



