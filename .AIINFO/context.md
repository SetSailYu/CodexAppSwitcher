# CodexAppSwitcher 项目上下文

最近更新：2026-07-13 10:12 (Asia/Shanghai)

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
- `v1.1.2` 用量接口兼容已完成：`wham/usage` 只返回历史用量、不返回额度窗口时不再报解析失败，界面显示 `--` / `未提供`。
- 主界面、桌面挂件和 README 已将“5 小时”展示改为“短期”，避免固定暗示旧额度窗口；旧“5 小时”文本仅保留在解析测试样例和更新日志说明中。
- `CodexAppSwitcher.csproj`、`app.manifest`、README 和主窗口底部版本已同步到 `1.1.2`。
- 2026-07-13 09:19 已验证：`dotnet build .\CodexAppSwitcher\CodexAppSwitcher.csproj -c Debug -v minimal -o .\CodexAppSwitcher\bin\CodexCheckApp` 通过，0 警告 0 错误；`dotnet run --project .\CodexAppSwitcher.Tests\CodexAppSwitcher.Tests.csproj -c Debug --no-build` 输出“全部测试通过”。
- 2026-07-13 09:40 修复真实返回结构：`primary_window.limit_window_seconds=604800` 且 `secondary_window=null` 时，按每周额度解析为 98% 剩余，短期额度显示未提供。
- 2026-07-13 09:40 已验证：主项目构建到 `CodexAppSwitcher\bin\CodexCheckApp` 通过；测试项目构建到 `CodexAppSwitcher.Tests\bin\CodexCheckTests` 通过，运行测试 DLL 输出“全部测试通过”。
- 2026-07-13 09:47 账号列表已隐藏短期额度和短期重置列，只保留账号、状态、每周额度、每周重置；桌面挂件和当前账号悬浮提示仍保留短期信息。
- 2026-07-13 09:47 已验证：`dotnet build .\CodexAppSwitcher\CodexAppSwitcher.csproj -c Debug -v minimal -o .\CodexAppSwitcher\bin\CodexCheckApp` 通过，0 警告 0 错误。
- 2026-07-13 09:54 桌面额度挂件已改为每周额度单指标：外圈进度、中心数字和 tooltip 均使用每周额度；主窗口默认尺寸调整为 `1480x860`，最小尺寸为 `1180x720`。
- 2026-07-13 09:54 已验证：`dotnet build .\CodexAppSwitcher\CodexAppSwitcher.csproj -c Debug -v minimal -o .\CodexAppSwitcher\bin\CodexCheckApp` 通过，0 警告 0 错误。
- 2026-07-13 10:12 已定版 `v1.1.2`：`README.md` 和 `CHANGELOG.md` 已补齐发布说明，Debug 构建、测试 DLL、`FolderProfile` Release 发布均通过。
- 2026-07-13 10:12 已生成发布包：`CodexAppSwitcher\bin\Release\net8.0-windows\publish\CodexAppSwitcher-v1.1.2-win-x64.zip`，大小约 75 MB；压缩包来源仅为 `publish\win-x64` 目录。

进行中：

- `v1.1.2` 发布准备已完成，等待用户决定是否执行 git commit / tag / push / GitHub Release。

待开始：

- 如用户确认，可继续执行 git commit、tag、push 或 GitHub Release；这些操作需要用户明确确认。
- 若后续反馈切换无效，只围绕 live auth 路径、快照来源、Codex App 是否完全退出和启动读取时机排查。

## 重要决策

- 当前只保留 auth 主导真实切换，避免 Roaming/MSIX 附加状态造成个人资料加载异常。
- 关闭主窗口不等于退出程序；后台状态统一由托盘图标承载，退出必须走托盘菜单。
- 桌面额度悬浮球只展示已解析用量和当前账号基础信息，不展示或导出任何登录态敏感内容。
- `v1.1.0` 作为当前功能增强版本，覆盖两栏布局、固定操作列、桌面额度悬浮球、托盘后台入口和托盘菜单优化。
- `v1.1.2` 对用量窗口做兼容适配：按 `limit_window_seconds` 分类额度窗口；7 天窗口显示为每周额度；历史用量结构仅表达“接口可读”，不伪造剩余比例或重置时间。

## 快速恢复说明

从 `D:\Object\CodexAppSwitcher\CodexAppSwitcher\CodexAppSwitcher.sln` 进入。当前构建校验命令为 `dotnet build .\CodexAppSwitcher\CodexAppSwitcher.csproj -c Debug -v minimal -o .\CodexAppSwitcher\bin\CodexCheckApp`；若主程序正在运行导致默认测试输出被锁，先执行 `dotnet build .\CodexAppSwitcher.Tests\CodexAppSwitcher.Tests.csproj -c Debug -v minimal -o .\CodexAppSwitcher.Tests\bin\CodexCheckTests`，再运行 `dotnet .\CodexAppSwitcher.Tests\bin\CodexCheckTests\CodexAppSwitcher.Tests.dll`；发布命令为 `dotnet publish .\CodexAppSwitcher\CodexAppSwitcher.csproj -p:PublishProfile=FolderProfile -v minimal`，发布包路径为 `CodexAppSwitcher\bin\Release\net8.0-windows\publish\CodexAppSwitcher-v1.1.2-win-x64.zip`。

## 归档

- 旧长上下文已归档到 `.AIINFO/archive/context-20260703-1527.md`。
- 2026-07-09 10:13 前的长上下文已归档到 `.AIINFO/archive/context-20260709-1013.md`。
