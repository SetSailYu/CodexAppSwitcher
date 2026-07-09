# CodexAppSwitcher 项目上下文

最近更新：2026-07-09 10:49 (Asia/Shanghai)

## 会话进度

已完成：

- 当前产品边界已收敛为 `auth.json` 主导切换：添加账号只保存 WebView2 登录态；账号行 `接管/完成` 或当前账号 `更新` 才采集 live `.codex\auth.json`。
- 切换流程固定为：关闭全部 Codex App，写入目标账号 live auth，标记目标账号为当前账号，再自动启动 Codex App。
- Token、rollback、Roaming/MSIX 替换、提权 Helper、package-state 检测、宿主保护模式和模拟切换入口均已移出当前产品链路。
- 主窗口已改为两栏布局；账号列表使用固定操作列，当前账号使用整行背景高亮。
- 桌面额度悬浮球已完成：显示 5 小时额度和每周额度，支持手动刷新，主窗口关闭时可继续保留。
- 托盘后台入口已完成：托盘图标使用软件 Logo，右键菜单为暗色样式，只保留打开主窗口、显示/隐藏额度挂件和退出程序。
- 当前版本已定为 `v1.1.0`：项目元数据、manifest、主窗口版本显示、`README.md` 和 `CHANGELOG.md` 均已更新；输出到 `bin\CodexCheckApp` 构建通过，0 警告 0 错误。
- `README.md` 已新增“界面预览”区域，引用脱敏后的主窗口截图和桌面额度悬浮球截图；图片位于 `CodexAppSwitcher/Assets/screenshots/`。
- VS 发布失败已修复：`CodexAppSwitcher.csproj` 已加入 `InterceptorsNamespaces`，支持 `Microsoft.Extensions.Configuration.Binder.SourceGeneration`；`dotnet publish -p:PublishProfile=FolderProfile` 已通过。
- 主窗口顶部白色系统边框已修复：`MainWindow.xaml` 使用 `WindowChrome` 关闭非客户区 frame，并设置窗口背景为应用底色；输出到 `bin\CodexCheckApp` 构建通过，0 警告 0 错误。
- README 界面预览已更新为最新三张脱敏截图：主窗口、桌面额度悬浮球和托盘右键菜单；图片位于 `CodexAppSwitcher/Assets/screenshots/`。

进行中：

- 等待用户确认 README 截图展示和脱敏效果。

待开始：

- 如用户确认 UI 通过，可继续准备 GitHub Release 发布包和发布说明。
- 若后续反馈切换无效，只围绕 live auth 路径、快照来源、Codex App 是否完全退出和启动读取时机排查。

## 重要决策

- 当前只保留 auth 主导真实切换，避免 Roaming/MSIX 附加状态造成个人资料加载异常。
- 关闭主窗口不等于退出程序；后台状态统一由托盘图标承载，退出必须走托盘菜单。
- 桌面额度悬浮球只展示已解析用量和当前账号基础信息，不展示或导出任何登录态敏感内容。
- `v1.1.0` 作为当前功能增强版本，覆盖两栏布局、固定操作列、桌面额度悬浮球、托盘后台入口和托盘菜单优化。

## 快速恢复说明

从 `D:\Object\CodexAppSwitcher\CodexAppSwitcher\CodexAppSwitcher.sln` 进入。不要把运行或测试输出改到 `.tmp`；使用 VS2022 或默认 `bin\Debug` 路径。当前构建校验命令为 `dotnet build .\CodexAppSwitcher\CodexAppSwitcher.csproj -c Debug -v minimal -o .\CodexAppSwitcher\bin\CodexCheckApp`；发布验证命令为 `dotnet publish .\CodexAppSwitcher\CodexAppSwitcher.csproj -p:PublishProfile=FolderProfile -v minimal`。

## 归档

- 旧长上下文已归档到 `.AIINFO/archive/context-20260703-1527.md`。
- 2026-07-09 10:13 前的长上下文已归档到 `.AIINFO/archive/context-20260709-1013.md`。
