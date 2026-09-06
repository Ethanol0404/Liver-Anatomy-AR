# Patient Whole-Liver Import Design

## Goal

Export the existing `whole` liver segment from the Python workflow and load it in Unity as the initial patient view. Imported patients must retain separate Couinaud segments, blood vessels, and tumor structures for their existing menus and interactions.

## Export Contract

The Python exporter will look for the existing segment named `whole` and export it as `WholeLiver.obj`. It will add this metadata entry before converting the export folder into `patient.glb`:

```json
{
  "name": "WholeLiver",
  "file": "WholeLiver.obj",
  "role": "whole_liver",
  "id": "WholeLiver",
  "displayName": "Whole Liver"
}
```

`patient.glb` will therefore contain a `WholeLiver` node alongside the nine Couinaud segments, optional vessels, and optional tumor. The exporter will not merge segment geometry. If the source `whole` segment is unavailable, export continues with a warning and Unity falls back to the existing segment view.

## Unity Import Behaviour

Unity will recognise the `whole_liver` role and `WholeLiver` name as the `WholeLiver` category. After a successful import:

- `WholeLiver` is visible.
- Couinaud segment parts are hidden.
- Blood vessels remain visible according to the export and their own menu controls.
- Tumor remains hidden by default.

Opening `Couinaud Segments` hides `WholeLiver` and shows the imported segment parts. Returning to the normal whole-liver view restores `WholeLiver` and hides the segment parts. Imports without `WholeLiver` retain the existing segment-first fallback.

## Selection Outline

The selection outline must be owned by the selected `AnatomyPart`. It must be disabled when that part is deselected, hidden, replaced, or destroyed. This avoids white contour fragments remaining after an imported part changes visibility.

## Opacity Panel

The opacity panel will contain a title, slider, reset button, and close button in distinct anchored regions. It will show the selected structure name and initialise the slider from that structure's current opacity. Moving the slider changes only the selected part.

## Information Panel

The study information panel will reserve a title header inside its top boundary, then place the scroll viewport below it. The viewport clips its text so content never renders above the panel or over the title. The compact double-tap panel remains separate.

## Verification

- Python tests verify `WholeLiver` metadata and GLB node output.
- Unity EditMode tests verify whole-liver classification and initial imported visibility.
- Unity EditMode tests verify an outline is disabled when a part is hidden or deselected.
- Unity compile/build verification confirms the modified scripts compile.
- Android manual check imports a fresh export containing `WholeLiver`, then confirms the initial whole-liver view, segment switch, selection clearing, opacity adjustment, and panel clipping.
