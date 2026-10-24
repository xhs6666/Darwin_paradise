using UnityEngine;

namespace DarwinPark.AI.Modules
{
    public class FlightAIModule : IGeneAIModule
    {
        public string ModuleId => "ai_flight";

        private EntityAIContext _context;

        // ★ 修复 1：参数必须是 EntityAIContext
        public void Initialize(EntityAIContext context)
        {
            _context = context;
            Debug.Log($"[AI] {_context.Entity.name} 开启了飞行模块");
        }

        public void Tick(float deltaTime)
        {
            if (_context == null || _context.Entity == null) return;

            // 从基因控制器获取当前飞行速度属性
            float moveSpeed = _context.Genes.GetAttribute("moveSpeed");
            if (moveSpeed > 0)
            {
                // 简单测试：让实体沿球体切线方向移动
                // 实际项目中可以加上飞行高度、耗能等逻辑
            }
        }

        // ★ 修复 2：补上 OnDisable 方法
        public void OnDisable()
        {
            if (_context?.Entity != null)
            {
                Debug.Log($"[AI] {_context.Entity.name} 关闭了飞行模块");
            }
        }
    }
}