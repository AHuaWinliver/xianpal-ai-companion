# 仙伴 @VERSION@ · Linux @ARCH@ 验收候选包

内含 @RUNTIME@ 自包含程序、原版角色和 Noto 字体；兼容包还包含程序私有 C++ 库及许可。不包含 Claude、模型、密钥或系统 RPM/DEB。

源码提交：`@SOURCE_COMMIT@`

自动验证：@VALIDATION_URL@

容器检查验证用户空间，仍共用CI主机内核。你的真实内网内核、TurboVNC会话和Claude服务还需要验收；这不是Linux稳定发布版。

## 校验与试用

1. 先校验外部 `.sha256`，再用 `tar -xzf` 解压，保留执行权限。
2. 进入目录执行 `sha256sum -c SHA256SUMS.txt`。
3. 在当前图形桌面的终端执行 `sh diagnose-linux.sh`、`sh launch.sh --version`、`sh launch.sh --self-test`。
4. TurboVNC首次建议CPU渲染：`XIANPAL_RENDERER=software sh launch.sh`。
5. 默认设置中选择真正的Claude程序文件、工作目录、中英文和各角色JSON配置。`~/APP/claude`下应选能执行`--version`的文件，不是文件夹。

宠物本身不要求安装.NET SDK、Node或Python；独立CLI可能有自身运行环境要求。系统仍需具备ICU、OpenSSL、Fontconfig和X11等原生库，可见任务还需本机终端。缺少库时，应按目标系统准备获准的离线依赖，不要在内网目标机上运行联网制作脚本。

诊断与自检不安装软件、不调用模型。真实AI功能通过你配置的服务执行，可能消耗用量。

## 验收项目

- 四个人物左右行走、拖动加速、停步、吸附、隐藏恢复、比例及透明边。
- 中文与表情、首次气泡定位、继续对话和拖文件后确认需求。
- 定时任务道友、目录、重叠策略、终端、独立历史和CLI支持时的Background模式。
- 当前显示器内的倒计时与提醒、减少动态、关闭和角色演绎。
- 大堂桌案、所有运行状态、历史、真实交接与窗口自适应。
- 外置角色发现、有效热更新、损坏时保留当前形象和移除后的回退。

光轨要求X11合成器和鼠标穿透同时可用，否则停用覆盖层。请实际检查VNC桌面，不能只凭自动截图验收。

直接试用通过后先退出宠物，再执行`sh install.sh`选择目录；静默示例：`sh install.sh --silent --install-dir "$HOME/.local/opt/xianpal"`。升级保留旧安装，配置与角色包在XDG用户目录，不自动结束独立Claude任务。

记录通过、失败和未测项；分享诊断前隐藏内网地址、密钥、私人路径和对话内容。
