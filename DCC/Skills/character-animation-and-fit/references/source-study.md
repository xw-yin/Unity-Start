# 公开作品与方法研究

研究日期：2026-10-10。以下是作者、制作机构或官方技术来源。本文记录访问深度，避免把搜索摘要当完整学习。只借鉴方法，不复制角色造型或把影片动作直接挪给另一种服装。

## 实际观察与完整阅读

| 来源 | 本次接触的内容 | 可迁移的方法 | 边界 |
|---|---|---|---|
| David Revoy，《Spring》[角色设计与概念稿](https://www.davidrevoy.com/article712/spring-open-movie-character-design-and-concept-art)（2019） | 阅读作者说明；浏览器实际观察[模型设定图](https://www.davidrevoy.com/data/images/blog/2019/spring/2017-10-11_spring-character_model-sheet.jpg) | 立体制作前同时定义正面、侧面、衣服外轮廓与内部结构。设定图中手握道具的表演姿态与供建模使用的展开姿态用途不同 | 这是概念稿观察，不是已审阅其 3D 拓扑、绑定或整部动画。不能套用儿童比例到成人角色 |
| Dana Boadway-Masson，[10 Advanced Acting Performance Tips](https://www.animationmentor.com/blog/10-advanced-acting-performance-tips-for-animators/)（2021） | 阅读作者对《料理鼠王》等案例的分析；实际观察[害羞](https://www.animationmentor.com/wp-content/uploads/2015/05/blog-danaboadway-shy-neutralpose-animation.jpg)和[不耐烦](https://www.animationmentor.com/wp-content/uploads/2015/05/blog-danaboadway-impatient-neutralpose-animation.jpg)待机姿态对照图 | 同一角色用头、肩、躯干和手的整体安排传达不同态度。少数主要姿态内也可以表演；静止时刻和变化形成节奏，不能始终动个不停 | 对照图是静态姿态。《料理鼠王》视频嵌入未能播放，影片时间与动态细节仅是作者描述 |
| Anthony Wong，[Building Appealing Character Poses](https://www.animationmentor.com/blog/tutorial-building-appealing-character-poses-for-animation/)（2026） | 完整阅读机构发布的教学文字；文中讨论 Colette、学生姿态和足部控制 | 用意图、剪影、动作线和肩髋对势审阅姿态；保持其他视角的立体合理性。细化控制不能挽救坏的基础姿态 | 未观看嵌入视频；教学特定 rig 的足部数值不可当通用踝角度 |
| Joseph White，[Pushing and Pulling Motions](https://www.animationmentor.com/blog/tutorial-animating-pushing-and-pulling-motions/)（2026） | 完整阅读从真人参考、草图、stepped blocking 到 spline 的制作说明；尝试打开视频但超时 | 在动软件前分析物体、摩擦和受力。接触前的自由摆动与接触后的稳定控制不同；补清楚 breakdown 再细化 | 推拉箱子不是脱靴参考。本文未证明任何特定脱靴路线 |
| Jason Martinsen，[Basic Human Walk Cycle](https://www.animationmentor.com/blog/tutorial-animating-human-walk-cycle/)（2025） | 完整阅读步态制作文字 | 先建接触、下沉、经过、抬升姿态；上身反向配合骨盆，手臂分层拖带；从循环转向前进和停步需要单独处理 | 文中的周期仅示例；高跟鞋与裸足必须分别验证，不能机械镜像整个生活动作 |
| Nathaniel Seymour，[Hand Gestures](https://www.animationmentor.com/blog/tutorial-how-to-animate-hand-gestures/)（2025） | 完整阅读手势教学文字 | 手部要服务意图，避免平板掌和双手完全同步；手指、腕与整个上身一同设计 | 对话手势不是受力抓握。抓住软靴还需要拇指对置、手指包覆和物体变形证据 |
| Wayne Gilbert，[Body Mechanics Breakdown](https://www.animationmentor.com/blog/animation-tips-tricks-what-makes-or-breaks-a-good-body-mechanics-shot/)（2014） | 完整阅读 | 分析引发运动的原因和身体部位的先后，而非只匹配位置。Breakdown 要解释从 A 到 B 的机制 | 方法参考；本文没有把示例当本项目的动作片段 |
| Audrey Wong、David Eberle、Theodore Kim，《Bao》[Clean Cloth Inputs](https://www.researchgate.net/publication/326908946_Clean_cloth_inputs_removing_character_self-intersections_with_volume_simulation)（SIGGRAPH 2018） | 阅读作者上传的完整两页论文文本及图注 | 输入身体的自相交会破坏后续布料；先清理接触区，保留表演轮廓，再模拟服装。演员形体、碰撞体与布料是有关联的制作步骤 | 这里学习制作顺序，不声称 Unity 已实现论文的体积求解器。论文明确鞋未模拟；不能据它宣称软靴机制已验证 |
| Disney Animation，[Simulation](https://www.disneyanimation.com/process/simulation/) | 阅读制作部门说明 | 布料需针对角色裁制，并在物理响应中保留艺术控制 | 部门介绍没有提供移动端算法或本项目参数 |
| Unity Animation Rigging 1.3.1，[Rigging Workflow](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/RiggingWorkflow.html) | 阅读官方说明 | 基础动画后的约束有明确求值顺序；层和层级关系会影响结果 | 这是集成证据，不是优秀表演的证明；使用前核对项目实际包版本 |

## 部分可访问、不能宣称已看过的材料

- Blender Studio，《Sprite Fright》[Animation Tips Learned The Hard Way](https://studio.blender.org/blog/sprite-fright-animation-tips-learned-hard-way/)、[Style Guide](https://studio.blender.org/blog/sprite-fright-style-guide/)、[Retopology 案例](https://studio.blender.org/blog/puzzle-pro-retopo-sprite-frights-expert/)：搜索索引可读，正文抓取返回 402，浏览器页面超时。暂列后续研究，不用未读正文支持完成结论。
- Blender Studio [Modeling](https://studio.blender.org/tools/artist-guide/asset-creation/modeling) / [Rigging](https://studio.blender.org/tools/artist-guide/asset-creation/rigging)：本次只有索引摘要，涉及设计、重拓扑、corrective shapes；未审阅源文件。
- Blender 官方 [Cloth Shape](https://docs.staging.blender.org/manual/en/latest/physics/cloth/settings/shape.html)：索引提供 Pin Group 与 Dynamic Mesh 说明；正文返回 403。Pin 权重控制固定强度，Dynamic Mesh 可以更新静止形状。具体制作时还需核对本机版本和实际求值，不能因此宣称已跑通布料。
- 《Coco》旧 Pixar PDF 链接重定向到技术库首页；不能称已阅读该论文。Disney/UNC 2005 碰撞论文正文可读，但没有取得可用的图像截图，不作为动作艺术案例。

## 本次归纳与推断

**来源支持的方法**：参考与意图先行；关键姿态后设计 breakdown；身体受力与接触优先；形体、绑定和 cloth input 都要审阅；正常速度决定表演是否成立。

**应用到穿脱鞋的推断**：将抓稳、松鞋跟、抽脚、持鞋、放下分成不同接触状态；将软靴的鞋底、后跟与靴筒分区制作；长裙先露出可操作的靴口。它们是待匹配参考验证的镜头设计假设，不是上述电影提供的脱靴范本。没有观察到对应真实操作时，输出“设计候选”而非“动作正确”。

新增参考时记录：标题 / 作者 / 原链接 / 查看日期 / 实际可访问范围 / 帧或时刻 / 观察 / 推断 / 可以迁移的部分 / 不适用的部分。素材重用先核对对应许可；本 skill 只保存链接和自行总结。
