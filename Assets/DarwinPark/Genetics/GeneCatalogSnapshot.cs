using System.Collections.Generic;
using System.Linq;
namespace DarwinPark.Genetics
{
    // ★ 新增：UI按定义重载版本读取目录，不在每次统计刷新复制/拼接整个基因库。
    public static class GeneCatalogSnapshot
    {
        private static List<GeneDefinition> _catalog;private static string _signature;
        public static IReadOnlyList<GeneDefinition> Read(){if(_catalog==null){_catalog=GeneDatabase.All.ToList();_signature=string.Join("|",_catalog.Select(g=>g.Id+":"+g.DisplayName));}return _catalog;}
        public static string Signature {get{Read();return _signature;}}
        public static void Clear(){_catalog=null;_signature=null;}
    }
}
