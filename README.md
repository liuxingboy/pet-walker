# 定时提醒助手（ScheduledPopUp）

这是一个基于C++和Windows API 开发的轻量级桌面提醒工具。它常驻系统托盘，能够根据设定的时间间隔弹出通知弹窗，提醒用户喝水、休息或远眺，缓解办公学习疲劳。

## 🚀 快速下载
可以直接在 [Releases 页面](https://github.com/liuxingboy/ScheduledPopUp/releases/) 下载编译好的程序，解压即用。

## ✨ 功能特点

- **常驻托盘**：不占用任务栏空间，通过托盘图标进行管理。
- **自定义间隔**：通过配置文件自由设置提醒频率。
- **动态文案**：支持随机或顺序循环展示预设的多条温馨提醒。
- **计时统计**：鼠标放托盘图标显示距离上次提醒过去的时间。
- **配置窗口**：提供简单的界面用于查看或快速调整。

## 🛠️ 技术栈

- **语言**：C++
- **平台**： Windows SDK（win32 API）
- **配置格式**：INI 文件

## 🚀 快速开始

### 1. 环境准备
- Windows 10 或更高版本。
- 编译器：MSVC (Visual Studio 2019+ / Build Tools)。

### 2. 编译
使用命令行或 VS 打开项目进行编译：
```bash
rc resource.rc && cl.exe /utf-8 main.cpp resource.res user32.lib shell32.lib gdi32.lib /Fe:ScheduledPopUp.exe
```

### 3. 运行
确保 config.ini 与生成的 main.exe 处于同一目录下，直接运行即可。

## ⚙️ 配置说明 (config.ini)
程序启动时会读取同级目录下的 config.ini：

Ini, TOML
[Settings]
IntervalMinutes=35  ; 提醒间隔，单位为分钟
IntervalMinutes: 设置两次提醒之间的逻辑间隔。默认值为 30 分钟。

## 📂 文件结构
| 文件/目录 | 说明 |
| :--- | :--- |
| **`main.cpp`** | **核心源码**：包含程序主逻辑、托盘图标管理及定时器实现。 |
| **`config.ini`** | **配置文件**：用户可在此自定义提醒间隔（分钟）。 |
| `ReminderApp.vcxproj` | 项目主配置文件，定义了编译包含的源文件和库依赖。 |
| `main.exe` | 编译产物：可执行程序。 |

## 📝 预设提醒语示例
亲爱的，该喝口水休息一下啦~

眼睛累了吗？望望窗外远方吧。

站起来活动活动筋骨，身体是革命的本钱哦！


---
