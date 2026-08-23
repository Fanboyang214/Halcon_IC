# VisualSorting IC 芯片视觉分拣系统 — 完整项目结构与详细功能描述（V2.0）

> 配套文档：《VisualSorting 模块化重构开发文档 V2.0》（`VisualSorting_Refactor_DevDoc.md`）
> 目标技术栈：**WPF + Prism 9 + .NET 8 + SQL Server + EF Core 8 + Halcon 24.11**
> 本文档给出**文件级完整项目结构**与**每个模块/关键类的详细功能描述**，可直接作为编码与评审基线。

---

## 1. 解决方案总览

解决方案 `VisualSorting.sln` 采用 **Prism 模块化（Modular Application）** 组织，遵循"契约在 Core、实现在模块、宿主只引导"的原则：

- **宿主工程（Shell）**：仅负责 Prism 引导、`MainWindow` 与 Region 定义、模块目录、进程生命周期（有序退出）。
- **契约工程（Core）**：仅含接口、模型、配置 POCO、事件定义，**不依赖任何具体实现**，供所有模块引用。
- **功能模块（Vision / Motion / Data / Inspection / Login / Settings / Yolo / Reports）**：各自独立 `.csproj`，通过 `IModule` 向容器注册自身服务与视图。
- **基础设施（Infrastructure）**：跨模块的横切关注点（日志、配置、Halcon 资源助手、Dispatcher 助手）。
- **测试工程（tests）**：xUnit 单元/集成测试，验证算法、仓储、资源释放。

---

## 2. 完整项目结构树（文件级）

```
VisualSorting.sln
├─ src/
│  ├─ VisualSorting.Shell/                 # 宿主工程（启动、Region、模块目录、退出）
│  │  ├─ VisualSorting.Shell.csproj        # SDK 风格，OutputType=WinExe，TargetFramework=net8.0-windows
│  │  ├─ App.xaml                          # prism:PrismApplication 引导
│  │  ├─ App.xaml.cs                       # PrismApplication：CreateShell/RegisterTypes/ConfigureModuleCatalog
│  │  ├─ MainWindow.xaml                   # MetroWindow + Region 定义（ContentRegion / StatusBarRegion / NavRegion）
│  │  ├─ MainWindow.xaml.cs                # 窗口加载默认导航到 Login；OnClosing 有序释放
│  │  ├─ appsettings.json                  # 硬件/检测/连接串(加密)/日志 配置
│  │  ├─ NLog.config                       # NLog 输出目标（文件+控制台）
│  │  ├─ Properties/launchSettings.json
│  │  └─ Resources/                        # 图标、背景图等
│  │
│  ├─ VisualSorting.Core/                  # 契约层（接口/模型/配置/事件/常量）
│  │  ├─ VisualSorting.Core.csproj         # 类库，net8.0-windows，无具体实现依赖
│  │  ├─ Interfaces/
│  │  │  ├─ /ICameraService.cs              # 相机连接/采集/停止
│  │  │  ├─ /ITemplateService.cs            # 形状模板创建/管理/释放
│  │  │  ├─ /IDetectionService.cs           # 视觉检测核心算法
│  │  │  ├─ IMotionControlService.cs       # 运动控制卡/传送带
│  │  │  ├─ ISensorService.cs              # 光电传感器轮询
│  │  │  ├─ ISolenoidValueService.cs       # 剔除电磁阀
│  │  │  ├─ /ILogService.cs                 # 日志收集/广播/持久化
│  │  │  ├─ /IConfigService.cs              # 配置读取/热更新/写回
│  │  │  ├─ IStatisticsService.cs          # 统计/分页/时间过滤
│  │  │  ├─ /IRepository.cs                 # 泛型仓储接口
│  │  │  ├─ /IMemberRepository.cs           # 用户仓储（登录校验）
│  │  │  ├─ /IProductInspectionRecordRepository.cs # 检测记录仓储
│  │  │  └─ IUnitOfWork.cs                 # 工作单元（DbContext 作用域）
│  │  ├─ Models/
│  │  │  ├─ DetectionResult.cs             # 检测结果（分数/合格/针脚数/显示图/触发标志）
│  │  │  ├─ InspectionConfig.cs            # 检测配置（芯片型号/针脚范围/缺陷原因）
│  │  │  ├─ TemplateConfig.cs              # 模板配置（JSON 序列化，支持离线加载）
│  │  │  ├─ LogEntry.cs                    # 日志实体（时间/级别/消息/颜色）
│  │  │  ├─ ImageGrabbedPayload.cs         # 图像采集事件载荷（HObject 所有权约定）
│  │  │  └─ ImageSize.cs                   # 首帧尺寸（设置 Halcon 窗口显示区）
│  │  ├─ Options/
│  │  │  ├─ HardwareOptions.cs             # 相机SN/控制卡IP/IO口/剔除延时
│  │  │  ├─ InspectionOptions.cs           # 针脚合格范围/最小匹配分/下降沿超时
│  │  │  ├─ ConnectionOptions.cs           # 数据库连接串（密文）
│  │  │  └─ LoggingOptions.cs              # 日志级别
│  │  ├─ Events/
│  │  │  ├─ LogPublishedEvent.cs           # PubSubEvent<LogEntry>
│  │  │  ├─ ImageGrabbedEvent.cs           # PubSubEvent<ImageGrabbedPayload>
│  │  │  ├─ FirstImageReceivedEvent.cs     # PubSubEvent<ImageSize>
│  │  │  ├─ NavigateRequestEvent.cs        # PubSubEvent<string>（"Login"/"Main"/...）
│  │  │  └─ DetectionResultEvent.cs        # PubSubEvent<DetectionResult>（分拣/统计订阅）
│  │  └─ Constants/
│  │     ├─ RegionNames.cs                 # "ContentRegion"/"StatusBarRegion"/"NavRegion"
│  │     └─ ModuleNames.cs                 # 模块名常量（导航用）
│  │
│  ├─ VisualSorting.Infrastructure/        # 横切关注点实现
│  │  ├─ VisualSorting.Infrastructure.csproj
│  │  ├─ Services/
│  │  │  ├─ LogService.cs                  # ILogService：NLog 持久化 + EventAggregator 广播
│  │  │  └─ ConfigService.cs               # IConfigService：IOptions 绑定 + 写回 appsettings.json
│  │  ├─ Halcon/
│  │  │  ├─ HalconScope.cs                 # IDisposable 包装，确保 HObject/HTuple 释放（治理 M2/M3）
│  │  │  └─ HalconRuntime.cs               # HALCON 授权初始化/校验（24.11 许可）
│  │  └─ Prism/
│  │     └─ DispatcherHelper.cs            # 跨线程安全回到 UI 线程（治理 T1）
│  │
│  ├─ VisualSorting.Data/                  # 数据模块（EF Core）
│  │  ├─ VisualSorting.Data.csproj
│  │  ├─ DataModule.cs                     # IModule：注册 DbContext(Scoped)/仓储/统计服务
│  │  ├─ AppDbContext.cs                   # DbContext（Code First，连接串解密注入）
│  │  ├─ Entities/
│  │  │  ├─ Member.cs                      # 用户表（密码哈希）
│  │  │  └─ ProductInspectionRecord.cs     # 检测记录表（带 InspectTime 索引，治理 D3）
│  │  ├─ Migrations/
│  │  │  └─ 20260501000000_InitialCreate.cs # 初始迁移脚本
│  │  ├─ Repositories/
│  │  │  ├─ EfRepository.cs                # IRepository<T> 泛型实现
│  │  │  ├─ MemberRepository.cs            # IMemberRepository
│  │  │  ├─ ProductInspectionRecordRepository.cs # IProductInspectionRecordRepository
│  │  │  └─ UnitOfWork.cs                  # IUnitOfWork：DbContext 作用域管理
│  │  └─ Services/
│  │     └─ StatisticsService.cs           # IStatisticsService：时间范围+分页+端侧聚合
│  │
│  ├─ VisualSorting.Vision/                # 视觉模块
│  │  ├─ VisualSorting.Vision.csproj       # 引用 halcondotnet（HALCON-24.11\bin\dotnet）
│  │  ├─ VisionModule.cs                   # IModule：注册 Camera/Template/Detection（单例）
│  │  └─ Services/
│  │     ├─ CameraService.cs               # ICameraService：GigE 相机连接/采集线程/事件投递
│  │     ├─ TemplateService.cs             # ITemplateService：形状模板创建/离线加载/释放
│  │     └─ DetectionService.cs            # IDetectionService：生产者-消费者检测调度+Process 算法
│  │
│  ├─ VisualSorting.Motion/                # 运动/硬件模块
│  │  ├─ VisualSorting.Motion.csproj
│  │  ├─ MotionModule.cs                   # IModule：注册 Motion/Sensor/Solenoid（单例）
│  │  ├─ Services/
│  │  │  ├─ MotionControlService.cs        # IMotionControlService：连接/使能/启停/变速
│  │  │  ├─ SensorService.cs               # ISensorService：10ms 轮询 IN0，触发分拣
│  │  │  └─ SolenoidValueService.cs        # ISolenoidValueService：OUT2 电磁阀剔除
│  │  └─ Interop/
│  │     └─ LTSMC.cs                       # 雷赛控制卡 P/Invoke 封装（Connect/IP/IO 配置化）
│  │
│  ├─ VisualSorting.Inspection/            # 业务编排模块（中枢）
│  │  ├─ VisualSorting.Inspection.csproj
│  │  ├─ InspectionModule.cs               # IModule：注册 MainView/SystemStatusView + VM
│  │  ├─ Views/
│  │  │  ├─ MainView.xaml / .xaml.cs        # 主检测界面（Halcon 窗口+日志+图表+控制）
│  │  │  └─ SystemStatusView.xaml / .xaml.cs # CPU/内存实时状态（StatusBarRegion）
│  │  ├─ ViewModels/
│  │  │  ├─ MainViewModel.cs               # 全系统调度中枢（14 命令，IDisposable）
│  │  │  └─ SystemStatusViewModel.cs        # 性能计数器采集（_initLock 保护）
│  │  └─ Controls/
│  │     └─ HalconDisplayControl.cs        # Halcon 窗口封装（显示/ROI 绘制交互）
│  │
│  ├─ VisualSorting.Login/                 # 登录模块
│  │  ├─ LoginModule.cs
│  │  ├─ Views/Login.xaml / .xaml.cs
│  │  └─ ViewModels/LoginViewModel.cs      # 用户名/密码校验 → 导航 Main
│  │
│  ├─ VisualSorting.Settings/              # 设置模块（治理 H1/H2/H3）
│  │  ├─ SettingsModule.cs
│  │  ├─ Views/SettingsView.xaml / .xaml.cs
│  │  └─ ViewModels/SettingsViewModel.cs   # 编辑相机SN/IP/IO/延时/速度/针脚范围→写回配置
│  │
│  ├─ VisualSorting.Yolo/                  # YOLO 检测模块
│  │  ├─ YoloModule.cs
│  │  ├─ Views/YoloDetectionView.xaml / .xaml.cs
│  │  ├─ ViewModels/YoloDetectionViewModel.cs # 接入推理（复用相机图像事件）
│  │  └─ Services/YoloInferService.cs      # ONNX Runtime / Halcon DL 推理封装（可选）
│  │
│  └─ VisualSorting.Reports/               # 报表/统计模块（治理 D3）
│     ├─ ReportsModule.cs
│     ├─ Views/ReportsView.xaml / .xaml.cs
│     └─ ViewModels/ReportsViewModel.cs    # 时间范围+分页查询+LiveCharts 按分钟聚合
│
├─ tests/
│  ├─ VisualSorting.Vision.Tests/          # DetectionService 算法/资源释放/触发逻辑单测
│  ├─ VisualSorting.Data.Tests/            # 仓储/统计查询（SQLite 内存库）集成测试
│  └─ VisualSorting.UnitTests/             # 配置/消息总线/事件聚合器单测
│
└─ docs/
   ├─ VisualSorting_Refactor_DevDoc.md     # 重构开发文档（前文）
   └─ VisualSorting_Project_Structure.md   # 本文档
```

---

## 3. 各项目/模块详细功能描述

### 3.1 VisualSorting.Shell（宿主）

| 文件 | 功能 |
|---|---|
| `App.xaml.cs` | 继承 `PrismApplication`。`CreateShell()` 解析 `MainWindow`；`RegisterTypes` 注册基础设施（日志/配置）；`ConfigureModuleCatalog` 显式注册 8 个功能模块（确定性启动顺序，优于目录发现）。 |
| `MainWindow.xaml/.cs` | `MetroWindow` 定义三个 Region：主内容 `ContentRegion`、状态栏 `StatusBarRegion`、导航 `NavRegion`。加载时 `RequestNavigate("ContentRegion","LoginView")`；`OnClosing` 按"停止检测→停传送带→关阀→停采集→关相机→断卡→释放 DB→注销消息→Shutdown"有序退出（治理 X1/X2）。 |
| `appsettings.json` | 集中硬件/检测/连接串(密文)/日志配置，替代原 `App.config` 明文（治理 D1/H1）。 |
| `NLog.config` | NLog 输出目标（滚动文件 + 控制台），由 `LogService` 使用。 |

### 3.2 VisualSorting.Core（契约层）

纯契约，不含实现，被所有模块引用。

- **Interfaces/**：原 `Services\I*.cs` 接口原样保留并归入此层（`ICameraService` 等 7 个服务接口 + `IRepository<T>`/`IMemberRepository`/`IProductInspectionRecordRepository`/`IUnitOfWork` 数据接口 + `IConfigService`/`IStatisticsService`）。接口稳定即重构面可控。
- **Models/**：`DetectionResult`（匹配分/合格/针脚数/显示图/触发标志）、`TemplateConfig`（JSON 可序列化，支持离线加载 L3）、`InspectionConfig`、`LogEntry`、`ImageGrabbedPayload`（携带 `HObject`，约定 Clone+所有权转移）、`ImageSize`。
- **Options/**：`HardwareOptions`（相机 SN、控制卡 IP、IO 口、剔除延时）、`InspectionOptions`（针脚范围、最小匹配分 0.65、下降沿超时 2500ms）、`ConnectionOptions`、`LoggingOptions`——经 `IOptions<T>` 绑定。
- **Events/**：5 个 `PubSubEvent<T>` 强类型事件，替代原 `WeakReferenceMessenger` 与 `ServiceLocator` 的 `LogMessage`/`ImageSizeEventArgs`。
- **Constants/**：`RegionNames`、`ModuleNames` 集中管理魔法字符串，防止拼写错误。

### 3.3 VisualSorting.Infrastructure（横切实现）

- **LogService**：实现 `ILogService`。`AddLog(level,msg)` 同时写 NLog 文件与 `LogPublishedEvent` 广播（跨层日志，原 `LogService` 行为保留）。
- **ConfigService**：实现 `IConfigService`。启动时从 `appsettings.json` 绑定 `IOptions<T>`；提供保存接口将设置页改动写回文件并热更新（支撑 Settings 模块 H1/H2/H3）。
- **HalconScope**：`IDisposable` 包装 `HObject`/`HTuple`，`using` 即释放（治理 M2/M3），并辅助 `CurrentImage` 旧值释放模式。
- **HalconRuntime**：封装 HALCON 24.11 许可校验与全局初始化（如 `HOperatorSet.SetSystem` 等）。
- **DispatcherHelper**：封装 `Application.Current.Dispatcher`，供非 UI 线程安全回 UI（治理 T1）。

### 3.4 VisualSorting.Data（数据模块）

- **AppDbContext**：`DbContext` 子类，构造函数接收 `DbContextOptions`（连接串由 `ConfigService` 解密注入，治理 D1）。`OnModelCreating` 为 `ProductInspectionRecord.InspectTime` 建索引（治理 D3）。
- **Entities**：`Member`（密码哈希，禁明文）、`ProductInspectionRecord`（检测时间/型号/合格/针脚数/匹配分/缺陷原因）。
- **Repositories**：`EfRepository<T>` 泛型实现 `IRepository<T>`；`MemberRepository` 供登录校验；`ProductInspectionRecordRepository` 供记录写入与查询。`UnitOfWork` 管理 DbContext 作用域（治理 D2：Scoped 生命周期）。
- **StatisticsService**：`IStatisticsService` 实现。按 `start/end` 时间范围 + 分页查询，端侧 `GROUP BY` 按分钟聚合合格/不合格，避免全表拉取（治理 D3）。
- **DataModule**：`IModule.RegisterTypes` 注册 `AppDbContext`(Scoped)、仓储、`StatisticsService`。

### 3.5 VisualSorting.Vision（视觉模块）

- **CameraService**（单例，`ICameraService`）：`Open(Sn)`/`Close` 用配置相机 SN（治理 H1）；`StartGrabbing`/`StopGrabbing` 加 `lock`（治理 T3）；后台 `Task.Run` 循环 `GrabImage`，每帧 `Clone` 经 `ImageGrabbedEvent` 投递（`ThreadOption.UIThread` 保证 UI 线程）。
- **TemplateService**（单例，`ITemplateService`）：`SetTemplateRegion`/`CreateTemplate`（`GenRectangle1`→`EdgesSubPix`→`CreateShapeModelXld`→`GetShapeModelContours`）；`ClearTemplate` 全量释放。**解耦相机与模板生命周期**（治理 L2），**支持 JSON 反序列化离线创建模板**（治理 L3）。
- **DetectionService**（单例，`IDetectionService`）：内部 `BlockingCollection<HObject>` 生产者-消费者队列——相机帧入队、独立消费者线程执行 `Process`（治理 L4 漏帧）。`Process` 算法与 `finally` 释放逻辑保持（M1 合规），仅换 Halcon 24.11 引用。上升沿/下降沿、针脚计数、合格判定逻辑不变。跨线程标志用 `Interlocked`（治理 T2）。

### 3.6 VisualSorting.Motion（运动/硬件模块）

- **MotionControlService**（单例）：`Connect(ip)` 用配置 IP（治理 H1）；`Sevon`(使能)/`Vmove`(连续运动)/`Vstop`(停止)/`ChangeSpeed`——`ChangeSpeed` 内部自动 `Vstop+Vmove` 实时生效（治理 H3）。
- **SensorService**（单例）：`System.Timers.Timer` 10ms 轮询读 `IN0`（`ReadSensorState`），`_isActionRunning` 加 `volatile`（治理 T2）；检测到上升沿从结果队列取结果，**队列空默认判 NG 并剔除**（治理 L1）。
- **SolenoidValueService**（单例）：`OpenValue`/`CloseValue` 控制 `OUT2`；延时来自 `RejectOptions`（3.3s / 0.3s），`AutoComputeBySpeed=true` 时按速度自动换算（治理 H2）。
- **LTSMC.cs**：雷赛 `LTSMC.dll` P/Invoke 封装，IP 与 IO 口全部来自配置（治理 H1）。

### 3.7 VisualSorting.Inspection（业务编排中枢）

- **MainView / MainViewModel**：全系统调度中枢。`MainViewModel` 实现 `BindableBase` + `IDisposable` + `INavigationAware`/`IDestructible`：**离开页面即停检测与自动刷新**（治理 M4）；维护 `_chipResultQueue`（lock 保护）；14 个 `DelegateCommand`/`AsyncDelegateCommand`（相机、模板、检测、运动、分拣、统计）；接收 `ImageGrabbedEvent` 入队、订阅 `DetectionResultEvent` 驱动统计与图表；**队列空默认 NG**（治理 L1）。
- **SystemStatusViewModel**：每秒采集 CPU/内存，`_initLock` 保护性能计数器初始化（原 `SystemStatusViewModel` 行为保留）。
- **HalconDisplayControl**：封装 Halcon 窗口 `SetPart`/`DispObj`，承载 ROI 绘制交互（`RequestDrawTemplate` 等事件响应）。

### 3.8 VisualSorting.Login（登录模块）

- 登录页与 `LoginViewModel`：`IMemberRepository` 校验用户名 + 密码哈希；成功经 `IRegionManager.RequestNavigate("ContentRegion","MainView")` 切换；失败时经 `LogPublishedEvent` 提示。

### 3.9 VisualSorting.Settings（设置模块，治理 H1/H2/H3）

- 设置页与 `SettingsViewModel`：编辑相机 SN、控制卡 IP、IO 口、剔除延时、传送带速度、针脚合格范围；保存到 `IConfigService` 并热更新相关 Service（必要时重启相机/控制卡连接）。把"换硬件必须改源码"变为"界面配置"。

### 3.10 VisualSorting.Yolo（YOLO 模块）

- `YoloDetectionView` 与 `YoloDetectionViewModel`：框架沿用原 `YoloDetectionViewModel`，接入 `YoloInferService`（ONNX Runtime 或 Halcon DL）消费相机图像事件做推理；同样支持离线加载思路。

### 3.11 VisualSorting.Reports（报表/统计模块，治理 D3）

- `ReportsView` 与 `ReportsViewModel`：时间范围选择器 + 分页表格 + LiveCharts 按分钟聚合图；数据来自 `IStatisticsService`（端侧聚合，百万级记录 < 1s）。

---

## 4. 核心类功能详述（按职责）

### 4.1 配置 / DI / 消息总线
- **ConfigService**：读 `appsettings.json` → `IOptions<T>`；写回 + 热更新。是所有硬件参数（H1/H2/H3）与连接串（D1）的唯一真相源。
- **Prism 容器**：原 `App.ConfigureServices()` 的 `AddSingleton/AddTransient` 平移到各 `IModule.RegisterTypes(IContainerRegistry)`；`DbContext` 为 Scoped（D2）；ViewModel 由导航解析、卸载即释放（M4）。
- **EventAggregator**：5 个 `PubSubEvent` 替代 `WeakReferenceMessenger`；图像事件遵守"Clone + 所有权转移"。

### 4.2 数据层类
- **AppDbContext / Entities / Repositories / StatisticsService**：见 3.4。关键点是 Scoped 生命周期、索引、`GROUP BY` 端侧聚合。

### 4.3 视觉类
- **CameraService**：采集线程 + `lock` + 事件投递。
- **TemplateService**：模板创建/离线加载/释放。
- **DetectionService**：生产者-消费者调度 + `Process` 算法（灰度→匹配→仿射→二值化→形态学→连通域→计数→判定）。

### 4.4 运动类
- **MotionControlService / SensorService / SolenoidValueService / LTSMC**：见 3.6，重点在 `volatile`(T2)、`lock`(T3)、配置化(H1)、速度实时生效(H3)、阈值配置(H2)、队列空默认 NG(L1)。

### 4.5 编排与视图模型
- **MainViewModel**：系统调度中枢 + 生命周期治理（M4）+ 队列空默认 NG（L1）。
- **LoginViewModel / SettingsViewModel / ReportsViewModel / SystemStatusViewModel / YoloDetectionViewModel**：各自模块的功能入口。

---

## 5. 关键文件职责速查表

| 文件 | 所属模块 | 一句话职责 |
|---|---|---|
| `App.xaml.cs` | Shell | Prism 引导与模块注册 |
| `MainWindow.xaml.cs` | Shell | Region 导航 + 有序退出 |
| `appsettings.json` | Shell | 全部可配置参数 |
| `ICameraService.cs` 等 | Core | 服务契约 |
| `DetectionResult.cs`/`TemplateConfig.cs` | Core | 业务模型（模板可离线序列化） |
| `LogPublishedEvent.cs` 等 | Core | 强类型事件契约 |
| `LogService.cs` | Infrastructure | NLog + 广播 |
| `ConfigService.cs` | Infrastructure | 配置读写/热更新 |
| `HalconScope.cs` | Infrastructure | HObject 自动释放 |
| `AppDbContext.cs` | Data | EF Core 上下文 |
| `StatisticsService.cs` | Data | 时间范围统计 |
| `CameraService.cs` | Vision | 相机采集 |
| `DetectionService.cs` | Vision | 检测算法+队列调度 |
| `MotionControlService.cs` | Motion | 传送带控制 |
| `SensorService.cs` | Motion | 传感器轮询触发 |
| `MainViewModel.cs` | Inspection | 业务调度中枢 |
| `SettingsViewModel.cs` | Settings | 硬件参数配置 |
| `ReportsViewModel.cs` | Reports | 统计报表 |

---

## 6. 运行时协作（一次完整检测的业务流）

```
用户登录(LoginVM→MemberRepository)
   │ 成功 → RequestNavigate("ContentRegion","MainView")
   ▼
MainViewModel 启动：
   ├─ MotionControlService.Connect(IP) → Sevon → Vmove(传送带)
   ├─ CameraService.Open(Sn) → StartGrabbing (Task 采集线程)
   │     └─每帧 Clone → ImageGrabbedEvent → MainViewModel 入队 _frameQueue
   ├─ 用户在 HalconDisplayControl 绘制模板/针脚区域 → TemplateService.CreateTemplate
   │     (可离线加载 TemplateConfig，L3)
   └─ DetectionService 消费者线程：Process(帧) → DetectionResultEvent
            ├─ MainViewModel 更新显示/累计
            ├─ SensorService 10ms 轮询 IN0 上升沿 → 取队列结果
            │     ├─ 合格：放行
            │     └─ 不合格/队列空(L1)：延时→OpenValue(剔除)→CloseValue
            └─ ProductInspectionRecordRepository.Add → 落库
               ReportsViewModel/StatisticsService 按分钟聚合 → LiveCharts
退出：MainWindow.OnClosing 按 X2 顺序释放全部资源
```

---

## 7. 工程配置要点

### 7.1 各 .csproj 关键项（SDK 风格）
- `OutputType`：`WinExe`（Shell）/ `Library`（其余）。
- `TargetFramework`：`net8.0-windows`（统一）。
- `UseWPF`：`true`。
- Shell 引用 `Prism.DryIoc` + `Prism.Container.Extensions`；Vision 引用本地 `halcondotnet.dll`（`HintPath` 指向 `HALCON-24.11\bin\dotnet`）。
- 统一 `PackageReference`（弃用 `packages.config`）。

### 7.2 NuGet 包（目标栈）
`Prism.Wpf`/`Prism.DryIoc`/`Prism.Container.Extensions`(9.x)、`Microsoft.EntityFrameworkCore`(+`SqlServer`/`Tools`)(8.x)、`Microsoft.Extensions.Configuration.Json`/`Binder`/`Options`/`DependencyInjection`(8.x)、`NLog`/`NLog.Extensions.Logging`(5.x)、`MahApps.Metro`(2.4+)、`LiveCharts2`(2.x)、`System.Security.Cryptography.ProtectedData`(8.x，D1 加密)、本地 `halcondotnet`(24.11)。

### 7.3 编译/运行前置
- 安装 .NET 8 SDK、Halcon 24.11 运行时与许可、SQL Server（建议 2019+）、雷赛控制卡驱动。
- 首次运行：`dotnet ef database update`（Data 模块迁移建库 `VisualSortingDB`）。
- 连接串经 DPAPI 加密存储，运行账户需具备对应加密/解密权限。

---

> 本文档与《重构开发文档》互为补充：前者讲"为何改、改成什么架构、风险如何闭环"，本文档讲"项目里到底有哪些文件、每个文件干什么、运行时怎么协作"。两者结合即可进入编码阶段。
