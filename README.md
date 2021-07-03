# TrajVis3D

TrajVis3D 是一个基于 Unity3D 引擎的大规模轨迹数据可视化沙盘。

![](doc/img/structure.png)

## 运行前准备

### 下载客户端用的 gRPC 插件

访问 [gRPC Packages](https://packages.grpc.io/) 下载 gRPC 的最新版本。在 Daily Builds of master Branch 节，选中最新的构建版本，单击 Build ID 进入下载界面。

![](doc/img/gRPC.png)

进入下载界面后，选择下载 C# 节下的 grpc_unity_package。

![](doc/img/gRPC-2.png)

下载到一个 zip 压缩包，内含一个 `Plugins` 文件夹。将这个文件夹内的所有内容复制到 [`client/Assets/Plugins`](client/Assets/Plugins) 文件夹中。

### 注册 Google Maps API

本项目依赖 Google Maps，运行客户端时必须确保客户端能够访问 Google 服务。在无法访问 Google 服务的国家或地区请确保已正确配置**系统代理**。[Windows 用户点此转至 [代理] 设置](ms-settings:network-proxy)。

请在 [Google Maps Platform](https://console.cloud.google.com/google/maps-apis/start) 注册一个新项目。这一项目的名称可以随意取，TrajVis3D 将不会调用这一项目内除API Key的其它资源。

注册好项目后，转至项目的凭据界面，并创建一个 API Key。本项目将仅使用 [Semantic Tile API](https://console.cloud.google.com/marketplace/product/google/vectortile.googleapis.com)。

![](doc/img/create-key.png)

**注意：无需下载和安装Maps SDK for Unity，本项目中已经包含了一个足够新的Maps SDK版本。**

注册完成后，Google Cloud Platform 控制台中应注册好一个项目，并在项目存在一个如下图所示的API Key（我对Key进行了访问限制，但这并不是必选项）。

![](doc/img/api.png)

这一API按调用次数收费，但每月的前10,000次调用（即加载10,000个区块的地图）是免费的。

### 编译 Protocol Buffer 和 gRPC

运行 [`protobuf/generate_protocol.bat`](protobuf/generate_protocol.bat) 来生成 Protocol Buffer 和 gRPC 的 Python 和 C# 脚本。

## 运行服务端

### 安装 Python 环境

服务端 python 环境为 python3，需要安装的软件包有

```bash
> python -m pip install protobuf grpcio pandas
```

### 运行指令

服务器运行指令为

```bash
> python server.py [file] [row_uid] [row_time] [row_lat] [row_lng] [coord_system]
```

其中，`[file]`为文件路径（只支持csv格式），`[row_uid] [row_time] [row_lat] [row_lng]`为uid、时间（**形如 "2021-01-01 12:00:03" 的字符串** 或 **UNIX 时间戳**）、纬度和精度所在的**列的编号**（从左侧开始，第一列为0，第二列为1，……），`[coord_system]`为坐标系统，TrajVis3D 支持 "GCJ02" 和 "WGS84" 两种坐标系统。

例如，对于如下数据：
```
./data/sample_data.csv

uid,time,lng_gcj02,lat_gcj02
7f48191598117f3976847409b162aad0,1477967139,104.08984,30.69281
7f48191598117f3976847409b162aad0,1477967142,104.08983,30.69279
7f48191598117f3976847409b162aad0,1477967145,104.08982,30.69278
7f48191598117f3976847409b162aad0,1477967148,104.08981,30.69276
7f48191598117f3976847409b162aad0,1477967151,104.08976,30.69269
```

则开启服务端的指令为

```bash
> python server.py ./data/sample_data.csv 0 1 3 2 GCJ02
```

等待服务端输出

```
ready to serve. timestamp between 0 and 0. total number of valid timestamps: 0.
```

即代表服务器已完成部署。如果数据量比较大，则部署时间会比较漫长，请耐心等待。

## 运行客户端

**在运行客户端实例前，请务必确认服务端已经完成加载！** 目前客户端尚不支持热重置，若服务端发生变更，则必须重新启动客户端。

### 安装 Unity

要编译客户端，系统必须装有：
 - Unity 版本 2020.3.13f1 或以上版本（Unity Hub：[从此处下载](https://unity3d.com/cn/get-unity/download)）
 - 带有“使用 Unity 的游戏开发”工作负荷的 Visual Studio 2019。

![](doc/img/visual-studio.png)

### 设置参数

进入项目后，请先设置 Google Maps API Key。

![](doc/img/unity1.png)

在 Hierarchy 中选中 Map Base，在 Inspector 中找到 Maps Service 组件。其中有一个酒红色的输入框，请将之前获取的 API Key 输入到这一红框内。

![](doc/img/unity2.png)

在 Hierarchy 中选中 Dispatcher，在 Inspector 中找到 Dispatcher 组件。这一组件的可设置项有：

 - `Transmit Batch` 控制客户端每一次向服务端获取多少秒的数据。例如，图中的300意味着客户端每一次将向服务端询问5分钟的数据。
 - `Preload Buffer` 控制客户端最多预加载多少秒的数据。例如，如果这里设置为5000，则获取数据的速度超过当前动画播放时间的5000秒后，将停止加载。类似于看 YouTube 视频时的预加载量。

### 运行客户端

点击播放按钮即可开始运行客户端。

### 打包客户端

按下 `Ctrl` + `B` 组合键，选择输出路径，即可将客户端打包为可执行文件分发。也可通过 `File -> Build Settings...` 自定义打包参数。
