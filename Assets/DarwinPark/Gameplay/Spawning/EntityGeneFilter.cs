using System;
using System.Collections.Generic;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;

namespace DarwinPark.UI
{
    // ★ 新增：包含条件全部满足、排除条件任一命中即排除；按携带判断，失效/抑制不等于没有基因。
    public sealed class EntityGeneFilter
    {
        private readonly HashSet<string> _included=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _excluded=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _seen=new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlyCollection<string> Included=>_included;
        public IReadOnlyCollection<string> Excluded=>_excluded;
        public void Set(string id,bool include,bool enabled)
        {
            if(string.IsNullOrEmpty(id))return;
            var selected=include?_included:_excluded;var other=include?_excluded:_included;
            if(enabled){selected.Add(id);other.Remove(id);}else selected.Remove(id);
        }
        public void Clear(){_included.Clear();_excluded.Clear();}
        public void Clear(bool include){if(include)_included.Clear();else _excluded.Clear();}
        public void KeepKnown(HashSet<string> known){_included.RemoveWhere(id=>!known.Contains(id));_excluded.RemoveWhere(id=>!known.Contains(id));}
        public bool Contains(string id,bool include)=>include?_included.Contains(id):_excluded.Contains(id);
        // 只在UI主线程调用，复用去重集合，避免每个实体分配HashSet或复制完整基因组。
        public bool Matches(DarwinEntity entity)
        {
            if(!EcologyQueries.Alive(entity)||entity.Genes?.Genome==null)return false;
            if(_included.Count==0&&_excluded.Count==0)return true;
            int required=0;_seen.Clear();
            foreach(var gene in entity.Genes.Genome.Genes)
            {
                if(gene==null||string.IsNullOrEmpty(gene.DefinitionId))continue;
                if(_excluded.Contains(gene.DefinitionId))return false;
                if(_included.Contains(gene.DefinitionId)&&_seen.Add(gene.DefinitionId))required++;
            }
            return required==_included.Count;
        }
    }
}
