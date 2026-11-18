# ServerCore 模块流程

[返回总索引](../README.md) · [离线交互手册](../index.html)

## 脚本目录

- [Assets/DarwinPark/ServerCore/LanTransport.cs](#sfd148adcbfd4d09d)（15个方法/回调）
- [Assets/DarwinPark/ServerCore/ServerCompatibility.cs](#s9acebbb485f3dc57)（21个方法/回调）

---

<a id="sfd148adcbfd4d09d"></a>

## LanTransport.cs

`Runtime` · `Assets/DarwinPark/ServerCore/LanTransport.cs`

[源文件](../../../Assets/DarwinPark/ServerCore/LanTransport.cs) · 源码校验：`bd3c5713d9fd85e6390080dbc02ad4c9ff1f647af9def2f9bd18405f26487d68`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="mf3757b5881d55a12"></a>

### Id.get · L19

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m8e3890eb44d392e7"></a>

### Connected.get · L20

`public bool Connected`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Volatile.Read(ref _disposed)==0"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m5e1f70bc7fd056b4"></a>

### LanConnection.ctor · L21

`public LanConnection(TcpClient client,int maxBytes,int maxQueue,Action<LanConnection,string> received,Action<LanConnection> closed)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["public LanConnection(TcpClient client,int maxBytes,int maxQueue,Action&lt;LanConnection,string&gt; received,Action&lt;LanConnection&gt; closed) {_client=client;_client.NoDelay=true;_maxBytes=maxBytes;_maxQueue=maxQueue;_received=received;_closed=clo…"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="mf12837a14b366c30"></a>

### Start · L23

`public void Start()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_ = Task.Run(ReadLoop);<br/>_ = Task.Run(WriteLoop);"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [LanConnection.ReadLoop · L33](ServerCore.md#me89dc1948395f53b)（方法）
- [LanConnection.WriteLoop · L50](ServerCore.md#m452d6ca2ea06f150)（方法）

<a id="mf6b30ad306eab539"></a>

### Send · L24

`public void Send(string json)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Connected"}
  b2{"判断：Volatile.Read(ref _closing)!=0"}
  b3["Enqueue(json,false);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b4
  b2 -->|"否"| b3
  b3 --> b4

```

项目内调用/回调引用：

- [LanConnection.Enqueue · L31](ServerCore.md#mc7a684db22463eec)（方法）

<a id="m0e2d60a2a0c1b6ea"></a>

### SendAndClose · L29

`public void SendAndClose(string json)`

✦ 新增：拒绝原因写完再断开，避免客户端只看到不明确的网络中断。

#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Connected"}
  b2{"判断：Interlocked.Exchange(ref _closing,1)!=0"}
  b3["Enqueue(json,true);"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"是"| b4
  b2 -->|"否"| b3
  b3 --> b4

```

项目内调用/回调引用：

- [LanConnection.Enqueue · L31](ServerCore.md#mc7a684db22463eec)（方法）

<a id="mc7a684db22463eec"></a>

### Enqueue · L31

`private void Enqueue(string json,bool close)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_outgoing.Count&gt;=_maxQueue"}
  b2["Dispose();"]
  b3["_outgoing.Enqueue((json,close));<br/>_signal.Release();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b4
  b3 --> b4

```

项目内调用/回调引用：

- [LanConnection.Dispose · L68](ServerCore.md#m9c3fd25afe51cbe8)（方法）

<a id="me89dc1948395f53b"></a>

### ReadLoop · L33

`private async Task ReadLoop()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["stream=_client.GetStream()<br/>header=new byte[4]"]
  b2{"判断：Connected"}
  b3{"await Exact(stream,header);<br/>length=header[0]&#124;header[1]&lt;&lt;8&#124;header[2]&lt;&lt;16&#124;header[3]&lt;&lt;24<br/>判断：length&lt;=0"}
  b4{"判断：length&gt;_maxBytes"}
  b5["结果：new InvalidDataException(&quot;网络数据包长度无效&quot;)"]
  x5(["抛出异常 / 离开方法"])
  b6["bytes=new byte[length]<br/>await Exact(stream,bytes);<br/>_received(this,Encoding.UTF8.GetString(bytes));"]
  b7{"(Exception ex)<br/>判断：ex is IOException"}
  b8{"判断：ex is SocketException"}
  b9{"判断：ex is ObjectDisposedException"}
  b10{"判断：ex is InvalidOperationException"}
  b11["连接 / 离开区域"]
  b12["Dispose();"]
  b13(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否 · 经过finally 12"| b13
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 -->|"Throw"| x5
  b6 --> b2
  b7 -->|"否"| b8
  b7 -->|"是"| b11
  b8 -->|"否"| b9
  b8 -->|"是"| b11
  b9 -->|"否"| b10
  b9 -->|"是"| b11
  b10 -->|"是"| b11
  b11 -->|"经过finally 12"| b13
  b1 -. "退出保护区时 finally（示意）" .-> b12

```

项目内调用/回调引用：

- [LanConnection.Exact · L48](ServerCore.md#m119708237969637a)（方法）
- [LanConnection.Dispose · L68](ServerCore.md#m9c3fd25afe51cbe8)（方法）

<a id="m119708237969637a"></a>

### Exact · L48

`private static async Task Exact(NetworkStream stream,byte[] bytes)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["offset=0"]
  b2{"判断：offset&lt;bytes.Length"}
  b3{"count=await stream.ReadAsync(bytes,offset,bytes.Length-offset)<br/>判断：count==0"}
  b4["结果：new EndOfStreamException()"]
  x4(["抛出异常 / 离开方法"])
  b5["offset+=count;"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否"| b6
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 -->|"Throw"| x4
  b5 --> b2

```

<a id="m452d6ca2ea06f150"></a>

### WriteLoop · L50

`private async Task WriteLoop()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["stream=_client.GetStream()"]
  b2{"判断：Connected"}
  b3{"await _signal.WaitAsync();<br/>判断：Connected"}
  b4{"判断：_outgoing.TryDequeue(out var packet)"}
  b5{"bytes=Encoding.UTF8.GetBytes(packet.json)<br/>判断：bytes.Length&gt;_maxBytes"}
  b6["结果：new InvalidDataException(&quot;网络快照超出配置上限&quot;)"]
  x6(["抛出异常 / 离开方法"])
  b7{"length=bytes.Length<br/>header=new[]{(byte)length,(byte)(length&gt;&gt;8),(byte)(length&gt;&gt;16),(byte)(length&gt;&gt;24)}<br/>await stream.WriteAsync(header,0,4);<br/>await stream.WriteAsync(bytes,0,bytes.Length);<br/>判断：packet.close"}
  b8{"(Exception ex)<br/>判断：ex is IOException"}
  b9{"判断：ex is SocketException"}
  b10{"判断：ex is ObjectDisposedException"}
  b11{"判断：ex is InvalidOperationException"}
  b12["连接 / 离开区域"]
  b13["Dispose();"]
  b14(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 -->|"是"| b3
  b2 -->|"否 · 经过finally 13"| b14
  b3 -->|"否 · 经过finally 13"| b14
  b3 -->|"是"| b4
  b4 -->|"否"| b2
  b4 -->|"是"| b5
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 -->|"Throw"| x6
  b7 -->|"是 · 经过finally 13"| b14
  b7 -->|"否"| b2
  b8 -->|"否"| b9
  b8 -->|"是"| b12
  b9 -->|"否"| b10
  b9 -->|"是"| b12
  b10 -->|"否"| b11
  b10 -->|"是"| b12
  b11 -->|"是"| b12
  b12 -->|"经过finally 13"| b14
  b1 -. "退出保护区时 finally（示意）" .-> b13

```

项目内调用/回调引用：

- [LanConnection.Dispose · L68](ServerCore.md#m9c3fd25afe51cbe8)（方法）

<a id="m9c3fd25afe51cbe8"></a>

### Dispose · L68

`public void Dispose()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Interlocked.Exchange(ref _disposed,1)!=0"}
  b2["_client.Close();<br/>_signal.Release();<br/>_closed(this);"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b3
  b1 -->|"否"| b2
  b2 --> b3

```

<a id="mf09084b18b6f501d"></a>

### Port.get · L73

`public int Port`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["((IPEndPoint)_listener.LocalEndpoint).Port"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mdb9ae5e8d236eb67"></a>

### LanListener.ctor · L74

`public LanListener(int port,Action<TcpClient> accept)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["public LanListener(int port,Action&lt;TcpClient&gt; accept) { _listener=new TcpListener(IPAddress.Any,port);_listener.Start(); _=Task.Run(async()=&gt;{while(!_stopped)try{var client=await _listener.AcceptTcpClientAsync();if(_stopped)client.Close(…"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [LanListener.回调@77 · L77](ServerCore.md#m48970e09ef771235)（回调/委托）

<a id="m48970e09ef771235"></a>

### 回调@77 · L77

`async async … => 回调`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_stopped"}
  b2{"client=await _listener.AcceptTcpClientAsync()<br/>判断：_stopped"}
  b3["client.Close();"]
  b4["accept(client);"]
  b5{"(Exception ex)<br/>判断：ex is SocketException"}
  b6{"判断：ex is ObjectDisposedException"}
  b7["连接 / 离开区域"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b8
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b1
  b4 --> b1
  b5 -->|"否"| b6
  b5 -->|"是"| b7
  b6 -->|"是"| b7
  b7 --> b8

```

<a id="m7e2bf004b3f3a8f7"></a>

### Dispose · L79

`public void Dispose()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_stopped=true;<br/>_listener.Stop();"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```


---

<a id="s9acebbb485f3dc57"></a>

## ServerCompatibility.cs

`Runtime` · `Assets/DarwinPark/ServerCore/ServerCompatibility.cs`

[源文件](../../../Assets/DarwinPark/ServerCore/ServerCompatibility.cs) · 源码校验：`f6cecefe9e444a7a697acb025df0b59e680efc0899687ea79d349534eb1e8920`

方法按源文件位置排列。事件/回调引用不表示立即执行；完整源码与关系图请打开离线手册。

<a id="ma340a35e43a8f5b7"></a>

### Validate · L12

`public void Validate()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：protocol != WireVersion"}
  b2{"判断：contentEpoch &lt; 1"}
  b3{"判断：minimumSave &lt; 1"}
  b4{"判断：maximumSave &lt; minimumSave"}
  b5{"判断：string.IsNullOrWhiteSpace(release)"}
  b6["结果：new ArgumentException(&quot;Invalid server release manifest&quot;)"]
  x6(["抛出异常 / 离开方法"])
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b6
  b2 -->|"否"| b3
  b2 -->|"是"| b6
  b3 -->|"否"| b4
  b3 -->|"是"| b6
  b4 -->|"否"| b5
  b4 -->|"是"| b6
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 -->|"Throw"| x6

```

<a id="mf9d29c649406b0f5"></a>

### Reject · L18

`public string Reject(ServerCompatibility remote)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"Validate();<br/>判断：remote == null"}
  b2["结果：&quot;缺少版本握手，请更新客户端&quot;"]
  b3{"判断：remote.minimumSave&lt;1"}
  b4{"判断：remote.maximumSave&lt;remote.minimumSave"}
  b5["结果：&quot;客户端存档版本范围无效&quot;"]
  b6{"判断：remote.protocol != protocol"}
  b7["结果：&quot;通信协议不兼容，请更新客户端与服务端&quot;"]
  b8{"判断：remote.contentEpoch != contentEpoch"}
  b9["结果：&quot;游戏内容版本不兼容，请安装同一内容纪元的版本&quot;"]
  b10{"判断：remote.minimumSave &gt; maximumSave"}
  b11{"判断：remote.maximumSave &lt; minimumSave"}
  b12["结果：&quot;存档格式范围不兼容&quot;"]
  b13{"判断：string.IsNullOrWhiteSpace(remote.release)"}
  b14["结果：&quot;缺少客户端发行版本&quot;"]
  b15["结果：null"]
  b16(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b16
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 -->|"返回"| b16
  b6 -->|"是"| b7
  b6 -->|"否"| b8
  b7 -->|"返回"| b16
  b8 -->|"是"| b9
  b8 -->|"否"| b10
  b9 -->|"返回"| b16
  b10 -->|"否"| b11
  b10 -->|"是"| b12
  b11 -->|"是"| b12
  b11 -->|"否"| b13
  b12 -->|"返回"| b16
  b13 -->|"是"| b14
  b13 -->|"否"| b15
  b14 -->|"返回"| b16
  b15 -->|"返回"| b16

```

项目内调用/回调引用：

- [ServerCompatibility.Validate · L12](ServerCore.md#ma340a35e43a8f5b7)（方法）

<a id="md74a190ee8a489bc"></a>

### RequireSave · L30

`public void RequireSave(int version)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"Validate();<br/>判断：version &lt; minimumSave"}
  b2{"判断：version &gt; maximumSave"}
  b3["结果：new InvalidOperationException(&quot;Unsupported save format &quot; + version + &quot;; supported &quot; + minimumSave + &quot;..&quot; + maximumSave)"]
  x3(["抛出异常 / 离开方法"])
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 -->|"Throw"| x3

```

项目内调用/回调引用：

- [ServerCompatibility.Validate · L12](ServerCore.md#ma340a35e43a8f5b7)（方法）

<a id="m8e11cb32173288d5"></a>

### RequireWorld · L36

`public void RequireWorld(int saveVersion,int savedContentEpoch)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"RequireSave(saveVersion);<br/>判断：savedContentEpoch!=contentEpoch"}
  b2["结果：new InvalidOperationException(&quot;存档内容版本不兼容：&quot;+savedContentEpoch+&quot;；当前版本：&quot;+contentEpoch+&quot;。本版本不提供旧存档迁移，原文件保留。&quot;)"]
  x2(["抛出异常 / 离开方法"])
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"Throw"| x2

```

项目内调用/回调引用：

- [ServerCompatibility.RequireSave · L30](ServerCore.md#md74a190ee8a489bc)（方法）

<a id="m01d1a7c89e89072f"></a>

### Joined.get · L47

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mcc3a692021d1f37e"></a>

### Joined.set · L47

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="md945894d2516ff50"></a>

### Ready.get · L48

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="me58001927e7e77d5"></a>

### Ready.set · L48

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mcfe5fbfd32c5fb43"></a>

### Rejected.get · L49

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m1d8fae951f4e1822"></a>

### Rejected.set · L49

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="mb1408164390abf62"></a>

### Since.get · L50

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="me7dfb8dc81f5ccf0"></a>

### Since.set · L50

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m866299fb7c4ec453"></a>

### WorldRevision.get · L51

`get`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m5a4cfefae0ea59a8"></a>

### WorldRevision.set · L51

`private set`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["声明 / 自动存储；没有显式方法体"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

<a id="m084a9a108efbcf9f"></a>

### ServerAdmission.ctor · L52

`public ServerAdmission(double now)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["public ServerAdmission(double now) { Since = now; }<br/>Since = now;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [ServerAdmission.Since.set · L50](ServerCore.md#me7dfb8dc81f5ccf0)（访问器）

<a id="m03e456fd38a36135"></a>

### Join · L53

`public string Join(ServerCompatibility local, ServerCompatibility remote, double now)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Joined"}
  b2{"判断：Rejected"}
  b3["结果：&quot;重复入服握手&quot;"]
  b4{"reason = local.Reject(remote)<br/>判断：reason != null"}
  b5["Rejected = true;<br/>结果：reason"]
  b6["Joined = true;<br/>Since = now;<br/>结果：null"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b3
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 -->|"返回"| b7
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 -->|"返回"| b7
  b6 -->|"返回"| b7

```

项目内调用/回调引用：

- [ServerAdmission.Joined.get · L47](ServerCore.md#m01d1a7c89e89072f)（访问器）
- [ServerAdmission.Rejected.get · L49](ServerCore.md#mcfe5fbfd32c5fb43)（访问器）
- [ServerCompatibility.Reject · L18](ServerCore.md#mf9d29c649406b0f5)（方法）
- [ServerAdmission.Rejected.set · L49](ServerCore.md#m1d8fae951f4e1822)（访问器）
- [ServerAdmission.Joined.set · L47](ServerCore.md#mcc3a692021d1f37e)（访问器）
- [ServerAdmission.Since.set · L50](ServerCore.md#me7dfb8dc81f5ccf0)（访问器）

<a id="m65f266b34fa1ac4b"></a>

### AcknowledgeWorld · L60

`public bool AcknowledgeWorld(long revision)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Joined"}
  b2{"判断：Rejected"}
  b3{"判断：WorldRevision==0"}
  b4{"判断：revision!=WorldRevision"}
  b5["结果：false"]
  b6["Ready = true;<br/>结果：true"]
  b7(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b5
  b2 -->|"否"| b3
  b2 -->|"是"| b5
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 -->|"是"| b5
  b4 -->|"否"| b6
  b5 -->|"返回"| b7
  b6 -->|"返回"| b7

```

项目内调用/回调引用：

- [ServerAdmission.Joined.get · L47](ServerCore.md#m01d1a7c89e89072f)（访问器）
- [ServerAdmission.Rejected.get · L49](ServerCore.md#mcfe5fbfd32c5fb43)（访问器）
- [ServerAdmission.WorldRevision.get · L51](ServerCore.md#m866299fb7c4ec453)（访问器）
- [ServerAdmission.Ready.set · L48](ServerCore.md#me58001927e7e77d5)（访问器）

<a id="m109240af8cd4a29e"></a>

### Loading · L65

`public void Loading(double now)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Ready = false;<br/>Since = now;<br/>WorldRevision++;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [ServerAdmission.Ready.set · L48](ServerCore.md#me58001927e7e77d5)（访问器）
- [ServerAdmission.Since.set · L50](ServerCore.md#me7dfb8dc81f5ccf0)（访问器）
- [ServerAdmission.WorldRevision.set · L51](ServerCore.md#m5a4cfefae0ea59a8)（访问器）
- [ServerAdmission.WorldRevision.get · L51](ServerCore.md#m866299fb7c4ec453)（访问器）

<a id="mf9b00ea83f84329b"></a>

### Expired · L66

`public bool Expired(double now, double handshakeTimeout, double loadingTimeout)`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：Ready"}
  b2{"now - Since<br/>判断：Joined"}
  b3["loadingTimeout"]
  b4["handshakeTimeout"]
  b5["now - Since &gt; (Joined ? loadingTimeout : handshakeTimeout)"]
  b6["!Ready"]
  b7["结果：!Ready &amp;&amp; now - Since &gt; (Joined ? loadingTimeout : handshakeTimeout)"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b6
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 --> b7
  b6 --> b7
  b7 -->|"返回"| b8

```

项目内调用/回调引用：

- [ServerAdmission.Ready.get · L48](ServerCore.md#md945894d2516ff50)（访问器）
- [ServerAdmission.Since.get · L50](ServerCore.md#mb1408164390abf62)（访问器）
- [ServerAdmission.Joined.get · L47](ServerCore.md#m01d1a7c89e89072f)（访问器）

<a id="m15197a3de713a7c8"></a>

### CanCommand.get · L68

`public bool CanCommand`



#### Editor / Player · 语法控制结构 / 声明（非CFG）

```mermaid
flowchart TD
  n2(["入口"])
  n1["Joined &amp;&amp; Ready &amp;&amp; !Rejected"]
  n0(["结束 / 返回"])
  n1 --> n0
  n2 --> n1

```

项目内调用/回调引用：

- [ServerAdmission.Joined.get · L47](ServerCore.md#m01d1a7c89e89072f)（访问器）
- [ServerAdmission.Ready.get · L48](ServerCore.md#md945894d2516ff50)（访问器）
- [ServerAdmission.Rejected.get · L49](ServerCore.md#mcfe5fbfd32c5fb43)（访问器）

<a id="m2e2a407706678d36"></a>

### Block · L69

`public void Block()`



#### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["Rejected=true;<br/>Ready=false;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [ServerAdmission.Rejected.set · L49](ServerCore.md#m1d8fae951f4e1822)（访问器）
- [ServerAdmission.Ready.set · L48](ServerCore.md#me58001927e7e77d5)（访问器）
