# CodexAppSwitcher 风险记录

最近更新：2026-07-06 16:47 (Asia/Shanghai)

## 2026-07-03 15:27 (Asia/Shanghai) - live auth 路径不一致

描述：工具写入的 live `.codex\auth.json` 可能不是 Codex App 当前版本实际读取的 auth 文件。

影响：工具显示切换完成，但 Codex App 启动后账号不变。

当前应对策略：当前排查只围绕 live auth 路径、账号快照来源和 Codex App 启动读取时机，不再转向 Roaming/MSIX。

## 2026-07-03 15:27 (Asia/Shanghai) - Codex App 未完全退出

描述：如果 Codex App 有残留进程，写入 auth 后重新打开可能仍沿用旧内存状态。

影响：账号切换看似无效或个人资料加载异常。

当前应对策略：切换入口写 auth 前调用 `CloseForSwitch`，失败则中止；切换成功后自动调用启动服务，启动失败只记录警告。

## 2026-07-06 16:47 (Asia/Shanghai) - Codex App 自动启动入口失效

描述：切换成功后自动启动 Codex App 依赖现有启动入口发现逻辑。

影响：auth 已切换成功，但 Codex App 未自动打开，用户仍需手动启动。

当前应对策略：启动失败不回滚 auth；操作日志记录启动失败原因，后续只排查启动入口发现逻辑。

## 2026-07-03 15:27 (Asia/Shanghai) - Token 模块暂不实现

描述：当前版本不刷新、不校验目标账号 token。

影响：目标 auth 若已被服务端吊销，工具不会提前发现，Codex App 启动后可能要求重新登录。

当前应对策略：这是当前手动测试边界；失败后通过重新登录目标账号并重新接管更新快照。

## 2026-07-03 15:27 (Asia/Shanghai) - rollback 不提供

描述：当前版本写 live auth 前不创建 rollback，也不暴露回滚入口。

影响：切换后无法由工具自动恢复旧 auth。

当前应对策略：需要恢复时由用户重新登录或重新接管当前账号快照。

## 2026-07-03 15:27 (Asia/Shanghai) - 敏感登录态保护

描述：账号快照包含敏感登录态。

影响：误输出、误上传或跨机器迁移可能造成账号安全风险。

当前应对策略：不解密、不展示、不导出 token/cookie；`.AIINFO` 不记录个人账号或 token 内容。

## 2026-07-03 16:07 (Asia/Shanghai) - 历史提权代码残留（已解除）

描述：源码中曾保留提权 Helper 与 package-state 检测相关历史代码，但主界面和切换链路已不调用。

影响：后续维护者若只看服务文件，可能误以为当前仍支持或需要测试提权/package-state 路径。

当前应对策略：用户已确认继续清理；`CodexAppSwitcher.Elevated`、提权 Helper 服务和 package-state 路径发现/检测逻辑已删除。测试中仅保留模拟目录断言，防止该路径回流。
