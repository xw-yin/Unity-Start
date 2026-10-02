# 暮色归家

Unity 6 / URP 的 3D 归家场景原型。打开 `Scenes/Homecoming.unity`，进入 Play，点击“开始”。人物在门外等待，开门后进屋、坐下，用手逐只脱下长靴、放到地面、换上拖鞋并起身。

## 手机操作

- 单指滑动画面：水平转动视角、上下调整观察角度。
- 双指捏合：缩放；触摸按钮不会同时拖动镜头。
- “全景／人物／换鞋”：平滑切换三个视角，人物视角跟随角色。
- “开始／暂停／继续”和“重播”：控制动画；暂停期间仍可调整视角。
- 横屏支持两个方向，按钮避开刘海和底部手势安全区。对话框与欢迎、倒茶对话已移除。

电脑可拖动鼠标和使用滚轮；空格播放或暂停，R 重播，C 依次切换视角，左右方向键转动。

## 画面

手机预设使用完整渲染分辨率、4x MSAA、柔和主光阴影和四骨骼权重。场景使用分层环境光、前方柔光、室内环境反射及 ACES 色调映射。反射只捕获一次，不持续刷新。房间补齐墙面、天花板，家具仍为程序化原型网格。

`Scripts/HomecomingCameraController.cs` 处理触屏手势、平滑视角和房间边界；`HomecomingMobileUI.cs` 创建安全区内的触控按钮；`HomecomingPresentation.cs` 配置光照后期。`Resources/Homecoming/IndoorLighting.asset` 保存后期配置供移动端构建使用。

## 人物与动作

默认人物为 `Resources/Homecoming/YenneferDLC.prefab`，使用叶奈法 DLC 服装、完整长靴与蕾丝丝袜。来源和适配说明见 `Assets/ThirdParty/WitcherYennefer/README.md`。原 VRoid 模型及授权保留在 `Assets/ThirdParty/VRoidSample/LICENSE-AND-SOURCE.md`；程序化人物 `Xia.prefab` 为旧版备份。

`HomecomingMotion.cs` 使用人形走路动画缓存及换鞋 IK；`HomecomingShoeGrip.cs` 保持手掌与靴口的接触，控制抽出、手持和落地。原始步行动画来自项目内 Starter Assets，经 Humanoid 重定向并按行走距离匹配相位。菜单 `Homecoming → 创建或重新生成归家场景` 重建房间；普通 Play 不覆盖人物预制体。

## 验证与打包

`Homecoming.Editor.HomecomingMobileReview.Run` 检查手机画质、五个触控按钮、安全区、暂停重播、三个视角及镜头边界，并生成 `Artifacts/Mobile-*.png`。`Homecoming.Editor.HomecomingRuntimeSmoke.Run` 在 Play Mode 检查原有换鞋流程，并注入真实触屏输入事件验证转动、缩放和按钮手势隔离。

iOS 未签名 IPA 的 Actions 使用方式见仓库 `Docs/iOS-Build.md`。手机帧率目标为 60；实际性能需在目标 iPhone 上验证。

界面内嵌 [Noto Sans CJK](https://github.com/notofonts/noto-cjk) 简体中文字体，按 SIL Open Font License 1.1 使用，完整许可证位于 `Resources/Homecoming/Fonts/LICENSE.txt`。
