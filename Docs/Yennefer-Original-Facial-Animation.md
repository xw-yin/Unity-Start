# 叶奈法原版面部动画接入（2026-10-21）

镜头意图：回家时保持叶奈法克制的神态，坐下与脱靴后逐渐放松；眨眼与眼神使用原游戏的面部曲线，避免固定周期的程序表情。

## 来源与转换

- 原姿态：`characters/models/main_npc/yennefer/h_01_wa__yennefer/h_01_wa__yennefer.w3fac`。本地文件有 184 个骨架节点、129 个表情姿态，其中 89 个姿态映射骨骼。不是 129 段完整表演，也不是 BlendShape。
- 原曲线：`animations/woman/dialog/yennefer_mimic_layers.w2anims`，导出中性和愉快的连续动画及两段静态姿态。连续动画分别为 611 帧 / 20.333 秒、541 帧 / 18 秒，源采样率 30 Hz。SHA256 保存在 JSON 和 Unity 资源中。
- 原作者的骨骼表情系统说明：[Paweł Mielniczuk / Yennefer](https://blooddragon.artstation.com/projects/yEOk9)。这是作者说明，不是本次操作观看的游戏视频。
- 原版面部动画目录与导入方式：[CDPR REDkit 文档](https://cdprojektred.atlassian.net/wiki/spaces/W3REDkit/pages/136413260/HOW-TO+Import+new+body+and+facial+animation)。

`audit_yennefer_face.py` 保存源数据审计；`inspect_game_mimics.py` 定位原动画库；`export_yennefer_face.py` 调用原 Blender 插件的姿态导入，并从实际驱动网格的骨架求变形；`export_yennefer_face_clips.py` 导出源浮点曲线。源 blend 文件只读，未覆盖。

当前 Unity 模型有 78 个可用面部变形骨骼。按已导入骨架的实际父子关系转换局部位移、旋转，不改脸型、顶点或权重。转换初始位置最大误差为 7.45e-9 米。剩余 11 个源骨骼是虹膜、独立睫毛和 UV 辅助节点；它们未伪装成已接入。原库 normal0–normal13 等材质皱纹轨道不参与骨骼混合。源颈部、头部骨骼轨道也未覆盖当前剧情动作。

## 运行时

`YenneferFace.asset` 只挂到 `YenneferDLC.prefab`。`HomecomingFacePlayer` 让原连续曲线驱动原加法姿态，从参考位置和旋转重新求值，不逐帧累积变形。循环接缝作 0.5 秒交叉淡化；剧情后段将愉快状态混合到 35%。眨眼保持中性源动画完整幅度，避免两段不同相位的眨眼混合成半闭眼。循环和情绪混合属于本项目适配，不等于完整还原 REDengine 的表演系统。

叶奈法接入后跳过旧版的周期眼睑、眉毛、下颌和嘴角扰动。身体、头部仍由剧情动作控制，其余角色仍使用原有回退逻辑。重新执行叶奈法 DLC 导入会重新绑定面部库。

## 实际验证

在独立验证副本 `StartMotionValidation` 中使用 Unity 6000.6.3f1 / Direct3D11 / URP 验证，避免覆盖主项目场景。

- 编译、面部资源导入成功；姿态测试包括闭眼、上下左右视线、轻微微笑和四分之一下颌张开。
- 已逐张查看原版中性、完整闭眼、左右视线、嘴角、下颌及连续动画中的近景截图。闭眼图眼睑盖住眼球，测试幅度内未见嘴唇拉裂。小幅嘴角变化保持克制。未检查完整张嘴时口腔和牙齿的所有细节。
- 实际 Play Mode 连续渲染 1200 帧，源时间步长 1/30 秒，输出 40 秒 MP4。不是稀疏截图插帧。固定身体与头部，独立观察面部；20–25 秒逐渐加入愉快状态。已检查眨眼过程的真实帧；正常速度整段的艺术表现仍应由观看视频评估，不能由数值测试代替。
- 最大面部骨骼局部位移 0.009318042 米，最大局部旋转 45.8335762 度；任意跳时后重复采样一致，无累积漂移，未改变身体或头部变换。
- 整段剧情技术检查通过 1981 帧。该检查不代表脱靴等既有身体动作的艺术质量已提升。
- 本次前后 49 个叶奈法网格资源 SHA256 相同；资产引用另作验证。

证据在本地 `Artifacts/FaceReview-20261021/`：源审计 JSON、静态姿态图、`runtime.csv`、`runtime-result.txt`、`runtime.log`、`integration.log`、`native-blink-sequence.jpg`、`yennefer-original-face.mp4`。完整 1200 张源帧保存在验证副本的 `Artifacts/FaceReview/film/`。

## 尚未接入

没有对话音频口型同步、完整游戏情绪/语音状态机、动态皱纹贴图或虹膜大小动画。眼球旋转已接入，但不等于根据玩家位置自动注视。当前光照、眼睛和头发材质与原游戏渲染仍有差异。本次只恢复可追溯的原版面部姿态和连续曲线，不宣称画面与原游戏完全一致。
