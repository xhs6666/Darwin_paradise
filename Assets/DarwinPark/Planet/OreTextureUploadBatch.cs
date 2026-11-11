using UnityEngine;
namespace DarwinPark.Planet
{
    // ★ 新增：矿量在CPU立即结算，整张纹理只在本帧模拟结束后上传一次；不丢开采或改变收入。
    [DefaultExecutionOrder(900)]public sealed class OreTextureUploadBatch:MonoBehaviour
    {
        private Texture2D _texture;private Color[] _pixels;private bool _dirty;
        public long Uploads{get;private set;}public long Changes{get;private set;}
        public void Mark(Texture2D texture,Color[] pixels){_texture=texture;_pixels=pixels;_dirty=true;Changes++;}
        // ★ 修复（防Bug回归）：切世界或恢复矿图后不能把旧像素上传到已替换的纹理。
        public void Cancel(){_dirty=false;_texture=null;_pixels=null;}
        public void Flush(){if(!_dirty)return;_dirty=false;if(_texture==null||_pixels==null)return;_texture.SetPixels(_pixels);_texture.Apply(false,false);Uploads++;_texture=null;_pixels=null;}
        private void LateUpdate()=>Flush();
        private void OnDisable()=>Flush();
    }
}
