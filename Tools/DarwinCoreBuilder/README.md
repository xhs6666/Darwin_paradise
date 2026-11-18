# DarwinCoreBuilder

## 两文件便携核心（项目一键构建）

在 Unity 项目菜单选择 `DarwinPark / Build / Windows Server Core (Core + Start)`。它重新构建真实游戏服务端，再生成 `Builds/ServerCore/Windows/<本次构建>/` 和同名下载 ZIP；解压下载 ZIP 后只有 `DarwinCore.zip`、`Start.cmd`。构建需要 Unity，开服机器不需要安装 Unity、Python 或 .NET SDK。

将这两个文件放到可写目录，双击 `Start.cmd`。启动器校验核心文件后自动生成 `.runtime` 运行依赖，以及 `data/server.json`、日志和存档。修改 `data/server.json` 后重新启动即可更改配置；已有配置和存档不会被初始化覆盖。控制台支持 `status`、`help`、`stop`（保存停服）。不要在运行中删除 `.runtime` 或 `data`。

`DarwinServer_Data` 包含游戏主题、程序集与资源，`MonoBleedingEdge` 是运行库，不能从原始构建直接删除。两者都封装在核心中，启动时自动释放；`D3D12` 同样保留在内部。明确标记 `_BurstDebugInformation_DoNotShip` 的调试目录不进入便携核心。

便携入口通过替换离线核心后重新启动来选用新运行目录，保留旧缓存与数据。它不执行下面更新器的备份/启动失败回滚事务；需要自动事务更新时仍使用原有构建与更新工具及标准核心包。

## Windows 便携软件

将 `DarwinCoreBuilder-Windows-x64.zip` 完整解压到可写目录，双击 `DarwinCoreBuilder.exe`。原生桌面界面提供构建、ZIP 校验、更新、启动、保存停服和环境检查；`DarwinCoreBuilderCLI.exe` 是命令行入口。随附隔离的 Python 运行时，无需安装 Python 或 .NET。不要只复制单个 EXE，`app` 和 `runtime` 都必须保留。

在普通 Windows 桌面账户下运行，先填入 Unity.exe 和工程目录并点击“环境检查”。仍需匹配的 Unity、有效许可证和对应构建模块。源码方式的 `gui` 使用 tkinter；发行版 EXE 使用 Windows 原生控件。当前受限执行环境无法完成原生图形界面的运行验证，命令行和核心运行时已检查；请在桌面环境完成界面验收。

若 Unity 弹出 `0x80000003`，先看环境检查和 `unity-launch.log`。本机定位到的原因是用户级 `Unity/Caches` 目录拒绝写入，导致 `CurlFileCache::Instance` 在游戏脚本编译前失败。构建器现在先申请目录写入/删除权限句柄并检查已有数据库的读写访问，失败会直接报错，不启动 Unity。只修改 `LOCALAPPDATA` 对这版 Unity 的缓存路径无效。此检查不删除缓存、不更改 ACL、不触碰许可证；环境访问限制需要在正常桌面账户下运行工具来避开受限构建上下文。本轮普通执行环境检查已通过并完成真实核心构建。构建子进程使用每次构建独立的临时目录，避免 Burst 继承不可访问的用户 TEMP；未修改系统级环境变量。

构建软件本身：`python Tools/DarwinCoreBuilder/bundle.py --output Artifacts/Software/<新目录>`，需要本机 Visual C++ x64 编译工具和 Windows SDK。`--test-gui` 仅在允许创建 Windows 窗口的交互式桌面环境使用。

自动把当前 Unity 工程构建为完整服务端核心包，并在 Windows 上安装、更新、回滚。需要 Python 3.12 或更新版本，图形界面需要 tkinter；不依赖第三方 Python 包。构建需要工程指定的 Unity `6000.3.14f1`、有效许可证和相应构建模块。

服务端从当前 Unity 项目构建。构建机需要 Unity 编辑器；部署机器无需安装 Unity 编辑器、Unity Hub 或工程源码，完整解压核心包即可独立开服。生态、物理和地形模拟使用核心包附带的 Unity Player 运行库。EXE、运行库、程序集和资源目录必须一起部署，不能只复制单个 EXE 或 DLL。

## 核心包控制台开服

完整解压核心包后双击 `Start.cmd`，或运行 `powershell -File ./Console.ps1 -DataDirectory D:/DarwinServerData`。无需安装 Unity 编辑器、Unity Hub、Python 或 .NET SDK；Windows PowerShell 使用系统自带版本。控制台输出实时日志，输入 `status` 查看状态，输入 `stop` 请求保存并等待退出。不要通过强制结束进程停服；关闭窗口的系统强制终止行为不保证完成保存。

默认数据目录是核心包旁的 `data`。首次启动从 `server.example.json` 复制生成 `data/server.json`，以后保留用户配置。可在首次运行前自行创建 `data/server.json`，或保存停服后修改，重新启动生效。`-DataDirectory` 可让数据独立于程序目录，升级时推荐固定使用外部数据目录。

| 配置项 | 用途 |
| --- | --- |
| `port` | TCP 监听端口，默认 47777 |
| `worldName` / `seed` | 首次新建世界的名称及种子 |
| `worldId` | 指定已有世界；留空时优先恢复 `server-world.json` 指向的世界 |
| `tickRate` | 目标运行帧率，范围 1～240，不保证实际达到 |
| `autoSaveSeconds` | 自动保存间隔（秒） |
| `startupTimeout` | 世界启动超时（秒） |
| `land` / `temperature` / `humidity` | 首次新建世界的地形和气候参数 |
| `cheats` | 是否允许联机成员使用编辑功能，默认 false |

已有世界恢复自身存档中的地形和气候；修改新建参数不会自动重建已有世界。`server.example.json` 是配置模板，实际生效文件是数据目录中的 `server.json`。退出码非零时控制台入口保留窗口，方便查看错误。日志保存于数据目录中的 `console-*.log`。

## 图形界面

运行 `Tools/DarwinCoreBuilder/Launch.ps1`，或：

```powershell
python Tools/DarwinCoreBuilder/main.py gui
```

填写 Unity.exe、工程、输出目录，选择构建目标后点击“构建核心包”。默认 `windows-headless` 不需要 Dedicated Server 模块；`windows-server` / `linux-server` 需要对应模块。构建不改动发行版本，先编辑 `Assets/Resources/Entities/server_release.json` 再发布。

选择 ZIP 和部署目录，点击“安装 / 更新并开服”。部署目录应位于工程外，例如 `D:/DarwinServer`。首次安装使用空数据目录，成功后配置位于 `data/server.json`。修改配置后保存停服、重新启动。旧的手工部署应先停服并备份，再离线迁入已初始化的 `data` 目录；保留原来的世界指针和完整存档。

## 命令行

在工程根目录执行：

```powershell
python Tools/DarwinCoreBuilder/main.py build --unity "D:/Program Files/Unity 6000.3.14f1/Editor/Unity.exe"
python Tools/DarwinCoreBuilder/main.py verify Artifacts/CorePackages/<核心包>.zip
python Tools/DarwinCoreBuilder/main.py update --root D:/DarwinServer --package Artifacts/CorePackages/<核心包>.zip
python Tools/DarwinCoreBuilder/main.py stop --root D:/DarwinServer
python Tools/DarwinCoreBuilder/main.py start --root D:/DarwinServer
```

已有完整构建目录可以跳过 Unity 构建：

```powershell
python Tools/DarwinCoreBuilder/main.py pack --source Builds/Server/Windows --output Artifacts/CorePackages/manual.zip --target windows-headless
```

`build` 每次使用独立临时构建目录，Unity 返回非零时不会拿旧文件打包。ZIP 附带 `manifest.json` 和整包 `.sha256` 文件，逐文件记录大小和 SHA256。校验会拒绝目录穿越、设备路径、符号链接、大小写重名、额外文件、内容损坏和版本清单不一致。SHA256 用于完整性校验，发布来源仍应由运维控制。

## 更新事务

1. 校验完整新包、目标平台和已有版本兼容性，解压至新的不可变发行目录。
2. 写入事务日志，向当前服务端请求保存停服。必须收到同一 `runId` 的 `stopped` 凭证，且 `server.lock` 已释放；保存失败或超时则中止。
3. 将整个离线 `data` 复制到外部 `backups`，包括配置、世界指针和存档，排除租约文件。
4. 启动候选版本，核对新 `runId`、PID、发行版本、数据锁和 READY 状态，并观察三秒。READY 只在世界加载、监听均成功后发布。
5. 成功后原子更新 `current.json`；失败则终止本次创建的候选进程、保留候选数据目录，恢复备份，并重新启动旧版本。

更新器与服务器独立，不执行运行中程序自替换。`releases` 保存历史二进制，`data` 保存在线数据，`backups` 保存更新前数据，`failed-data-*` 保存失败候选数据。暂不自动清理历史数据，运维可离线归档。事务期间 `data/update.guard` 阻止不携带本次更新令牌的手工开服；旧的、尚未实现该检查的二进制需要自行避免并发启动。人工恢复完成并归档事务时，同时移除残留的 `data/update.guard`。

相同内容纪元且新版存档范围覆盖旧版范围才允许自动更新。协议变化可以发布，但客户端必须满足新协议；破坏性内容变化或缩小存档范围会在停服前拒绝。尚未实现跨纪元迁移，不会伪装兼容或自动改写存档版本。

更新器突然断电或崩溃时，`transaction.json` 会阻止后续写操作。检查其 `phase`、`old/new`、`backup`、实际进程和数据锁；确认所有相关服务端停止后，将候选 `data` 移到另一个目录留证，再从记录的完整备份恢复 `data`，恢复 `current.json` 的旧 `directory`，最后归档事务文件并启动旧版。若事务停在 `stopping`，且没有完成备份，必须先确认原世界已安全保存，不能把不完整备份当成可恢复副本。当前不提供未经核实的自动断电恢复。

Windows 更新流程使用系统文件共享语义匹配 .NET 数据租约。Linux 核心包可以构建与校验，目前需要按服务端职责文档手工部署。

## 分层与验证

| 文件 | 职责 |
| --- | --- |
| `build.py` | 调用 Unity、隔离构建目录、生成核心包 |
| `package.py` | 清单、版本数据、哈希和安全解压 |
| `deploy.py` | 更新事务、进程、离线备份与回滚 |
| `main.py` | 命令行参数与调用 |
| `gui.py` | 桌面界面，子进程执行任务 |
| `DedicatedServerHealth.cs` | 服务端健康和保存完成凭证 |

运行 `python -m unittest discover -s Tools/DarwinCoreBuilder -p test_builder.py -v`。部署测试使用真实 Windows 子进程和独占文件租约，模拟成功更新、候选启动失败回滚、纪元拒绝与停服失败；它们不替代真实 Unity Player 开服测试。
