# Blender character workspace

`Characters.blend` contains all seven original-game character meshes, their deform rigs, UVs, materials, and sampled Unity poses. The current visible collection is Triss. Other characters are in the `Unity_*` collections. Original textures are referenced relative to this repository.

The checked poses are frames 0, 90, 240, 300, 390, 450, 570, 630, 810, 870, 930, 990, 1050, 1110, and 1170 at 30 FPS. Triss and Yennefer include the revised extraction and relaxed standing poses. These are inspection samples; detached-shoe scene objects and Unity's runtime cloth solver are checked in Unity, rather than reproduced by the Blender file. `audit_lower_body.py` checks welded copies of lower-body meshes without changing the source geometry.

## Applied refinements

- Repair weights on verified geometric seams while retaining UV splits and vertex identities.
- Smooth only nearly aligned normals on matching geometric seams, preserving hard edges.
- Recalculate normals on Shani's restored covered upper-arm vertices after their sleeve fitting.
- Preserve original anatomy, footwear/hosiery separation, triangles, UVs, bind poses, and existing shape keys.

`refinements.json` records sparse vertex normal/weight edits. Unity validates each source geometry's SHA256 before applying them. `WitcherCharacterImport` reapplies the checked edits before generating detached shoe proxies, so a future import keeps these refinements.

## Tools

1. Unity: **Homecoming > Blender > 导出全部角色和检查姿势** exports current meshes and pose samples to `Artifacts/BlenderOptimization/Exchange`.
2. Blender: `character_workspace.py` imports this explicit interchange. `refine_seams.py` repairs verified seams and writes the sparse edits.
3. Unity: **Homecoming > Blender > 应用保存的接缝和法线修正** applies reviewed edits.

The current scripts use the isolated `F:\Unity\Project\StartMotionValidation` exchange directory for validation. Set `PROJECT` and `EXCHANGE` when working from another checkout. The `.blend` source lives outside `Assets` to avoid Unity implicitly running Blender during imports or CI builds.
