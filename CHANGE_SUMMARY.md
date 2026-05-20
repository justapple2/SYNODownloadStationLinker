# SYNODownloadStationLinker 重构说明

本文档记录本次相对旧版本的主要改动，便于后续继续维护 UI、群晖 Download Station API，以及未来扩展其他 NAS 下载接口。

## 总览

旧版本已经具备“登录群晖、手动创建下载任务、监听剪贴板自动创建任务”的核心能力，但存在界面粗糙、API 拼接分散、通知与日志不足、目录预创建不稳定、任务状态不可见等问题。

本次重构将应用调整为更接近 Win11 Fluent 的桌面工具形态：左侧为可折叠 Pane，右侧为 Tab 工作区；通信层改为规范的 typed API client；任务创建结果会通过右下角 toast 通知、日志和任务状态列表同步反馈。

## 功能对比

| 模块 | 旧版本 | 当前版本 |
| --- | --- | --- |
| 主界面 | 大量控件内联属性，布局接近表单堆叠，任务、响应区域空间较小 | 使用 `SplitView` 可折叠左侧 Pane，右侧 `TabControl` 展示下载任务、活动日志、响应调试和 NAS 扩展 |
| 样式组织 | 样式零散，颜色与尺寸直接写在控件上 | 将主要视觉样式收拢到 `Window.Styles`，减少重复内联属性 |
| 图标 | 基本依赖纯文字按钮 | 使用 Avalonia Fluent Icons 的 `StreamGeometry` + `PathIcon`，用于导航、按钮和 Tab 标题 |
| Download Station API | 手工字符串拼接 GET URL，参数未统一编码，接口较少 | 新增 typed API client，支持 API info、登录/登出、info/config、list/create/pause/resume/delete，参数统一编码，创建任务使用 POST |
| 响应模型 | 旧 DTO 命名与拼写不稳定，反序列化使用较少 | 新增 `DownloadStationDtos.cs`，以 `SynologyResponse<T>` 和任务 DTO 规范解析响应 |
| 剪贴板监听 | 使用高频 `Timer`，任务创建逻辑和 UI 状态混杂 | 改为异步循环和取消令牌，避免重复提交，并统一走创建任务流程 |
| 任务创建反馈 | 只显示原始 response，成功/失败反馈不明显 | 成功/失败触发右下角 toast，写入 UI 日志和本地日志文件 |
| 任务状态 | 创建后不便观察任务进度 | 刷新最新 10 个任务，按创建时间倒序显示进度、速度、状态和目标目录 |
| 新建任务定位 | 创建后只刷新列表，可能看不到刚创建的任务 | 创建成功后短暂轮询任务列表，并优先用 `detail.uri` 匹配刚提交链接 |
| 日志 | 缺少结构化历史记录 | 新增活动日志 Tab，并写入 `logs/SYNODownloadStationLinker.log` |
| 下载目录预创建 | 仅支持 IPv4，SMB 账号密码判断反向，目录创建失败信息不清晰 | 支持主机名/IP，按 `\\host\share\subdir` 连接共享根目录后创建目标目录，并返回结构化结果 |
| 配置保存 | 可保存 NAS 地址、账号、密码和下载路径 | 保留 AES 加密保存逻辑，修复 nullable/obsolete 警告，兼容旧配置字段 `Denision` |
| 扩展性 | 逻辑主要集中在 MainWindowViewModel 和旧 API helper 中 | API 层、DTO、目录 helper、ViewModel 状态与 UI 事件边界更清晰，预留 NAS 扩展入口 |

## 主要源码变化

- `src/Views/MainWindow.axaml`
  - 使用 `SplitView` 实现可折叠配置 Pane。
  - 使用 `TabControl` 承载下载任务、活动日志、响应调试、NAS 扩展。
  - 新增 Fluent 风格矢量图标资源，并应用到导航、按钮和 Tab。

- `src/Views/MainWindow.axaml.cs`
  - 将原窗口内 Flyout 通知改为桌面右下角 toast 风格窗口。
  - 保留窗口拖动、最小化、关闭和 ViewModel 生命周期处理。

- `src/ViewModels/MainWindowViewModel.cs`
  - 统一登录、测试、监听、创建任务、刷新任务、日志和通知流程。
  - 新增最新任务状态集合，最多展示 10 条并按创建时间倒序。
  - 新增左侧导航命令和 Pane 折叠命令。

- `src/Models/SYNODSApiHandler.cs`
  - 重构为 typed Download Station API client。
  - 统一 API 版本/path 查询、参数编码、Cookie 会话和 response 解析。

- `src/Models/DownloadStationDtos.cs`
  - 新增群晖响应、错误、任务列表、任务详情、传输进度等 DTO。

- `src/Models/NasPathHelper.cs`
  - 重写 NAS 目录预创建逻辑，返回 `NasPathResult`。

- `src/JsonHelper.cs`
- `src/Models/AESKey.cs`
- `src/Models/AesEncryption.cs`
  - 清理空引用警告和过时 RNG API。

## 已移除的旧结构

以下旧模型/枚举已不再使用并已移除：

- `src/Models/DownloadStationMothed.cs`
- `src/Models/DownloadApiType.cs`
- `src/Models/CgiTypes.cs`

旧文件中存在拼写错误、职责混杂和重复 DTO，新 API client 与 DTO 已覆盖其用途。

## 验证情况

已执行：

```powershell
dotnet build src\SYNODownloadStationLinker.csproj
```

结果：

```text
0 warning
0 error
```

## 后续建议

1. 在真实 DSM 环境中验证登录、目录创建、任务创建和任务状态刷新。
2. 后续若扩展其他 NAS 下载服务，可将“NAS 扩展”从 Tab 进一步迁移为左侧导航主入口，并抽象 `IDownloadServiceClient`。
3. 可继续补充任务暂停、恢复、删除等 UI 操作，使已实现的 API 能在界面上直接使用。

