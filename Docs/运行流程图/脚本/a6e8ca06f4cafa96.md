# SimulationTimeHelp.cs

[返回总索引](../README.md) · `Runtime` · `Assets/DarwinPark/UI/SimulationTimeHelp.cs`

[源文件](../../../Assets/DarwinPark/UI/SimulationTimeHelp.cs) · 源码校验：`b6e542177214c85ecc259d89abcedc2cf53b701a4865ff29bfe736807b767fa2`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m7a09fdf6dec870ff"></a>

## ToSeconds · L10

`public static float ToSeconds(float days)`

★ 新增：界面按天校验并换算，报错也使用玩家当前看到的单位。

### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"day=DarwinPark.Entities.LifeCycleSettings.Load().secondsPerDay<br/>seconds=days*day<br/>判断：float.IsFinite(seconds)"}
  b2{"判断：seconds&lt;1"}
  b3{"判断：seconds&gt;86400"}
  b4["结果：new System.FormatException($&quot;周期应为{1/day:0.####}～{86400/day:0.####}游戏天（1游戏天={day:0.##}模拟秒）&quot;)"]
  x4(["抛出异常 / 离开方法"])
  b5["结果：seconds"]
  b6(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b4
  b2 -->|"否"| b3
  b2 -->|"是"| b4
  b3 -->|"是"| b4
  b3 -->|"否"| b5
  b4 -->|"Throw"| x4
  b5 -->|"返回"| b6

```

项目内调用/回调引用：

- [LifeCycleSettings.Load · L16](ace76969fe8f61de.md#mbd2925785484f343)（方法）

<a id="m30230b4a61c84d87"></a>

## OnPointerEnter · L17

`public void OnPointerEnter(PointerEventData data)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_hover=true;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m4f7238fb8b5cf66f"></a>

## OnPointerExit · L17

`public void OnPointerExit(PointerEventData data)`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_hover=false;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m90a3c01cbf71345d"></a>

## OnDisable · L18

`private void OnDisable()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["_hover=false;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="m3b92a878621d00b9"></a>

## OnGUI · L19

`private void OnGUI()`



### Editor / Player · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：_hover"}
  b2["skin=GUI.skin<br/>depth=GUI.depth"]
  b3["GUI.depth=-1100;<br/>RuntimeMenuTheme.Apply();"]
  b4["_style"]
  b5{"_style<br/>判断：_style"}
  b6["new GUIStyle(GUI.skin.box){wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,12,9,9)}<br/>wordWrap=true<br/>alignment=TextAnchor.MiddleLeft<br/>padding=new RectOffset(12,12,9,9)<br/>_style??=new GUIStyle(GUI.skin.box){wordWrap=true…"]
  b7["day=DarwinPark.Entities.LifeCycleSettings.Load().secondsPerDay<br/>text=$&quot;1游戏天 = {day:0.##}模拟秒\n1倍速正常运行约{day:0.##}现实秒；2倍速约{day/2:0.##}秒\n暂停不计时；负载过高时可能更慢。&quot;<br/>position=Input.mousePosition<br/>width=Mathf.Min(330,Screen.width-20)<br/>height=_style.CalcHe…"]
  b8["GUI.skin=skin;<br/>GUI.depth=depth;"]
  b9(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b9
  b1 -->|"是"| b2
  b2 --> b3
  b3 --> b4
  b4 --> b5
  b5 -->|"否"| b7
  b5 -->|"是"| b6
  b6 --> b7
  b7 -->|"经过finally 8"| b9
  b3 -. "退出保护区时 finally（示意）" .-> b8

```

项目内调用/回调引用：

- [RuntimeMenuTheme.Apply · L39](b9d8455882f2a532.md#m587a63eff5a85605)（方法）
- [LifeCycleSettings.Load · L16](ace76969fe8f61de.md#mbd2925785484f343)（方法）
