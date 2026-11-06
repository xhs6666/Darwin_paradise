# ReadmeEditor.cs

[返回总索引](../README.md) · `Editor` · `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`

[源文件](../../../Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs) · 源码校验：`2ed5109bfd6774170df2551098d87eaf14d453b0173f8c9c37d83655eaac6ff9`

方法按源文件位置排列。事件/回调引用不表示立即执行；分支与异常说明见总索引。完整源码与可点击关系图请打开离线手册。

<a id="m2bc93f747c466e42"></a>

## ReadmeEditor.ctor · L19

`static ReadmeEditor()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["EditorApplication.delayCall += SelectReadmeAutomatically;"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

项目内调用/回调引用：

- [ReadmeEditor.SelectReadmeAutomatically · L54](aec8f8a911da4f2b.md#md02ddd0ec05de7d9)（方法）

<a id="m538e648d516b6192"></a>

## RemoveTutorial · L24

`static void RemoveTutorial()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：EditorUtility.DisplayDialog(&quot;Remove Readme Assets&quot;, $&quot;All contents under {s_ReadmeSourceDirectory} will be removed, are you sure you want to proceed?&quot;, &quot;Proceed&quot;, &quot;Cancel&quot;)"}
  b2{"判断：Directory.Exists(s_ReadmeSourceDirectory)"}
  b3["FileUtil.DeleteFileOrDirectory(s_ReadmeSourceDirectory);<br/>FileUtil.DeleteFileOrDirectory(s_ReadmeSourceDirectory + &quot;.meta&quot;);"]
  b4["Debug.Log($&quot;Could not find the Readme folder at {s_ReadmeSourceDirectory}&quot;);"]
  b5{"readmeAsset = SelectReadme()<br/>判断：readmeAsset != null"}
  b6["path = AssetDatabase.GetAssetPath(readmeAsset)<br/>FileUtil.DeleteFileOrDirectory(path + &quot;.meta&quot;);<br/>FileUtil.DeleteFileOrDirectory(path);"]
  b7["AssetDatabase.Refresh();"]
  b8(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b8
  b2 -->|"是"| b3
  b2 -->|"否"| b4
  b3 --> b5
  b4 --> b5
  b5 -->|"是"| b6
  b5 -->|"否"| b7
  b6 --> b7
  b7 --> b8

```

项目内调用/回调引用：

- [ReadmeEditor.SelectReadme · L77](aec8f8a911da4f2b.md#me663834154b0c9b6)（方法）

<a id="md02ddd0ec05de7d9"></a>

## SelectReadmeAutomatically · L54

`static void SelectReadmeAutomatically()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：SessionState.GetBool(s_ShowedReadmeSessionStateName, false)"}
  b2{"readme = SelectReadme()<br/>SessionState.SetBool(s_ShowedReadmeSessionStateName, true);<br/>判断：readme"}
  b3{"判断：readme.loadedLayout"}
  b4["LoadLayout();<br/>readme.loadedLayout = true;"]
  b5(["结束 / 返回"])
  b0 --> b1
  b1 -->|"否"| b2
  b1 -->|"是"| b5
  b2 -->|"是"| b3
  b2 -->|"否"| b5
  b3 -->|"否"| b4
  b3 -->|"是"| b5
  b4 --> b5

```

项目内调用/回调引用：

- [ReadmeEditor.SelectReadme · L77](aec8f8a911da4f2b.md#me663834154b0c9b6)（方法）
- [ReadmeEditor.LoadLayout · L69](aec8f8a911da4f2b.md#m2f9485cd03aefd0c)（方法）

<a id="m2f9485cd03aefd0c"></a>

## LoadLayout · L69

`static void LoadLayout()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["assembly = typeof(EditorApplication).Assembly<br/>windowLayoutType = assembly.GetType(&quot;UnityEditor.WindowLayout&quot;, true)<br/>method = windowLayoutType.GetMethod(&quot;LoadWindowLayout&quot;, BindingFlags.Public &#124; BindingFlags.Static)<br/>method.Invoke(null, ne…"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 --> b2

```

<a id="me663834154b0c9b6"></a>

## SelectReadme · L77

`static Readme SelectReadme()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"ids = AssetDatabase.FindAssets(&quot;Readme t:Readme&quot;)<br/>判断：ids.Length == 1"}
  b2["readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]))<br/>Selection.objects = new UnityEngine.Object[] { readmeObject };<br/>结果：(Readme)readmeObject"]
  b3["Debug.Log(&quot;Couldn't find a readme&quot;);<br/>结果：null"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 -->|"返回"| b4
  b3 -->|"返回"| b4

```

<a id="mb1c2e6e0380e2bbe"></a>

## OnHeaderGUI · L95

`protected override void OnHeaderGUI()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"readme = (Readme)target<br/>Init();<br/>iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f)<br/>GUILayout.BeginHorizontal(&quot;In BigTitle&quot;);<br/>判断：readme.icon != null"}
  b2["GUILayout.Space(k_Space);<br/>GUILayout.Label(readme.icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));"]
  b3["GUILayout.Space(k_Space);<br/>GUILayout.BeginVertical();<br/>GUILayout.FlexibleSpace();<br/>GUILayout.Label(readme.title, TitleStyle);<br/>GUILayout.EndVertical();<br/>GUILayout.EndHorizontal();"]
  b4(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b2
  b1 -->|"否"| b3
  b2 --> b3
  b3 --> b4

```

项目内调用/回调引用：

- [ReadmeEditor.Init · L199](aec8f8a911da4f2b.md#mcb728f6977a5031a)（方法）
- [ReadmeEditor.TitleStyle.get · L169](aec8f8a911da4f2b.md#m3b22f55b5414f50a)（访问器）

<a id="m3f2a2f1be878cd46"></a>

## OnInspectorGUI · L123

`public override void OnInspectorGUI()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["readme = (Readme)target<br/>Init();"]
  b2["readme.sections"]
  b3{"判断：readme.sections"}
  b4{"var<br/>判断：string.IsNullOrEmpty(section.heading)"}
  b5["GUILayout.Label(section.heading, HeadingStyle);"]
  b6{"判断：string.IsNullOrEmpty(section.text)"}
  b7["GUILayout.Label(section.text, BodyStyle);"]
  b8{"判断：string.IsNullOrEmpty(section.linkText)"}
  b9{"判断：LinkLabel(new GUIContent(section.linkText))"}
  b10["Application.OpenURL(section.url);"]
  b11["GUILayout.Space(k_Space);"]
  b12{"readme.sections<br/>判断：readme.sections"}
  b13["readme.sections"]
  b14["连接 / 离开区域"]
  b15{"判断：GUILayout.Button(&quot;Remove Readme Assets&quot;, ButtonStyle)"}
  b16["RemoveTutorial();"]
  b17(["结束 / 返回"])
  b0 --> b1
  b1 --> b2
  b2 --> b3
  b3 -->|"是"| b4
  b3 -->|"否 · 经过finally 12"| b15
  b4 -->|"否"| b5
  b4 -->|"是"| b6
  b5 --> b6
  b6 -->|"否"| b7
  b6 -->|"是"| b8
  b7 --> b8
  b8 -->|"否"| b9
  b8 -->|"是"| b11
  b9 -->|"是"| b10
  b9 -->|"否"| b11
  b10 --> b11
  b11 --> b3
  b12 -->|"否"| b13
  b12 -->|"是"| b14
  b13 --> b14
  b15 -->|"是"| b16
  b15 -->|"否"| b17
  b16 --> b17
  b3 -. "退出保护区时 finally（示意）" .-> b12

```

项目内调用/回调引用：

- [ReadmeEditor.Init · L199](aec8f8a911da4f2b.md#mcb728f6977a5031a)（方法）
- [ReadmeEditor.HeadingStyle.get · L177](aec8f8a911da4f2b.md#mc523aab48aec9cf4)（访问器）
- [ReadmeEditor.BodyStyle.get · L185](aec8f8a911da4f2b.md#mdbc0f0dcb48513c8)（访问器）
- [ReadmeEditor.LinkLabel · L228](aec8f8a911da4f2b.md#ma9e4b063637b43cd)（方法）
- [ReadmeEditor.ButtonStyle.get · L193](aec8f8a911da4f2b.md#m8846aee09b3e216f)（访问器）
- [ReadmeEditor.RemoveTutorial · L24](aec8f8a911da4f2b.md#m538e648d516b6192)（方法）

<a id="mc932f870b7aae1ea"></a>

## LinkStyle.get · L161

`get`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m_LinkStyle"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m3b22f55b5414f50a"></a>

## TitleStyle.get · L169

`get`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m_TitleStyle"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="mc523aab48aec9cf4"></a>

## HeadingStyle.get · L177

`get`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m_HeadingStyle"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="mdbc0f0dcb48513c8"></a>

## BodyStyle.get · L185

`get`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m_BodyStyle"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="m8846aee09b3e216f"></a>

## ButtonStyle.get · L193

`get`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["结果：m_ButtonStyle"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

<a id="mcb728f6977a5031a"></a>

## Init · L199

`void Init()`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1{"判断：m_Initialized"}
  b2["m_BodyStyle = new GUIStyle(EditorStyles.label);<br/>m_BodyStyle.wordWrap = true;<br/>m_BodyStyle.fontSize = 14;<br/>m_BodyStyle.richText = true;<br/>m_TitleStyle = new GUIStyle(m_BodyStyle);<br/>m_TitleStyle.fontSize = 26;<br/>m_HeadingStyle = new GUIStyle(m_Bo…"]
  b3(["结束 / 返回"])
  b0 --> b1
  b1 -->|"是"| b3
  b1 -->|"否"| b2
  b2 --> b3

```

<a id="ma9e4b063637b43cd"></a>

## LinkLabel · L228

`bool LinkLabel(GUIContent label, params GUILayoutOption[] options)`



### Editor · Roslyn编译器控制流

```mermaid
flowchart TD
  b0(["入口"])
  b1["position = GUILayoutUtility.GetRect(label, LinkStyle, options)<br/>Handles.BeginGUI();<br/>Handles.color = LinkStyle.normal.textColor;<br/>Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));<br/>Handle…"]
  b2(["结束 / 返回"])
  b0 --> b1
  b1 -->|"返回"| b2

```

项目内调用/回调引用：

- [ReadmeEditor.LinkStyle.get · L161](aec8f8a911da4f2b.md#mc932f870b7aae1ea)（访问器）
