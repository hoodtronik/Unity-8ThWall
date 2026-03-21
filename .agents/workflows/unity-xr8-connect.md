---
description: How to connect Unity MCP and set up the XR8WebAR project for WebAR development
---

# Unity XR8WebAR — Connection & Setup Workflow

## IMPORTANT: Repository Setup

This project has TWO git remotes:
- **`origin`** = `https://github.com/hoodtronik/Unity-8ThWall.git` ← PUBLIC (free, no plugins)
- **`pro`** = `https://github.com/hoodtronik/Unity-8ThWall-Pro.git` ← PRIVATE (with premium plugins)

**PRIMARY DEVELOPMENT REPO: `pro` (Unity-8ThWall-Pro)**

When committing and pushing:
```bash
# Always push to the Pro repo for active development
git push pro main

# Only push to origin (public) for open-source releases
# Make sure plugin folders are in .gitignore (they already are)
git push origin main
```

## Installed Premium Plugins (Pro Version)

| Plugin | Folder | Namespace/API |
|--------|--------|---------------|
| Mesh Baker | `Assets/MeshBaker/` | `MB3_MeshBaker`, `MB3_TextureBaker` |
| Mantis LOD Editor Pro | `Assets/MantisLODEditor/` | `Mantis.LODEditor` |
| Mesh Animator | `Assets/MeshAnimator/` | `MeshAnimator` |
| GPU Instancer (Crowd) | `Assets/GPUInstancer/` | `GPUInstancer` |
| Amplify Shader Editor | `Assets/AmplifyShaderEditor/` | Visual editor |
| DOTween Pro | `Assets/Plugins/Demigiant/` | `DG.Tweening` |
| Animation Converter | `Assets/AnimationConverter/` | Editor tool |
| NaughtyAttributes | `Assets/NaughtyAttributes/` | `[Button]`, `[ShowIf]` |

## Step 1 — Verify Unity MCP Connection

// turbo
```bash
# Check if Unity MCP server is connected
# If not, open Unity MCP Tools window in Unity Editor
```

Use the `manage_editor` tool with `telemetry_ping` action to verify connection.

## Step 2 — Read the Skill Guide

Before doing ANY work on this project, read the skill file:

```
.agents/skills/unity-xr8-webar/SKILL.md
```

This contains the complete API reference, component list, plugin info, and important rules (like NO APP KEYS).

## Step 3 — Check Scene State

Use `manage_scene` with `get_hierarchy` to understand the current scene.
Use `read_console` to check for any errors.

## Step 4 — Development

// turbo-all

All pushes go to `pro` remote by default:
```bash
git add .
git commit -m "your message"
git push pro main
```

Only push to `origin` (public) for open-source releases — ensure plugins are gitignored.
