# VisualSorting IC 芯片视觉分拣系统 — 模块化重构开发文档（V2.0）

> 文档版本：V2.0 ｜ 生成日期：2026-05 ｜ 编制依据：《VisualSorting IC 芯片视觉分拣系统 技术文档 V1.0》（IC.pdf）+ 原仓库 `Liberodx/VisualSorting` 源码
>
> 目标技术栈：**WPF + Prism 9 + .NET 8 + SQL Server + Entity Framework Core 8 + Halcon 24.11**

---

## 0. 文档说明与重构目标

本文档基于 IC.pdf 对原项目（`.NET Framework 4.8` + `CommunityToolkit.Mvvm` + `ServiceLocator` 手搭 DI + `EF6 Database First` + `Halcon` 25.11 引用）进行**模块化优化重构**，给出可直接落地的完整开发规范。

重构不是简单"换框架"，而是围绕 IC.pdf 第 5 章梳理的 **6 大类 16 个风险点（M/T/H/D/L/X）** 做系统性治理，并把单体的 7 层结构重组成 **Prism 模块化解决方案**。

核心目标：

1. **可维护性**：通过 Prism 模块解耦，按业务域拆分解决方案，消除单工程臃肿。
2. **可配置性**：消除硬件（相机 SN、控制卡 IP、IO 口、剔除延时）硬编码（H1/H2/H3）。
3. **健壮性**：修复内存泄漏、线程安全、资源释放、业务逻辑缺陷、退出竞态（M/T/L/X）。
4. **现代性**：升级到 .NET 8 + EF Core + Halcon 24.11，迁移到 SDK 风格工程与 Code First。

---

## 1. 重构总览

### 1.1 原项目现状与痛点（基于 IC.pdf + 真实源码核对）

| 维度 | 原项目实现 | 问题 |
|---|---|---|
| 工程格式 | 经典 `.csproj`（非 SDK 风格），`packages.config` | 不支持 `PackageReference`、不可 `dotnet build`，引用 Halcon 用绝对路径 `D:\Halcon\...` |
| 框架 | `.NET Framework 4.8` + `MahApps.Metro` | 无法使用 Prism 9 / EF Core 8 / Halcon 24.11 的 .NET 现代化特性 |
| MVVM | `CommunityToolkit.Mvvm` + 手写 `ServiceLocator` | 全局静态定位器，弱引用消息总线，无区域导航、无模块、无生命周期管理（M4） |
| DI | `Microsoft.Extensions.DependencyInjection` 经 `ServiceLocator` 包一层 | ViewModel 经瞬态注册但手动 `new Login()` 构造，DI 未真正贯穿（M4） |
| 数据 | `EF6 Database First`（`Model.edmx` + `.tt` 自动生成） | 强耦合 SQL Server 元数据，无法迁移到 EF Core；连接串 `sa/1` 明文（D1） |
| 导航 | `MainWindow.ContentControl.Content = new Login()` 手动替换 | 切换页面不触发 `Unloaded`，旧 VM 持续运行（M4） |
| 算法 | `DetectionService.Process` 全 Halcon 算子 | 算法本身规范（finally 释放），但"仅更新显示不检测"存在漏帧（L4） |
| 硬件 | 相机 SN、控制卡 IP `192.168.5.11`、IO 口均硬编码 | 换硬件必须改源码重编译（H1） |

### 1.2 目标技术栈与版本映射

| 层次 | 原项目 | 重构目标 | 说明 |
|---|---|---|---|
| 运行时 | .NET Framework 4.8 | **.NET 8 (LTS)** | WPF 在 .NET 8 完全支持，Prism 9 / EF Core 8 基线 |
| UI 框架 | MahApps.Metro | **MahApps.Metro 2.4+**（兼容 .NET 8） | 复用 MetroWindow 视觉，无强制变更 |
| MVVM/模块化 | CommunityToolkit.Mvvm + ServiceLocator | **Prism 9**（`Prism.Wpf` / `Prism.DryIoc` 或 `Prism.Unity`） | 提供 `BindableBase`、`DelegateCommand`、`IEventAggregator`、`IRegionManager`、`IModule` |
| DI 容器 | Microsoft.Extensions.DependencyInjection 裸用 | **Prism 容器抽象**（`IContainerRegistry`/`IContainerProvider`，经 `Prism.Container.Extensions` 桥接 `IServiceCollection`） | 原 `AddSingleton/AddTransient` 注册几乎原样迁移 |
| ORM | Entity Framework 6 (Database First) | **Entity Framework Core 8** (Code First + Migrations) | 去掉 edmx，实体手写，迁移脚本管 schema |
| 数据库 | SQL Server（`VisualSortingDB`，`sa/1` 明文） | **SQL Server + 连接串加密 + 低权限账号** | 解决 D1 |
| 视觉 | Halcon（引用 25.11/23.11） | **Halcon 24.11**（引用 `bin\dotnet\halcondotnet.dll`） | 算子 API 跨版本稳定，主要改引用路径与目标框架 |
| 日志 | NLog | **NLog 5.x**（结构化 + `IOptions` 注入） | 保留 NLog，接入 DI |
| 可视化 | LiveCharts 0.9.7 | **LiveCharts2**（`.NET 8` 兼容）或保留 0.9.7（需 net48 兼容包） | 建议升级 LiveCharts2；统计查询加时间过滤（D3） |
| 配置 | `App.config` 明文 | **`appsettings.json` + `IOptions<T>` + 加密** | 解决 H1/D1 |

### 1.3 重构原则

1. **接口先行**：所有 Service 维持原有 `I*Service` 接口（与原 `Services\I*.cs` 一一对应），实现可替换、可测试。
2. **契约兼容**：`DetectionResult`、`TemplateConfig`、`InspectionConfig`、`LogEntry` 等模型语义保持不变，仅迁移到 Core 模块。
3. **资源所有权单一**：创建者负责释放；事件跨线程传递的 `HObject` 必须 `Clone` 并约定所有权（继承 IC.pdf "资源所有权约定"）。
4. **配置外置**：一切硬件/工艺参数进 `appsettings.json`，提供设置页（H1/H2/H3）。
5. **生命周期显式**：页面/VM 通过 `INavigationAware`、`IDisposable` 显式释放（M4）。

---

## 2. 目标架构设计（Prism 模块化）

### 2.1 解决方案与模块划分

将单工程拆分为如下 Prism 模块（每个为独立 `.csproj`，SDK 风格）：

| 项目/模块 | 类型 | 职责 | 依赖 |
|---|---|---|---|
| `VisualSorting.Shell` | Prism 宿主（启动工程） | `PrismApplication` 引导、MainWindow 与 Region 定义、模块目录、有序退出 | Core, Infrastructure |
| `VisualSorting.Core` | 类库 | 全部 `I*Service` 接口、业务模型（`DetectionResult` 等）、配置 POCO、常量、事件定义 | — |
| `VisualSorting.Infrastructure` | 类库/模块 | NLog 日志服务、配置服务（`IOptions` 绑定）、Halcon 互操作基类、Dispatcher 助手 | Core |
| `VisualSorting.Data` | Prism 模块 | `AppDbContext`、实体（`Member`/`ProductInspectionRecord`）、泛型仓储、统计服务 | Core, Infrastructure |
| `VisualSorting.Vision` | Prism 模块 | `CameraService`、`TemplateService`、`DetectionService`、Halcon 封装 | Core, Infrastructure |
| `VisualSorting.Motion` | Prism 模块 | `MotionControlService`、`SensorService`、`SolenoidValueService`、LTSMC P/Invoke | Core, Infrastructure |
| `VisualSorting.Inspection` | Prism 模块 | `MainView`/`MainViewModel`、系统状态、`LiveCharts` 统计、分拣编排 | Vision, Motion, Data, Core |
| `VisualSorting.Login` | Prism 模块 | 登录页与 `LoginViewModel`（`Member` 校验） | Data, Core |
| `VisualSorting.Settings` | Prism 模块 | 硬件参数/工艺参数配置页（相机 SN、控制卡 IP、IO、剔除延时、速度） | Core, Infrastructure, Vision, Motion |
| `VisualSorting.Yolo` | Prism 模块 | YOLO 检测页（`YoloDetectionViewModel` 接入推理） | Vision, Core |
| `VisualSorting.Reports` | Prism 模块 | 检测记录查询/统计报表（时间范围过滤，D3） | Data, Core |

> 模块加载采用 `DirectoryModuleCatalog`（插件式）或显式 `ModuleCatalog.AddModule`（确定性）。工业上位机建议用 **显式 ModuleCatalog**，确保启动顺序与权限可控。

### 2.2 模块依赖与导航关系

```
                    +-----------------------------+
                    |   VisualSorting.Shell        |
                    |  (PrismApplication + Regions) |
                    +-------------+----------------+
                                  |
              +-------------------+-------------------+
              |                   |                   |
        +-----v-----+       +-----v------+      +------v------+
        |   Core    |<------|Infrastructure|      |  (host)    |
        +-----------+       +-------------+      +-------------+
              ^                   ^                     |
   +----------+----------+        |      +--------------+--------------+
   |          |          |        |      |              |             |
+--v--+   +---v---+  +----v---+  +v----+ +---v----+  +--v--+   +----v----+
|Data |   | Vision|  | Motion |  |Login| |Inspect |  |Sett.|   | Reports |
+-----+   +-------+  +--------+  +-----+ +--------+  +-----+   +---------+
   ^         ^          ^                  |  ^          ^
   |         |          |                  |  |          |
   +---------+----------+------------------+--+----------+  (Reports/Settings 反向用 Core)
```

Region 设计（MainWindow）：

| Region 名称 | 内容 | 说明 |
|---|---|---|
| `ContentRegion` | Login / MainView / YoloDetectionView / SettingsView / ReportsView | 主内容区，区域导航 |
| `StatusBarRegion` | SystemStatusView（CPU/内存实时） | 常驻 |
| `RibbonRegion`（可选） | 模块切换导航按钮 | 替代原 `AppData.Container.Content` 手动切换 |

### 2.3 原 7 层 → 新模块映射

| 原层 | 原文件 | 落位 |
|---|---|---|
| Views | `Login.xaml`、`MainView.xaml`、`YoloDetectionView.xaml` | 对应模块 Views |
| ViewModels | `LoginViewModel` 等 4 个 | 对应模块 ViewModels（`BindableBase` 替代 `ObservableObject`） |
| Services | `Camera/Template/Detection/Motion/Sensor/Solenoid/Log` | `Vision` / `Motion` / `Infrastructure` 模块 |
| Repositories | `RepositoryBase`、`MemberRepository`、`ProductInspectionRecordRepository` | `Data` 模块（泛型化） |
| Common | `ServiceLocator`、`ObservableObject`、`AppData` | 删除 `ServiceLocator`/`AppData` → Prism `IContainerProvider`/`IRegionManager`/`IEventAggregator`；`ObservableObject` → Prism `BindableBase` |
| Models | `DetectionResult` 等 | `Core` 模块 |
| Entities | `Member`、`ProductInspectionRecord`（edmx 生成） | `Data` 模块（手写 Code First 实体） |

---

## 3. 基础设施与公共层重构

### 3.1 配置中心（解决 H1/H2/H3/D1）

原 `App.config` 中相机 SN、控制卡 IP、IO 口、剔除延时全部硬编码或明文。重构为 `appsettings.json` + `IOptions<T>`，并支持设置页写回。

`appsettings.json` 示例：

```json
{
  "Hardware": {
    "CameraSn": "c42f90f6ad7a_GEV_MVCA06010GC",
    "Motion": {
      "ControlCardIp": "192.168.5.11",
      "ConveyorAxis": 0,
      "SensorInputPort": 0,
      "SolenoidOutputPort": 2
    },
    "Reject": {
      "RejectDelaySeconds": 3.3,
      "SolenoidOpenSeconds": 0.3,
      "AutoComputeBySpeed": true
    }
  },
  "Inspection": {
    "PinCountRange": { "Min": 4, "Max": 4, "Min2": 4, "Max2": 4 },
    "MinMatchScore": 0.65,
    "FallingEdgeTimeoutMs": 2500
  },
  "ConnectionStrings": {
    "VisualSortingDb": "<加密后的连接串或经 DPAPI 加密的占位>"
  },
  "Logging": { "MinLevel": "Info" }
}
```

配置绑定 POCO（Core 模块）：

```csharp
public class HardwareOptions {
    public string CameraSn { get; set; }
    public MotionOptions Motion { get; set; }
    public RejectOptions Reject { get; set; }
}
// MotionOptions / RejectOptions 略
```

> 连接串加密（D1）：使用 `Microsoft.Data.SqlClient` + **Windows DPAPI**（`ProtectedData.Protect`）或第三方密钥库，运行时解密注入 `DbContext.Options`；同时数据库侧改用**低权限应用账号**（非 `sa`）。

### 3.2 容器与 DI（替换 ServiceLocator）

原 `App.xaml.cs` 的 `ConfigureServices()` 几乎可原样迁移到 Prism 的 `RegisterServices(IServiceCollection)`（经 `Prism.Container.Extensions`）。各模块在 `IModule.RegisterTypes(IContainerRegistry)` 中注册自身服务。

`Shell` 引导（PrismApplication）：

```csharp
public class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry registry)
    {
        // 基础设施（原 ConfigureServices 内容平移）
        registry.RegisterSingleton<ILogService, LogService>();
        registry.RegisterSingleton<IConfigService, ConfigService>();
        // 注意：服务在各自模块的 RegisterTypes 中注册更优
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog catalog)
    {
        catalog.AddModule<DataModule>();
        catalog.AddModule<VisionModule>();
        catalog.AddModule<MotionModule>();
        catalog.AddModule<LoginModule>();
        catalog.AddModule<InspectionModule>();
        catalog.AddModule<SettingsModule>();
        catalog.AddModule<YoloModule>();
        catalog.AddModule<ReportsModule>();
    }

    // 连接串解密后注入（D1）
    protected override IContainerExtension CreateContainerExtension() => PrismIocExtensions.Create(...);
}
```

生命周期调整：

- 硬件 Service（`Camera`/`Template`/`Detection`/`Motion`/`Sensor`/`Solenoid`/`Log`）维持 **单例**（与原一致）。
- ViewModel 由 Prism **按导航解析**（瞬态语义），通过 `IRegionManager.RequestNavigate` 创建，卸载时触发 `IDisposable.Dispose`（解决 M4）。
- `DbContext` 注册为 **Scoped**（解决 D2），由工作单元/仓储解析。

### 3.3 消息总线（WeakReferenceMessenger → IEventAggregator）

原 `ServiceLocator.Messenger`（`WeakReferenceMessenger.Default`）与 `LogMessage` 全部迁移为 Prism 强类型事件：

```csharp
// Core 中定义事件（替代 ServiceLocator.LogMessage / ImageSizeEventArgs）
public class LogPublishedEvent : PubSubEvent<LogEntry> { }
public class ImageGrabbedEvent : PubSubEvent<ImageGrabbedPayload> { }
public class FirstImageReceivedEvent : PubSubEvent<ImageSize> { }
public class NavigateRequestEvent : PubSubEvent<string> { }   // "Login" / "Main" / "Settings" ...

// ViewModel 订阅（构造函数注册，Dispose 中 Unsubscribe，解决 M4）
_eventAggregator.GetEvent<LogPublishedEvent>().Subscribe(OnLog, ThreadOption.UIThread);
```

> `ImageGrabbedPayload` 携带 `HObject` 时，约定：**发布方 Clone 一份并转移所有权，订阅方负责 Dispose**（继承 IC.pdf 资源所有权约定；解决 M3）。

### 3.4 日志（NLog 结构化）

`LogService` 实现 `ILogService`，注入 `IOptions<LoggingOptions>` 与 `IEventAggregator`：

```csharp
public class LogService : ILogService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IEventAggregator _ea;
    public LogService(IEventAggregator ea) { _ea = ea; }
    public void AddLog(LogLevel level, string msg)
    {
        var entry = new LogEntry { Time = DateTime.Now, Level = level, Message = msg };
        _logger.Log(level.ToNLog(), msg);          // 持久化
        _ea.GetEvent<LogPublishedEvent>().Publish(entry);  // 跨层广播
    }
}
```

### 3.5 Halcon 24.11 封装与资源治理

- 引用路径改为 `HALCON-24.11-Progress\bin\dotnet\halcondotnet.dll`（**`dotnet` 而非 `dotnet35`**，匹配 .NET 8）。
- 在 `Infrastructure` 提供 `HalconHandleScope`/`using` 包装，确保 `HObject`/`HTuple` 自动释放（解决 M2/M3）。
- `DetectionService.Process` 的 Halcon 算子（`FindShapeModel`、`Rgb1ToGray`、`BinaryThreshold`、`FillUp`、`OpeningCircle`、`ClosingCircle`、`Connection`、`SelectShape`、`CountObj` 等）**跨版本稳定**，无需改算法，仅替换命名空间引用与引用程序集。

```csharp
// 资源自动释放助手（扩展 M3 建议）
using var gray = channels.I == 3
    ? HOperatorSet.Rgb1ToGray(image)   // 伪代码：实际用 out 变量 + using
    : image.CloneScoped();
```

---

## 4. 数据层重构（EF6 → EF Core）

### 4.1 DbContext 与实体（Code First）

删除 `Model.edmx` + `.tt`，手写实体与 `AppDbContext`：

```csharp
public class Member
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public string PasswordHash { get; set; }   // 不再明文存储
    public string Role { get; set; }
}

public class ProductInspectionRecord
{
    public long Id { get; set; }
    public DateTime InspectTime { get; set; }
    public string ChipModel { get; set; }
    public bool IsOk { get; set; }
    public int PinCount { get; set; }
    public int PinCount2 { get; set; }
    public double MatchScore { get; set; }
    public string DefectReason { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Member> Members { get; set; }
    public DbSet<ProductInspectionRecord> Records { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ProductInspectionRecord>().HasIndex(r => r.InspectTime); // 加速 D3 时间过滤
    }
}
```

迁移：`dotnet ef migrations add InitialCreate` → `dotnet ef database update`（目标库 `VisualSortingDB`）。

### 4.2 仓储泛型化

```csharp
public interface IRepository<T> where T : class {
    Task<T> GetByIdAsync(object id);
    IQueryable<T> Query();
    Task AddAsync(T entity);
    Task<int> SaveChangesAsync();
}
public class EfRepository<T> : IRepository<T> where T : class {
    private readonly AppDbContext _db;
    public EfRepository(AppDbContext db) { _db = db; }
    // ... 实现
}
// 特化：IMemberRepository、IProductInspectionRecordRepository 继承 IRepository<T>
```

### 4.3 连接串加密与低权限账号（D1）

- `appsettings.json` 中 `ConnectionStrings.VisualSortingDb` 以 **DPAPI 加密后的密文**存储，运行时 `ConfigService` 解密。
- 数据库创建专用应用账号（如 `vs_app`），仅授予 `VisualSortingDB` 的 `db_datareader/db_datawriter`，禁用 `sa`。

### 4.4 DbContext 生命周期（D2）

注册为 Scoped：`registry.RegisterScoped<AppDbContext>()`，由仓储/`IUnitOfWork` 解析，退出作用域自动释放，杜绝"每次 new 上下文"（原 D2 风险）。

### 4.5 统计查询分页与时间过滤（D3）

`Reports` 模块的统计服务必须带时间范围与分页：

```csharp
var rows = await _db.Records
    .Where(r => r.InspectTime >= start && r.InspectTime < end)
    .OrderBy(r => r.InspectTime)
    .Skip((page-1)*size).Take(size)
    .ToListAsync();
// 按分钟聚合在数据库端完成（GROUP BY DATEPART(minute, ...)），避免全表拉取
```

> LiveCharts 折线图按分钟统计合格/不合格，数据源由上述分页/聚合查询驱动（替代原"每次刷新查全表"）。

---

## 5. 视觉模块重构（Vision）

### 5.1 CameraService（单例）

- 相机 SN 来自 `HardwareOptions.CameraSn`（H1）。
- `StartGrabbing`/`StopGrabbing` **加 `lock`**（解决 T3）。
- 采集线程 `Task.Run` 循环 `GrabImage`，每帧 `Clone` 后通过 `ImageGrabbedEvent` 投递（UI 线程由订阅方 `ThreadOption.UIThread` 保证）。
- 图像副本显式释放（M3）。

### 5.2 TemplateService（含离线加载，解决 L3）

- 模板创建逻辑不变（`GenRectangle1`→`EdgesSubPix`→`CreateShapeModelXld`→`GetShapeModelContours`）。
- **解耦相机与模板生命周期**（L2）：模板句柄独立持有，关闭相机不清模板。
- **支持离线加载**（L3）：`TemplateConfig` JSON 反序列化后，无需相机在线即可 `CreateShapeModel`，相机打开后直接可用。
- `ClearTemplate` 全量释放 `HObject`（保持）。

### 5.3 DetectionService（生产者-消费者，解决 L4）

核心改造：原"检测中仅更新显示、不执行检测"改为 **每帧入队、独立消费者线程检测**，确保不漏帧。

```csharp
// DetectionService 内部
private readonly BlockingCollection<HObject> _frameQueue = new(new ConcurrentQueue<HObject>(), boundedCapacity: 4);
private readonly CancellationTokenSource _cts = new();

public void EnqueueFrame(HObject frame) => _frameQueue.Add(frame.Clone()); // 转移所有权

// 消费者（启动检测时 Task.Run）
private void ConsumeLoop()
{
    foreach (var frame in _frameQueue.GetConsumingEnumerable(_cts.Token))
    {
        var result = Process(frame);
        frame.Dispose();                 // 消费者负责释放入队副本
        _resultQueue.Enqueue(result);    // 交给分拣/统计
    }
}
```

- `Process` 方法算法与 `finally` 释放逻辑保持（M1 已合规），仅替换为 Halcon 24.11 引用。
- 上升沿/下降沿逻辑保持（`FALLING_EDGE_TIMEOUT_MS = 2500` → 来自 `InspectionOptions`）。
- `_isDetecting` 等跨线程标志加 `volatile`/`Interlocked`（T2）。

---

## 6. 运动 / 硬件模块重构（Motion）

### 6.1 MotionControlService（速度实时生效，解决 H3）

`ChangeSpeed` 内自动 `Vstop()` + `Vmove()`，无需重启传送带：

```csharp
public void ChangeSpeed(double speed)
{
    lock (_lock) { _card.Vstop(axis); _card.Vmove(axis, speed); }
    _config.Reject.AutoCompute... // 速度变化自动重算剔除延时（H2）
}
```

### 6.2 SensorService（10ms 轮询，volatile，解决 T2）

- `System.Timers.Timer` 间隔 10ms 读 `IN0`；`_isActionRunning` 加 `volatile` 防重复触发。
- 检测到上升沿 → 从 `_resultQueue` 取结果；**队列空默认判 NG 并剔除**（解决 L1）。

### 6.3 SolenoidValueService（剔除延时配置化，解决 H2）

- `OpenValue`/`CloseValue` 控制 `OUT2`；延时来自 `RejectOptions.RejectDelaySeconds`（默认 3.3s）、`SolenoidOpenSeconds`（0.3s）。
- 当 `AutoComputeBySpeed=true`：`延时 = f(传送带速度, 相机到剔除工位距离)`，由 `ChangeSpeed` 实时计算（H2）。

### 6.4 LTSMC 互操作（IP/IO 配置化，解决 H1）

`Services\Interop\LTSMC.cs` P/Invoke 保持；`Connect(ip)` 的 IP 来自 `MotionOptions.ControlCardIp`，IO 口来自配置，消除硬编码。

---

## 7. 业务编排与界面模块

### 7.1 Shell 与启动引导

`PrismApplication`（`App.xaml` 改 `prism:PrismApplication` 引导），MainWindow 定义 Region（见 2.2）。

### 7.2 区域导航与生命周期（解决 M4）

```csharp
// 导航：替代原 AppData.Container.Content = new Login()
_regionManager.RequestNavigate("ContentRegion", "LoginView");

// MainViewModel 实现 INavigationAware / IDisposable
public void OnNavigatedFrom() { StopLive(); StopAutoRefresh(); } // 离开即停
public void OnNavigatedTo(...) { }
public void Destroy() => Dispose();   // Prism 在区域替换时调用，释放线程/事件/消息
```

> 原 MainWindow.OnClosing 手动 `vm.Dispose()` 升级为：Prism 在 `RequestNavigate` 切换时自动卸载旧视图并触发 `IDestructible.Destroy()`，彻底解决 M4（旧 VM 持续运行）。

### 7.3 MainView / MainViewModel

- 14 个命令保持，类型改用 `DelegateCommand`/`AsyncDelegateCommand`。
- 维护 `_chipResultQueue`（lock 保护，`_queueLock`）。
- **队列空默认 NG**（L1）：传感器触发但队列空 → 判不合格并剔除，防止漏检。
- 实现 `IDisposable`：释放事件订阅、`Unregister` 事件聚合器、停线程（M4）。

### 7.4 登录模块

`LoginViewModel` 经 `IMemberRepository` 校验（密码哈希比对，非明文），通过后 `_regionManager.RequestNavigate("ContentRegion","MainView")`。

### 7.5 设置模块（解决 H1/H2/H3）

独立 `SettingsView`：编辑相机 SN、控制卡 IP、IO 口、剔除延时、传送带速度、针脚合格范围，写回 `appsettings.json` 并经 `IConfigService` 热更新（必要时重启相关 Service）。

### 7.6 YOLO 模块

`YoloDetectionViewModel` 接入推理引擎（ONNX Runtime / 原生 Halcon DL），复用 `CameraService` 图像事件（L3 离线思路同样适用）。

### 7.7 报表 / 统计模块（解决 D3）

`ReportsView` + `IStatisticsService`：时间范围选择器 + 分页表格 + LiveCharts 按分钟聚合图。

---

## 8. 多线程与并发治理

### 8.1 线程模型

| 线程 | 载体 | 职责 | 同步手段 |
|---|---|---|---|
| UI 主线程 | Dispatcher | 渲染、属性变更、图像显示 | 非 UI 线程经 `Application.Current.Dispatcher.Invoke` |
| 采集线程 | `Task.Run` | `GrabImage` 连续采集 | `CancellationToken` + `lock`（T3） |
| 检测消费者线程 | `Task.Run` | `Process` 算法 | `_isDetecting` 用 `Interlocked`（T2） |
| 传感器轮询 | `Timer` 10ms | 读 IN0、触发分拣 | `_isActionRunning` 用 `volatile`（T2） |
| 统计/IO | `Task.Run` | DB、状态采集 | `lock` + using 管理 DbContext |

### 8.2 锁与可见性（T1/T2/T3）

- **T1**：所有绑定属性修改必须在 UI 线程（`ThreadOption.UIThread` 或 `Dispatcher.Invoke`）。
- **T2**：跨线程 `bool` 标志加 `volatile` 或用 `Interlocked`/`lock`。
- **T3**：`StartGrabbing`/`StopGrabbing` 纳入 `_lock`，覆盖启停全流程。

### 8.3 HObject 跨线程生命周期

- 发布/订阅图像一律 `Clone` + 所有权转移（M3）。
- `CurrentImage` 属性采用"旧值暂存→赋新值→释放旧值"（M2），推广到所有 `HObject` 属性。

---

## 9. 资源释放与退出流程

### 9.1 非托管资源治理（M1/M2/M3）

- `Process` 的 `finally` 逐个释放（M1 已合规，新增单元测试校验）。
- 所有 `HObject` 属性统一封装 setter 自动释放旧值（M2）。
- 图像副本用 `using`/显式释放（M3）。

### 9.2 有序退出（解决 X1/X2）

`App.OnExit` / Shell `OnClosing` 按序释放：

```csharp
// 停止检测 → 停止传送带 → 关闭电磁阀 → 停止采集 → 关闭相机
// → 断开控制卡 → 释放 DbContext → 注销事件聚合器 → Shutdown
_detection.Stop(); _motion.Vstop(axis); _solenoid.CloseValue();
_camera.StopGrabbing(token, waitForExit: true);  // X1：等待线程真正退出再释放
_camera.Close(); _motion.Disconnect();
_container.DisposeScope(); _ea.UnsubscribeAll();
```

> X1：退出等待由"硬编码 500ms"改为**等待 `Task` 真正完成（`task.Wait(Timeout)` + `IsCompleted` 校验）**。
> X2：严格按上序释放，避免资源提前释放导致后台线程访问已释放句柄。

---

## 10. IC.pdf 风险点闭环对照表

| 编号 | 风险 | 等级 | 重构措施 | 落位模块 |
|---|---|---|---|---|
| M1 | 算法临时 HObject 未释放 | 低 | `finally` 已合规 + 单元测试校验 | Vision |
| M2 | `CurrentImage` 旧图未释放 | 低 | 统一 `HObject` 属性 setter 释放旧值 | Vision/Infra |
| M3 | 相机事件图像副本未释放 | 低 | `using`/显式释放 + 所有权约定 | Vision |
| M4 | 页面切换旧 VM 未释放 | **高** | Prism `INavigationAware`/`IDestructible` 显式释放 | Shell/各 VM |
| T1 | 定时器改 UI 属性未调度 | 中 | `ThreadOption.UIThread` / `Dispatcher` | 各 VM |
| T2 | 跨线程 bool 无 volatile | 低 | `volatile`/`Interlocked` | Vision/Motion |
| T3 | 相机启停无锁 | 中 | `_lock` 覆盖启停全流程 | Vision |
| H1 | 硬件参数硬编码 | **高** | `appsettings.json` + 设置页 + 配置服务 | Settings/Infra |
| H2 | 剔除延时与速度硬耦合 | 中 | 速度→延时自动换算公式 + 配置 | Motion/Settings |
| H3 | 改速度需重启生效 | 低 | `ChangeSpeed` 内 `Vstop+Vmove` | Motion |
| D1 | sa 密码明文 | **高** | 连接串 DPAPI 加密 + 低权限账号 | Data/Infra |
| D2 | DbContext 非 DI | 中 | 注册为 Scoped，由仓储解析 | Data |
| D3 | 统计无分页/时间过滤 | **高** | 时间范围 + 分页 + 端侧聚合 | Reports/Data |
| L1 | 队列空默认合格放行 | **高** | 队列空 → 默认 NG 并剔除 | Inspection/Motion |
| L2 | 关相机清模板 | 中 | 解耦相机/模板生命周期 | Vision |
| L3 | 模板需相机在线加载 | 中 | 支持离线加载模板配置 | Vision |
| L4 | 检测漏帧 | 中 | 生产者-消费者队列，每帧检测 | Vision |
| X1 | 采集线程停止仅等 500ms | 中 | 等待任务真正退出再释放 | Shell/Vision |
| X2 | 退出释放顺序不合理 | 低 | 严格有序退出链 | Shell |

---

## 11. 迁移实施路线图

| 阶段 | 目标 | 关键产物 | 退出标准 |
|---|---|---|---|
| P0 骨架 | 升级 .NET 8 + SDK 工程 + Prism 引导 | Shell + Core + Infrastructure 可启动，MainWindow 显示 Region | 应用启动、区域导航 Login↔Main 正常 |
| P1 数据 | EF6→EF Core Code First | `AppDbContext`、实体、仓储、迁移脚本、加密连接串 | 登录校验、记录写入成功；D1/D2/D3 闭环 |
| P2 视觉 | 迁移 Vision 模块 + Halcon 24.11 | Camera/Template/Detection 单例、生产者-消费者 | 实时检测、离线模板、无漏帧（L2/L3/L4） |
| P3 运动 | 迁移 Motion 模块 | 配置化 IP/IO/延时、速度实时生效 | H1/H2/H3 闭环，剔除动作正确 |
| P4 编排 | Inspection/Login/Settings/Reports/Yolo | 全生命周期、报表统计、设置页 | M4/T 系列、X 系列闭环 |
| P5 加固 | 资源/线程/退出专项 + 测试 | 单元/集成测试、退出演练 | 长时间运行无泄漏、退出无卡死 |

---

## 12. 解决方案目录结构（目标）

```
VisualSorting.sln
├─ src/
│  ├─ VisualSorting.Shell/        (PrismApplication, MainWindow, App.xaml)
│  ├─ VisualSorting.Core/         (I*Service, Models, Options, Events)
│  ├─ VisualSorting.Infrastructure/(LogService, ConfigService, Halcon 基类)
│  ├─ VisualSorting.Data/         (AppDbContext, Entities, Repositories, Migrations)
│  ├─ VisualSorting.Vision/       (Camera/Template/Detection Service, Interop)
│  ├─ VisualSorting.Motion/       (Motion/Sensor/Solenoid Service, LTSMC)
│  ├─ VisualSorting.Inspection/   (MainView, MainViewModel, Stats)
│  ├─ VisualSorting.Login/
│  ├─ VisualSorting.Settings/
│  ├─ VisualSorting.Yolo/
│  └─ VisualSorting.Reports/
├─ tests/
│  ├─ Vision.Tests/  Data.Tests/  (xUnit)
└─ docs/  (本开发文档)
```

---

## 13. NuGet 包清单（目标栈）

| 包 | 版本 | 用途 |
|---|---|---|
| `Prism.Wpf` / `Prism.DryIoc` / `Prism.Container.Extensions` | 9.x | 模块化 MVVM |
| `Microsoft.EntityFrameworkCore` / `.SqlServer` / `.Tools` | 8.x | ORM |
| `Microsoft.Extensions.Configuration.Json` / `.Binder` | 8.x | 配置 |
| `Microsoft.Extensions.Options` / `.DependencyInjection` | 8.x | DI/Options |
| `NLog` / `NLog.Extensions.Logging` | 5.x | 日志 |
| `MahApps.Metro` | 2.4+ | UI |
| `LiveCharts2` / `LiveCharts2.Wpf`（或保留 0.9.7 兼容包） | 2.x | 可视化 |
| `halcondotnet`（本地引用 `HALCON-24.11\bin\dotnet`） | 24.11 | 视觉 |
| `System.Security.Cryptography.ProtectedData` | 8.x | 连接串 DPAPI 加密（D1） |

---

## 14. 测试与验收标准

1. **功能**：登录→硬件初始化→相机→模板→检测→分拣→记录→统计全链路通过。
2. **配置**：改 `appsettings.json` 相机 SN/控制卡 IP 后无需重编译即生效（H1）。
3. **内存**：连续运行 2 小时，`HObject` 句柄数稳定无增长（M1/M2/M3）。
4. **线程**：传感器高频触发下无重复分拣、无 UI 卡顿（T1/T2/T3）。
5. **业务**：队列空时不合格品被剔除（L1）；离线可加载模板（L3）；每帧均检测（L4）。
6. **数据**：统计查询在百万级记录下 < 1s（D3）；连接串无明文（D1）。
7. **退出**：点关闭后 3 秒内干净退出，无后台线程残留（X1/X2）。

---

## 附录 A：原→新 关键 API 替换速查

| 原（IC.pdf / 源码） | 新（Prism/EF Core） |
|---|---|
| `ServiceLocator.GetService<T>()` | `Container.Resolve<T>()` / 构造函数注入 |
| `WeakReferenceMessenger.Default` | `IEventAggregator.GetEvent<T>()` |
| `ObservableObject` | `BindableBase`（`SetProperty`） |
| `RelayCommand`/`AsyncRelayCommand` | `DelegateCommand`/`AsyncDelegateCommand` |
| `AppData.Container.Content = new X()` | `IRegionManager.RequestNavigate("ContentRegion", "XView")` |
| `EF6 DbContext` (edmx) | `AppDbContext : DbContext` (Code First) |
| `App.config` 明文连接串 | `appsettings.json` + DPAPI 加密 |
| `halcondotnet` (dotnet35/绝对路径) | `halcondotnet` (HALCON-24.11\bin\dotnet) |

> 本文档与原 IC.pdf 配套使用：IC.pdf 描述"现状与为何"，本文档描述"目标架构与如何改"。所有整改项均可在第 10 章对照表中追溯闭环。
