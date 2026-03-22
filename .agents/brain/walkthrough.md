# XR8WebAR Unity Addon — Agent Handoff

> **Last updated:** 2026-03-20 16:44 EDT  
> **Branch:** `main` (all features merged)  
> **Project:** `f:\__PROJECTS\8thWall\Ar-Image-Template-8thWall`

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
| `XR8ImageTracker.cs` | Image target tracking with target ID + content root pairs |
| `XR8FaceTracker.cs` | Face tracking: pose, expressions, 15 attachment points, multi-face |
| `XR8VideoController.cs` | Video playback on tracked targets |
| `XR8EngineStatus.cs` | Engine lifecycle monitoring |
| `XR8ScreenCapture.cs` | Screenshot/share functionality |

### JS Bridge (`Assets/WebGLTemplates/8thWallTracker/`)
| File | Purpose |
|---|---|
| `xr8-bridge.js` | All bridge classes: `XR8CameraBridge`, `XR8TrackerBridge`, `XR8WorldBridge`, `XR8FaceBridge` |
| `index.html` | WebGL template, instantiates bridges, loads 8th Wall engine |

### Plugins (`Assets/XR8WebAR/Runtime/Plugins/`)
| File | Purpose |
|---|---|
| `XR8CameraLib.jslib` | Camera C#↔JS interop |
| `XR8TrackerLib.jslib` | Image tracker C#↔JS interop |
| `XR8FaceTrackerLib.jslib` | Face tracker C#↔JS interop |
| `Helpers.jslib` | Utility functions |
| `DownloadTexture.jslib` | Texture download helper |
| `TransparentBackground.jslib` | WebGL transparency |

### Editor (`Assets/XR8WebAR/Editor/`)
| File | Purpose |
|---|---|
| `XR8ImageTrackerEditor.cs` | Custom inspector: auto-discovery, thumbnails, drag-drop, debug buttons |
| `XR8ManagerEditor.cs` | Custom inspector: colored tracking toggles, contextual config |
| `XR8ImageTargetGizmos.cs` | Always-visible scene gizmos: renders target images as textured quads |
| `XR8MenuItems.cs` | `GameObject > XR8 WebAR` menu: one-click AR scene setup |
| `XR8WebAR.Editor.asmdef` | Editor assembly definition |

### Scene
- `Assets/Scenes/AR Scene.unity` — Has: Main Camera + XR8Camera, XR8ImageTracker + VideoOverlay, XR8Manager
- `Assets/image-targets/` — gallery-target JSON + images

## Git History (latest first)
```
acbc311 feat: always-visible image target gizmos + hide VideoOverlay in editor
120714c feat: render actual image texture on scene gizmo
d8ae06a feat: prefab library with menu items
b9c18f0 feat: custom XR8Manager inspector
2a8fbde feat: face tracking
6ef8111 fix: desktop preview waits for tracker init
fbebf4c fix: desktop preview uses real target IDs
cdfeb94 feat: custom inspector with auto-discovery
22ddc73 fix: remove InputSystem dependency
91e942b feat: XR8Manager unified controller
478f433 Initial commit
```

## What Works
- ✅ Image tracking (C# ↔ JS ↔ 8th Wall pipeline complete)
- ✅ Face tracking (expressions, landmarks, multi-face)
- ✅ World tracking (surface detection, mesh visualization)
- ✅ Custom inspectors (XR8Manager + XR8ImageTracker)
- ✅ Scene gizmos (image targets always visible as textured quads)
- ✅ Menu items (one-click scene setup)
- ✅ VideoOverlay hidden in edit mode (renderer disabled)

## Known Issues / Pinned
- ⚠️ **Desktop preview mode** — events fire but video doesn't play visually. Needs investigation into VideoController activation in editor.
- ⚠️ **Phone preview** — build WebGL → serve locally → open on phone. This is the real testing workflow.

## Convai AI Characters (Integrated)
- **convai-bridge.js** — JS bridge for Convai Web SDK ↔ Unity
- **ConvaiBridge.jslib** — C# DllImport bindings
- **XR8ConvaiCharacter.cs** — Drop-on Unity component (custom inspector, lip-sync, animation, events)
- Convai = brain only. 3D model comes from Reallusion CC4, Mixamo, or any source.
- Lip-sync: 60fps ARKit blendshapes (52 values) via `blendshapeQueue.getFrameAtTime()`

## Reallusion → Unity Pipeline
- **Auto Setup for Unity** (free plugin) handles CC4 → Unity shader/material import
- **InstaLOD** (built into CC4) optimizes poly count (target 10K-15K for WebAR)
- Export FBX with "Unity" preset + ARKit facial expressions
- Separate model + animation FBX files preferred

## Next Steps
1. Test WebGL build on phone (the real validation)
2. Import Reallusion characters (masked cyberpunk avatar + lip-sync test character)
3. Test Convai AI conversation on phone
4. Investigate desktop preview video playback
5. Add more image targets / test multi-target
6. Polish face tracking attachment point workflow
7. **Gaussian Splat support** — integrate a mobile-optimized Gaussian splat renderer for AR. Reference repo: [mobile-gs](https://github.com/xiaobiaodu/mobile-gs) (mobile Gaussian splatting). Also consider [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) for Unity integration. Key tasks:
   - Evaluate mobile-gs for WebGL/mobile browser compatibility
   - Add Gaussian splat renderer package to project
   - Create `XR8GaussianSplatTarget` component that parents a splat to a tracked image/surface
   - Ensure WebGL compatibility (splat rendering must work in WebGL builds)
   - Test performance on mobile browsers
