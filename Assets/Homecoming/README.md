光照和长靴动作已更新：补全网格切线、降低面部法线强度，并增加前方柔光。完整长靴使用靴口接触目标与小腿轴线拉出轨迹，配合辅助手、身体前移、柔和抬腿和手腕过渡。检查入口为 `HomecomingBootReview.Run`；动画预览见 `Artifacts/Yennefer/Long-boots-motion.gif`。

当前默认人物已替换为叶奈法的旅行装，预制体是 `Resources/Homecoming/Yennefer.prefab`。原 VRoid 人物仍保留。转换来源与适配说明见 `Assets/ThirdParty/WitcherYennefer/README.md`。打开 `Scenes/Homecoming.unity`，进入 Play 后按空格开始。

# 暮色归家

Unity 6 / URP 的独立 3D 场景原型，无需额外商店素材。

## 当前人物：下载的精细 VRoid 模型

当前场景使用 pixiv / VRoid Project 的免费 `AvatarSample_A`，已保存为 `Resources/Homecoming/XiaVRoid.prefab`。它带原始网格、28 张贴图、完整骨骼和面部表情；项目接入眨眼、微笑、原鞋脱下和家居拖鞋替换。原始双鞋已拆分，落地的鞋子是单独静态模型。URP 使用项目内的 Soft Anime 材质。

这是第三方人物资源，不是本项目原创。它不是 CC0；允许免费和商业游戏使用，但保留原作者版权，不能出售原始示例资源，也不能用它搭建人物生成服务。来源、原始 VRM、内嵌权限和完整授权链接保存在 `Assets/ThirdParty/VRoidSample/LICENSE-AND-SOURCE.md`。

日常修改材质、贴图或角色预制体可直接编辑 `XiaVRoid.prefab`。重新生成场景会使用这个预制体。菜单“导入精细 VRoid 人物”会从原始资源重建该预制体，覆盖对它的修改。

腿部网格已调整大腿、小腿的横向和前后轮廓，保留原来的 UV、贴图、骨骼权重及表情。修改后的网格为 `Models/XiaRefinedBody.asset` 和 `XiaRefinedLegwear.asset`；原始网格保留在第三方目录。`Editor/HomecomingCharacterRefine.cs` 每次从原始网格生成，重新导入不会重复加厚。

脱鞋流程为伸手抓住后跟、拉出鞋子、手持放到身旁地面、松开。`HomecomingShoeGrip.cs` 把手持鞋子的后跟接触点固定到手掌；松手后鞋子留在地面。手腕随鞋转向，手指使用简化的弯曲抓握姿态。坐姿、抬脚、重心侧移和另一只脚的支撑位置已配合调整。

上一版程序化人物及其 OBJ 保留在 `Xia.prefab` 与 `Exports/Xia.obj`，作为备份；它们不是当前精细人物。下文的程序化建模说明描述这个旧版。

首次导入脚本后会自动创建并打开 `Assets/Homecoming/Scenes/Homecoming.unity`。如果当前场景有未保存修改，先保存，再通过菜单 `Homecoming → 创建或重新生成归家场景` 生成。原教程场景保留。

点击 Play，再点击「开始」或按空格。小夏会进门、坐下、脱下通勤鞋、换拖鞋并起身。结束后可选择欢迎她或倒热茶。右键拖动或左右方向键调整镜头，R 重播，C 切换人物近景。

角色已升级为原创日系卡通成年女性网格：脸部和鼻部曲面、眼睛与睫毛、分层发丝、开衫领口和纽扣、手掌和手指、百褶裙、乐福鞋和开放式拖鞋。腿部是连续蒙皮网格，裙子带骨骼权重与坐姿形态。家具加入圆角、布艺滚边、木纹、窗帘褶皱和空心杯体。

这是风格化游戏模型，尚未制作影视级面部表情和配音。当前精细人物的动作由 `HomecomingMotion.cs` 控制：约 27 秒，人物先在门外等待，门向外打开后进门，沿平滑路线绕过凳子，转身坐下，逐脚换鞋后起身。走路使用项目自带 Starter Assets 的 `Walk_N` 人形动画，经 T 姿态坐标校准、Humanoid 重定向后缓存为 `WalkCycle.asset`，保留摆臂、重心变化和脚掌滚动；步态播放相位按行走距离匹配，人物朝向跟随路线。坐姿和换鞋仍使用双骨骼 IK。凳面高度按人物腿长调整。手指采用简化抓握，鞋子在手持期间固定到手掌接触点。重新烘焙走路可执行 `Homecoming.Editor.HomecomingWalkBake.Bake`；源 FBX 与 Avatar 均来自当前项目，不需要重新下载人物或动作。

`Editor/HomecomingMotionReview.cs` 检查 60 帧/秒的骨骼连续性、10 帧/秒的可见网格地板/凳面/门扇/后墙穿插，并渲染动作检查图。透明贴图隐藏的顶点不参与可见穿模检查。

- `Resources/Homecoming/Xia.prefab`：可独立使用的角色预制体；运行时直接加载此预制体，可在 Inspector 编辑模型和材质。
- `Models/Model_*.asset`：保存的网格，包含法线、UV、腿部蒙皮和裙子形态数据。
- `Exports/Xia.obj` 与 `Xia.mtl`：可导入 Blender 等软件的中立站姿模型与基础材质。OBJ 不保留骨骼与动作；Unity 预制体保留。
- 修改预制体之后，不要使用“重新生成”菜单，否则会按建模脚本重建角色和覆盖编辑。普通 Play 不会覆盖预制体。
- `Artifacts/Xia-model-preview.png`、`Xia-face-preview.png`、`Xia-seated-preview.png`：Unity 渲染的模型检查图。

`Scripts/HomecomingScene.cs`：房间、人物、灯光、镜头、动作与交互。
`Editor/HomecomingSetup.cs`：创建可在编辑器查看的场景，并加入构建列表。
`Scripts/ModelMesh.cs`、`XiaModelBuilder.cs`：原创网格建模源码。
