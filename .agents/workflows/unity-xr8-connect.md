---
description: How to connect Unity MCP and set up the XR8WebAR project for WebAR development
---

# Unity XR8WebAR Connect Workflow

## Prerequisites
- Unity 6 LTS (6003.1.9f1 or later)
- MCP for Unity package installed
- 8th Wall engine binary at `Assets/WebGLTemplates/8thWallTracker/xr8.js`

## Steps

1. Open the Unity project at `Ar-Image-Template-8thWall/`

2. Ensure MCP for Unity is installed (check `Window > MCP for Unity`)

3. If the Unity MCP server isn't connecting, check the config at `~/.gemini/antigravity/mcp_config.json`

4. Verify the project has the XR8WebAR addon at `Assets/XR8WebAR/`

5. Check the scene has the required components:
   - AR Camera Rig (Main Camera + XR8Camera)
   - XR8Manager
   - XR8ImageTracker
   - If missing, use `GameObject > XR8 WebAR > Complete AR Scene Setup`

6. Image targets should be in `Assets/image-targets/`

7. The WebGL template is at `Assets/WebGLTemplates/8thWallTracker/`
   - Set in Player Settings > WebGL > Resolution and Presentation > WebGL Template

## Testing on Phone
// turbo
8. Build WebGL: `File > Build Settings > WebGL > Build`
// turbo
9. Serve locally: `npx serve Build/`
10. Open the URL on your phone (must be same WiFi network)

## Brain Location
The project brain (handoff docs) is at `.agents/brain/` in the project root.
Always read the walkthrough.md there before starting work.
