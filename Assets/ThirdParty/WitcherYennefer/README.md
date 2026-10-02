# 叶奈法人物资源

当前场景使用本机《巫师 3》中的 `yennefer_travel_outfit`（旅行装）。人物的面部、眼睛、头发、上衣、裤装及原始 UV 来自本地游戏资源；Unity 内使用 URP 材质、人形 Avatar 和重新烘焙的走路循环。

- 资源来源：`F:/SteamLibrary/steamapps/common/The Witcher 3`；原始游戏文件没有修改。
- 原始提取目录：`F:/Unity/ExtractedModels/Yennefer`。
- 当前预制体：`Assets/Homecoming/Resources/Homecoming/Yennefer.prefab`。
- 原来的 VRoid 人物仍在 `Assets/Homecoming/Resources/Homecoming/XiaVRoid.prefab`。
- `Prepared` 保存转换后的网格、贴图及骨骼清单；`Unity` 保存可直接使用的网格、材质和 Avatar。
- 长靴保留完整靴筒与靴底，按左右脚分离；抓住靴口后沿小腿轴线拉出，辅助手随后收回，整只靴子由主手放到地上。靴内腿部与脚部使用独立长袜蒙皮。
- 原游戏 APEX 服装模拟、毛发模拟和表情控制器没有移植；当前使用旅行装的蒙皮网格，面部和头发骨骼跟随头部。

转换脚本与检查记录见 `Artifacts/WitcherYennefer`。资源来源为 CD PROJEKT RED 的《巫师 3》本地安装文件，没有把这些资产标记为免费模型。

转换工具：
- https://github.com/dingdio/Witcher3-Blender-Tools
- https://github.com/ArdCarraigh/Blender_APX_Addon
- https://github.com/NVIDIAGameWorks/PhysX-3.4
