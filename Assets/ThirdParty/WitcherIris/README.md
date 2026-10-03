# 爱丽丝人物资源

来源：本机《巫师 3》安装目录 F:/SteamLibrary/steamapps/common/The Witcher 3；原游戏文件保持不变。

实体：dlc/ep1/data/characters/npc_entities/main_npc/iris.w2ent。导入外观：iris_no_veil。爱丽丝使用游戏原有无面纱黑裙外观；席安娜使用常服外观，不包含任务丝带。

Prepared 保存原游戏网格、UV、贴图、骨骼及 REDcloth 的逐顶点活动权重。Unity 保存 URP 材质、人形 Avatar、可移除的左右靴子和脚部补全。预制体：Assets/Homecoming/Resources/Homecoming/Iris.prefab。

长裙使用 HomecomingGarmentDynamics 的惯性粒子、缝合边和活动范围约束。活动权重来自原游戏 APX/REDcloth；固定区域保持贴合骨架。运行时克隆网格，源资产保持不变，暂停和重播分别冻结、重置布料状态。爱丽丝额外使用坐姿及左右抬膝褶皱，并降低长裙的脱靴抬脚高度，避免腿部蒙皮重复抬起整条长裙。

这是项目内的布料适配，不依赖原游戏 APEX 运行时；原游戏表情控制器没有移植。脚部补全使用游戏自带 ls_02_wa__body_barefoot 原始女性腿脚网格，保留原三角面、UV 和蒙皮权重。

本地原始提取结果：F:/Unity/ExtractedModels/Characters；转换记录：Artifacts/WitcherCharacters。资源来自本地游戏，不标记为免费模型。
