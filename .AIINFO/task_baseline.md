# CodexAppSwitcher 执行基线

基线版本：v1.1

最近更新：2026-07-03 18:16 (Asia/Shanghai)

## 项目目标

实现一个 Win11 本机 Codex App 多账号登录态切换工具，支持账号添加、账号接管、auth 快照切换、Codex App 启停控制和用量显示。

## 当前主线策略

采用 C# + WPF + .NET 8 + WebView2。当前主链路只管理 live `.codex\auth.json` 与账号 auth 快照，不切换 Roaming/MSIX，不刷新 token，不创建 rollback，不做写后落地校验，不保留提权/package-state 产品能力，不暴露回滚入口，也不提供真实切换或宿主保护开关；appsettings 中保留的配置项必须有实际读取路径，源码中不保留旧分支公开接口。

## 阶段划分

1. 工程初始化。
2. auth 主导登录态快照与切换。
3. Codex App 启停与手动验证闭环。
4. WebView2 用量查询与 UI 完善。

## 本阶段执行基线

添加账号流程固定只保存 WebView2 登录态；App 快照必须通过账号行 `接管/完成` 或当前账号 `更新` 显式采集。切换流程固定为：用户确认目标账号，工具先关闭全部 Codex App，随后把目标账号快照中的 `auth.json` 原子写入 live auth。写入后不自动判定账号是否生效，等待用户启动 Codex App 后观察。顶部启动/停止 Codex App 能力保留；用户可见状态只围绕 live auth 与 Codex App 启停，不再展示 MSIX/package-state 作为主流程检测项。

## 偏离记录

2026-07-03 15:27 (Asia/Shanghai)

- 原计划：同时探索 Roaming、MSIX、Token、rollback 与落地校验。
- 实际执行：全部退出当前主链路，只保留 auth 写回和 Codex App 启停。
- 原因：历史分支造成个人资料加载异常和排查噪音；用户要求先验证最小真实链路。
- 是否更新基线：是，基线升级为 v1.1。
