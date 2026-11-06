# 运行流程图维护工具

这个离线工具只读取源码、Unity生成的项目引用和场景/预制体挂载，输出到 `Docs/运行流程图`。不启动游戏，不修改场景、玩家存档或运行数值。

## 打开手册

- 双击 `Docs/运行流程图/index.html`，在浏览器搜索脚本/方法、切换Editor/Player、查看控制流程、调用图、被调用者、接口候选、源代码行号与场景挂载。
- 在支持Mermaid的Markdown阅读器或GitHub查看 `Docs/运行流程图/README.md` → `主流程.md` / `脚本/*.md`。
- 图中可缩放、全屏、下载SVG和复制Mermaid。页面的数据、字体样式及脚本均为本地资源，没有CDN依赖。

## 一键更新

需要本机 **.NET 10 SDK**、**Python 3** 和当前Unity生成的根目录 `.csproj` 文件。Roslyn直接使用SDK自带程序集，NuGet下载源为空。如果换电脑，请先在Unity重新生成C#项目文件以更新引用路径。

```powershell
powershell -ExecutionPolicy Bypass -File Tools/FlowDocs/Generate.ps1
```

此操作依次编译工具、运行小型源码自测、重新分析所有Assets脚本、生成页面数据与Mermaid分图，然后校验覆盖、源码SHA256、图边、方法链接和条件编译版本。改动后出现校验失败时，应先修复生成器或引用，不能把缺图当作该方法未运行。

```powershell
# 只检查已有文档是否过期
python Tools/FlowDocs/validate.py
# 可选：真实浏览器交互和SVG检查（Node.js 22+，本机Microsoft Edge）
node Tools/FlowDocs/browser-check.mjs
```

可通过环境变量 `EDGE_PATH` 指定其他兼容的Edge/Chromium可执行文件。浏览器使用独立用户目录，结果和截图保存到 `Artifacts/FlowDocs`，不使用日常浏览器会话。

## 组织方式

- `Program.cs`：按Unity程序集分别建立Editor/Player语义模型，提取显式方法、访问器、构造、运算符、局部函数和匿名回调；建立源码调用、静态挂载和接口/继承候选。
- `Flow.cs`：Roslyn控制流基本块，包含短路判断、循环、返回、不可达块；无法独立建CFG的声明/少量表达式回调明确标注语法视图。异常处理入口使用示意虚线，并保留finally区域说明。
- `overview.json`：人工维护的12条跨模块业务导航；每个`target`须对应真实`类型.方法`，重新生成和校验时会检查。
- `FlowTests.cs`：独立样例验证分支、循环、异常/finally、yield/await、局部函数、回调和条件编译。
- `validate.py`：与源码逐文件比对并检查全部链接和图结构。
- `browser-check.mjs`：离线加载、搜索筛选、版本差异、关系导航、源码定位、全部SVG布局及窄屏显示检查。
- `Docs/运行流程图/atlas.js`、`atlas.css`、`index.html`：交互阅读器；生成器保留这些手写资源。
- 其余数据和脚本Markdown为生成结果，直接修改会在更新时覆盖。

扫描边界是Assets中的C#、Shader和HLSL。Packages/Library和Tools不属于游戏源码覆盖范围；零方法的数据类/枚举也会列入索引。自动属性的隐式存储、字段初始化原文可在源码视图查看。Unity消息跨组件顺序、虚方法/接口的实际实例、反射与字符串入口、线程调度和完整GPU宏展开都不能由静态文档证明。实现候选和动态边界已单独标记。

源码校验和与位置ID使用统一LF换行后的UTF-8文本，避免不同Git换行设置导致相同源码被误报为过期。生成数据不含生成时间，便于审查和复现。
