# 次元旅人 · AI 美术资料研究

> 状态：研究记录，不是项目规范。
>
> 调研日期：2026-07-15。
>
> 目标：为《杀戮尖塔 2》角色 Mod“次元旅人”建立可复现、可扩展的 AI 美术生产方法。本文保存来源与方法归纳；项目采用方案见《AI美术工作流草案.md》。

## 1. 筛选口径

### 1.1 “热门”的使用方式

“热门”只用于发现候选，不直接代表适合本项目。

- GitHub：参考 stars、forks、最近提交、版本发布和许可证。
- 文章与教程：优先官方文档、原作者文章、带参数或过程的案例。
- Skill：必须读取实际 `SKILL.md` 或工作流定义，不能只看仓库标题。
- 艺术工作者：优先公开过数据、参考、迭代、筛选或人工再加工过程的人。

### 1.2 采用价值

资料是否进入项目方法，按以下问题判断：

1. 能否维持同一角色的脸、服装、轮廓和标志物？
2. 能否维持 45 张左右卡图的系列风格？
3. 能否把身份、风格、构图和姿势参考分开控制？
4. 能否局部修改而不是整图重抽？
5. 能否保存模型、Prompt、参考图、参数、成本和父子版本？
6. 能否在 STS2 卡框和实际缩略尺寸中保持可读？
7. 来源、模型与素材的商用边界是否可以核验？

### 1.3 排除项

- 只有成品展示、没有过程的方法。
- SEO 拼接文章和同源转载。
- 只适合单张“惊艳图”，没有系列一致性方法的流程。
- 无法确认维护状态、真实职责或许可证的仓库。
- 要求直接模仿某位在世艺术家个人风格的方案。

---

## 2. 文章与教程来源

以下来源至少覆盖 Prompt、参考图、角色一致性、结构控制、编辑和游戏载体六个方面。

| 来源 | 类型 | 可提取方法 | 局限 |
|---|---|---|---|
| [OpenAI · GPT Image prompting guide](https://developers.openai.com/cookbook/examples/multimodal/image-gen-models-prompting-guide) | 官方 | 用结构化自然语言描述主体、媒介、构图、光线、约束；编辑时明确“改什么”和“保持什么” | 模型专属参数需通过实际 API 核验 |
| [OpenAI · Image generation API](https://developers.openai.com/api/docs/guides/image-generation) | 官方 | 区分生成与编辑端点，保留输入图、mask 和响应数据 | OpenAI-compatible 供应商不保证实现完全一致 |
| [Midjourney · Omni Reference](https://docs.midjourney.com/hc/en-us/articles/36285124473997-Omni-Reference) | 官方 | 用单独的身份/物体参考维持角色、道具或生物 | 商业云平台，参数与模型版本会变化 |
| [Midjourney · Style Reference](https://docs.midjourney.com/hc/en-us/articles/32180011136653-Style-Reference) | 官方 | 把身份参考和风格参考拆开；风格参考只约束视觉语言 | 不应把一张角色图同时承担所有参考职责 |
| [Black Forest Labs · FLUX prompting guide](https://docs.bfl.ml/guides/prompting_summary) | 官方 | 使用清晰的场景叙述、构图关系、材质和色彩约束 | 需按具体 FLUX 版本适配 |
| [Black Forest Labs · FLUX.1 Kontext](https://bfl.ai/blog/flux-1-kontext) | 官方 | 基于现有图连续编辑，强调角色一致性与局部变化 | 连续编辑仍可能发生渐进漂移 |
| [ComfyUI · ControlNet](https://docs.comfy.org/tutorials/controlnet/controlnet) | 官方 | 把姿势、深度、边缘等结构约束显式化；多个控制可组合 | 节点工作流复杂，维护成本高 |
| [ComfyUI · FLUX Kontext workflow](https://docs.comfy.org/tutorials/flux/flux-1-kontext-dev) | 官方 | 本地可复现的参考图编辑与流程保存 | 受显存、模型许可证和节点版本影响 |
| [Krea · Style references](https://www.krea.ai/blog/style-references-krea-2) | 官方 | 单图 Style Reference 用于精确风格延续；Moodboard 用于开放探索 | 平台侧模型和生成参数不完全透明 |
| [Leonardo · Image Guidance](https://intercom.help/leonardo-ai/en/articles/8497988-image-guidance) | 官方 | 将 Style、Content、Character、ControlNet 等参考职责拆分 | 公共图片的授权条款需要单独核验 |
| [Google · Gemini image generation](https://ai.google.dev/gemini-api/docs/image-generation) | 官方 | 多参考图、多轮编辑、对话式修改；适合先形成母图再小步修正 | 多轮编辑要防止非目标区域漂移 |
| [Adobe Firefly · Style Reference](https://helpx.adobe.com/firefly/web/work-with-images/generate-images/reference-images-for-styling.html) | 官方 | 风格、主题、情绪参考独立于内容描述 | 不能把“训练来源较清晰”简化为无条件法律保证 |
| [Adobe Firefly · Composition Reference](https://helpx.adobe.com/firefly/web/work-with-images/generate-images/match-image-composition-to-reference-image.html) | 官方 | 用构图参考锁定画面元素位置；可配合草图或 3D Blockout | 适合作为构图工具，不替代角色身份参考 |
| [Stability AI · SD3.5 ControlNets](https://stability.ai/news-updates/sd3-5-large-controlnets) | 官方 | Canny、Depth、Blur 等结构控制；适合固定剪影和视角 | 本项目是否采用本地 SD 系待硬件和效果测试 |
| [Henry · AI 卡牌游戏美术](https://hjcenry.com/archives/1773413625145) | 实践文章 | 锁定模型、模板化 Prompt、相似图变体、局部修复、最后放大 | 有平台宣传属性；对角色身份一致性和资产管理讨论不足 |
| 本地教程 `Visuals/02 - 风格原画绘制` | STS2 实践 | 以游戏截图作参考；美式卡通、色块优先、少硬线、夸张轮廓、弱化强光影；角色服装需便于动画拆分 | 不是 AI 教程，但对本项目风格和生产约束的优先级高于通用文章 |
| 本地教程 `RitsuLib/01 - 添加基础内容/01 - 添加卡牌` | STS2 规格 | 普通卡图官方尺寸 250×190，先古卡图 250×351；资源可用更高分辨率母图再导出 | 最终仍需游戏内卡框实测 |

### 2.1 文章类结论

- Prompt 不能承担全部一致性；身份参考、风格参考、构图参考和结构控制应分离。
- “同模型”只解决部分风格问题，不能自动解决人物身份和道具结构。
- 连续编辑应一次只改一个变量，并明确声明保持项。
- 本项目的目标风格不能只写成“暗黑厚涂”。本地 STS2 教程显示更关键的是色块、夸张轮廓、有限线条和不过度强烈的光影。
- 最终验收对象不是高清原图，而是 STS2 卡框中的 250×190/250×351 图像和游戏内缩略显示。

---

## 3. 仓库来源

> stars 为 2026-07-15 快照，只用于表示社区规模，不代表质量排名。

| 仓库 | 热度/状态快照 | 职责 | 对本项目的价值 |
|---|---:|---|---|
| [AUTOMATIC1111/stable-diffusion-webui](https://github.com/AUTOMATIC1111/stable-diffusion-webui) | 164k stars；2026-03 有提交 | 本地 Stable Diffusion WebUI | 生态庞大，但新项目不应仅因插件多而采用 |
| [open-webui/open-webui](https://github.com/open-webui/open-webui) | 145k stars；持续更新 | 通用自托管 AI 界面，可接 OpenAI、ComfyUI、A1111 | 适合聊天与多后端入口，不如 iLab 聚焦图片资产生产 |
| [Comfy-Org/ComfyUI](https://github.com/Comfy-Org/ComfyUI) | 120k stars；当日有提交；GPL-3.0 | 节点式生图引擎、API 和工作流后端 | 本地模型、ControlNet、LoRA 和可复现实验的首选候选 |
| [lllyasviel/Fooocus](https://github.com/lllyasviel/Fooocus) | 51k stars；2025-12 有提交；GPL-3.0 | 简化的本地生成界面 | 易用但流程显式性和项目管理能力有限 |
| [invoke-ai/InvokeAI](https://github.com/invoke-ai/InvokeAI) | 27k stars；当日有提交；Apache-2.0 | 专业画布、Control Layers、图库与工作流 | 本地创作体验强；适合局部控制和资产画布 |
| [Acly/krita-ai-diffusion](https://github.com/Acly/krita-ai-diffusion) | 10k stars；2026-06 有提交；GPL-3.0 | Krita 中的生成、修补、扩图与工作流 | 适合作为人工修图和 AI 局部修复侧车 |
| [LykosAI/StabilityMatrix](https://github.com/LykosAI/StabilityMatrix) | 8.5k stars；2026-07 有提交；AGPL-3.0 | 多种本地生图包和模型的管理器 | 只有决定引入本地模型时才需要 |
| [YouMind-OpenLab/awesome-gpt-image-2](https://github.com/YouMind-OpenLab/awesome-gpt-image-2) | 8.2k stars；持续更新 | GPT Image 2 Prompt 与示例图库 | 适合发现 Prompt 结构；许可证和社区来源需逐项核验 |
| [cubiq/ComfyUI_IPAdapter_plus](https://github.com/cubiq/ComfyUI_IPAdapter_plus) | 6k stars；GPL-3.0 | ComfyUI 身份/风格参考控制 | 方法有参考价值；具体节点维护状态需采用前复测 |
| [mcmonkeyprojects/SwarmUI](https://github.com/mcmonkeyprojects/SwarmUI) | 4.3k stars；2026-07 有提交；MIT | ComfyUI 后端上的高性能易用工作台 | 本地模型与批量网格实验候选 |
| [wuyoscar/GPT-Image2-Skill](https://github.com/wuyoscar/GPT-Image2-Skill) | 3.7k stars；2026-07 有提交；MIT | Prompt Gallery、Agent Skill、CLI | 有 character design、gaming、editing 等分类，可作为 Prompt 结构库 |
| [rgthree/rgthree-comfy](https://github.com/rgthree/rgthree-comfy) | 3.2k stars；2026-06 有提交；MIT | ComfyUI 工作流可用性增强节点 | 只有正式采用 ComfyUI 后才有价值 |
| [YouMind-OpenLab/nano-banana-pro-prompts-recommend-skill](https://github.com/YouMind-OpenLab/nano-banana-pro-prompts-recommend-skill) | 1.7k stars；持续更新 | 带样例图的 Prompt 检索 Skill | “先看样例再选模板”的交互值得吸收；来源授权需谨慎 |
| [zanllp/infinite-image-browsing](https://github.com/zanllp/infinite-image-browsing) | 1.3k stars；2026-06 有提交；MIT | 跨 ComfyUI/A1111/Fooocus 的图像元数据、标签、搜索和聚类 | 当生成资产数量增加时，可补 iLab 的项目资产索引能力 |
| [kadevin/ilab-gpt-conjure](https://github.com/kadevin/ilab-gpt-conjure) | 628 stars；2026-07-13 有提交；AGPL-3.0 | GPT-image-2/OpenAI-compatible 图片工作台 | 当前最符合用户的云 API 主工作台要求 |

### 3.1 仓库类结论

这些仓库不是一个层级，不能按 stars 直接选“最好”：

```text
API 图片工作台：iLab GPT Conjure
本地生成引擎：ComfyUI / InvokeAI / SwarmUI
人工修图：Krita + krita-ai-diffusion
资产索引：Infinite Image Browsing
灵感/Prompt：GPT-Image2-Skill / awesome-gpt-image-2
```

---

## 4. Skill 与可复用工作流来源

本类同时覆盖 Agent Skill 和可复用生图工作流，合计超过 10 项。

| Skill / 工作流 | 关键设计 | 可吸收点 |
|---|---|---|
| [GPT-Image2-Skill](https://github.com/wuyoscar/GPT-Image2-Skill/tree/main/skills/gpt-image) | Skill + 分类 Gallery + CLI | 将“工艺规则”和“案例图库”分开；按 character/gaming/editing 精读 |
| [Nano Banana Prompt Recommend Skill](https://github.com/YouMind-OpenLab/nano-banana-pro-prompts-recommend-skill) | 动态分类、样例图强制伴随 Prompt、最多推荐 3 项 | Prompt 不能脱离结果图评价；先选模板再定制 |
| [cc-nano-banana](https://github.com/kkoppenhaver/cc-nano-banana) | 按 generate/edit/icon/story 路由 | 先识别资产任务类型，再选择执行器 |
| [OpenMinis · codex-image](https://github.com/OpenMinis/MinisSkills/tree/main/codex-image) | 文生图和参考图编辑分离；本地保存结果 | 非官方会话接口不适合作为稳定项目依赖 |
| [OpenMinis · nano-banana](https://github.com/OpenMinis/MinisSkills/tree/main/nano-banana) | 文生图、编辑、批处理；按任务选模型 | 小步编辑优先于整图重生；批处理必须明确任务列表 |
| [imagegen-cli](https://github.com/StevenLi-phoenix/imagegen-cli) | OpenAI/OpenRouter/Gemini 自动路由；先保存原始响应 | 失败时避免重复付费；必须记录成本和原始响应 |
| [gemini-skill](https://github.com/WJZ-P/gemini-skill) | 工具→脚本→浏览器三级回退；参考图上传和高清下载 | 执行路径与回退策略必须显式，不能静默换通道 |
| [openNanoBanana](https://github.com/GeeveGeorge/openNanoBanana) | 查询提取→搜参考→视觉验证→带参考生成 | 把“要找什么”和“如何画”分开；外部参考必须验证来源和适用性 |
| [ImageRouter Skill](https://github.com/DaWe35/image-router) | 多模型路由、统一生成/编辑端点、成本返回 | 模型适配层应统一输入，但保留供应商差异和实际成本 |
| [openrouter-image](https://github.com/Mindbreaker81/openrouter-image) | 生成、编辑、模型列表、输出列表 | 工作台必须支持模型发现、编辑和输出检索，而非只有生成按钮 |
| [Infinite Image Browsing Skill](https://github.com/zanllp/infinite-image-browsing/tree/main/skills/iib) | Prompt/参数搜索、标签、移动、聚类、元数据读取 | 生成结果必须进入可查询的资产状态，而不是散落文件夹 |
| [ComfyUI ControlNet workflow](https://docs.comfy.org/tutorials/controlnet/controlnet) | 姿势、深度、边缘条件 | 结构参考与风格参考分离 |
| [ComfyUI FLUX Kontext workflow](https://docs.comfy.org/tutorials/flux/flux-1-kontext-dev) | 参考图连续编辑 | 适合作为高控制修复链，不作为无记录的聊天式修改 |

### 4.1 Skill 类结论

高质量 Skill 的共同结构不是“塞一个长 Prompt”，而是：

```text
识别任务
→ 检查输入是否完整
→ 选择生成/编辑/批处理通道
→ 固定输出路径和元数据
→ 生成或编辑
→ 展示候选
→ 收集具体反馈
→ 小步迭代
→ 验收与归档
```

---

## 5. AI 艺术工作者与公开分享

本节只提取工作方法，不复制个人风格。

| 艺术工作者 / 分享 | 可提取方法 | 对项目的启发 |
|---|---|---|
| [Anna Ridler · Mosaic Virus](https://www.nvidia.com/en-us/research/ai-art-gallery/artists/anna-ridler/) | 自行拍摄约一万张郁金香并手工标注 | 角色参考包和药剂器具库本身就是核心创作资产 |
| [Sofia Crespo · Neural Zoo](https://nextnature.org/en/magazine/story/2020/interview-sofia-crespo) | 围绕物种建立数据集，再由模型重组，人工决定作品体系 | 先定义“视觉世界”，再生成单张作品 |
| [Sougwen Chung · MIT Technology Review](https://www.technologyreview.com/2025/02/17/1111387/ai-sougwen-chung-art-robots-collaboration/) | 把自身绘画数据、机器反馈与现场人工创作组成循环 | AI 输出应进入反馈环，不是终稿自动售货机 |
| [Refik Anadol · WIPO](https://www.wipo.int/en/web/wipo-magazine/articles/painting-with-data-how-media-artist-refik-anadol-creates-art-using-generative-ai-67301) | 数据来源、模型和展示空间共同构成作品 | 项目需要记录参考来源与用途，不能只留最终 PNG |
| [Scott Eaton · Machine Learning / AI](https://www.scott-eaton.com/category/ml-ai) | 使用自己的摄影和 CG 输出训练专用工具，并结合解剖造型能力 | 自有素材和人工造型判断比堆风格词更可靠 |
| [Helena Sarin · Adobe Research](https://research.adobe.com/lecture/zen-and-the-practicalities-of-ai-art-making/) | 自有水彩、摄影、食物造型与 GAN 结合 | AI 与人工媒介分工；最终统一质感由人工完成 |
| [Mario Klingemann · Conversation](https://www.diegoferrante.com/en/post/art-in-the-age-of-ai-a-conversation-with-mario-klingemann) | 关注无限生成中的选择问题，并尝试自动化筛选 | 候选淘汰与拒绝理由必须结构化 |
| [Gene Kogan · ml4a](https://ml4a.net/) | 开源课程、模型实验和创作工具教育 | 工作流应该可理解、可复现，而不是黑箱秘方 |
| [Pindar Van Arman · Verisart Interview](https://verisart.com/blog/interview-with-pindar-van-arman-ai-paint-robotic-arms) | 画几笔→分析→再画的创作反馈环 | 每轮只做有限变化并重新评价，而不是一次追求完美 |
| [Memo Akten · Deep Meditations](https://arxiv.org/abs/2003.00910) | 有控制地探索潜空间轨迹并构造叙事 | 系列图需要明确变化轴，而不是随机变化 |
| [Robbie Barrat · art-DCGAN](https://github.com/robbiebarrat/art-DCGAN) | 公开模型实验，利用模型误读形成作品 | 允许受控意外，但必须服务于卡牌语义 |
| [Jake Elwes · Zizi](https://www.jakeelwes.com/project-zizi-2019.html) | 与参与者共同建立经同意和补偿的数据集，公开讨论偏差 | 参考素材的来源、同意和授权不可忽略 |
| [Kris Kashtanova · Interview](https://www.creationsatelier.com/blog/experimental-ai-art-interview-with-kris-kashtanova) | 将 AI 图用于连续叙事并讨论作者、编排和版权 | 连续卡图的角色一致性与人工编排是独立劳动 |
| [Claire Silver · AI Collaborative Art](https://www.widsworldwide.org/get-inspired/video/claire-silver-ai-collaborative-art/) | 强调 AI 协作、选择和跨工具处理 | 不把某个模型当作唯一作者或唯一生产环节 |

### 5.1 艺术工作者类结论

1. 好的结果通常来自自建参考/数据体系，而不是万能 Prompt。
2. 筛选、编排、局部修改、再绘制是创作本体的一部分。
3. 作品系列需要受控变化轴和不变项。
4. 来源透明与素材授权是生产流程的一部分，不是发布前临时补救。

---

## 6. Prompt、模型和素材社区

| 平台 / 社区 | 主要用途 | 使用限制 |
|---|---|---|
| [Civitai](https://civitai.com/models) | Checkpoint、LoRA、工作流、触发词和样例 | 每个资源许可证独立；不得默认可商用 |
| [Midjourney Explore](https://www.midjourney.com/explore) | 热门构图、Prompt 表达、Moodboard 灵感 | 只作灵感和结构分析，不复制个人作品 |
| [YouMind Awesome GPT Image 2](https://github.com/YouMind-OpenLab/awesome-gpt-image-2) | 大量 Prompt 与结果配对 | 社区来源与许可证需逐项确认 |
| [GPT-Image2-Skill Gallery](https://github.com/wuyoscar/GPT-Image2-Skill/tree/main/skills/gpt-image/references) | Character Design、Gaming、Illustration、Editing 分类案例 | 用于学习 Prompt 结构，不直接拼接风格名 |
| [PromptHero](https://prompthero.com/) | 跨模型 Prompt 搜索 | 模型版本不同会导致 Prompt 失效；来源需核验 |
| [OpenArt](https://openart.ai/) | 角色、模型、工作流和编辑案例 | 商业平台；导出能力与授权按当期条款确认 |
| [Tensor.Art](https://tensor.art/) | 模型、LoRA、在线工作流和社区样例 | 资源许可证分散，需逐项记录 |
| [Krea](https://www.krea.ai/) | 风格参考、Moodboard、实时探索和增强 | 平台参数透明度有限，适合探索而非唯一真源 |
| [Adobe Firefly Gallery](https://www.adobe.com/community/gallery) | Prompt、结果和 Remix 灵感 | 只能在 Adobe 条款范围内理解其授权优势 |
| [Lexica](https://lexica.art/) | Prompt 与结果检索 | 更适合灵感发现，不适合项目版本管理 |
| [LiblibAI](https://www.liblib.art/) | 中文模型、LoRA、工作流社区 | 每项资源需检查作者、模型底座和商用条款 |
| [SeaArt](https://www.seaart.ai/) | 模型、角色、工作流和社区 Remix | 平台内容复杂，生产采用前必须做来源筛选 |

### 6.1 社区使用原则

- 社区素材只进入“灵感池”，不得直接进入“项目参考真源”。
- 只有来源、用途、许可证明确的图片或模型，才能进入生产参考包。
- 不以艺术家姓名作为固定风格词；改写为可观察的形式语言，例如色块分割、轮廓夸张、低强度环境光、纸张颗粒。
- Prompt 必须与样例图成对保存，不能只收藏文本。

---

## 7. 工作台比较

| 工作台 | 自接 API | 参考图库 | Prompt 模板 | 编辑 | 本地模型控制 | 项目资产管理 |
|---|---|---|---|---|---|---|
| iLab GPT Conjure | OpenAI-compatible Images/Responses | 强 | 强，支持 `@`/`#`/`~` Chip | 图层、局部擦除、参考图编辑 | 弱 | 中：历史强，但缺少完整项目状态机和版本谱系 |
| Open WebUI | OpenAI、ComfyUI、A1111 等 | 中 | 中 | 支持图像生成/编辑 | 强 | 弱：更偏通用聊天平台 |
| ComfyUI | 自身 API，可接大量模型 | 依赖扩展 | 工作流本身即模板 | 强 | 极强 | 弱：输出管理需额外工具 |
| InvokeAI | 本地后端为主 | 强，Board/Gallery | 工作流 | Canvas/区域控制强 | 强 | 中 |
| SwarmUI | ComfyUI 后端 | 中 | 参数预设/工作流 | 中 | 强 | 中 |
| Krita AI Diffusion | 可连接 ComfyUI | 使用 Krita 文档 | 工作流 | 人工绘制、选区修补、扩图强 | 强 | 依赖文件管理 |
| Infinite Image Browsing | 不负责生成 | 极强 | 不负责 Prompt 创作 | 不负责编辑 | 跨工具索引 | 强：标签、参数搜索、聚类、移动 |

### 7.1 当前结论

**保留 iLab GPT Conjure 作为主工作台，不迁移。**

理由：

- 用户已经在运行，切换成本为零。
- 支持 `Base URL`、`API Key`、模型名、调用方式和并发上限。
- 公共图库适合保存角色身份、服装、道具、色板和风格母图。
- `@图库`、`#颜色`、`~片段`适合实现次元旅人的 Prompt DSL。
- 有模板、历史、任务队列、结果归档和图层编辑。

当前不足：

- 没有针对一个游戏项目的资产清单与完成度视图。
- 没有明确的父图→变体→局部修复→导出版本谱系。
- 没有项目级审核状态和拒绝原因统计。
- OpenAI-compatible 只表示接口形态兼容，不表示所有供应商的参考图、mask、尺寸、质量参数语义一致。

这些不足暂时优先通过项目文档和工作规范解决，不建议立刻改造或更换工作台。

### 7.2 可选侧车

- 人工修图：优先 Krita；若需要 AI 局部修补，再考虑 `krita-ai-diffusion`。
- 本地模型：只有云 API 无法满足角色一致性或结构控制时，才引入 ComfyUI。
- 资产数量明显增大后：可引入 Infinite Image Browsing 做跨工具标签和元数据检索。

### 7.3 许可证与数据注意

- iLab 采用 AGPL-3.0。私人本地使用不要求公开项目素材；若修改后通过网络向他人提供服务，应按许可证核验源码开放义务。
- API Key、OAuth 文件、输入参考图、生成结果、SQLite 数据库、Prompt 模板和日志不得提交到公共 Git。
- 工作台许可证不等于生成图片许可证；生成图片的商用边界取决于实际模型、API 供应商、输入素材和当地规则。

---

## 8. 综合方法

本次研究支持以下项目级结论：

1. **先建立参考资产，再生成。** 角色设定表、服装结构、药剂包、瓶型、炼金符号和色板是第一批资产。
2. **把参考职责分开。** 身份、服装/道具、风格、构图/姿势、色板不能混成一张万能参考图。
3. **建立规范化 Brief，而不是万能 Prompt。** Brief 是模型无关真源；各模型 Prompt 只是适配层。
4. **先小规模盲测，再选主模型。** 同一 Brief、同一参考包、同一输出要求对比模型，不能用不同 Prompt 比结果。
5. **一次只改变一个变量。** 角色漂移时回到最早稳定母图，不能继续在漂移图上叠加编辑。
6. **结果必须带谱系。** 保存父图、模型、Prompt、参考图、参数、成本、修改说明和拒绝理由。
7. **缩略图优先于高清细节。** 卡图在真实卡框中不能一眼读懂时，高清细节没有价值。
8. **按系列分批。** 先完成一个视觉系列的基准与 3～5 张试产，再扩展，不直接批量生产 45 张。
9. **人工统一是最终工序。** 修手、瓶体、服装、轮廓、色彩和笔触不能完全交给模型。
10. **项目风格来自可描述规则。** 不依赖某位在世艺术家的姓名或某个平台的隐藏模型。