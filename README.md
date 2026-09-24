# Pet Walker · 神里绫华 GIF 桌宠

<img src="icons/app-icon.png" width="128" alt="蓝蝶少女桌宠图标">

Windows 透明桌面散步宠物，集成定时休息提醒、漫画气泡和系统托盘控制。由原 ScheduledPopUp 定时提醒项目发展而来，旧版 C++ 实现保留在 Git 历史中。

## 功能

- 随机左右散步、停留休息、待机与挥手；直接使用作者原始 GIF 的全部帧、透明背景和时长。
- 拖动定位、多显示器、三档速度和大小、悬停暂停、托盘隐藏与恢复。
- 默认每 55 分钟提醒休息，支持 1–1440 分钟间隔及自定义文案，避免连续重复。
- 提醒时暂停散步并挥手，显示小型漫画气泡；点击文字或空白即可关闭，不点击则 5 分钟后消失，再开始下一轮计时。
- 支持测试提醒、暂停提醒 1 小时；隐藏、拖动或打开菜单时暂缓弹出，睡眠恢复后不补发通知队列。
- 圆角角色图标、单实例运行，设置自动保存。

程序独立运行，不依赖 Codex、Python 或大模型服务，不读取 Codex 任务状态。基础桌宠无需联网；启用 Todo 后访问自行配置的后端。

## 编译与启动

需要 Windows x64、Windows PowerShell 和 .NET Framework 4.x。使用系统自带的 Framework64/v4.0.30319/csc.exe，无需安装 Visual Studio。

```powershell
git clone https://github.com/liuxingboy/pet-walker.git
cd pet-walker
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\HydrangeaWalker.exe
```

EXE 与 assets 文件夹须放在同一目录。程序在自身目录写入 settings.json，请放在当前用户可写的位置。图标已嵌入 EXE。不默认设置开机自启。

## 操作

| 操作 | 效果 |
| --- | --- |
| 左键拖动 | 调整位置及所在显示器 |
| 鼠标悬停 | 默认暂停散步，移开后继续 |
| 右键角色或托盘 | 暂停、测试九类动作、速度、大小、显示器、回到底部、隐藏或退出 |
| 双击托盘 | 重新显示角色 |
| 右键 → 休息提醒 | 设置间隔和文案、测试提醒、暂停提醒 |
| 点击提醒气泡 | 收起气泡并重新计时 |

暂停散步不会关闭提醒。退出后停止计时；再次启动从完整提醒间隔开始。

## 打包与验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
Start-Process .\HydrangeaWalker.exe -ArgumentList '--self-test' -Wait
Get-Content .\self-test-report.txt
```

打包脚本自动编译，生成 dist/HydrangeaWalker-portable.zip，只包含程序、运行素材、图标和说明，不包含个人设置。

可选界面检查：--smoke-test（约 8 秒）和 --reminder-smoke-test（约 7 秒）。运行前退出正常桌宠；测试会显示窗口并自动退出。--pause、--quit 可控制已运行实例。

## 项目结构

- PetWalker.cs：透明窗口、散步、托盘及设置。
- Reminder.cs：提醒计时、漫画气泡、设置和图标。
- assets/local-ayaka-2d/gifs/：正式使用的九个原始 GIF；同级 CREDITS.txt 记录来源。
- icons/：构建图标与项目头像。
- build.ps1、package.ps1：编译和便携打包。
- GifPlaybackTests.cs、test-gif.ps1：原始时长、九个子菜单、播放恢复验证。
- sources/：本地保留的 GitHub 原始资源与「花时来信」3D 工程，不提交 Git 或打包。

仓库不包含编译产物、个人设置、备份、重复素材、原始生成图、动画实验及测试输出。旧 ScheduledPopUp 发布包属于原提醒程序，不是当前桌宠版本。

## Todo 提醒与任务完成

- 托盘右键 → **Todo → 账号、服务器和显示数量…**：设置启用开关、后端 IP/域名、端口、HTTPS、账号密码和提醒数量。默认 5 条（可设置 1–50），保存后成为下次打开和重启时的默认值。密码留空保留原密码，更换账号需要重新输入密码。
- 启用后每次提醒读取「我的一天」今日未完成任务，随机抽取最多设定条数，将任务内容逐条换行作为原气泡的文本。保留原气泡外观、点击文字/空白消失、5 分钟自动消失、暂停散步和重新计时行为，气泡内没有勾选框或按钮。
- **Todo → 今日任务／勾选完成…**：独立窗口显示全部未完成任务，不受提醒条数限制。勾选后点击“确认完成所选”，后端成功后才标记完成；部分失败保留未成功项，可再次确认。提交前重新查询，跳过其他客户端已完成的项目，阻止修改已不在今日列表的任务。
- **测试任务提醒**：手动获取并显示任务气泡。自动提醒沿用“休息提醒”的间隔、总开关和暂停 1 小时设置。关闭 Todo 恢复原休息文案。
- 数据为空、任务全部完成或请求失败时，气泡显示相应提示；可通过托盘重试。获取任务在后台进行，隐藏、暂停提醒、切换设置和退出会取消待显示的结果。
- 密码使用 Windows 当前用户 DPAPI 加密写入 `settings.json`；Token 仅保存在内存。后端登录可能使同一账号其他客户端的旧 Refresh Token 失效。“今天”由后端数据库日期决定。
- 关闭任务管理窗口不会撤回已到达后端的提交，下次刷新以服务端状态为准。

新增 `Todo.cs` 包含接口、设置和独立任务管理窗口；原 `Reminder.cs` 保持不变。

本地测试：运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\test-todo.ps1`。仅使用本地模拟后端和虚构账号，验证接口、原气泡点击消失、独立任务列表及部分失败重试；预览输出到 `qa/`，不修改个人设置。


## GIF 动作与测试菜单

素材来自 [TiantianTitan/codex-anime-pets](https://github.com/TiantianTitan/codex-anime-pets/tree/main/work/kamisato-ayaka/2d/qa/previews)。程序只加载 `assets/local-ayaka-2d/gifs/`，不再依赖旧 PNG、3D 行走帧或分层动画资源。GIF 未改动，保留原始帧顺序与时长；移动速度只影响桌面位置。默认 192×208 尺寸直接绘制，其他用户指定大小统一整格缩放。

右键 **测试任务动作** 包含待机、向左跑步、向右跑步、挥手、跳跃、失败／失落、等待确认、工作／思考、审阅结果。选择后原地播放约 6 秒再恢复；可以切换动作或点击“停止测试”。不会调用 Todo 服务。悬停暂停保存触发时的命中轮廓，避免动画透明区域变化导致抖动。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test-gif.ps1
```

GIF 测试覆盖九个子菜单、57 帧、逐帧时长与循环、停止测试和超时恢复，报告写入 `qa/gif-playback/`。测试前退出正常桌宠。缺失 GIF 时启动报错，不回退到其他角色。

便携包包含正式 GIF 及来源说明，不含个人设置、原始压缩包、3D 工程或历史试验。角色及素材权利仍归原权利人，来源见 `assets/local-ayaka-2d/CREDITS.txt`。
