# Linux ARM64 移植

Linux 桌面端在 `linux` 分支开发，使用 .NET 10 与 Avalonia，复用 Windows 端的角色配置、任务调度、提醒与门派依赖执行代码。发布条件是功能移植和原生 ARM64 验证全部通过。

## 已完成的原生验证

Ubuntu ARM64 上已通过程序构建、共享业务逻辑和原生窗口自动化检查。界面测试在 Xvfb 与 Openbox 中使用模拟 Claude，验证四个人物加载、中文与表情流式回复、后台任务独立记录和四道友依赖交接。[首轮 ARM64 验证](https://github.com/AHuaWinliver/xianpal-ai-companion/actions/runs/42342609755)

![ARM64 Linux 原生对话窗口，使用模拟回复](images/linux-conversation.png)

![ARM64 Linux 原生门派大堂，使用隔离测试数据](images/linux-workshop.png)

当前仍需完善原生终端与 Claude Background 模式、托盘与自启动、AI 路线和光轨、灵器移动、外置人物全屏演绎，以及真实目标桌面环境下的输入和窗口验证。完整功能验收仍在进行。

## 构建

在项目根目录运行以下命令。

```sh
dotnet publish src/DesktopTasks/XianPal.Linux -r linux-arm64 --self-contained true -c Release -o build/linux-app
build/linux-app/xianpal --self-test
```

人物原画与动画使用现有 Windows 素材生成工具制作。Linux 构建流程会将这些资源交给原生 ARM64 runner，分别验证业务逻辑与桌面窗口。构建产物中的 `assets/`、`scenes/`、`performances/` 与程序放在同一目录。

运行时数据放在 XDG 状态目录下的 `xianpal/`，角色资源包放在 XDG 数据目录下的 `xianpal/Characters/`。Claude Code 由用户在 Linux 上独立安装与配置。

## 发布验收

| 功能 | 发布前需要验证 |
| --- | --- |
| 人物与桌面互动 | 四位角色、透明边缘、双向步态、悬停、拖动、贴边、隐藏恢复、多显示器与缩放 |
| Claude 对话 | 中文与表情、流式刷新、上下文续接、取消、原生终端、错误与权限提示 |
| 文件请求 | 文件与文件夹拖放、吃文件动作、需求确认与正确工作目录 |
| 定时任务 | 编辑、每日与每周与间隔、阻塞与并行、后台与可见终端、运行情况与历史、重启恢复 |
| 提醒 | 每日提醒、倒计时、四位角色演绎、提示框、稍后提醒、减少动画、单显示器与 Esc |
| 事件与门派 | 事件确认、角色职责与配置、目录隔离、依赖交接、并发限流、取消与中断记录、大堂状态 |
| 角色资源 | 热更新、原子替换、错误资源回退、完整帧数与透明度校验、外置角色演绎 |
| AI 桌面行为 | 主动发言、休息提醒、历史上的今天、批量路线、彩色光轨与长途灵器 |
| 安装与更新 | ARM64 包、依赖、菜单图标、自启动、更新、卸载、离线部署材料与 SHA256 |

自动化桌面验证使用隔离目录和模拟 CLI，截图与测试结果作为 Actions 产物保存。模拟验证通过后仍需检查真实 Linux 桌面会话下的交互和兼容性。

[返回首页](../README.md) · [开发指南](DEVELOPMENT.md)
