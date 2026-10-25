# Yennefer DLC original boot panels

Source: `body_05_wa__yennefer_lod0` in the original `Yennefer-dlc-dress.blend`.
The blend is opened read-only; the exporter never saves it.

The old export selected original material 1 (shell) and 8 (straps), omitting
the knee panels in original material 4. That material also contains gloves,
and its texture is named `glove_01_wa__yennefer`. Its lower two components
are knee panels weighted to the original knee/leg bones, not gloves.

Per boot, the restored panel has 110 UV-split vertices and 182 triangles.
The complete boot has 2167 vertices, 3235 triangles and three material slots.
The third slot uses `DLCBootPanels`, derived from `Surface_05` with its original
diffuse and normal maps. Its independent URP reflectance map preserves source
roughness alpha and specular hue on a dielectric F0; it does not change gloves.
The existing 2057 vertices, UVs, named weights and first two source binaries
are unchanged. No replacement anatomy or new panel geometry was generated.

Rebuild in a disposable Unity project first:

1. Run `export_dlc_yennefer_boots.py` with Blender, optionally passing
   `-- --output <project>/Assets/ThirdParty/WitcherYenneferDLC/DLCBoots`.
2. For an older 2057-vertex boot fold, run `extend_dlc_boot_shape.py`
   with Blender and `-- --project <project>`. This preserves every old fold
   delta and transfers nearby existing surface deformation to the added panel.
3. Run `Homecoming.Editor.YenneferOriginalBoots.RepairPrefab` in Unity.
   It updates the worn boots, ground props and four material lists in place.
   Existing mesh asset GUIDs remain stable.

Boot material adaptation: run `prepare_yennefer_boot_reflectance.py --project
<project>` with Python/Pillow, then
`Homecoming.Editor.YenneferBootMaterials.RepairPrefab` in Unity. This avoids
treating REDengine's raw specular tint as URP absolute reflectance. Leather
normal strength and gloss are calibrated for this scene; diffuse pixels remain
unchanged. Full costume imports reapply these boot settings at the end.

Validated in Unity 6000.6.3f1: original positions, UVs and named weights of
the first 2057 vertices; both source panels; standing and crossed-knee views;
600 consecutive Play Mode GPU frames including both removals and standing up.
Local evidence: `Artifacts/BootRiseReview-20261022/`.

This restores omitted geometry. It does not certify that foot/boot intersections
throughout extraction or all hand contacts have been eliminated.
