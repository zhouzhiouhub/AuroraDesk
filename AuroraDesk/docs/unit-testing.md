## 单元测试指南（AuroraDesk）

### 目标
为项目提供可重复、可扩展的单元测试流程，确保非 UI 业务逻辑的正确性与回归安全。

### 技术栈
- **测试框架**: xUnit
- **测试项目**: `AuroraDesk.Tests`
- **目标框架**: `net8.0-windows10.0.19041.0`
- **覆盖率收集**: `coverlet.collector`

### 快速开始
```bash
dotnet test -v:minimal
```
或先还原依赖再测试：
```bash
dotnet restore
dotnet test -v:minimal
```

### 项目结构与约定
- `AuroraDesk.Tests/AuroraDesk.Tests.csproj`
  - 由于应用项目为 WinUI（包含 WindowsAppSDK 打包任务），测试阶段可能触发与 PRI 相关的 MSBuild 任务错误。
  - 为降低耦合，当前测试项目未直接引用应用项目，而是以“链接编译”的方式引入可测试的纯模型代码：
    - 链接文件：`AuroraDesk/Models/WallpaperItem.cs`
  - 这样可在不构建 WinUI 应用的前提下运行核心逻辑测试。

### 已有测试
- `WallpaperItemTests`
  - `DefaultValues_ShouldBeEmptyStrings`: 验证 `WallpaperItem` 默认值均为空字符串。
  - `Properties_SetAndGet_ShouldRoundTrip`: 验证属性读写的一致性。

### 新增测试指引
- 放置位置：`AuroraDesk.Tests/` 下新建测试文件，命名建议以 `*Tests.cs` 结尾。
- 命名约定：
  - 类名：`{被测类名}Tests`
  - 方法名：`方法_场景_期望`（示例：`Parse_WhenInputInvalid_ShouldThrow`）
- 范围建议：
  - 优先为不依赖 UI 的纯逻辑（模型、解析、计算、路径处理等）编写测试。
  - 如果需要测试 `Core` 中的逻辑，建议将该逻辑提取到独立的纯 .NET 类库（例如 `AuroraDesk.CoreLogic`），测试项目再引用该类库，避免引入 WinUI 构建依赖。

### 代码覆盖率（可选）
项目已引入 `coverlet.collector`，可通过 VSTest 收集覆盖率：
```bash
dotnet test -v:minimal --collect:"XPlat Code Coverage"
```
生成的覆盖率报告默认位于：
`AuroraDesk.Tests/TestResults/<GUID>/In/coverage.cobertura.xml`

### 常见问题排查
- 构建时报 `ExpandPriContent` 或 PRI 相关任务错误：
  - 原因：测试时触发 WinUI/WindowsAppSDK 的打包任务。
  - 处理：
    - 保持当前做法（仅链接需要的纯逻辑文件，不直接引用应用项目）。
    - 如必须引用应用项目，请确保本机具备相应 WindowsAppSDK 打包工具环境，且目标框架匹配（`net8.0-windows10.0.19041.0`）。

### 约定与风格
- 遵循模块化、低耦合原则。将可测试逻辑与 UI 分离，最大化测试可达性与稳定性。
- 测试应清晰、原子、可重复。一个测试仅验证一个行为或断言主题。

### 后续规划（建议）
- 抽取 `Core` 中的可测试逻辑到独立类库，完善其单元测试。
- 持续增加模型、工具类等非 UI 模块的测试覆盖率。

