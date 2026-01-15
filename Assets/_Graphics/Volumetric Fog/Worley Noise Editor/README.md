# Worley Noise Editor (Unity 6)

A Unity 6 **Editor Extension** for generating and exporting 3D Worley noise textures.  
This tool is designed primarily for **volumetric rendering** such as **clouds, fog, and atmospheric effects**.

## 📂 Project Structure

- **Editor/**
    - `WorleyNoiseEditorWindow.cs` → Editor UI.
    - `WorleyNoiseEditor.cs` → Core noise generation logic.
    - `EditorSettings.cs` → Stores editor preferences & references.
    - `Saver3D.cs` → Exports generated `RenderTexture` to `Texture3D`.
    - `RenderTextureExtension.cs` → Helper for converting textures.
    - `WorleyNoiseSettings.cs` / `Preset.cs` → Noise parameters & presets.
- **ComputeShaders/**
    - `WorleyNoise.compute` → Core Worley noise generator.
    - `WorleyUtils.compute` → Utilities (clear, slice, etc.).
- **Settings/**
    - Default configuration assets (`Settings.asset`, `WorleyShapeSettings.asset`, `WorleyDetailSettings.asset`).

---

## 🚀 Quick Start

1. **Open the Editor**  
   `Tools → Worley Noise Editor`

2. **Shaders Setup**
    - `WorleyNoise.compute` and `WorleyUtils.compute` are required.
    - If not auto-assigned, drag them into the *Editor Settings* foldout.

3. **Configure Noise**
    - Select `Texture Type` → *Shape* or *Detail*.
    - Select active **channel** → R / G / B / A.
    - Set **resolution** (8–256, multiple of 8).
    - Adjust settings via `WorleyNoiseSettingsPreset` (grid sizes, seed, persistence, invert, tiling).

4. **Generate**
    - Click **Generate** to compute 3D noise.
    - Use the **Slice** slider to scroll through the volume.
    - Toggle *Greyscale* / *Show All Channels* for different views.

5. **Export**
    - Click **Export** → Save as `.asset` inside your Unity `Assets/` folder.
    - The saved `Texture3D` can now be used in materials, shaders, or custom rendering features.

---

## ❓ Q&A

**Q: Nothing shows in the preview.**  
A: Check that both compute shaders are assigned in *Editor Settings*. Then click **Generate**.

**Q: Can I use the texture at runtime?**  
A: Yes. Exported `.asset` files are standard Unity `Texture3D` assets.

**Q: Why does resolution need to be a multiple of 8?**  
A: Compute shaders dispatch in 8×8×8 thread groups. Non-multiples would cause errors.

**Q: Can each channel (RGBA) be unique?**  
A: Yes. Each channel has independent Worley parameters via presets.

---

## ⚠️ Known Limitations

- No undo/redo integration for parameter changes.
- Temporary `RenderTextures` may persist until Unity refreshes.
- Parameter values in presets are not saved after restarting Unity if you change them through the window.
