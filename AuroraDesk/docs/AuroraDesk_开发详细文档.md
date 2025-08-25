# AuroraDesk 开发详细文档（规划蓝图 | 与仓库对齐）

本文件用于阐述系统目标、分层与路线图，并标注与当前仓库状态的对应关系。

---

## 1. 目标与原则

- 轻量、稳定、易用的动态壁纸管理器。
- 高内聚、低耦合；UI 与核心分离；遵循 SOLID/DRY/SRP。
- 统一日志与错误处理策略，支持 Explorer 重启与显卡 TDR 的恢复路径。

---

## 2. 分层与模块（现状标注）

```text
AuroraDesk (WinUI 3 应用)
├─ Core               [已存在]
│  ├─ NativeMethods   [已存在]
│  ├─ DesktopHost     [已存在]
│  ├─ Win32Window     [已存在]
│  ├─ WallpaperManager[已存在/以项目实际实现为准]
│  └─ MonitorManager  [规划/根目录历史文件，建议迁至 Core]
│
├─ Sources            [占位/规划]
│  ├─ IWallpaperSource   [规划]
│  ├─ VideoSource        [规划]
│  ├─ WebSource          [规划]
│  ├─ ShaderSource       [规划]
│  └─ VisualizerSource   [规划]
│
├─ Controls           [已存在]
├─ Models             [已存在]
├─ Utilities          [已存在]
└─ Assets/Library     [已存在：资源/示例壁纸]
```

说明：仓库根目录的 `DesktopHost.cs`、`NativeMethods.cs`、`Win32Window.cs` 与 `MonitorManager.cs` 为历史/重复项，建议按“清理与迁移”执行统一化。

---

## 2.1 应用页面与导航

- 页面：`图库`、`设置`、`帮助`（三主页）+ 全局 `搜索`。
- 导航：顶部或侧边栏标签切换；搜索框常驻标题栏并作用于图库过滤。

状态与存储：
- 当前页、搜索关键字与最近操作通过 UI 层状态管理；
- 持久化设置写入本地（JSON/首选项），应用启动时加载；
- 壁纸索引来源于 `Library/wallpapers/` 与用户导入目录。

交互概要：
- 图库：展示卡片、导入、设为壁纸、删除、刷新；
- 搜索：对 `WallpaperItem.name` 执行不区分大小写的子串匹配；
- 设置：性能与行为开关（全屏暂停、帧率上限、自启动、声音）；
- 帮助：快速上手、常见问题、日志定位与反馈链接。

数据流（示意）：
```
User → UI(Gallery/Search/Settings/Help)
   → WallpaperManager(Core) ↔ DesktopHost/Win32Window
   → Sources(规划：Web/Video/Shader)
   → Models.WallpaperItem（索引/绑定）
```

错误与恢复：
- Explorer 重启或显卡 TDR → `DesktopHost` 负责重新挂载并通知 UI；
- 素材缺失 → 回退到静态壁纸并在帮助页提示排查步骤。

---

## 3. 关键流程

- 桌面挂载（WorkerW）：
  1. 获取/创建 WorkerW 层;
  2. 创建宿主窗口并 SetParent 到 WorkerW；
  3. 管理窗口生命周期与尺寸。
- 多显示器（规划）：
  - 统一 `MonitorManager` 到 `Core/`；
  - 按屏创建宿主并坐标对齐；
  - 支持独立/克隆模式切换。
- 资源来源（规划）：
  - Web：WebView2 透明/合成模式；
  - Video：Media Foundation 硬解；
  - Shader：D3D11/HLSL 渲染。

---

## 4. 日志与错误处理

- 日志：统一 `LogManager`（英文日志内容），替换零散输出；
- 恢复：
  - Explorer 重启 → 重新挂载 WorkerW；
  - TDR → 设备重建与资源重载；
  - 资源缺失 → 回退静态壁纸。

---

## 5. 性能策略（分阶段）

- 第一阶段：失焦降帧/暂停；
- 第二阶段：全屏检测 → 自动暂停；
- 第三阶段：省电模式与策略切换。

---

## 6. 路线图（里程碑）

- M1：统一 Core 与根级历史文件；补文档与测试；
- M2：WebSource 最小可用；
- M3：VideoSource 与多屏管理；
- M4：ShaderSource 与可视化；
- M5：设置面板与预设管理；
- M6：稳定性测试与发布。

---

## 7. 清理与迁移

- 以 `AuroraDesk/Core/` 为权威版本：
  - 合并根级差异（如 `MonitorManager.cs`）→ 迁入 `Core/`；
  - 删除根级重复文件；
  - 同步更新文档与构建配置；
  - 提交信息标注影响面（docs/refactor）。

---

## 8. 文档与维护

- 文档集中在仓库根 `docs/`；接口/行为变化需同步更新；
- 图标与打包流程见 `docs/应用图标配置说明.md`；
- 结构图与示例参考 `docs/项目结构.md`。
