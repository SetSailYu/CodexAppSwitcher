# CodexAppSwitcher 项目上下文

最近更新：2026-07-07 11:24 (Asia/Shanghai)

## 会话进度

已完成：

- 当前产品边界已重新收敛为 `auth.json` 主导切换：账号快照保存目标账号 `auth.json`，切换时写回 live `auth.json`。
- 先不做 Token 模块；`CodexAuthTokenService`、token 接口和测试替身已移除。
- 写入 live `auth.json` 前会先关闭全部 Codex App；顶部 `启动 Codex` 和 `停止 Codex` 继续保留真实控制能力。
- 切换不创建 rollback，不做写后落地校验，不替换 `%APPDATA%\Codex` Roaming 附加状态，也不替换 MSIX 包状态。
- 回滚入口已从 UI、ViewModel 和切换服务中删除；当前只保留“不创建 rollback”的边界说明和测试断言。
- 已删除当前主链路不再使用的 rollback 计划模型/服务、文件锁探测服务和进程预检方法，避免旧设计继续干扰排查。
- 添加 ChatGPT 账号流程已与 Codex App 接管彻底解耦：添加/重复添加只保存 WebView2 登录态和账号元数据，不再自动采集或覆盖 Codex App 快照。
- 内部命名已统一到“接管/采集”和“添加 ChatGPT 账号”语义，避免后续把“添加 Web 账号”和“接管 App 快照”混淆。
- 右侧不再显示回滚卡片，避免把禁用能力误认为可用功能。
- 右侧安全检查已从 MSIX/package-state 检测降噪为 live auth、auth 目录、Roaming 附加目录和 Codex App 状态。
- 切换成功诊断文案已改为“不替换 Roaming 附加目录”，不再出现 Roaming/MSIX 混合表述。
- 已取消独立 `CodexAppSwitcher/ManualValidation.md` 说明文档，真实功能验证说明只保留在 `README.md` 的简短入口中。
- 已删除 `CodexAppSwitcher.Elevated` 独立提权项目、主项目提权 Helper 服务、package-state 路径解析和安全检测能力。
- 测试中的 package-state 仅作为外部模拟目录保留，用于防止未来误把该目录重新纳入接管或切换。
- 已移除 `EnableRealAccountSwitching` 与 `EnableHostProtectionMode` 配置，当前版本固定执行真实切换和真实关闭 Codex App。
- 左侧无命令的“用量/切换/设置”假导航已改为只读工作台状态项；底部“服务运行中/本地模式”改为“本地运行/本机数据”。
- `Logging` 配置已接入 Serilog 初始化，`Level` 和 `Format` 不再是摆设。
- `Usage` 配置已删除未生效的周期刷新字段，只保留 `AnalyticsUrl` 和 `PageLoadTimeoutSeconds`。
- 已删除未使用的 `AccountHealthStatus`、未引用 SVG、未使用 XAML 图标资源、旧预检接口和用量占位接口。
- 切换成功后会同步工具本地账号元数据，把目标账号标记为当前账号，避免 UI 仍显示切换前账号。
- 切换确认和成功诊断文案已回到真实动作描述：先关闭 Codex App、写 live `auth.json`，再由用户手动启动 Codex App 验证。
- 用户可见切换文案不再反复强调已取消的 token/rollback/落地校验模块，减少手动测试干扰。
- 用户确认接管和切换功能已可用；切换成功后已接入自动启动 Codex App，启动失败只记录警告，不回滚已写入的 live `auth.json`。
- 用户确认自动重启 Codex App 测试通过。
- 默认窗口下账号列表已重新设计：账号列改为弹性列，固定列收窄，表格宽度绑定视口，默认宽度优先显示操作列，窄窗口才横向滚动。
- 用户确认账号列表新布局检查通过。
- 已补齐辅助入口：操作日志可清空、底部可打开 Switcher 数据目录、右侧当前账号登录时间绑定到账号最近 App 快照采集时间。
- 用户确认清空日志、打开目录和登录时间显示手动验证通过。
- 已完成账号管理增强：账号行右键菜单支持重命名、打开 App 快照目录、打开 Web 登录目录、删除非当前本地账号。
- 删除账号带工具内确认弹窗；当前使用账号、切换中账号和接管中账号均不会直接删除。
- 账号目录解析已统一到账号本地根目录，避免多处手写 `accounts/{id}` 路径。
- 本轮因 VS2022 和正在运行的工具锁定 `bin\Debug`，常规 Debug 构建无法覆盖输出；改用正常 `bin\CodexCheck` 输出目录完成测试，并用主项目 `bin\CodexCheckApp` 输出完成 0 警告 0 错误构建。
- 上一轮解决方案 Debug 完整构建通过，0 警告 0 错误；本轮因运行中进程锁定 `bin\Debug`，使用 `bin\CodexCheck` 完成测试，使用 `bin\CodexCheckApp` 完成主项目构建。
- 账号行右键菜单视觉已优化：自定义 ContextMenu/MenuItem/Separator 模板，移除默认白色边框和系统高亮，删除项使用危险色，当前账号禁用态保留提示。
- 本轮只改 XAML 外观，不改变重命名、打开目录、删除保护等账号管理业务逻辑；主项目输出到 `bin\CodexCheckApp` 构建通过，0 警告 0 错误。
- 删除菜单项已改为独立危险样式模板：正常态红色文字和弱红背景，悬停态红色边框和更深危险背景，当前账号禁用态仍为灰色并保留禁用原因提示。
- 本轮只改右键菜单删除项视觉，不改变删除确认和当前账号保护逻辑；主项目输出到 `bin\CodexCheckApp` 构建通过，0 警告 0 错误。
- 用户确认账号管理右键菜单和删除项危险色 UI 通过。
- 账号管理能力已同步到当前版本说明，后续不再维护独立手动验证文档。
- 左侧中段已从“账号工作台 / 用量显示 / auth 切换”静态假导航改为状态摘要，显示当前账号、账号快照数、Codex App 状态、用量刷新状态和 live auth 切换策略。
- 顶部工具栏四个按钮保持真实功能入口，不做额外改动；本轮主项目输出到 `bin\CodexCheckApp` 构建通过，0 警告 0 错误。
- 左侧状态摘要已完成并纳入当前 UI 能力范围。
- 因切换策略后续固定为 live auth，不再在左侧状态摘要中展示“切换策略”；已删除对应 ViewModel 属性和 XAML 行。
- 固定切换策略展示移除后，本轮主项目输出到 `bin\CodexCheckApp` 构建通过，0 警告 0 错误。
- 已补齐仓库入口说明 `README.md`：说明产品边界、首次使用流程、本地数据位置、构建命令和手动验证入口。
- 已补齐 `CHANGELOG.md`：记录 v1.0.0 已实现能力、设计收敛、已移除模块和已通过的用户手动验证项。
- 发布说明收尾后使用默认 Debug 输出目录构建主项目通过，0 警告 0 错误。
- 按用户要求删除 `CodexAppSwitcher/ManualValidation.md`，并清理 `README.md`、`CHANGELOG.md` 中对该文件的引用；删除后默认 Debug 构建通过，0 警告 0 错误。
- 已优化 `README.md`：重排为适用场景、核心行为、使用流程、功能清单、安全边界、本地数据、构建、验证建议和常见问题。
- 已优化 `CHANGELOG.md`：重排为核心能力、账号管理、界面调整、设计收敛和已验证项，便于发布阅读。
- 文档优化后确认无 `ManualValidation` 引用，默认 Debug 构建通过，0 警告 0 错误。

进行中：

- README 和 CHANGELOG 已优化，等待用户确认是否继续做安装包、版本号或发布清单。

待开始：

- 当前主链路、已补齐辅助入口和账号管理右键菜单均已通过用户手动验证；左侧状态摘要精简版待手动验证。
- 若用户确认当前 auth 写入仍无效，再只围绕 live `auth.json` 路径、文件内容来源和 Codex App 启动读取时机排查。
- 若后续用户反馈切换无效，只围绕 live auth 路径、快照来源、Codex App 是否完全退出和启动读取时机排查。

## 重要决策

- 当前只保留 auth 主导真实切换：先关闭全部 Codex App，再替换 live `.codex\auth.json`。
- 不做 Token 模块，不创建 rollback，不做落地校验；这是当前手动测试边界，不再把失败归因于 token、rollback 或 package-state。
- Roaming 和 MSIX 包状态退出新接管/新切换主链路；UI 不再展示 MSIX/package-state 检测项，源码也不再包含 package-state 发现或提权 dry-run 能力。
- 配置只保留用量刷新开关；账号切换不再提供模式或保护开关。
- appsettings 中保留的配置项都应有实际读取路径。

## 快速恢复说明

从 `D:\Object\CodexAppSwitcher\CodexAppSwitcher\CodexAppSwitcher.sln` 进入。不要把运行或测试输出改到 `.tmp`；使用 VS2022 或默认 `bin\Debug` 路径。当前已通过用户手动验证的能力包括接管、切换、切换后自动启动 Codex App、默认窗口账号列表布局、清空日志、打开目录、登录时间显示、账号管理右键菜单和删除项危险色 UI。左侧状态摘要仅显示当前账号、账号数、Codex 状态和用量刷新状态；根目录 `README.md` 已作为用户入口说明，`CHANGELOG.md` 已作为 v1.0.0 发布记录，独立 `ManualValidation.md` 已删除。

## 归档

- 旧长上下文已归档到 `.AIINFO/archive/context-20260703-1527.md`。
