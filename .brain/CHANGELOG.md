# Unity-8ThWall — Running Journal / Changelog

> **Purpose:** Running log of ALL changes, updates, decisions, and discoveries across every session.  
> **Rule:** Every agent session MUST append to this file before ending. No exceptions.  
> **Mirror:** A matching note should also be added to the NotebookLM notebook (`512ab6e1-87a7-4960-90dc-744c26d766ea`).

---

## 2026-03-22 — Advanced WebAR Features (evening)

**Agent:** Antigravity (Gemini)  
**Branch:** `feature/advanced-webar`  
**What was done:**
- Researched Lightship ARDK 4.0, ARCore Geospatial, 8th Wall XR8 APIs for WebAR-feasible features
- Created comprehensive analysis report filtering native-only vs browser-compatible features
- Implemented **5 advanced WebAR features** (11 files, 2,316 lines):

| # | Feature | Files Created |
|---|---------|---------------|
| 1 | **Semantic Segmentation** | `XR8SemanticLayer.cs`, `XR8SemanticLib.jslib`, `SkyReplacement.shader` |
| 2 | **AR NavMesh** | `XR8ARNavMesh.cs` (uses XR8WorldTracker surfaces → Unity NavMesh baking) |
| 3 | **Depth Occlusion** | `XR8DepthOcclusion.cs`, `XR8DepthLib.jslib`, `DepthOcclusion.shader` |
| 4 | **Hand Tracking** | `XR8HandTracker.cs`, `XR8HandLib.jslib` (21-point landmarks + gestures) |
| 5 | **VPS** | `XR8VPSTracker.cs`, `XR8VPSLib.jslib` (wayspot location AR) |

**Architecture notes:**
- All components follow existing SendMessage/jslib bridge pattern
- XR8ARNavMesh integrates with `XR8WorldTracker.ActiveSurfaces` + `OnSurfaceDetected`
- XR8VPSTracker integrates with existing `XR8GPSTracker` for coarse→fine positioning
- Hand tracking uses MediaPipe 21-point standard with gesture detection (pinch, point, fist, open palm, peace, thumbs up)
- Depth occlusion has ground-plane fallback for non-depth devices

**Files created:** `XR8SemanticLayer.cs`, `XR8SemanticLib.jslib`, `SkyReplacement.shader`, `XR8ARNavMesh.cs`, `XR8DepthOcclusion.cs`, `XR8DepthLib.jslib`, `DepthOcclusion.shader`, `XR8HandTracker.cs`, `XR8HandLib.jslib`, `XR8VPSTracker.cs`, `XR8VPSLib.jslib`  
**Committed:** `e285c96` on `feature/advanced-webar`  
**Next steps:** Test features in WebGL build, merge to main when stable

---

## 2026-03-22 — InputSystem Fix (afternoon, follow-up)

**Agent:** Antigravity (Gemini)  
**What was done:**
- Fixed `InvalidOperationException` spam — all 3 desktop preview files used legacy `Input.*` API
- `XR8WorldTracker.Update_EditorDebug()` — 14 `Input.GetKey` → `Keyboard.current`
- `XR8Manager.DesktopPreviewUpdateLoop/HandleInput` — mouse + keys → `Mouse/Keyboard.current`
- `XR8FaceTracker.PreviewHandleInput/UpdateFace` — keys + mouse → `Mouse/Keyboard.current`
- Repositioned test scene objects (camera moved from z=-10 to z=-3, objects scaled up)
- Wired XR8Camera Component reference on XR8Manager

**Files changed:** `XR8WorldTracker.cs`, `XR8Manager.cs`, `XR8FaceTracker.cs`  
**Known issues:** Only CS0414 warnings remain (unused fields, harmless)  
**Next steps:** Press Play and test desktop preview controls (WASD, mouse, scroll)

---

## 2026-03-22 — AG Bridge Setup for Remote Phone Control (afternoon)

**What:** Set up AG Bridge (Mario4272/ag_bridge v0.6.5) for remote phone control of Antigravity sessions.

**Changes:**
- Cloned `ag_bridge` repo into project folder
- Ran `npm install` (154 packages)
- Created `launch_ag_bridge.bat` — dual-launcher for Antigravity + AG Bridge with lid-close prevention
- Tailscale not installed yet — download link provided

**Files:**
- `ag_bridge/` — AG Bridge clone
- `launch_ag_bridge.bat` — One-click launcher

---

## 2026-03-22 — Session: Feature Parity Comparison & Gap Fixes (morning)

**Agent:** Antigravity (Gemini)  
**What was done:**
- Cloned both Imaginary Labs repos (`Ar-World-Template`, `Ar-Image-Template`)
- Completed line-by-line feature parity comparison (world + image tracking)
- **World tracking:** 22 features at full parity, 4 medium gaps found
- **Image tracking:** Near-perfect parity; XR8 has improvements (anchor system, quality presets)
- **Combined tracker:** `XR8CombinedTracker.cs` already fully supports Image+World together

**Gap fixes implemented:**
1. **InputSystem dual support** — `XR8WorldTracker.cs` orbit code + all 4 interaction scripts (`XR8PinchToScale`, `XR8SwipeToRotate`, `XR8TapToReposition`, `XR8TwoFingerPan`) now have `#if ENABLE_INPUT_SYSTEM` branches
2. **Tracking confidence event** — `OnTrackingConfidence(float)` + JS callback in `XR8WorldTracker.cs`
3. **3DOF fallback** — `fallbackToThreeDOFOnLost` auto-switches to gyro when SLAM surfaces are lost
4. **Vertical plane mode** — `PlaneMode` enum (`Horizontal`/`Vertical`), sent to JS as `PLANE_MODE`
5. **TextureExtractor** — New `XR8TextureExtractor.cs` ported from IL's `TextureExtractor_WarpedImage`

**Files modified:** `XR8WorldTracker.cs`, `XR8PinchToScale.cs`, `XR8SwipeToRotate.cs`, `XR8TapToReposition.cs`, `XR8TwoFingerPan.cs`, `XR8WebAR.Runtime.asmdef`  
**Files created:** `XR8TextureExtractor.cs`

---

## 2026-03-22 — Session: Project Scan & Journal Setup

**Agent:** Antigravity (Gemini)  
**What was done:**
- Scanned all project notes, code, brain files, and git history
- Traced Imaginary Labs influence across 6 files (see below)
- Verified NotebookLM notebook: 105 sources, 8 notes, all healthy
- Created this CHANGELOG.md as the running journal
- Added Rule #11 (Running Journal) to `.agents/rules.md`

**Imaginary Labs influence map:**
| File | What was borrowed |
|------|-------------------|
| `XR8WorldTracker.cs` | 3DOF/6DOF/Orbit architecture, JSON settings format |
| `XR8PlacementIndicator.cs` | Placement indicator system |
| `XR8GPSTracker.cs` | GPS-based AR tracking system |
| `XR8GPSPin.cs` | GPS pin/POI system |
| `XR8ImageTargetFactory.cs` | Image target creation workflow |
| `xr8-bridge.js` | Replaced Imagine WebAR's `arcamera.js` + `itracker.js` |
| `ProjectSettings.asset` | `productName: Ar-Image-Template` (original fork name) |

**Source repos:**
- Image tracking origin: `https://github.com/hoodtronik/Ar-Image-Template.git`
- World tracking origin: `https://github.com/hoodtronik/Ar-World-Template.git`

---

## 2026-03-21 — Session: XR8 Audit & Modernization (evening)

**What was done:**
- Full codebase audit — found and fixed 10 critical/medium issues
- Fixes: duplicate jslib symbol, return type mismatch, editor guard, shader null checks, GC allocation, material leak, boolean marshalling, missing JS bridge functions, multi-touch, transform preservation
- Kicked off deep research: "Modern WebAR optimization techniques (2025-2026)"
- Added 11 new sources from deep research to NotebookLM
- Created post-audit report note in NotebookLM

**Remaining low-priority items:**
- `XR8CombinedTracker.cs` — redundant child search loop
- `XR8TapToReposition.cs` — incomplete reposition flow
- `XR8MeshOptimizer.cs` — `isStatic = true` at runtime doesn't trigger batching

---

## 2026-03-21 — Session: Reallusion Unity Integration (afternoon)

**What was done:**
- Researched Reallusion Auto Setup for Unity plugin
- Documented full CC4 → Unity → Convai WebAR pipeline
- Added pipeline note to NotebookLM
- InstaLOD mesh optimization documented (10K-15K tri targets)

---

## 2026-03-21 — Session: Plugin Docs & YouTube Tutorials (morning)

**What was done:**
- Scraped documentation for 8 plugins: Mesh Baker, Mantis LOD, Mesh Animator, GPU Instancer, DOTween Pro, Amplify Shader, NaughtyAttributes, Animation Converter
- Found and added YouTube tutorial videos for all 8 plugins
- Total sources in NotebookLM: 90+ (now 105 after later sessions)

---

## 2026-03-20 — Session: AR Scene Templates & Agent Workflow

**What was done:**
- Created `XR8SceneGenerator.cs` — 19 AR scene template generator
- Categories: Gallery, Portals, Interactive, Product Viz, Events
- Created `XR8OptimizeScene.cs` — WebGL scene analyzer
- Created `XR8MeshOptimizer.cs` — runtime optimization
- Created `XR8ARCrowd.cs` — crowd spawning with VAT + GPU Instancer
- Created `XR8TweenFX.cs` — DOTween AR effects library
- Set up dual repo: public (origin) + private Pro (pro)
- Established agent rules (`.agents/rules.md`) with 10 hard rules
- Created `/unity-xr8-connect` workflow
- All committed and pushed

---

## 2026-03-20 — Session: Setup Wizard, Templates, Portal Shaders

**What was done:**
- Created `XR8SetupWizard.cs` — one-click setup with Validate + Build tabs
- Created `XR8SceneTemplates.cs` — 5 pre-built scene templates
- Created portal shaders: `PortalMask.shader`, `PortalInterior.shader`
- Created `XR8CombinedTracker.cs` — Image + World combined tracking
- Created `ImageTrackabilityAnalyzer.cs` — image scoring 0-100
- Created `XR8TrackerSettings.cs` — quality presets
- All merged to main

---

## Pre-2026-03-20 — Original Build Sessions

**What was done (cumulative):**
- Built core addon from scratch: XR8Camera, XR8ImageTracker, XR8Manager
- Replaced Imagine WebAR's obfuscated code with transparent `xr8-bridge.js`
- Added face tracking with expressions, landmarks, 6 attachment points
- Added world tracking with 6DOF/3DOF/Orbit modes
- Added Gaussian Splat pipeline (WebGL-compatible, CPU depth sort)
- Added Convai AI character integration (WebRTC lip-sync)
- Custom inspectors, scene gizmos, menu items
- Desktop preview mode
- All feature branches merged to main
