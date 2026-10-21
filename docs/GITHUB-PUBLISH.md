# 发布仙伴 0.5.0 预览版到 GitHub

目标仓库：<https://github.com/AHuaWinliver/xianpal-ai-companion>。

本指南只描述发布步骤；源码整理过程不会替你创建仓库、提交、推送或创建 Release。

## 准备发布目录

优先使用 `dist/XianPal-0.5.0-Source.zip` 的解压内容作为初次提交目录。它只包含公开文件及校验清单，不含个人设置、任务、聊天、旧素材或备份。

也可以直接提交当前源码根目录中的公开文件，但先检查 `.gitignore` 和 `git status`。不要从外层旧工作区整体上传，更不要提交 `build/`、`dist/`、`work/`、Claude 配置或 API 密钥。

运行检查和构建：

```powershell
.\tools\TestRepository.ps1
.\tests\release-source-test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\tests\smoke-test.ps1
.\tools\ExportOpenSource.ps1
```

`ExportOpenSource.ps1` 默认拒绝覆盖已存在的同名源码包。如需重复导出，请使用不同的 `-Destination` 文件名；先检查旧文件，不要误删原版。

## 首次上传

以下命令仅适合新建的本地仓库。如果远程已有提交，请先克隆远程、把整理后的公开文件合入并审阅差异；不要强制覆盖远程历史。

```powershell
git init
git branch -M main
git add .
git status
git diff --cached --stat
git commit -m "Release XianPal 0.5.0"
git remote add origin https://github.com/AHuaWinliver/xianpal-ai-companion.git
git push -u origin main
```

`.gitignore` 已为必需构建脚本放行，包括演出预览和 README 素材生成器。推送后查看 Actions 的真实结果，不在 README 手工写“CI 已通过”。

## 建议的仓库展示信息

- **名称**：`xianpal-ai-companion`
- **About**：`国风 Q 版 AI 桌面伴侣，让 Claude Code 陪你聊天、处理文件与完成定时任务。A lively Windows AI companion powered by your local Claude Code CLI.`
- **Topics**：`ai-companion`、`desktop-pet`、`claude-code`、`windows`、`csharp`、`winforms`、`productivity`、`virtual-pet`。
- **Social preview**：在仓库设置中上传 `docs/images/social-preview.png`。该图采用现有角色素材制作，没有引入外部 IP 图片。
- 主 README 为中文，`README.en.md` 为英文；所有展示图和动图采用仓库相对路径，不依赖本机绝对路径。

## 发布 v0.5.0

在 Releases 中新建发布，Tag 使用 `v0.5.0`，标题建议 `XianPal 0.5.0 Preview · 欢迎一起共建 AI 桌面伴侣`，将此次发布标记为 **pre-release（预发布）**。引用 `CHANGELOG.md` 的 0.5.0 说明，附上：

- `XianPal-0.5.0-Setup.exe`：Windows 安装器。
- `XianPal-0.5.0-Source.zip`：含原画、构建工具、测试、文档的完整源码包。
- SHA256 文件：与附件逐一对应。

说明安装器未签名、AI 需用户自行配置 CLI、可能消耗模型额度、虚拟桌面与 Background 依赖环境，以及安装会修改当前用户的 Hooks。

发布前继续执行 [发布检查清单](RELEASE-CHECKLIST.md)，审阅授权和敏感信息。**0.5.0 为持续迭代的公开预览版，不是稳定性、授权或兼容性的担保。** 鼓励贡献代码，但不要把尚未实现的能力写成已交付功能。
