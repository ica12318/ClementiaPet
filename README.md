# 克莱门莎桌宠 · 问候与对话版

双击 `dist/ClementiaPet.exe` 即可运行。工程面向 Windows 11 x64，使用系统自带的 .NET Framework 4.x，动画、文案与手记均内嵌在单个 EXE 中，可离线使用。角色与原始美术来自《明日方舟》；变身、巡游路线、台词和手记为本作同人内容。

## 本版体验

右键角色或托盘图标，选 **聊两句** 即可打开离线对话窗，从手稿、小说或小帮手聊起。用快捷回复或输入文字继续追问，Enter 发送，Esc 关闭。聊天期间角色暂停自动移动，关闭窗口或隐藏角色后结束会话。对话记录仅保存在窗口内存中。

她主动开口后，选 **接着聊** 可以找回刚才的原话。邀请保留 10 分钟，新邀请会替换旧邀请。气泡显示约 6 秒，可继续在原窗口打字。

| “问候与提醒”菜单 | 初始设置与行为 |
| --- | --- |
| 晚间问候 | 默认开启。本地时间 18:00–22:59 之间，角色空闲时每天问候一次。 |
| 随机闲谈 | 默认开启。每次等待 25–45 分钟，两种形态分别使用适合自己的文字。 |
| 喝水提醒 | 默认关闭。开启后等待一个完整间隔，默认 60 分钟，可选 30、60、90、120 分钟。 |

收到喝水邀请时选“接着聊”。“喝过了”从回复时重新计时，“十分钟后”只延后这一次，“关闭提醒”会保存关闭状态。忽略气泡则从本次展示起计算下一个间隔。

23:00 至次日 08:00 为安静时段。自动发言至少间隔 5 分钟。隐藏、锁屏或休眠后恢复时至少缓冲 60 秒。等待点击裁决、提起、爱心淡出、变形、显示文字、读手记、聊天和打开菜单时，角色会等到交互结束再开口。全身穿透时可通过托盘接话。

### 角色操作

- 首次启动显示一次“点手稿读信，双击变身，右键找我”，之后不再重复。
- 单击头、手／躯干、手稿或蝠鲼身体，播放对应反馈并显示约 3 秒气泡；同一热区轮换两句文案。
- 按住人形约 0.26 秒后从腰部提起，头部前垂，两臂和双腿自然下落，随拖速自然回摆。她会略微移开视线、抿嘴，脸颊泛红，右上方浮现爱心气泡。松手后气泡淡出。
- 双击任意角色部位，约 0.7 秒水纹淡入淡出后切换人形与小蝠鲼；两次快点不会先触发单击。
- 单击手稿打开三页《同人手记》，支持上一页、下一页、键盘方向键、Page Up／Page Down、Esc 和关闭按钮。
- 人形会随机停走、受重力落地；蝠鲼在前台应用客户区安全范围的上方按三次贝兹曲线缓慢巡游，端点悬停并左右交替。
- 右键角色或托盘图标可变形、读手记、切换全身鼠标穿透、隐藏、召回与退出。托盘双击始终取消穿透并召回角色。
- 保存形态、位置、DPI 比例、穿透偏好、上次篇目和首次提示状态。

## 一分钟试用

1. 打开记事本或浏览器。按住角色约 0.26 秒，她会从腰部被托起，头、手和鞋尖垂在腰点下方；左右拖动时，身体会滞后回摆；拖到半空后松手，她会下落并在工作区底部随机停走。
2. 轻点头部、手部或衣摆，观察分部位台词与基建互动动作；连续点手稿附近会打开手记。
3. 双击角色任意可见部位。水纹过后变成小蝠鲼，她会在当前窗口上方缓慢换点巡游。再双击还原。
4. 在蝠鲼形态点击身体，她会停住并轻摆；按住拖动可暂停航线，松手后重新规划。
5. 右键选择“全身鼠标穿透”，角色覆盖区域也不再拦截下方应用。通过托盘双击或菜单召回即可恢复。
6. 右键“读手记”，用左右方向键翻页；关闭后角色回到阅读前的当前形态与行为。

## 工程入口

从本目录执行：

```powershell
# 生成已交付单文件 EXE。先从托盘退出正在运行的版本。
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\ClementiaPet.exe

# 安装 .NET 10 SDK 的开发机也可走 SDK 入口
dotnet build .\src\ClementiaPet
dotnet run --project .\src\ClementiaPet
```

当前机器若只安装 .NET 10 运行时而无 SDK，仍可使用 `build.ps1` 的离线系统编译器路径。

| 文件 | 用途 |
| --- | --- |
| `src/ClementiaPet/Program.cs` | 托盘、菜单、单实例、手记窗口协调与退出 |
| `src/ClementiaPet/SpriteWindow.cs` | 点击／双击裁决、形态状态机、气泡、拖曳、穿透与渲染 |
| `src/ClementiaPet/BubbleWindow.cs` | 独立、穿透、不抢焦点的分层气泡窗口 |
| `src/ClementiaPet/JournalWindow.cs` | 三页可翻页手记卡 |
| `src/ClementiaPet/ContentStore.cs` | 内嵌台词语料与手记加载、轮换 |
| `src/ClementiaPet/SpeechScheduler.cs` | 时间策略、形态筛选、洗牌、暂停恢复和喝水到期状态 |
| `src/ClementiaPet/Conversation.cs`、`ChatWindow.cs` | 语料校验、当前节点与全局意图匹配、对话窗口 |
| `content/conversation.zh-CN.json` | 4 条晚间问候、4 条喝水邀请、7 条闲谈，对应形态、话题、写作依据、对话分支与界面文字 |
| `src/ClementiaPet/PetSettings.cs` | `%LOCALAPPDATA%/ClementiaPet/settings.json` 原子保存与损坏备份 |
| `src/ClementiaPet/PetMotion.cs` | 重力、走停、安全区与三次贝兹巡游 |
| `src/ClementiaPet/FlightTarget.cs` | 前台客户区与目标显示器工作区求交 |
| `content/zh-CN.json` | 首次提示、分部位台词与三篇手记正文 |
| `src/ClementiaPet/Assets/` | 人形、互动、悬提和蝠鲼透明帧图集 |
| `tools/export_sheet_gif.py` | 将透明图集导出为预览 GIF |
| `tools/dorm-pickup-pose.js` | 在克莱门莎原始骨骼上生成起手与悬空循环姿态 |
| `tools/DesktopSmoke.cs` | 跨进程桌面命中、焦点、拖曳、变形、巡游、手记与持久化回归 |
| `tools/MotionContractTests.cs` | 固定种子轨迹、安全区、窄窗、负坐标和空目标测试 |

## 验证

问候与对话测试使用隔离的临时设置目录。以下命令在工程根目录运行。第二条会短暂显示真实 WinForms 窗口。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\test-speech.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\test-speech.ps1 -Desktop
```

本次验收环境为 Windows 11 25H2 x64（26200.9711），使用系统 .NET Framework 4.x 编译器。原桌面回归针对打包 EXE，新对话窗口测试使用相同源码和资源并注入测试时钟。

| 验收项 | 本次结果 |
| --- | --- |
| 17:59→18:00、同日晚间重启、次日晚间、邀请过期 | 通过。未成功展示时保留问候资格，已展示的日期可恢复。 |
| 两种形态、类别洗牌、冷却、多个事件同时到期 | 通过。固定随机种子验证整轮及换轮边界，水提醒优先，5 分钟后可展示问候。 |
| 喝过了、延后、关闭和忽略 | 通过。测试覆盖 9:59 与 10:00 边界、重复操作、修改间隔、重启保存和错过多次到期。 |
| 隐藏、锁屏及恢复 | 通过测试时钟、窗口会话通知和电源消息检查暂停与恢复缓冲。挂起期间召回仍暂停。物理合盖、系统实际休眠唤醒尚未实机验证。 |
| 提起时提醒到期 | 通过。按住和松手后爱心淡出期间均无主动气泡，恢复后仅展示一次到期提醒。 |
| 主动开口与接话 | 通过。首次提示优先，自动气泡保留原窗口焦点，从托盘恢复邀请原文，按钮与 Enter 可连续对话。 |
| 全穿透、手动选题与窗口清理 | 通过。关闭全部主动选项后可手动开聊，未知问题回到选题，重复打开只出现一个窗口，Esc 和隐藏结束会话。 |
| 旧设置和文案审阅 | 通过。旧形态、篇目、负坐标及穿透偏好可恢复，显式关闭可以回读。新增台词均附形态、承接节点和人物依据。 |
| 原桌面功能 | 通过。覆盖透明命中、焦点、腰部悬吊与爱心淡出、连续十次变形、巡游重新规划、手记和重启。固定种子的 1800 个轨迹样本通过边界检查。 |

本机未安装 .NET 10 SDK，SDK 构建入口尚未实测。外层 JSON 语法损坏时独立的新增语料会停用，原有点击台词与手记仍从原资源加载。

轨迹契约测试：

```powershell
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$probe = Join-Path $env:TEMP 'ClementiaMotionTests.exe'
& $compiler /nologo /target:exe /platform:x64 /r:System.Drawing.dll "/out:$probe" `
  .\tools\MotionContractTests.cs .\src\ClementiaPet\PetMotion.cs
& $probe
```

完整桌面测试会显示一个蓝色底窗和独立桌宠进程，并使用隔离的临时设置目录：

```powershell
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$probe = Join-Path $env:TEMP 'ClementiaM1Smoke.exe'
& $compiler /nologo /target:exe /platform:x64 /win32manifest:src\ClementiaPet\app.manifest `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$probe" .\tools\DesktopSmoke.cs
& $probe (Resolve-Path .\dist\ClementiaPet.exe).Path
```

最近一次验证包含：透明角落穿透、非激活点击、腰部悬吊拖放、重力、30 秒级连续十次变身、手稿双击只变身、手记打开与翻页、关闭后恢复、窗口移动／切换后的巡游重规划，以及形态和篇目的重启保存。

## 素材层限制

当前包内没有人形与蝠鲼的专用变形逐帧素材，因此使用 0.7 秒交叉淡变加水纹完成可辨识过渡；真实桌宠仍沿用现有 Relax、Interact、Move、Lift 与蝠鲼 GIF 图集。未来若补交 `morph` 帧、正式手稿道具和逐帧部位索引图，可直接替换表现层，不需要改动点击裁决、内容或持久化契约。

设计文档中的“小帮手命名”属于首版验收后的可选扩展，本包未启用。
