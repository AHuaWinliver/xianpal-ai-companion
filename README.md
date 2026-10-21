<div align="center">

<img src="docs/images/hero.png" alt="仙伴 XianPal：四位国风 Q 版 AI 桌面伙伴，连接本机 Claude Code" width="100%">

# 仙伴 XianPal · AI 桌面伴侣

**让 AI 不止在对话框里，也陪在你的桌面上。**

四位国风 Q 版伙伴 × 本机 Claude Code × 对话、文件处理与定时任务

[![版本 0.5.0 · Preview](docs/images/version.svg)](CHANGELOG.md)
![Windows 10 / 11](docs/images/windows.svg)
[![Apache-2.0](docs/images/license.svg)](LICENSE)

[下载与安装](#quick-start) · [看看能做什么](#features) · [定制自己的角色](#custom-characters) · [从源码构建](#build) · [English](README.en.md)

</div>

> 一只会陪你说话、接住文件、记得提醒，还能把任务交给 Claude Code 的桌面伙伴。  
> 桌面程序在本地运行；AI 能力由你自行安装、配置和授权的 **Claude Code CLI** 提供，不内置模型或 API 密钥。

**0.5 开放预览版，欢迎一起共建。** 当前发布号为 **0.5.0**，功能、接口和素材格式仍在迭代，不代表 1.0 稳定版。欢迎提交问题、原创角色与代码 PR；使用重要任务前，请备份配置并先在测试目录验证。

<a id="features"></a>
## 不只好看，也能一起把事情做好

| 当你想…… | 仙伴可以…… |
| --- | --- |
| 有个有性格的 AI 伙伴 | 切换四位角色，按角色口吻聊天；开启主动发言、趣味介绍和休息提醒 |
| 不来回切换聊天窗口 | 在宠物旁输入需求，查看最新回复，双击气泡展开会话动态并继续回复 |
| 把一份文件交给 AI | 拖放文件或文件夹，看伙伴“吃掉”它，再确认需求与工作目录 |
| 把重复工作交给 Claude | 设置每日、每周或间隔任务，选择阻塞 / 并行，查看历史运行与对话 |
| 到点有人叫你一下 | 设置倒计时或每日提醒，选择专属全屏演出或安静的提示框 |
| 让桌面更有生气 | 悬停互动、拖动追随、随机漫游、边缘吸附与半透明彩色尾迹 |
| 安装后添加自己的角色 | 复制外置角色资源包，运行中自动发现和刷新，不需要重新安装 |

### 四位伙伴，四种陪伴方式

<img src="docs/images/characters.png" alt="青衡、绯笺、苍芷与墨黑灵龟小墨的实际角色素材" width="100%">

| 角色 | 性格与灵感 | 专属提醒 |
| --- | --- | --- |
| **青衡** | 温和可靠的青衣游修，陪你理清思路、稳稳推进 | **御剑飞仙**：召剑、踏剑腾空、巡游、落剑提醒 |
| **绯笺** | 轻快细心的符箓手艺人，把计划写得一笔一画 | **灵符报时**：挥袖放符、轻跃、灵符环绕 |
| **苍芷** | 安静体贴的草木药师，提醒你留些余裕给自己 | **草木知时**：催生藤叶，落叶与萤光缓缓散去 |
| **小墨** | 稳稳当当的墨黑云纹灵龟，慢一点也没关系 | **墨涟唤醒**：轻跃落地，金边墨涟与云纹向外舒展 |

### 动起来，才像真的陪在身边

<div align="center">
<img src="docs/images/walking.gif" alt="四位角色的实际行走循环：人物对侧摆臂，灵龟四足交替" width="720">
</div>

24 相位行走、自然屈肘、轻微颈部联动和独立眨眼。人物左右手脚对侧协调，小墨采用四足交替步态。动作由随源码提供的透明原画在本地组合，**构建和安装不调用图片生成服务**。

### 说一句需求，接上你的 Claude Code

<img src="docs/images/workbench.png" alt="由真实界面控件渲染的会话动态和定时任务示例，使用隔离演示数据，无真实模型调用" width="100%">

*界面示例使用真实控件与预设演示数据，不是一次真实模型执行记录，也不包含私人路径或聊天。*

- **对话**：最新回复气泡、可手动关闭 / 固定的动态窗口、直接继续回复。
- **文件**：拖入文件 / 文件夹，确认工作目录和需求后再发送；“吃文件”动画**不会删除原文件**。
- **任务**：每日 / 每周 / 间隔调度，指定目录、阻塞或并行策略，运行记录与历史对话。
- **运行方式**：后台执行、PowerShell / Windows Terminal 可见窗口、原生 CLI 交互；支持适用 CLI 版本的原生 Background。
- **桌面**：托盘隐藏与恢复、桌面 1 / 所有虚拟桌面显示、可用时指定任务窗口的虚拟桌面。

### 提醒，也可以有一点仪式感

<div align="center">
<img src="docs/images/sword-flight.gif" alt="青衡御剑飞仙：人物与飞剑共同沿弧线飞行，带出半透明剑光尾迹，再回中落剑" width="640">
</div>

每日提醒与倒计时；四位角色各有演绎。也可以选择 **仅提示框** 或开启 **减少动态**。全屏演出只在宠物所在显示器内展开，可按 **Esc** 退出，或选择稍后 5 分钟提醒。倒计时保持轻量提醒，不加入大型演出。

<a id="quick-start"></a>
## 几分钟，领一位伙伴回桌面

### 方式一：安装使用

1. 在 [GitHub Releases](https://github.com/AHuaWinliver/xianpal-ai-companion/releases) 下载 `XianPal-0.5.0-Setup.exe`，并按需核对发布页的 SHA256。
2. 在 Windows 10 / 11 上运行安装器。它会创建开始菜单快捷方式，并配置当前用户的宠物联动。
3. **右键宠物**打开菜单，选择角色、设置 Claude Code 默认工作目录，或进入提醒 / 定时任务。
4. 使用 AI 功能前，先确认本机 Claude Code 已能在终端正常运行、完成登录或服务配置。

> 若尚未发布 Release，下载页可能暂不可用，可先按下方步骤从源码构建。安装器未做代码签名，Windows 可能提示未知发布者，请确认文件来源。

### 常用交互

| 操作 | 用途 |
| --- | --- |
| 右键宠物 | 打开功能和设置菜单，并收起气泡 |
| 单击 / 双击宠物 | 角色互动；按当前设置触发角色发言或短途跑动 |
| 拖动宠物 | 追随鼠标移动，靠近屏幕边缘可吸附 |
| 双击回复气泡 | 展开会话动态；展开后可选中文本并继续回复 |
| 托盘图标右键 | 显示 / 隐藏宠物，恢复窗口与退出 |
| 拖入文件 / 文件夹 | 准备文件处理需求与对应工作目录 |

菜单会随 AI 开关、当前角色和运行状态变化；先在小目录里验证任务和权限，再用于真实工作。

<a id="build"></a>
## 从源码构建

**环境**：Windows 10 / 11、系统 .NET Framework 4.x 编译器、PowerShell。无需 Node.js、NuGet、图片生成账号或构建用 API 密钥。

也可以下载仓库源码 ZIP 并解压，再执行构建命令；Git 仅用于下面的克隆步骤，不是编译依赖。
建议解压到类似 `C:\src\xianpal` 的短目录；过深的目录可能触发 .NET Framework 的传统路径长度限制。

```powershell
git clone https://github.com/AHuaWinliver/xianpal-ai-companion.git
cd xianpal-ai-companion

# 生成动画素材、编译程序和安装器
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1

# 直接启动构建出的程序
.\build\payload\OrbitPet.exe
```

| 生成内容 | 路径 |
| --- | --- |
| 安装包 | `dist/XianPal-0.5.0-Setup.exe` |
| 可运行程序 | `build/payload/OrbitPet.exe` |
| 全动作离线预览 | `build/previews/index.html` |
| 全屏演绎预览 | `build/previews/performances.html` |
| README 展示素材 | `docs/images/` |

只开发、不制作安装包时：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipInstaller
```

当前公开版本为 **0.5.0（Preview）**。应用命名空间为 `XianPal`，安装器为 `XianPal.Installer`。

Windows 任务管理器中的启动项与程序文件描述使用“仙伴 · AI桌面伴侣”。更新后的程序在正常启动时自动迁移旧 `OrbitPet` 启动项，保留原启动命令及 Windows 已有的启用/禁用状态；没有启动项时不会自行开启自启动。

<a id="custom-characters"></a>
## 定制自己的角色

不只使用内置伙伴，你也可以用自己的原创形象、人设和专属演出，打造一位 AI 桌面伴侣。

**0.5 支持外置角色资源包热更新，无需重新安装。** 右键宠物 → **切换形象 → 打开角色资源目录**，放入完整人物文件夹，待校验后在菜单中选择。更新当前角色的有效资源后，会自动刷新；坏包不会替换已加载的形象。默认目录为 `%LOCALAPPDATA%\XianPal\Characters`，也可手动“刷新外置角色 / 查看校验结果”。

资源包是 **角色配置＋12 种状态的 240 张透明 PNG 动作帧＋校验清单**，不是单张图片自动生成全部动作。新增外置人物不需要改应用源码；制作方仍需完成原画与动作，并用导出器生成成品包。[查看资源格式、导出命令和热更新说明](docs/CHARACTER-PACKS.md)。

### 从哪里开始？

1. **确定形象和人设**：复制 [人物定制规格单](docs/templates/CHARACTER-SPEC.md)，记录名字、体型、配色、说话风格、台词及素材来源。先确认正常视角和侧向造型，再制作动作；不要在修动作时顺带改变五官、服饰或头身比例。
2. **准备透明分层素材**：人体需要中性头、躯干、两条完整肩袖与手掌、两条含鞋靴的腿，以及张嘴/闭眼参考头；四足角色使用对应的头颈、身体、四肢与尾巴。正常与行走视角分别准备，按实际像素填写 `regions.json`，不要盲切九宫格。详细部件索引和绘图说明见 [制作规范](docs/CHARACTER-CUSTOMIZATION.md)。
3. **连接关节并补齐动作**：对齐颈根、肩口、掌心、髋点和脚底。双足保持“左腿前、右臂前”的对侧步态，四足采用对角交替；行走至少 24 个不同相位，肘部自然弯折，头颈轻微联动。两条手臂/前肢始终处于最外层，避免被身体遮挡。
4. **加入表情和特色演出**：眼睛/嘴使用局部贴片，不替换整张头导致脸型漂移；准备招手、查看、吃文件和擦嘴等交互。全屏演绎按人物性格设计，不把同一套动作套给所有角色，并保留减少动态、Esc 退出和单显示器边界。
5. **预览、确认后交付**：逐帧检查双向行走、停走、动作过渡、连接、透明边缘与裁切，再验收全屏演出。确认后使用 [资源包导出工具](tools/ExportCharacterPack.ps1) 分享人物，使用者只复制资源、不重装程序；只有修改程序能力时才需要重新构建应用。不以自动测试通过代替视觉验收。

### 资源包与底层制作入口

只添加外置人物时，填写 [角色配置模板](docs/templates/character-profile.json) 并提供完成的动作帧即可，下面的源码入口供修改内置渲染器、骨架或程序功能时使用。

| 定制内容 | 源码 / 素材入口 |
| --- | --- |
| 名字、人设、台词、提醒样式 | [PetCharacter.cs](src/DesktopTasks/XianPal/PetCharacter.cs) |
| 正常与侧向原画、分层裁区 | [art/rigs](art/rigs)、[art/side-rigs](art/side-rigs)、人体侧向躯干 [art/side-torsos](art/side-torsos) |
| 素材导入、动作帧与表情贴片生成 | [BuildOriginalCharacters.cs](tools/BuildOriginalCharacters.cs) |
| 颈根、肩袖、屈肘、髋腿和手势目标 | [CompanionMotion.cs](src/DesktopTasks/XianPal/CompanionMotion.cs) |
| 运行时加载、速度自适应和表情合成 | [PetForm.cs](src/DesktopTasks/XianPal/PetForm.cs)、[PetForm.Expressions.cs](src/DesktopTasks/XianPal/PetForm.Expressions.cs) |
| 专属全屏演绎与道具时间线 | [ReminderPerformance.cs](src/DesktopTasks/XianPal/ReminderPerformance.cs) |

现有 ID `orbit`、`spark`、`moss`、`pippin` 为内置人物保留。外置人物使用自己的唯一 ID，不占用这些名称；动态列表无需修改内置四角色数量。若要新增编译进程序的内置体型/渲染算法，仍需同步几何分支、预览和测试，见 [人物定制指南](docs/CHARACTER-CUSTOMIZATION.md)。

### 本地查看自己的角色

在 Windows 的项目根目录执行，无需图片生成账号，不调用 Claude 或付费模型：

```powershell
# 生成角色素材、程序和离线预览，暂不制作安装包
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipInstaller

# 检查动作协调性、素材连接与全屏演绎
.\tests\companion-motion-test.ps1
.\tests\original-assets-test.ps1
.\tests\reminder-performance-test.ps1
```

构建后打开 `build/previews/index.html` 查看全部动作，打开 `build/previews/performances.html` 查看演出。原画与源码应保存在 `art/`、`src/`、`tools/` 等输入目录，**不要把唯一一份手工素材放在会被构建替换的 `build/payload` 中**。

[完整制作规则与常见问题排查](docs/CHARACTER-CUSTOMIZATION.md) · [可复制的规格单](docs/templates/CHARACTER-SPEC.md) · [全屏演绎说明](docs/REMINDER-PERFORMANCES.md)

制作好动作帧后，按 [资源包教程](docs/CHARACTER-PACKS.md) 执行 `tools/ExportCharacterPack.ps1`；无需在接收方电脑上安装编译工具。外置人物的表情画入完整动作帧，不使用内置头部贴片。全屏演出目前复用四种预设时间线，不加载外部脚本。

请使用自己有权授权的原创素材，随贡献保留实际提示词、参考授权和人工修改记录；不要只给现有知名角色换色改名。素材权利说明见 [ASSET-LICENSE.md](ASSET-LICENSE.md)，提交要求见 [贡献指南](CONTRIBUTING.md)。

## 验证与贡献

### 一起把 0.5 做得更好

项目持续接受代码贡献，不需要等到“大功能全部做好”才提交。适合参与的方向包括：

- **角色与动画**：原创人物、动作过渡、角色制作工具和可视化预览。
- **桌面体验**：多显示器、不同 DPI、窗口恢复及性能优化。
- **CLI 与任务**：不同运行模式的兼容性、任务记录和错误反馈。
- **质量与文档**：隔离测试、可复现问题、上手教程和翻译。

先通过 [Issues](https://github.com/AHuaWinliver/xianpal-ai-companion/issues) 讨论改动范围，再按 [贡献指南](CONTRIBUTING.md) 提交小而清晰的 PR，附复现步骤、测试结果；涉及角色或界面时附预览。不要在公开内容中上传凭据、私人会话或内网数据。

```powershell
.\tools\TestRepository.ps1
.\tests\release-source-test.ps1
.\tests\startup-registration-test.ps1
.\tests\character-pack-test.ps1
.\tests\smoke-test.ps1
.\tests\companion-motion-test.ps1
.\tests\original-assets-test.ps1
.\tests\reminder-performance-test.ps1
```

专项测试使用模拟 CLI 或隔离目录，不调用真实付费模型。交互窗口、虚拟桌面与安装测试需要 Windows 桌面会话。CI 的实际结果请查看仓库 [Actions](https://github.com/AHuaWinliver/xianpal-ai-companion/actions)，不以本地通过代替全部 Windows 环境的兼容性验证。

欢迎提交可复现的问题、界面建议、动画修复和原创角色贡献。先看 [贡献指南](CONTRIBUTING.md)，再到 [Issues](https://github.com/AHuaWinliver/xianpal-ai-companion/issues) 交流。请不要上传凭据、真实工作记录或未经授权的角色素材。

## 在把任务交给它之前

- **本地程序不等于离线模型**：AI 请求由 Claude Code 按你的服务配置发出，可能联网并消耗额度。
- **安装会修改当前用户的联动配置**：追加本项目 Hooks 并备份设置，保留其他处理器；详见 [隐私与配置](docs/PRIVACY.md)。
- **不是安全沙箱**：发送文件或执行任务后，Claude 的读写能力由你的账户、权限和 CLI 设置决定。不要自动信任陌生目录或提示词。
- **可按需关闭 AI 主动功能**；其他本机 Claude 会话的采集默认关闭，不自动终止其他会话。
- 原生 Background 依赖 CLI 能力；Windows Terminal、中文 / emoji 字体和虚拟桌面效果也受本机环境影响。暂不提供语音对话。
- 本项目与 Anthropic、Microsoft 或任何影视、小说、游戏厂商无隶属关系。

## 文档与许可

[开始使用](docs/GETTING-STARTED.md) · [角色资源包热更新](docs/CHARACTER-PACKS.md) · [开发与动画](docs/DEVELOPMENT.md) · [人物定制指南](docs/CHARACTER-CUSTOMIZATION.md) · [全屏演绎](docs/REMINDER-PERFORMANCES.md) · [GitHub 发布指南](docs/GITHUB-PUBLISH.md) · [更新日志](CHANGELOG.md)

代码采用 [Apache-2.0](LICENSE)。内置角色为 AI 辅助原创设计，具体授权范围见 [素材许可](ASSET-LICENSE.md) 与 [来源记录](art/PROVENANCE.md)；AI 生成不等于绝对不侵权担保。系统字体不随项目分发。

安全问题请先阅读 [安全报告](SECURITY.md)，不要在公开 Issue 中泄露漏洞细节或敏感数据。

---

<div align="center">

**给忙碌的桌面，留一个有回应的角落。**

如果仙伴让你的桌面多了一点温度，欢迎用一个 Star 支持它。

</div>
