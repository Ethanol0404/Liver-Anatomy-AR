# Patient Whole-Liver Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export an existing whole-liver segment into the patient GLB and make Unity open imported patients in a whole-liver view with reliable selection, opacity, and information UI.

**Architecture:** The Python exporter remains the single source of patient model metadata. It adds an optional `WholeLiver` model entry and its GLB converter includes it as a named node. Unity maps that entry to its existing `WholeLiver` category and switches between whole and Couinaud views without affecting vessels or tumor. UI corrections remain within the existing runtime-panel builder.

**Tech Stack:** Python 3, 3D Slicer exporter, OBJ, glTF 2.0 GLB, Unity 6, C#, URP, Unity UI, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-09-06-patient-whole-liver-import-design.md`

## Global Constraints

- Export the existing source segment called `whole`; do not merge Couinaud meshes.
- Write `WholeLiver.obj`, add a `WholeLiver` GLB node, and use metadata role `whole_liver`.
- Missing whole-liver input must not prevent export; Unity must retain its segment-view fallback.
- Imported patients begin with Whole Liver visible, segments hidden, tumor hidden, and vessel visibility unchanged when `WholeLiver` exists.
- Selection outlines must never remain after a part is hidden, deselected, or destroyed.
- The compact double-tap information panel stays separate from the study information panel.

---

### Task 1: Export the existing whole-liver segment

**Files:**
- Modify: `C:/Users/xspang/Desktop/FYP_python/LiverARExporter/Lib/ModelExporter.py`
- Modify: `C:/Users/xspang/Desktop/FYP_python/LiverARExporter/LiverARExporter.py`
- Modify: `C:/Users/xspang/Desktop/FYP_python/tests/test_liver_ar_exporter.py`

**Interfaces:**
- Produces: metadata model `{name: "WholeLiver", file: "WholeLiver.obj", role: "whole_liver"}`.
- Produces: a `WholeLiver` node in `patient.glb` through the existing `convert_obj_folder_to_glb` call.
- Consumes: an existing 3D Slicer segment named `whole`, `whole liver`, or `WholeLiver`.

- [ ] **Step 1: Write failing metadata and GLB tests**

```python
def test_normalizes_whole_liver_source_name(self):
    self.assertEqual(canonical_segment_name("whole"), "WholeLiver")
    self.assertEqual(display_name("WholeLiver"), "Whole Liver")

def test_glb_includes_metadata_declared_whole_liver_node(self):
    # Create WholeLiver.obj, write whole_liver metadata, convert to GLB,
    # then assert "WholeLiver" is a node in the GLB JSON chunk.
```

- [ ] **Step 2: Run the focused Python tests and confirm they fail because whole-liver support is absent**

Run: `python -m unittest tests.test_liver_ar_exporter.ModelExporterTest tests.test_liver_ar_exporter.GlbExporterTest`

Expected: the new assertions fail for an unsupported `whole` name and absent expected node.

- [ ] **Step 3: Add canonical whole-liver metadata support**

```python
_CANONICAL_NAMES.update({
    "whole": "WholeLiver",
    "wholeliver": "WholeLiver",
    "liverwhole": "WholeLiver",
})

def display_name(name):
    return {
        "WholeLiver": "Whole Liver",
        # existing mappings...
    }.get(name, name.replace("_", " "))
```

- [ ] **Step 4: Export the existing `whole` segment as an optional model**

```python
SEGMENT_ALIASES = {
    "WholeLiver": ("whole", "whole liver", "whole_liver"),
    # existing aliases...
}

export_names = ["WholeLiver"] + expected_export_names(include_tumor=True) + ["BloodVessels"]
optional_names = {"WholeLiver", "Tumor", "BloodVessels"}
```

For `WholeLiver`, write `role = "whole_liver"`; retain `vessels` for vessel names and `anatomy` for all other exports. Skip an unavailable optional part with a warning.

- [ ] **Step 5: Run the focused Python tests and confirm they pass**

Run: `python -m unittest tests.test_liver_ar_exporter.ModelExporterTest tests.test_liver_ar_exporter.GlbExporterTest`

Expected: PASS, including a separate `WholeLiver` GLB node.

- [ ] **Step 6: Commit the exporter contract**

```bash
git add LiverARExporter/LiverARExporter.py LiverARExporter/Lib/ModelExporter.py tests/test_liver_ar_exporter.py
git commit -m "Export whole liver with patient GLB"
```

### Task 2: Import and switch whole-liver patient views in Unity

**Files:**
- Modify: `Assets/LiverAR/Scripts/RuntimePatientGlbLoader.cs`
- Modify: `Assets/LiverAR/Scripts/AnatomyManager.cs`
- Modify: `Assets/LiverAR/Scripts/ARUIController.cs`
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`

**Interfaces:**
- Consumes: a `PatientModelEntry` with `role == "whole_liver"` or name/id `WholeLiver`.
- Produces: `RuntimePatientGlbLoader.ResolveCategory(entry) == AnatomyCategory.WholeLiver`.
- Produces: `AnatomyManager.ShowWholeLiverOverview()` only when a whole-liver part is registered.

- [ ] **Step 1: Write failing Unity EditMode tests**

```csharp
[Test]
public void WholeLiverMetadataUsesWholeLiverCategory()
{
    var entry = new PatientModelEntry { Name = "WholeLiver", Role = "whole_liver" };
    Assert.That(RuntimePatientGlbLoader.ResolveCategory(entry), Is.EqualTo(AnatomyCategory.WholeLiver));
}

[Test]
public void WholeLiverOverviewLeavesVesselsVisibleAndHidesSegments()
{
    // Register WholeLiver, Segment I, and Blood Vessels; then verify overview visibility.
}
```

- [ ] **Step 2: Run the Unity test assembly or project build and confirm the new tests fail**

Run: `dotnet build "Liver AR.sln" --no-restore`

Expected: compile failure until `ResolveCategory` recognises `whole_liver`, or test failure in Unity EditMode.

- [ ] **Step 3: Classify and initialise imported Whole Liver**

```csharp
if (string.Equals(entry.Role, "whole_liver", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(entry.Name, "WholeLiver", StringComparison.OrdinalIgnoreCase))
    return AnatomyCategory.WholeLiver;
```

After registering the model, detect whether the manager has a whole-liver part. Call `ShowWholeLiverOverview()` only in that case; otherwise retain current `ShowAll()` fallback. Apply the lesion-hidden rule afterwards.

- [ ] **Step 4: Ensure the segment menu always enters segment view**

```csharp
public void OpenCouinaudSegmentsPanel()
{
    CurrentAnatomyManager?.ShowLiverSegments();
    RebuildSegmentToggles();
    SetNavigationPanel(couinaudSegmentsPanel);
}
```

Add a `ShowWholeLiverOverview` UI method for the model view only if the existing model menu has an appropriate return action; do not add another top-level menu item.

- [ ] **Step 5: Run verification**

Run: `dotnet build "Liver AR.sln" --no-restore`

Expected: build succeeds. In Unity Test Runner, run `LiverAR.EditorTests` and confirm the new EditMode tests pass.

- [ ] **Step 6: Commit the Unity whole-liver import behaviour**

```bash
git add Assets/LiverAR/Scripts/RuntimePatientGlbLoader.cs Assets/LiverAR/Scripts/AnatomyManager.cs Assets/LiverAR/Scripts/ARUIController.cs Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs
git commit -m "Show whole liver first for patient imports"
```

### Task 3: Make the selection outline follow part lifecycle

**Files:**
- Modify: `Assets/LiverAR/Scripts/AnatomyPart.cs`
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`

**Interfaces:**
- Produces: outline objects parented under their owning `AnatomyPart`.
- Produces: `SetVisible(false)` and `SetSelected(false)` deactivate every outline object.

- [ ] **Step 1: Write a failing lifecycle test**

```csharp
[Test]
public void HidingSelectedPartDisablesAllSelectionOutlineRenderers()
{
    var part = CreatePartWithMesh("segment-i");
    part.SetSelected(true);
    part.SetVisible(false);
    Assert.That(part.GetComponentsInChildren<Renderer>(true)
        .Where(renderer => renderer.gameObject.name == "Selection Outline")
        .All(renderer => !renderer.enabled || !renderer.gameObject.activeSelf), Is.True);
}
```

- [ ] **Step 2: Run the focused EditMode test and confirm it exposes the retained-outline behaviour**

Run: Unity Test Runner, `LiverARLogicTests.HidingSelectedPartDisablesAllSelectionOutlineRenderers`.

Expected: FAIL before the outline is parented to the owning part.

- [ ] **Step 3: Parent each generated outline to the `AnatomyPart` and clean it up**

Create the outline under the component transform, copy its world transform after parenting, and on `OnDestroy` destroy any generated outline objects. Do not traverse or duplicate a child whose name is `Selection Outline`.

- [ ] **Step 4: Run the focused test and all Unity logic tests**

Run: Unity Test Runner, `LiverAR.EditorTests`.

Expected: PASS with no remaining outline child active after deselection or hiding.

- [ ] **Step 5: Commit the outline lifecycle fix**

```bash
git add Assets/LiverAR/Scripts/AnatomyPart.cs Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs
git commit -m "Prevent stale anatomy selection outlines"
```

### Task 4: Repair opacity and information-panel layout

**Files:**
- Modify: `Assets/LiverAR/Scripts/ARUIController.cs`
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`

**Interfaces:**
- Consumes: current selected `AnatomyPart` and its `Opacity`.
- Produces: an opacity panel whose controls remain inside non-overlapping anchor rectangles.
- Produces: a study information panel with an internal header and a masked content viewport below it.

- [ ] **Step 1: Write failing layout and selection tests**

```csharp
[Test]
public void OpeningOpacityPanelSetsSliderToSelectedPartOpacity()
{
    var part = CreatePart("segment-i", "Segment I");
    part.SetOpacity(0.42f);
    // Select part, open panel, assert slider.value == 0.42f.
}

[Test]
public void StudyInformationHeaderAndViewportDoNotOverlap()
{
    // Render study information and assert viewport.anchorMax.y <= header.anchorMin.y.
}
```

- [ ] **Step 2: Run the focused EditMode tests and confirm they fail**

Run: Unity Test Runner, `LiverARLogicTests.OpeningOpacityPanelSetsSliderToSelectedPartOpacity` and `LiverARLogicTests.StudyInformationHeaderAndViewportDoNotOverlap`.

Expected: FAIL against the existing overlapping anchor layout.

- [ ] **Step 3: Rebuild the runtime opacity panel anchors**

Use a title region `(0.08, 0.72)` to `(0.92, 0.92)`, slider region `(0.08, 0.45)` to `(0.92, 0.61)`, and bottom buttons from `y = 0.12` to `0.30`. In `OnSelectionChanged`, set the slider with `SetValueWithoutNotify(selected.Opacity)` before changing the title.

- [ ] **Step 4: Reserve a header above the study panel scroll viewport**

Create the study title at `(0.06, 0.88)` to `(0.94, 0.96)` and its `RectMask2D` viewport at `(0.06, 0.20)` to `(0.94, 0.84)`. Keep Back and Close at the existing bottom button region. Remove the duplicate title inside the scroll content.

- [ ] **Step 5: Run the focused and full verification**

Run: `dotnet build "Liver AR.sln" --no-restore`

Expected: build succeeds. In Unity Test Runner, `LiverAR.EditorTests` passes.

- [ ] **Step 6: Commit the panel fixes**

```bash
git add Assets/LiverAR/Scripts/ARUIController.cs Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs
git commit -m "Fix imported model opacity and information panels"
```

### Task 5: Publish and validate on Android

**Files:**
- Modify: `.git/config` in each repository through `git remote set-url` only.

**Interfaces:**
- Python remote: `https://github.com/Ethanol0404/Liver-AR-Exporter.git`
- Unity remote: `https://github.com/Ethanol0404/Liver-Anatomy-AR.git`

- [ ] **Step 1: Point each origin remote at its canonical repository**

```bash
git -C "C:/Users/xspang/Desktop/FYP_python" remote set-url origin https://github.com/Ethanol0404/Liver-AR-Exporter.git
git -C "C:/Users/xspang/Desktop/FYP_AR/Liver AR" remote set-url origin https://github.com/Ethanol0404/Liver-Anatomy-AR.git
```

- [ ] **Step 2: Push each tested repository**

```bash
git -C "C:/Users/xspang/Desktop/FYP_python" push origin main
git -C "C:/Users/xspang/Desktop/FYP_AR/Liver AR" push origin main
```

- [ ] **Step 3: Build and manually validate Android**

Build a fresh Android APK. Export a patient folder containing `WholeLiver.obj`, `patient.glb`, and `metadata.json`; import it through the app. Confirm whole liver appears first, Couinaud Segments switches to coloured parts, blood vessels and tumor controls work, deselecting/hiding a part leaves no white outline, the opacity slider visibly adjusts the selected part, and information text is clipped inside its panel.
