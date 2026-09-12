# Pet Walker · 蓝蝶少女桌宠

<img src="icons/app-icon.png" width="128" alt="蓝蝶少女桌宠图标">

Windows 透明桌面散步宠物，集成定时休息提醒、漫画气泡和系统托盘控制。由原 ScheduledPopUp 定时提醒项目发展而来，旧版 C++ 实现保留在 Git 历史中。

## 功能

- 随机左右散步、停留休息、待机与挥手；向右采用固定角色图层制作轻柔小步走动画。
- 拖动定位、多显示器、三档速度和大小、悬停暂停、托盘隐藏与恢复。
- 默认每 55 分钟提醒休息，支持 1–1440 分钟间隔及自定义文案，避免连续重复。
- 提醒时暂停散步并挥手，显示小型漫画气泡；点击文字或空白即可关闭，不点击则 5 分钟后消失，再开始下一轮计时。
- 支持测试提醒、暂停提醒 1 小时；隐藏、拖动或打开菜单时暂缓弹出，睡眠恢复后不补发通知队列。
- 圆角角色图标、单实例运行，设置自动保存。

程序独立运行，不依赖 Codex、Python 或大模型服务，不读取 Codex 任务状态。运行过程无需联网。

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
| 右键角色或托盘 | 暂停、挥手、速度、大小、显示器、回到底部、隐藏或退出 |
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
- RightWalkRig.cs：向右行走分层动画。
- assets/：26 张基础动作图；rig/ 包含身体、双腿图层与关节配置。
- icons/：构建图标与项目头像。
- build.ps1、package.ps1：编译和便携打包。

仓库不包含编译产物、个人设置、备份、重复素材、原始生成图、动画实验及测试输出。旧 ScheduledPopUp 发布包属于原提醒程序，不是当前桌宠版本。
