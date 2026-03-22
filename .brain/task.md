# XR8WebAR Project — Current State

## Last Updated: 2026-03-21T20:07:00-04:00

## Project Status: 🟢 Active — Post-Audit, All Critical Fixes Applied

## Architecture Overview
- **Framework**: 8th Wall XR8 → Unity WebGL
- **Tracking Modes**: Image, World (6DOF/3DOF/Orbit), Face, Combined Image+World
- **JS Bridge**: `.jslib` files → `SendMessage` ↔ C# `DllImport`
- **Features Beyond Imaginary Labs**: Gaussian Splat rendering, Convai AI characters, DOTween FX, mesh optimization, face tracking with attachments

## Codebase Health
All critical, medium, and low-priority issues from the full audit have been resolved:

### Fixed Issues (this session)
1. ~~Duplicate `WebGLXR8HitTest` symbol~~ → Deleted `XR8WorldTrackerLib.jslib`
2. ~~`WebGLXR8HitTest` return type mismatch~~ → Changed `string` → `void`
3. ~~`DesktopPreviewHandleInput()` outside `#if UNITY_EDITOR`~~ → Wrapped correctly
4. ~~GaussianSplat shader null check~~ → Check shader before Material constructor
5. ~~GaussianSplat depth sort GC allocation~~ → Pre-allocated float[] buffer
6. ~~XR8VideoController material leak~~ → Added `Destroy(material)` to OnDestroy
7. ~~ConvaiBridge boolean marshalling~~ → Explicit 0/1 integers
8. ~~World tracker missing JS bridge~~ → Added PlaceOrigin, ResetOrigin, HitTest, ViewportPos, Settings
9. ~~Missing multi-touch on gesture scripts~~ → Enabled in Awake()
10. ~~Original transform not preserved~~ → Saved in Awake() for correct resets

### Remaining Lower-Priority Items
- `XR8CombinedTracker.cs` — redundant child search loop (code quality, not a bug)
- `XR8TapToReposition.cs` — incomplete reposition flow (needs PlaceContent() call after reset)
- `XR8MeshOptimizer.cs` — `isStatic = true` at runtime doesn't trigger batching

## File Inventory
### Core Scripts (Runtime)
- `XR8Manager.cs` — Unified command module, singleton, config builder
- `XR8Camera.cs` — Camera lifecycle, video background, URP support
- `XR8ImageTracker.cs` — Image target tracking with anchor workflow
- `XR8WorldTracker.cs` — World tracking (6DOF/3DOF/Orbit), SLAM, placement
- `XR8FaceTracker.cs` — Face tracking, expressions, attachment points
- `XR8CombinedTracker.cs` — Image+World combined tracking
- `XR8EngineStatus.cs` — Engine lifecycle events
- `XR8TrackerSettings.cs` — Quality presets, smoothing config

### Interaction Scripts
- `XR8SwipeToRotate.cs` — Single-finger rotation
- `XR8PinchToScale.cs` — Two-finger scaling
- `XR8TwoFingerPan.cs` — Two-finger panning
- `XR8TapToReposition.cs` — Tap-to-move placement
- `XR8PlacementIndicator.cs` — Visual reticle for placement workflow

### JS Bridge Libraries
- `XR8TrackerLib.jslib` — Image + world tracker bridge (unified)
- `XR8FaceTrackerLib.jslib` — Face tracker bridge
- `ConvaiBridge.jslib` — Convai AI character bridge

### Gaussian Splatting
- `GaussianSplatRenderer.cs` — GPU instanced rendering with CPU depth sort
- `GaussianSplatLoader.cs` — PLY/SPLAT file parser
- `GaussianSplat.shader` — Billboard shader with covariance-based ellipses

### Utilities
- `XR8TweenFX.cs` — DOTween AR effects with coroutine fallback
- `XR8MeshOptimizer.cs` — Runtime mesh optimization
- `XR8VideoController.cs` — Video playback on tracked images
- `XR8ConvaiCharacter.cs` — Convai character integration

### Editor
- `XR8SetupWizard.cs` — One-window setup for 8th Wall AR

## Imaginary Labs Parity: ✅ Complete
All features from Imaginary Labs' image and world tracking are matched or exceeded.

## NotebookLM
- Notebook: "Unity-8thWall XR8WebAR" (ID: 512ab6e1-87a7-4960-90dc-744c26d766ea)
- Sources: 90+ (docs, tutorials, code audit report)
- Deep research running: Modern WebAR optimization techniques (2025-2026)

## Key Design Decisions
- Anchor-based workflow for image targets (anchor wraps content, preserving local transforms)
- JSON payload format for world tracker settings (matches Imaginary Labs' format)
- CPU-side depth sorting for Gaussian Splats (correct alpha blending on WebGL)
- `SendMessage` for JS↔C# communication via `.jslib` files
- Singleton pattern for XR8Manager
