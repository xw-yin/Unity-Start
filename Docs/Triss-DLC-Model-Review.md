# 特莉丝 DLC 模型核对（2026-10-09）

更新：这是第一轮审阅记录。辅助骨矩阵、完整原版腿脚、内外裙联动及脱靴动作的后续修正与最终验证，见 [特莉丝与脱靴修正](Boot-Extraction-and-Triss-Repair.md)。下文“尚未解决”描述的是当时的状态。

当前角色确实来自 DLC6 `triss_dlc.w2ent` 的 `triss_dlc` 外观。主体为 `body_01_wa__triss_dlc`，袖子、裙摆、附件分别来自 `a_01_wa__triss_dlc_px`、`d_01_wa__triss_dlc_px`、`i_02_wa__triss_dlc_px`。原始文件保存在 `F:/Unity/ExtractedModels/Characters/TrissDLC.blend`。这套外观使用自己的模型，不是给常服换一张贴图。

## 已确认并修正

当前 Unity 裙摆原有 992 个顶点，其中 795 个权重被坐姿适配永久改为骨盆绑定。静态顶点位置没有改变，但站立、走路时的裙摆跟随与源模型不同。

本次恢复 Prepared `Part_10.bin` 的原始权重，保留已有 Sitting / BootLeft / BootRight 形态。坐下时通过动态网格补偿原始腿部蒙皮，渐变到骨盆空间中的坐姿折叠；起身后恢复原始蒙皮。提裙目标也使用同一套补偿，避免手部目标和裙布位置不一致。仅特莉丝裙摆启用此选项，导入器保留配置。

## 检查结果与限制

- 原始裙摆与实际资产：992 个顶点、静态位置相同、权重差异 0。
- 实际 Play Mode：完整 961 帧、最大骨骼单帧变化约 15.28°、鞋子接触和重播检查通过。
- 60 / 20 FPS 连续布料检查：有限顶点、最大物理偏移约 0.11m、暂停不变、重播恢复、源网格及权重保持。提裙目标与烘焙后布料顶点距离约 0.00025mm。

视觉审阅仍发现腰前灰白折片及胸前皮肤形状异常。灰白折片属于 `body_01_wa__triss_dlc_lod0_4_Body`，并非恢复裙摆权重后产生。临时隐藏该部件或整个身体只用于诊断，没有写入正式 prefab。当前不能声称模型视觉问题全部解决，也不能把该画面当作原游戏服装效果。

证据位于 `Artifacts/TrissModelReview/`：站立、部件隐藏对比、权重差异与布料检查；`Artifacts/TrissModelReview-repair.log` 为实际 Play Mode 日志。截图检查与数值流程检查应分开判断。

官方外观参考：[PlayStation 特莉丝替代外观](https://store.playstation.com/pt-br/product/UP4497-CUSA00527_00-00000000000DLC06)。
