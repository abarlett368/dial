# dial

**dial** 是一款适用于 Windows 的轮盘启动器。按下快捷键打开轮盘，再用鼠标
或键盘选择并执行操作。

[下载最新版本](https://github.com/abarlett368/dial/releases/latest) ·
[English](README.md) ·
[使用指南](docs/USER-GUIDE.zh-CN.md) ·
[架构说明](ARCHITECTURE.md) ·
[版本记录](CHANGELOG.md)

## 开始使用

1. 从 [Releases](https://github.com/abarlett368/dial/releases) 下载 `dial.exe`。
2. 将文件保存到固定文件夹后运行。便携版已包含 .NET 10，无需安装程序或
   单独安装 .NET。
3. 首次启动时，在“设置”中编辑示例操作并选择“保存”。
4. 按 **Ctrl+Tab** 打开轮盘。

系统要求：64 位 Windows 10（版本 1809 或更高版本）或 Windows 11。

## 使用轮盘

- 将鼠标移到扇区上并单击，即可执行对应操作。
- 使用方向键切换选中项，按 **Enter** 执行。
- 按 **Esc**、单击圆心或轮盘外部可关闭轮盘；鼠标移出轮盘会重置选择。
- 要更改快捷键或操作，请从通知区域打开“设置”。关闭设置窗口后，dial
  仍会继续运行；要退出，请使用通知区域菜单。

轮盘最多支持 16 项。每项可用于打开应用、文件、文件夹或网址，也可以运行
Windows 命令或脚本，或粘贴预先保存的文本。还可以设置主题以及图标和标签的
布局。部分可执行文件支持通过 Windows 用户帐户控制（UAC）请求管理员权限。

设置按用户保存在 `%LOCALAPPDATA%\dial\settings.json`。替换应用程序后，
现有设置仍会保留。亚克力效果取决于 Windows 透明效果设置和图形环境；
不可用时，轮盘会改用不透明扇区并保留透明间隙。

浅色主题使用正确的 Windows Blue #357EC7 作为强调色；深色主题使用更亮的蓝色 #4CC2FF。

## 构建

在 Windows 上安装 .NET 10 SDK，然后从仓库根目录运行：

```powershell
dotnet build .\Dial\Dial.csproj -c Release -warnaserror
dotnet publish .\Dial\Dial.csproj -p:PublishProfile=Portable -o .\publish\dial-win-x64
```

公开仓库仅包含应用源码、必要资源和用户文档。测试、图标生成工具和发布脚本
由维护者在本地保留；发布验证包括真实桌面检查和独立可执行文件验证。
生成文件和个人配置不属于仓库内容。

## 项目文档

- [使用指南](docs/USER-GUIDE.zh-CN.md)
- [架构说明](ARCHITECTURE.md)
- [版本记录](CHANGELOG.md)

## 版权

版权所有 2026 abarlett368。保留所有权利。未授予使用、修改或分发本软件的许可。
