# CodexAppSwitcher 实施计划

最近更新：2026-07-13 10:12 (Asia/Shanghai)

## 执行摘要

构建 Win11 本机 WPF 工具，用于管理多个 Codex App 账号的登录态快照，并按 `xjoker/codex-switch` 的 auth profile 思路执行账号切换。当前阶段固定执行 live `auth.json` 写回，切换成功后自动启动 Codex App 读取新账号；Token、Roaming 替换、MSIX、rollback、提权路径和切换保护开关均不在当前产品链路内。

## 阶段划分

阶段一：工程初始化  
已完成。C# WPF/.NET 8 工程、配置、主窗口、账号列表、日志和基础服务已可构建。

阶段二：登录态管理  
进行中。当前主线为：添加/识别账号只保存 WebView2 登录态；账号行 `接管/完成` 才采集当前 Codex App 的 live auth；切换时先关闭 Codex App，再写回目标账号 `auth.json`，成功后自动启动 Codex App。已移除模式分支、Token 模块、rollback 创建、写后落地校验、旧预检服务、提权 Helper 和 package-state 探测。

阶段三：状态与用量  
进行中。WebView2 账号 profile 和 `wham/usage` 用量解析保留；真实用量刷新仍由 `EnableUsageRefresh` 控制。已适配官方不再返回短期窗口、仅返回 7 天窗口或历史用量数组的接口结构，缺失窗口时展示 `--` / `未提供`。

阶段四：体验完善  
已完成。主窗口两栏布局、账号列表固定操作列、当前账号整行高亮、桌面额度悬浮球、托盘后台入口和托盘菜单优化均已完成。账号列表已隐藏短期额度和短期重置列，挂件已改为每周额度单指标，主窗口默认尺寸已下调；`v1.1.2` 已定版并生成 win-x64 发布包。

## 详细任务

- 维持 auth-only 主链路：只切 live `.codex\auth.json`。
- 切换写 auth 前关闭全部 Codex App。
- 添加账号与 App 快照接管保持解耦。
- 保留工具启动/停止 Codex App 能力。
- 禁用 Token、rollback、Roaming/MSIX 替换和写后校验。
- 用户可见诊断不再展示 MSIX/package-state 主流程提示。
- 源码不再保留提权 Helper 或 package-state 发现逻辑。
- UI 不再保留回滚入口。
- 配置不再保留真实切换和宿主保护开关。
- 左侧栏不再保留无行为导航按钮。
- 配置项必须真实生效；已接入 Logging 并删除无效用量周期字段。
- 删除未使用类型、资源和旧接口；用户可见文案与当前能力保持一致。
- 当前版本说明已整理并优化到 `README.md` 和 `CHANGELOG.md`；独立 `ManualValidation.md` 已删除；当前版本号已更新为 `v1.1.0`；README 已加入主窗口和悬浮球 UI 图展示。
- 主窗口信息架构已调整为两栏布局；账号列表固定操作列和当前账号整行高亮已完成，下一步等待用户手动验证默认窗口显示。
- 根据反馈只围绕 auth 路径和启动读取时机排查。
- 用量刷新只围绕 `wham/usage` 结构变化、窗口秒数分类、WebView2 会话授权和界面降级展示排查。
- 发布包路径固定为 `CodexAppSwitcher\bin\Release\net8.0-windows\publish\CodexAppSwitcher-v1.1.2-win-x64.zip`。

## 风险评估

- live `auth.json` 路径若与 Codex App 实际读取路径不一致，会表现为写入成功但账号不变。
- Codex App 若没有完全退出，可能继续使用旧内存状态或旧进程缓存；当前切换入口会先关闭再写 auth，成功后再自动启动。
- 快照包含敏感登录态，只能本机保存，不得输出 token/cookie 内容。
- 当前不做 Token 模块，若目标 auth 已被服务端吊销，工具不会提前阻断，只能由手动测试观察。
- 当前不创建 rollback，写入 live auth 后恢复依赖用户重新接管或重新登录。
- 官方用量接口结构可能继续调整；解析器不得假设 `primary_window` 固定代表短期额度窗口。

## 时间线

- MVP 工程骨架：已完成。
- auth 主导切换链路：已可用，自动启动测试已通过。
- 用量查询与 UI 完善：旧额度窗口、单一 7 天窗口和新历史用量结构已兼容，等待真实账号刷新验证。
- 发布前说明：`README.md` 和 `CHANGELOG.md` 已优化，已取消独立 `ManualValidation.md`。
- UI 信息架构：左侧栏已取消，主内容区和右侧状态详情已完成；账号列表固定操作列、短期列隐藏、桌面额度挂件每周单指标、球内手动刷新、外圈进度显示、托盘后台状态入口、托盘右键菜单图标优化、托盘 Logo 图标、菜单顶部状态文案移除、主窗口顶部白色边框去除和 README 脱敏截图更新已完成。
- v1.1.2 定版：用量接口兼容、版本元数据、窗口版本显示、README、CHANGELOG、测试验证、Release 发布和 zip 发布包均已完成。
