---
description: How to connect Unity MCP and set up the XR8WebAR project for WebAR development
---

# Unity XR8WebAR — Connection & Setup Workflow

## ⛔ HARD RULES — ALL AGENTS MUST FOLLOW

### Rule 1: NotebookLM is the Project RAG Memory
**Notebook:** "Unity-8thWall XR8WebAR" (ID: `512ab6e1-87a7-4960-90dc-744c26d766ea`)

Before starting any work, query NotebookLM to refresh your context:
```
notebook_query(notebook_id="512ab6e1-87a7-4960-90dc-744c26d766ea", query="current project state, what was done last session, what needs to be done")
```

**Where to find what in NotebookLM:**
| What | Where | How to Access |
|------|-------|---------------|
| Plugin capabilities & API details | Note: "🔌 Plugin Capabilities & XR8 Power Components Reference" | `notebook_query` |
| AR scene ideas & inspiration | Note: "🎨 AR Art & Interactive Experience Ideas" | `notebook_query` |
| Assets needed for templates | Note: "🛒 Scene Templates — Additional Assets Needed" | `notebook_query` |
| Session progress & history | Note: "📌 Session Progress" (latest date) | `notebook_query` |
| Plugin documentation | Sources (scraped docs for each plugin) | `notebook_query` |
| Plugin video tutorials | Sources (YouTube tutorials) | `notebook_query` |
| AR research & trends | Sources (83 deep research results) | `notebook_query` |
| Project architecture | Source: "XR8WebAR Project Architecture" | `notebook_query` |

**At the END of every session, you MUST:**
1. Create a new note titled "📌 Session Progress — [DATE TIME]" summarizing what was done
2. Update the task.md artifact in the .brain folder

### Rule 2: Save .brain Folder in the Project
**The .brain folder MUST be saved/copied into the project directory** so other agents can find it:
```
G:\_AR_Projects\Unity-8ThWall\.brain\
```
This folder contains task.md, implementation plans, and session state. Copy the latest artifacts here.

### Rule 3: Dual Documentation — NotebookLM + Project Files
Always save documentation in BOTH locations:
- **NotebookLM** → for RAG queries, cross-session memory, AI-accessible knowledge
- **Project files** → `.agents/skills/`, `.agents/workflows/`, `.brain/` for local agent access

### Rule 4: Repository Setup
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

## Step 2 — Refresh Context from NotebookLM

Before doing ANY work, run these queries:
```
1. notebook_query → "current project state and what was done last session"
2. notebook_query → "what needs to be done next, to-do list"
3. Read .brain/task.md for the master task tracker
```

## Step 3 — Read the Skill Guide

Read the skill file for full API reference:
```
.agents/skills/unity-xr8-webar/SKILL.md
```

## Step 4 — Check Scene State

Use `manage_scene` with `get_hierarchy` to understand the current scene.
Use `read_console` to check for any errors.

## Step 5 — Development

// turbo-all

All pushes go to `pro` remote by default:
```bash
git add .
git commit -m "your message"
git push pro main
```

Only push to `origin` (public) for open-source releases — ensure plugins are gitignored.

## Step 6 — End of Session

1. Save session note to NotebookLM (see Rule 1)
2. Update `.brain/task.md` with progress
3. Commit and push to `pro`
