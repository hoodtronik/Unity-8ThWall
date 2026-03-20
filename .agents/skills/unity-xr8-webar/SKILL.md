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

### Manual Setup (Alternative)
1. `GameObject > XR8 WebAR > Complete AR Scene Setup`
2. In `XR8Manager` inspector, check tracking modes (Image, World, Face)
3. In `XR8ImageTracker` inspector, add targets
4. Wire references manually

## Editor Tools Menu

| Menu Item | Purpose |
|---|---|
| `XR8 WebAR > Quick Setup Wizard` | One-click scene setup, validation, and build |
| `XR8 WebAR > Import Gaussian Splat` | Drag-drop .ply/.splat → ready prefab |
| `XR8 WebAR > Image Trackability Analyzer` | Score images 0-100 for tracking quality |
| `XR8 WebAR > Build WebGL` | Quick build shortcut |

## Key Components

| Script | Purpose |
|---|---|
| `XR8Manager` | Unified controller — enable tracking modes, desktop preview |
| `XR8Camera` | Camera feed background rendering |
| `XR8ImageTracker` | Image target tracking with quality presets |
| `XR8FaceTracker` | Face tracking with expressions, landmarks, attachments |
| `XR8WorldTracker` | SLAM world tracking and surface detection |
| `XR8VideoController` | Video playback on targets |
| `XR8TrackerSettings` | Quality presets (Performance/Balanced/Quality) |
| `GaussianSplatRenderer` | WebGL-compatible Gaussian Splat rendering |
| `GaussianSplatLoader` | PLY/splat file parser (Mobile-GS compatible) |

## Tracking Quality Presets

In `XR8ImageTracker` inspector > Tracker Settings > Tracking Quality:
- **Performance** — Raw poses, no smoothing (fast targets, lowest latency)
- **Balanced** — Moderate smoothing (default, good for most use cases)
- **Quality** — Heavy smoothing (stationary targets, buttery smooth)
- **Manual** — Check "Manual Smoothing" to set position/rotation values independently

## Desktop Preview Controls

Enable `Desktop Preview` on XR8Manager, then Play:

| Key | Action |
|-----|--------|
| `T` | Toggle tracking on/off |
| `Tab` | Cycle through image targets |
| `Mouse Drag` | Move target position |
| `Scroll Wheel` | Adjust target distance |
| `R` | Reset target position |
| `Esc` | Lose tracking |

### Face Tracking Preview
| Key | Action |
|-----|--------|
| `F` | Toggle face found/lost |
| `1-5` | Expression presets (neutral/smile/surprise/wink/talk) |
| `Right-Click Drag` | Move face position |

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
- Shaders: `Assets/XR8WebAR/Runtime/Shaders/`
- JS plugins: `Assets/XR8WebAR/Runtime/Plugins/`
- Editor tools: `Assets/XR8WebAR/Editor/`
- JS bridge: `Assets/WebGLTemplates/8thWallTracker/xr8-bridge.js`
- HTML template: `Assets/WebGLTemplates/8thWallTracker/index.html`

## IMPORTANT: No App Keys

8th Wall engine is self-hosted. **There are NO app keys, NO cloud project IDs.** The engine binary lives at `Assets/WebGLTemplates/8thWallTracker/xr8.js`. Never ask for or try to configure an 8th Wall app key.
