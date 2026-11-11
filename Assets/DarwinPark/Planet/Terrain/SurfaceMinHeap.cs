using System.Collections.Generic;
namespace DarwinPark.Planet
{
    // ★ 新增：地表汇流及寻路共用确定性最小堆；清空复用容量，不逐节点分配对象。
    public sealed class SurfaceMinHeap
    {
        private struct Item {public int id;public float value;public Item(int i,float v){id=i;value=v;}}
        private readonly List<Item> _items=new();public int Count=>_items.Count;
        public void Clear()=>_items.Clear();
        private static bool Less(Item a,Item b)=>a.value<b.value||a.value==b.value&&a.id<b.id;
        public void Push(int id,float value){var item=new Item(id,value);int index=_items.Count;_items.Add(item);while(index>0){int parent=(index-1)/2;if(!Less(item,_items[parent]))break;_items[index]=_items[parent];index=parent;}_items[index]=item;}
        public int Pop(){var result=_items[0];var last=_items[_items.Count-1];_items.RemoveAt(_items.Count-1);if(_items.Count==0)return result.id;int index=0;while(index*2+1<_items.Count){int child=index*2+1;if(child+1<_items.Count&&Less(_items[child+1],_items[child]))child++;if(!Less(_items[child],last))break;_items[index]=_items[child];index=child;}_items[index]=last;return result.id;}
    }
}
