---
name: Unity XR8WebAR Usage
description: How to use the XR8WebAR Unity addon for 8th Wall AR experiences in Unity WebGL builds
---

# XR8WebAR Unity Addon — Skills Guide

## Overview

This addon provides 8th Wall AR tracking for Unity WebGL builds. It bridges C# scripts to the 8th Wall JS engine via `.jslib` files and `xr8-bridge.js`.

## Architecture

```
C# Scripts  ←→  .jslib (DllImport)  ←→  xr8-bridge.js  ←→  8th Wall Engine
```

## Quick Setup — The Setup Wizard

**Recommended:** Use `XR8 WebAR > Quick Setup Wizard` in the menu bar.

The wizard has 3 tabs:
1. **Setup** — Pick tracking modes (Image/World/Face), enter target ID, click "Run Setup"
   → creates camera, manager, trackers, targets, all auto-wired
2. **Validate** — Checklist showing ✅/❌ for every requirement
3. **Build** — One-click WebGL build + serve instructions

## Scene Templates

Use `XR8 WebAR > Scene Templates` for pre-configured scenes:
1. 🖼 **Image + Video** — Track image, play video overlay
2. 🏛 **Gallery Mode** — Image + World + floor detection (museums)
3. 🚪 **AR Portal** — Stencil-based walk-through portal
4. 😊 **Face Filter** — Face tracking with attachment points
5. 📍 **World Placement** — Tap-to-place on surfaces

## Editor Tools Menu

| Menu Item | Purpose |
|---|---|
| `XR8 WebAR > Quick Setup Wizard` | One-click scene setup, validation, and build |
| `XR8 WebAR > Scene Templates` | 8 pre-built AR scene types (New/Add To Scene toggle available) |
| `XR8 WebAR > Image → Video Quick Setup` | Instantly creates an Image Target with an auto-scaled, matching Video Overlay |
| `XR8 WebAR > Generate All Scene Templates` | Batch create all templates into new scenes |
| `XR8 WebAR > Import Gaussian Splat` | Drag-drop .ply/.splat → ready prefab |
| `XR8 WebAR > Image Trackability Analyzer` | Score images 0-100 for tracking quality |
| `XR8 WebAR > Build WebGL` | Quick build shortcut |

## Key Components

| Script | Purpose |
|---|---|
| `XR8Manager` | Unified controller — enable tracking modes, desktop preview |
| `XR8Camera` | Camera feed background rendering, URP support |
| `XR8ImageTracker` | Image target tracking with anchor-based workflow + quality presets |
| `XR8FaceTracker` | Face tracking with expressions, landmarks, 6 attachment points |
| `XR8WorldTracker` | SLAM world tracking — 6DOF, 3DOF, Orbit modes. JS origin place/reset, compass, angle smoothing |
| `XR8CombinedTracker` | Image + World combined (gallery/museum mode) |
| `XR8PlacementIndicator` | Visual reticle for tap-to-place placement workflow |
| `XR8VideoController` | Video playback on targets (with proper material cleanup) |
| `XR8TrackerSettings` | Quality presets (Performance/Balanced/Quality) |
| `GaussianSplatRenderer` | WebGL-compatible Gaussian Splat rendering (GPU instanced, pre-allocated depth sort) |
| `GaussianSplatLoader` | PLY/splat file parser (Mobile-GS compatible) |

## Installed Optimization Plugins (Pro Version)

The project has these Unity Asset Store plugins installed:

| Plugin | Folder | C# API | Purpose |
|--------|--------|--------|---------|
| **Mesh Baker** | `Assets/MeshBaker/` | `MB3_MeshBaker`, `MB3_TextureBaker` | Mesh combining + texture atlasing |
| **Mantis LOD Editor Pro** | `Assets/MantisLODEditor/` | `Mantis.LODEditor` namespace | Auto-LOD generation |
| **Mesh Animator** | `Assets/MeshAnimator/` | `MeshAnimator` namespace | VAT baking (skeletal → texture) |
| **GPU Instancer (Crowd)** | `Assets/GPUInstancer/` | `GPUInstancer` namespace | GPU instancing for objects + crowds |
| **Amplify Shader Editor** | `Assets/AmplifyShaderEditor/` | Visual editor (not scriptable) | Visual shader graph |
| **DOTween Pro** | via Plugins/Demigiant/ | `DG.Tweening` namespace | Tweening: `transform.DOMove()` etc. |
| **Animation Converter** | `Assets/AnimationConverter/` | Editor tool | Animation retargeting + compression |
| **NaughtyAttributes** | `Assets/NaughtyAttributes/` | `[Button]`, `[ShowIf]` attributes | Better inspector UX |

When writing code that uses these plugins, add appropriate `#if` guards or check for namespace availability.

## Tracking Quality Presets

In `XR8ImageTracker` inspector > Tracker Settings > Tracking Quality:
- **Performance** — Raw poses, no smoothing (fast targets, lowest latency)
- **Balanced** — Moderate smoothing (default, good for most use cases)
- **Quality** — Heavy smoothing (stationary targets, buttery smooth)
- **Manual** — Check "Manual Smoothing" to set position/rotation values independently

## Portal Shaders

Two shaders for AR portal effect:
- `XR8WebAR/PortalMask` — Stencil-only, renders no color (the invisible window)
- `XR8WebAR/PortalInterior` — Stencil-tested, only visible through portal mask

Both must share the same `Stencil Reference` value (default: 1).

## Desktop Preview Controls & Modes

Desktop Preview is activated and configured on the **XR8Manager component Inspector**, NOT the top menu bar.

To use it:
1. Select the `XR8Manager` GameObject in your scene hierarchy.
2. In the Inspector, locate the **Preview Mode** dropdown.
3. Select your desired mode:
   - **Static** — Camera stays still, use keyboard to move target
   - **FlyThrough** — Drone-like continuous forward motion
   - **RecordedPlayback** — Pre-recorded camera path simulation
   - **SimulatedNoise** — Hand-shake / jitter simulation to test tracker robustness

| Key (Static Mode) | Action |
|-----|--------|
| `T` | Toggle tracking on/off |
| `Tab` | Cycle through image targets |
| `Mouse Drag` | Move target position |
| `Scroll Wheel` | Adjust target distance |
| `R` | Reset target position |
| `Esc` | Lose tracking |
| `F` | Toggle face found/lost |
| `1-5` | Expression presets (neutral/smile/surprise/wink/talk) |
| `Right-Click Drag` | Move face position |

## Image Targets: EXIF Orientation & Video Scaling

### EXIF Orientation
Sometimes a landscape image displays as portrait in Unity because phone cameras use hidden EXIF tags to rotate pixels, and standard Unity import ignores them.
**The Fix:**
- **Auto-Fix:** Any newly imported JPEG with an EXIF tag will now be automatically rotated and fixed by `XR8TextureOrientationFixer`.
- **Manual Fix:** Right-click any image in the `Assets` folder -> Select `XR8 -> Fix EXIF Orientation` or use the manual 90° rotation options.

### Video Overlay Scaling
To ensure a Video Overlay perfectly matches an Image Target in WebAR:
- The video quad's dimensions MUST use the **image's aspect ratio/mesh dimensions**, not the video's. 
- Use the `XR8 WebAR -> Image → Video Quick Setup` wizard, which automatically calculates the correct exact physical quad size to perfectly align the overlay geometry using standard 1x1 primitive sizing mapped to localScale.
- Optimal video resolution for WebAR overlays is **1080p (1920x1080)**. 4K is too heavy for mobile web browsers, ensuring smooth playback and fast texture memory upload.

## Gaussian Splat Pipeline

WebGL-compatible (no compute shaders). Workflow:
1. Prepare .ply with [Mobile-GS](https://github.com/xiaobiaodu/Mobile-GS) or any 3DGS pipeline
2. `XR8 WebAR > Import Gaussian Splat` → browse/drag .ply or .splat
3. Auto-creates: .bytes data + material + prefab with renderer
4. Drop prefab into scene — done

## Testing

- **Phone:** Build WebGL → `npx serve Build/` → open URL on phone (same WiFi)
- **Desktop Preview:** Toggle in XR8Manager inspector → full interactive simulation

## File Locations

- Runtime scripts: `Assets/XR8WebAR/Runtime/Scripts/`
- Gaussian Splat: `Assets/XR8WebAR/Runtime/Scripts/GaussianSplat/`
- Portal shaders: `Assets/XR8WebAR/Runtime/Shaders/`
- JS plugins: `Assets/XR8WebAR/Runtime/Plugins/`
- Editor tools: `Assets/XR8WebAR/Editor/`
- JS bridge: `Assets/WebGLTemplates/8thWallTracker/xr8-bridge.js`
- HTML template: `Assets/WebGLTemplates/8thWallTracker/index.html`

## Convai AI Characters

The project includes a Convai Web SDK integration for AI-powered talking characters in WebAR.

### Architecture
- **convai-bridge.js** (`WebGLTemplates/8thWallTracker/`) — JS bridge, same pattern as xr8-bridge.js
- **ConvaiBridge.jslib** (`Runtime/Plugins/`) — Unity C#↔JS interop
- **XR8ConvaiCharacter.cs** (`Runtime/Scripts/`) — Drop-on component for any character
- SDK loaded via CDN: `@convai/web-sdk@1.2.0`

### Key Facts
- Convai = **brain only** (AI personality, voice, lip-sync data). You provide the 3D model.
- Characters from Convai's Avatar Studio **CANNOT** be downloaded as 3D models.
- Lip-sync: 60fps ARKit blendshapes (52 values) streamed via WebRTC.
- Min animations: Idle + Talking. Optional: Listening, Thinking, Wave, Nod.

### Reallusion → Unity Pipeline
1. Create/customize character in CC4
2. Use InstaLOD Remesher → optimize to 10K-15K triangles for WebAR
3. Enable ARKit facial expressions (Facial Profile > ARKit)
4. Export FBX with "Unity" preset (separate model + animation FBX files preferred)
5. Install **Auto Setup for Unity** plugin (free, from Reallusion)
6. Drag FBX into Unity → Auto Setup handles shader assignment
7. Set up Animator Controller (Idle ↔ Talking, "IsTalking" Bool)
8. Add `XR8ConvaiCharacter` component → paste Character ID + API Key

### Poly Budget for WebAR
- Character: 5K-15K triangles (ideal), 30K max
- Total scene: under 100K triangles
- Tools: Mantis LOD, Mesh Baker, InstaLOD (in CC4)

## IMPORTANT: No App Keys

8th Wall engine is self-hosted. **There are NO app keys, NO cloud project IDs.** The engine binary lives at `Assets/WebGLTemplates/8thWallTracker/xr8.js`. Never ask for or try to configure an 8th Wall app key.
