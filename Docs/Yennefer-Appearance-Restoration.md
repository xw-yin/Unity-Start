# 叶奈法外观还原记录 · 2026-10-17

目标：保留当前 DLC 人物几何，在固定姿势、镜头、光照下修复皮肤、头发和眼睛的材质转换问题。本轮不修改动作编排、身体比例、袜腿和靴子的网格。

## 依据与来源

- 实际读取本机 `F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend` 的源材质节点和参数；后台 Blender 只读，没有保存覆盖源文件。
- `Prepared/Appearance/source.json` 记录七张新增贴图的原始 DDS 路径、尺寸和 SHA256。它们来自本机已提取资源，不是绘制或生成的替代纹理。
- 实际查看 [CDPR 官方叶奈法形象指南](https://public.cdn.cdpr.app/thewitcher/cosplay-guides/yennefer/YENNEFER_COSPLAY_GUIDE_EN.pdf) 中的脸部、头发参考。该指南是默认服装参考，不作为当前 DLC 衣服的逐片依据。
- 皮肤的包裹漫反射参考 [NVIDIA GPU Gems 第 16 章](https://developer.nvidia.com/gpugems/gpugems/part-iii-materials/chapter-16-real-time-approximations-subsurface-scattering)。这是实时近似，不能当作原游戏皮肤渲染器。

原始漫反射与导出的 PNG 对照，没有发现足以解释形象差距的颜色空间转换错误，因此没有通过重绘肤色或修改脸型来弥补。

## 本轮修改

| 部位 | 源数据与处理 | 保留的边界 |
| --- | --- | --- |
| 脸部 | 恢复源 4096 漫反射和法线；源 Ambient 的 R 为遮蔽、G 为粗糙度；使用原毛孔细节法线 | 光泽及包裹漫反射为 URP 适配参数，并非 REDengine 原着色器 |
| 皮肤 | 独立皮肤着色，按源法线图方向翻转绿色通道，减轻过硬的明暗交界 | 未实现屏幕空间皮肤散射 |
| 头发 | 原有贴片、UV、贴图和骨骼不变；原高光偏移图驱动发丝高光；透明边缘使用共享原网格的附加渲染层 | 增加两个 SkinnedMeshRenderer 和相应绘制开销；贴片排序及发际边缘仍有局限 |
| 眼球 | 恢复眼部法线，分离眼球高光与瞳孔表现 | 没有恢复原眼球视差、折射及 BlickCube |
| 眼睑阴影 | 原 `he_...Material1` 实为 `pbr_eye_shadow`，此前因没有漫反射被禁用；恢复独立遮罩层 | 不是角膜；遮罩强度经过 URP 下的外观调整 |
| 睫毛 | 使用源 alpha 阈值 0.08367346 | 保留原网格和贴图 |

头发主体使用深度写入和 MSAA 覆盖，附加层只绘制主体阈值以下的边缘像素，不写入深度、深度法线或阴影。新增层共享原网格和骨骼，不是额外制作的发型。

`YenneferDLCImport.Import()` 在通用服装材质处理之后应用此材质流程，后续重新导入会保留这些设置。

桌面脸部贴图使用 4096 BC7；iPhone 设置为 2048 ASTC 4x4。这里仅设置导入格式，没有完成真机性能或 iOS 渲染验证。

## 验证与证据

证据位于项目的 `Artifacts/AppearanceReview-20261017/`（该目录不纳入 Git）：

- `baseline/`、`current/`：相同姿势与镜头的房间近景、标准灯光正面、侧面、三分之四面及全身图。
- `face-three-quarter-comparison.jpg`、`room-face-comparison.jpg`：原图并排缩放，不做重绘或亮度补偿。
- `source-materials.json`、`skin-group-links.json`：源材质和皮肤通道连接审计。
- `geometry-hashes.json`：49 个现有网格资产与验证副本逐文件 SHA256 一致。
- `applied-assets.json`、`before-assets/`：本轮应用的材质、元数据和预制体备份。
- `runtime/`、`runtime.log`：独立 Unity 6000.6.3f1 编辑器 Play Mode 实际 URP 渲染 930 帧，时间线按 30 Hz 采样；保存六张没有调用 BakeMesh 的 GPU 蒙皮截图。运行时检查附加发层与原发层共享网格、骨骼和局部原点；当前 D3D11 着色器错误为零。

Play Mode 第一轮因进入运行模式时脚本域重新加载而丢失审阅回调，未作为通过证据。审阅器增加重载恢复后，第二轮完整结束。

截图工具另修复了临时烘焙对象丢失角色层、投影设置，以及 RenderTexture 未使用项目 MSAA 的问题。前后对照都使用同样修复后的工具，不能把截图抗锯齿差异当作材质提升。

本轮可以确认头发的灰色硬片高光减轻、眼睛瞳孔更清楚；不同角度仍能看出发片和肩部羽毛边缘。皮肤、头发、灯光距离原游戏仍有差距，未宣称完全还原。

## 复现

在项目副本中使用同版本 Unity，以保留用户正在编辑的场景：

```powershell
& 'F:/Unity/6000.6.3f1/Editor/Unity.exe' -batchmode -projectPath '<项目副本>' -executeMethod Homecoming.Editor.YenneferAppearanceRuntime.Run -logFile '<日志绝对路径>'
```

运行器自行结束编辑器，不添加 `-quit` 或 `-nographics`。它会重建副本里的审阅场景；只允许 batchmode 调用。静态审阅菜单位于 `Homecoming/审阅叶奈法外观还原`。

源贴图可使用 `DCC/Blender/prepare_yennefer_appearance.py --source F:/Unity/ExtractedModels/Yennefer` 从已提取的 DDS 重新转换。输入哈希不匹配时停止。

## 未完成范围

本次没有重新评审脱靴动作美感或解决新的接触问题；此前动作修复保留。裙子、丝袜、靴子的既有适配不能称为完整原版服装还原。其他角色材质、原眼部折射、服装和羽毛着色、目标手机性能仍需单独检查。
