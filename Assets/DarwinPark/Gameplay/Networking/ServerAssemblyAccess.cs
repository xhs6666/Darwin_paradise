using System.Runtime.CompilerServices;

// ✦ 新增：只向权威适配程序集开放会话内部接口，不扩大客户端公共修改面。
[assembly: InternalsVisibleTo("DarwinPark.Server.Unity")]
[assembly: InternalsVisibleTo("DarwinPark.UI")]
