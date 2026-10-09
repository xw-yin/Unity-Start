# 三人 DLC 服装核对（2026-10-09）

## 外观来源

| 角色 | 实际使用的外观 | 修正 |
| --- | --- | --- |
| 希里 | DLC11 `ciri_dlc.w2ent`，明确选择 `ciri_dlc` 外观，主体 `body_01_wa__ciri_dlc` | 此前仍是 `body_01_wa__ciri` 常服。本次替换护甲、配饰、靴子及 REDcloth 飘带，保留官方原始网格和贴图。 |
| 特莉丝 | DLC6 `triss_dlc.w2ent`，主体 `body_01_wa__triss_dlc` | 原主体已经是 DLC 礼服。补齐原版彩色高光、粗糙度与织纹/刺绣法线，保留现有裙摆、袖子、鞋袜分离。 |
| 叶奈法 | DLC4，主体 `body_05_wa__yennefer` 和配套原靴 | 原主体已经是 DLC。补齐材质输入，并校准黑色织物与皮革的反射，保留上次已修复的穿靴/抽靴层切换。 |

希里旧常服资源仍保留，角色槽 `Homecoming/Ciri` 改为从 `WitcherCiriDLC` 构建。新文件夹避免旧常服的 Blender 顶点补丁套用到 DLC 网格。靴子按完整连接部件分离，覆盖源高度约 0.60m 的靴口配件，保留衣服内的原始裤腿；补齐脚部仍使用已确认的原游戏女性脚部资源。

## 材质转换

`Prepared/costume-source-inputs.json` 保存原始 Blender / REDengine 材质图、贴图路径和参数；`costume-materials.json` 保存 URP 适配结果。原始漫反射和游戏网格没有绘制或生成替代版本。

此前只导出漫反射和主法线，未保留彩色高光贴图、粗糙度范围、细节法线平铺和旋转。现将源 specular RGB、normal alpha 与粗糙度范围打包到 URP SpecGloss，将两层布料细节合成到法线，减少移动端运行时的纹理采样。叶奈法标准材质的 SpecularColor 是高光色调，不能直接当成金属的绝对反射率；使用保留色调与源缩放的介电反射适配，并校准织物光泽，金属饰边单独处理。

希里 `pbr_chainmail_dlc11` 有单独的 `chain_d` / `chain_n` 和 `chain_tile`（75/75）。使用原版链环贴图和原始平铺，避免把身体贴图底色当作锁子甲颜色。REDcloth 缓存也重新读取各自 `CApexClothResource` 材质槽，保留源自由度、UV 和骨架绑定。原版允许布料复用身体材质，材质名字复用本身不能证明错配。

这仍是 Unity URP 的材质适配，不等同于重建整个 REDengine 的灯光、阴影、反射、布料着色系统。模型来源与服装部件可以逐项核对，跨引擎画面不能声称像素一致。

## 检查

独立验证项目执行 `DLCOutfitReview`：核对三人的主体资源、实际绑定的高光/粗糙度输入，保存正背面图；在实际 Play Mode 直接渲染蒙皮网格，检查站立、坐下、脱靴后的画面，并执行现有七角色完整流程、鞋袜分离和重播检查。截图与运行日志位于 `Artifacts/DLCOutfitReview/`。

最终网格与服装配置的七角色 Play Mode 检查通过，每个角色 961 个时间步；希里最大单帧骨骼转角约 15.28°，持鞋腕部目标误差小于 0.001mm。随后仅调整希里布料光泽，并另行保存最终蒙皮正背面截图；该材质调整没有改变网格、骨架或动画。自动流程通过不能代替视觉审阅，也不能证明所有视角、所有未来动画都没有穿模。

官方外观参考：[希里（PlayStation）](https://store.playstation.com/en-us/product/UP4497-CUSA00527_00-00000000000DLC11/)、[叶奈法（Steam）](https://store.steampowered.com/app/370002/The_Witcher_3_Wild_Hunt__Alternative_Look_for_Yennefer/)。
