# 手臂、手腕与抓握复查依据

## 参考

- Unity Two Bone IK：末端位置、末端旋转和肘部 Hint 分别控制；手到达目标并不说明整个姿势合理。https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html
- Animation Mentor / Jason Martinsen，步行动画教程：先确认接触、下沉、经过、抬升关键姿态，再处理上臂、前臂和手腕的摆动及跟随，最后处理起步与停止。https://www.animationmentor.com/blog/tutorial-animating-human-walk-cycle/
- Animation Mentor，手势教程：先依据参考建立主姿势，再调整手指和拇指的形状，不能让全部手指保持相同角度。https://www.animationmentor.com/blog/tutorial-how-to-animate-hand-gestures/
- Blender Bone Roll：骨骼朝向与沿骨轴旋转属于不同控制量，不能将某个固定局部轴视为所有骨骼的解剖轴。https://docs.blender.org/manual/en/3.6/animation/armatures/bones/editing/bone_roll.html

## 项目中的应用

走路使用已经重定向的肩、上臂、前臂和手腕动画，进入稳定步行后采用完整权重。前臂沿自身轴校正旋转，让掌心朝向大腿，手腕延续前臂方向。起步与停止渐进过渡；手指使用轻微且各指不同的放松弯曲。

当前导入模型的手指骨节位置已经形成弯曲。手指姿势依据实际骨段方向建立，避免在弯曲的绑定形状上重复叠加弯曲。放松、抓靴口、捏布、扶膝四种完整手型在初始化时确定，运行中插值关节局部旋转，避免每帧迭代求解在张开过程中换解。左右掌心由骨骼位置和左右手关系确定，不再用局部 Z 值正负猜测。

抓靴口分别确定手腕路径、掌向、拇指与食指的夹持，以及其余手指的弯曲；空闲手掌心向下扶膝。脱长靴时减小身体前倾、骨盆前移与抬腿高度，给靴口和手留下空间，避免手伸入腹部。长裙保持先抓布、提裙、抓鞋、放回布料、松指和收手的顺序。

短靴放鞋位置移近身体，保留较大的前倾幅度；长靴采用较直立的躯干，避免把同一套伸手距离用于不同鞋型。手指从持靴切换到扶膝或提裙时，使用相同的伸手权重渐变，避免手型突然切换。

验证包含正面与侧面关键帧、连续时间线和 30 fps 近景预览。预览由 Unity 按时间步渲染，另有实际 Play Mode 的全流程检查；它不是移动设备性能录像。数值检查用于发现接触脱离、突变和缺失帧，不单独作为自然度合格的依据。当前抓握仍是按导入骨架校准的程序动画，没有引入真人脱靴动捕。
