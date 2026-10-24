using UnityEngine;

namespace DarwinPark.AI.Modules
{
    public class AutotrophAIModule : IGeneAIModule
    {
        public string ModuleId => "ai_autotroph";

        private EntityAIContext _context;

        public void Initialize(EntityAIContext context)
        {
            _context = context;
            Debug.Log($"[AI] {_context.Entity.name} 开启了自养模块");
        }

        public void Tick(float deltaTime)
        {
            if (_context == null || _context.Entity == null) return;
            // 可以在这里实现光合作用增加能量的逻辑
        }

        public void OnDisable()
        {
            if (_context?.Entity != null)
            {
                Debug.Log($"[AI] {_context.Entity.name} 关闭了自养模块");
            }
        }
    }
}