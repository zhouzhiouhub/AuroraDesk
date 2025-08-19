 `AuroraDesk_DevDoc.md` 就行。

---

````markdown
# AuroraDesk 开发文档

## 1. 项目简介
AuroraDesk 是一个 **Windows 动态壁纸管理器**，灵感来源于 Lively Wallpaper。  
它支持将 **视频、网页、着色器渲染、音频可视化** 等作为桌面壁纸，并提供 **多显示器独立配置**、**性能优化**、**电源管理** 等功能。  

目标：
- 提供轻量、稳定、易用的动态壁纸解决方案  
- 支持多源壁纸（视频/网页/Shader/音频）  
- 多屏幕独立控制与克隆模式  
- 智能性能优化（全屏暂停、省电模式）  
- 良好的 UI 和可扩展的插件机制  

---

## 2. 技术栈

### 2.1 核心语言与框架
- **语言**: C# (.NET 8)  
- **UI 框架**: WinUI 3 (Windows App SDK)  
- **渲染**: Direct3D 11 (通过 Vortice.Windows 调用)  

### 2.2 多媒体支持
- **视频解码**: Media Foundation（主），LibVLCSharp（兼容）  
- **网页渲染**: WebView2 (Edge Runtime)  
- **音频可视化**: NAudio + WASAPI Loopback + FFT (Math.NET)  

### 2.3 系统交互
- **桌面挂载**: Win32 API (`FindWindow`, `SendMessage`, `SetParent`)  
- **多屏支持**: `EnumDisplayMonitors` / WinUI `DisplayInformation`  
- **输入处理**: Win32 API (`SetWindowLong` 设置 WS_EX_TRANSPARENT 等)  

### 2.4 打包与部署
- **安装**: MSIX  
- **自启**: Windows 任务计划/注册表  
- **日志**: Serilog + 文件日志  

---

## 3. 系统架构

### 3.1 模块划分
```plaintext
AuroraDesk (主程序)
 ├─ UI (WinUI3)         // 设置面板、托盘菜单
 ├─ ProfileManager      // 用户配置与预设
 ├─ MonitorManager      // 多屏幕检测与管理
 ├─ HostManager         // WorkerW 桌面挂载
 ├─ PlaybackController  // 播放控制
 └─ Sources/            // 壁纸来源
     ├─ IWallpaperSource
     ├─ VideoSource
     ├─ WebSource
     ├─ ShaderSource
     └─ VisualizerSource
````

### 3.2 模块职责

* **UI**: 用户交互（设置、托盘菜单、壁纸选择）
* **ProfileManager**: 保存/加载用户配置（JSON）
* **MonitorManager**: 管理多显示器，监听屏幕插拔/分辨率变化
* **HostManager**: 实现 WorkerW 挂载逻辑，保证壁纸嵌入桌面
* **PlaybackController**: 控制壁纸的播放/暂停/切换
* **Sources**: 提供壁纸来源实现（视频、网页、Shader、音频）

---

## 4. 多显示器管理

### 4.1 原理

1. 枚举显示器（`EnumDisplayMonitors` 或 `Screen.AllScreens`）
2. 为每个显示器创建一个 **壁纸 Host 窗口**
3. 将 Host 窗口挂载到 **WorkerW** 层
4. 根据显示器坐标（`RECT`）和 DPI 调整壁纸内容
5. 用户可选择 **独立壁纸** 或 **克隆模式**

### 4.2 示例 (C#)

```csharp
foreach (var screen in Screen.AllScreens)
{
    var bounds = screen.Bounds;
    var host = new HostWindow(bounds);
    host.AttachToWorkerW();
    host.Show();
}
```

---

## 5. 性能与优化

### 5.1 全屏检测

* 枚举前台窗口，判断是否覆盖整个屏幕 → 暂停/降低壁纸刷新率。

### 5.2 电源管理

* 使用 `SystemInformation.PowerStatus` 检测电池供电 → 自动切换省电模式。

### 5.3 渲染优化

* 视频：启用硬件解码（DXVA）
* 网页：使用 WebView2 Composition 模式，支持失焦时限帧
* Shader：仅在可见时渲染，降低后台帧率

---

## 6. UI/UX 设计

### 6.1 托盘菜单

* 选择壁纸（视频/网页/Shader/Visualizer）
* 多屏幕配置
* 暂停/恢复
* 设置
* 退出

### 6.2 设置界面

* 常规设置（启动行为、性能模式）
* 多屏配置（独立/克隆/禁用）
* 网络来源白名单
* 预设管理

---

## 7. 异常与恢复

* **Explorer 重启** → 自动重新挂载 WorkerW
* **显卡驱动重置 (TDR)** → 重建 D3D 设备
* **壁纸文件缺失** → 回退到默认静态壁纸

---

## 8. 开发里程碑

| 周期    | 功能目标                  |
| ----- | --------------------- |
| 第 1 周 | WorkerW 桌面挂载 + 单屏视频壁纸 |
| 第 2 周 | 网页壁纸 + 托盘菜单           |
| 第 3 周 | 多屏支持 + 电源/全屏检测        |
| 第 4 周 | Shader 壁纸 + 音频可视化     |
| 第 5 周 | 设置面板 + 预设管理           |
| 第 6 周 | 稳定性测试 + 打包发布          |

---

## 9. 后续扩展

* 插件系统（用户可开发新的壁纸来源）
* 壁纸市场（分享/下载预设）
* 跨平台支持（Linux/Wayland, macOS）
* 云同步（壁纸配置同步到多台设备）

```

---

你只需要：  
1. 把上面的内容复制到本地文件里  
2. 保存为 `AuroraDesk_DevDoc.md`  

然后就可以用 **VS Code / Typora** 打开，或者用 `pandoc` 转换成 **PDF/Word**。  

要不要我顺便给你加一段 **Mermaid 流程图代码**（比如多屏幕管理流程图），让文档在 GitHub/Typora 上渲染时更直观？
```
