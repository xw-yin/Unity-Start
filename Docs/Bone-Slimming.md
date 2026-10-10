# 骨骼裁剪（Draw Call 之外的每帧 CPU 优化）

## 结论先行

- **Draw call 合并（候选 A）暂不做**：实测 7 个角色每人 21–33 个 SMR，但导入器给每个部件建了独立材质（`Surface_00.mat`…），按材质合并只能省 1–2 个（左右袜层共用 `InnerLegs.mat`；左右靴虽然共用材质但必须保持独立——脱鞋流程单独动画）。真正的合并需要纹理 atlas，是 DCC 端的大工程；而场景一次只上场一个角色，30 个 draw call 在 iOS 上不是瓶颈。
- **做骨骼裁剪（候选 B）**：每个 SMR 的 `m_Bones` 里塞了整套骨架（AnnaHenrietta 394、Ciri 220），而 `HomecomingMotion.Sole`、`HomecomingGarmentDynamics`、`HomecomingFootContact` 每帧按 `bones.Length` 逐个算矩阵——没权重的骨骼是纯浪费。工具：`Homecoming/性能/裁剪角色未使用骨骼`。

## 工具行为

`Assets/Homecoming/Editor/SlimCharacterBones.cs`，菜单 `Homecoming/性能/裁剪角色未使用骨骼`：

1. 扫描 `Assets/Homecoming/Resources/Homecoming/*.prefab` 的全部 SMR，按网格资源分组。
2. 对每网格取 `boneWeights`，只保留权重 `> 0` 的骨骼索引（另并入 `refinements.json` 里该网格的修正顶点索引，保证以后重应用修正仍然合法）。
3. 一致 remap 三处：`mesh.boneWeights`、`mesh.bindposes`、各 SMR 的 `m_Bones`（保持原有相对顺序）。
4. 同步更新 `DCC/Blender/refinements.json`：修正顶点的骨骼索引 + 重算指纹（指纹覆盖 `bindposes.Length`）。首次运行自动备份 `refinements.json.pre-slim-bak`。
5. 只删权重精确为 0 的骨骼，蒙皮数学完全不变；运行时代码只通过权重索引或 Transform 本体查骨骼，没有写死绝对索引的地方（已逐处核对 `CharacterRig` / `HomecomingMotion` / `ShoeGrip` / `GarmentDynamics` / `FootContact`）。
6. 弹框预览每网格 before→after 和槽位总量，确认后执行；幂等，可重复跑。

## Mac 执行步骤

1. 先跑 `Homecoming/Blender/应用保存的接缝和法线修正`（让修正权重进入 used 集合）。
2. 跑 `Homecoming/性能/裁剪角色未使用骨骼`，看预览数字，确认执行。
3. 跑现有连续复查 `Homecoming.Editor.HomecomingContinuousReview.FullReview`（7 角色 × 961 帧），确认外观无变化。
4. 正常提交（含被改的网格 `.asset`、prefab、`refinements.json`）。

## Blender 重导入后的顺序

重导入会恢复 394 骨骼全列表，此时按此顺序：

1. 从 `refinements.json.pre-slim-bak` 恢复 `refinements.json`；
2. 跑 `应用保存的接缝和法线修正`；
3. 再跑一次 `裁剪角色未使用骨骼`。

（直接对瘦身后的网格应用旧 refinements 会因指纹校验失败而 loud 报错，不会静默写错。）
