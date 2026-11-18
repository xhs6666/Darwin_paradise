using DarwinPark.Core;
using DarwinPark.Planet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarwinPark.UI
{
    // ★ 新增：缓存真实地表/水系图，再投影成球形导航图；不增加每帧渲染整颗星球的第二台相机。
    public sealed class PlanetGlobeMinimap:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,ICanvasRaycastFilter
    {
        public float cardWidth=180,cardHeight=164,rightPadding=12,diameter=132,refreshSeconds=.1f;
        public int atlasWidth=512,atlasHeight=256,globeResolution=144;
        public float highlandMeters=22,deepWaterMeters=20;
        public Color landColor=new(.32f,.55f,.24f),mountainColor=new(.8f,.76f,.62f),oceanColor=new(.04f,.18f,.32f),shallowColor=new(.12f,.42f,.57f),freshWaterColor=new(.1f,.58f,.76f);
        public float ReservedWidth=>cardWidth+rightPadding*2;
        public bool Dragging=>_dragging;
        private RawImage _image;private Texture2D _texture;private Color32[] _atlas;private Material _projection;private Quaternion _drawnRotation;private bool _hasProjection;
        public long AtlasUploads {get;private set;}public long ProjectionUpdates {get;private set;}
        private PlanetTerrainMeshBuilder _planet;private PlanetTerrainData _data;private OrbitCamera _orbit;
        private bool _pressed,_dragging;private Vector3 _startPoint;private Quaternion _startRotation;private float _next;
        private void Start()
        {
            var rect=(RectTransform)transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one;rect.anchoredPosition=new Vector2(-rightPadding,0);rect.sizeDelta=new Vector2(cardWidth,cardHeight);
            UIVisualTheme.Paint(GetComponent<Image>());GetComponent<Image>().raycastTarget=false;
            var title=UIFactory.NewText("Title",transform,"星球导航",13,TextAnchor.UpperCenter);UIFactory.Stretch(title.rectTransform,4,2);title.raycastTarget=false;
            var image=UIFactory.NewRect("Globe",transform,typeof(RawImage));_image=image.GetComponent<RawImage>();var ir=(RectTransform)image.transform;ir.sizeDelta=new Vector2(diameter,diameter);ir.anchoredPosition=new Vector2(0,-3);
            var hint=UIFactory.NewText("Hint",transform,"拖动旋转视角",11,TextAnchor.LowerCenter);UIFactory.Stretch(hint.rectTransform,4,2);hint.color=UIVisualTheme.Muted;hint.raycastTarget=false;
            var marker=UIFactory.NewText("ViewCenter",image.transform,"+",14,TextAnchor.MiddleCenter);UIFactory.Stretch(marker.rectTransform);marker.color=UIVisualTheme.Accent;marker.raycastTarget=false;
            globeResolution=Mathf.Clamp(globeResolution,64,256);atlasWidth=Mathf.Clamp(atlasWidth,128,1024);atlasHeight=Mathf.Clamp(atlasHeight,64,512);
            // ★ 修复（防Bug回归）：导航只上传地表图一次，旋转通过小矩阵传给UI着色器，不再周期生成和上传圆盘像素。
            var material=Resources.Load<Material>("UI/globe_minimap");if(material==null){Debug.LogError("缺少UI/globe_minimap导航材质");enabled=false;return;}
            _projection=new Material(material);_image.material=_projection;
            _texture=new Texture2D(atlasWidth,atlasHeight,TextureFormat.RGBA32,false){name="PlanetGlobeMinimapAtlas",filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};_image.texture=_texture;
        }
        private bool Resolve()
        {
            if(_planet==null)_planet=DarwinPark.Entities.Ecology.EcologyQueries.Planet;
            if(_orbit==null&&Camera.main!=null)_orbit=Camera.main.GetComponent<OrbitCamera>();
            return _planet!=null&&_planet.Data!=null&&_orbit!=null;
        }
        public void RefreshNow()
        {
            if(_image==null||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||!Resolve())return;
            if(_data!=_planet.Data)BuildAtlas();
            var rotation=Quaternion.Inverse(_planet.transform.rotation)*_orbit.transform.rotation;
            if(_hasProjection&&Quaternion.Angle(rotation,_drawnRotation)<.01f)return;
            _drawnRotation=rotation;_hasProjection=true;_projection.SetMatrix("_GlobeRotation",Matrix4x4.Rotate(rotation));ProjectionUpdates++;
        }
        private void BuildAtlas()
        {
            _data=_planet.Data;_atlas=new Color32[atlasWidth*atlasHeight];
            for(int y=0;y<atlasHeight;y++)for(int x=0;x<atlasWidth;x++)
            {
                var direction=PlanetTerrainSampler.DirectionFromUV((x+.5f)/atlasWidth,(y+.5f)/atlasHeight);float height=PlanetTerrainSampler.SampleHeight(_data,direction),altitude=(height-_data.SeaLevel)*_data.HeightScale,water=_data.Hydrology?.Water(direction)??_data.SeaLevel;
                _atlas[y*atlasWidth+x]=altitude<0?Color.Lerp(shallowColor,oceanColor,Mathf.Clamp01(-altitude/Mathf.Max(.01f,deepWaterMeters))):water>height?freshWaterColor:Color.Lerp(landColor,mountainColor,Mathf.Clamp01(altitude/Mathf.Max(.01f,highlandMeters)));
            }
            _texture.SetPixels32(_atlas);_texture.Apply(false,false);AtlasUploads++;_hasProjection=false;
        }
        private void Update()
        {
            if(GameInputGate.Blocked||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading){Release();return;}
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+Mathf.Max(.03f,refreshSeconds);RefreshNow();
        }
        private bool Project(Vector2 screen,Camera eventCamera,bool clamp,out Vector3 point)
        {
            point=default;if(_image==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(_image.rectTransform,screen,eventCamera,out var local))return false;
            var rect=_image.rectTransform.rect;var p=new Vector2((local.x-rect.center.x)/(rect.width*.5f),(local.y-rect.center.y)/(rect.height*.5f));
            if(p.sqrMagnitude>1){if(!clamp)return false;p.Normalize();}point=new Vector3(p.x,p.y,-Mathf.Sqrt(Mathf.Max(0,1-p.sqrMagnitude)));return true;
        }
        public bool IsRaycastLocationValid(Vector2 screen,Camera eventCamera)=>Project(screen,eventCamera,false,out _);
        public void OnPointerDown(PointerEventData evt)
        {
            if(evt.button!=PointerEventData.InputButton.Left||GameInputGate.Blocked||!WorldRuntimeGate.Ready||WorldRuntimeGate.Loading||!Resolve()||!Project(evt.position,evt.pressEventCamera,false,out _startPoint))return;
            EntityViewController.Instance?.ExitView();PlacementQuantityPopup.CloseAll();EntityPlacementTool.Instance?.CancelPlacement();
            _startRotation=Quaternion.Inverse(_planet.transform.rotation)*_orbit.transform.rotation;_pressed=true;
        }
        public void OnBeginDrag(PointerEventData evt){if(!_pressed||evt.button!=PointerEventData.InputButton.Left)return;_dragging=true;OrbitCamera.IsDraggingUI=true;}
        public void OnDrag(PointerEventData evt)
        {
            if(!_dragging||evt.button!=PointerEventData.InputButton.Left||GameInputGate.Blocked||!Resolve()||!Project(evt.position,evt.pressEventCamera,true,out var point))return;
            // ★ 修复（防Bug回归）：球面轨迹和镜头都相对星球变换，旋转/移动星球后仍能正确导航。
            var rotation=_startRotation*Quaternion.FromToRotation(point,_startPoint);
            // ★ 修复（防Bug回归）：完整传递球面旋转，不经俯仰限位/世界向上重建，极点上仍能继续拖动。
            _orbit.SetOrbitRotation(_planet.transform.rotation*rotation);RefreshNow();
        }
        private void Release(){_pressed=false;if(_dragging)OrbitCamera.IsDraggingUI=false;_dragging=false;}
        public void OnPointerUp(PointerEventData evt){if(evt.button==PointerEventData.InputButton.Left)Release();}
        public void OnEndDrag(PointerEventData evt){if(evt.button==PointerEventData.InputButton.Left)Release();}
        private void OnDisable()=>Release();
        private void OnApplicationFocus(bool focused){if(!focused)Release();}
        private void OnDestroy(){Release();if(_texture!=null)Destroy(_texture);if(_projection!=null)Destroy(_projection);}
    }
}
