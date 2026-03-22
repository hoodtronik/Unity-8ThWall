# Hard Rules for XR8WebAR Project

## 1. Brain Location
**ALWAYS** read `.agents/brain/walkthrough.md` before starting any work on this project. It contains the full architecture, file map, current state, and known issues.

## 2. No 8th Wall App Keys
The 8th Wall engine is **self-hosted**. There are NO app keys, NO cloud project IDs, NO 8th Wall console accounts. Never ask for or try to configure an 8th Wall app key. The engine binary is at `Assets/WebGLTemplates/8thWallTracker/xr8.js`.

## 3. Branch Workflow
Create feature branches for new work. Build, test, and merge to `main` when stable:
```
git checkout -b feature/my-feature
# ... work ...
git checkout main && git merge feature/my-feature
```

## 4. Unity Version
Use **Unity 6 LTS** (6003.1.9f1). Do not upgrade without user approval.

## 5. Assembly Definitions
- Runtime: `Assets/XR8WebAR/Runtime/XR8WebAR.Runtime.asmdef`
- Editor: `Assets/XR8WebAR/Editor/XR8WebAR.Editor.asmdef`
Editor scripts MUST go in the Editor folder. Runtime scripts MUST NOT reference UnityEditor.

## 6. JS Bridge Pattern
All C#↔JS communication uses:
- `.jslib` files with `DllImport("__Internal")` on the C# side
- `window.XR8*Bridge` classes in `xr8-bridge.js`
- `SendMessage()` from JS to Unity
- CSV-formatted strings for data transfer

## 7. Image Targets
Image target files go in `Assets/image-targets/`. The editor auto-discovers JSON files there. Never hardcode target IDs — always read from the JSON.

## 8. Update the Brain
When finishing a session, **always update** `.agents/brain/walkthrough.md` with what was done and what's next.

## 9. Update NotebookLM
**ALWAYS** update the NotebookLM notebook after every new feature, fix, or significant change. Use the `nlm` CLI or MCP tools:
```
nlm source add text --notebook "Unity-8thWall XR8WebAR" --title "<description of change>" --text "<detailed notes>"
```
Notebook ID: `512ab6e1-87a7-4960-90dc-744c26d766ea`
Include: what changed, why, files affected, and any new known issues.

## 10. Triple-Capture Knowledge Pattern
For **every significant thing learned** (new technique, pattern, fix, or integration), do ALL THREE:
1. **Skill/Guide** — Create or update a skill in `.agents/skills/` (or workflow in `.agents/workflows/`)
2. **Brain** — Update `.agents/brain/walkthrough.md` with the new knowledge
3. **NotebookLM** — Add a source to the `Unity-8thWall XR8WebAR` notebook documenting it

This ensures knowledge is never lost between sessions and can be retrieved via RAG.

## 11. Running Journal (CHANGELOG)
**EVERY session MUST** update the running journal before ending. No exceptions.

### Where:
1. **`.brain/CHANGELOG.md`** — Append a new dated entry at the top (below the header)
2. **NotebookLM** — Add/update a note titled `📌 Session Progress — [DATE]` in notebook `512ab6e1-87a7-4960-90dc-744c26d766ea`

### What to include:
- Date and time
- Agent name (e.g., "Antigravity (Gemini)", "Cursor", etc.)
- Bullet list of everything done this session
- Files created, modified, or deleted
- Any new issues discovered
- What's next / what was left unfinished
- Any decisions made and why

### Format (CHANGELOG.md):
```markdown
## YYYY-MM-DD — Session: [Brief Title]

**Agent:** [Agent Name]  
**What was done:**
- Item 1
- Item 2

**Files changed:** `file1.cs`, `file2.js`  
**Known issues:** ...  
**Next steps:** ...
```

This journal is the **source of truth** for project continuity. When conversations get erased, this file + NotebookLM preserve the full history.
