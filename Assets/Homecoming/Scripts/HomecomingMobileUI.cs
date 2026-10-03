using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Homecoming
{
    public sealed class HomecomingMobileUI : MonoBehaviour
    {
        [SerializeField] HomecomingScene story;
        [SerializeField] RectTransform safeRoot;
        Rect previousSafeArea;
        Vector2Int previousScreen;
        [SerializeField] Font font;
        [SerializeField] Sprite buttonSprite;
        [SerializeField] Text playLabel, hint;
        [SerializeField] Image[] views=new Image[3];
        [SerializeField] Image[] characters=new Image[4];
        float hintUntil;
        static readonly Color Idle=new Color(.07f,.10f,.13f,.76f), Selected=new Color(.24f,.38f,.36f,.94f);
        void OnEnable()
        {
            if(!story || !safeRoot) return;
            var buttons=GetComponentsInChildren<Button>();
            foreach(var button in buttons) button.onClick.RemoveAllListeners();
            foreach(var button in buttons)
            {
                if(button.name=="开始")button.onClick.AddListener(story.TogglePlayback);
                else if(button.name=="重播")button.onClick.AddListener(story.Replay);
                else
                {
                    int viewIndex=System.Array.IndexOf(new[]{"全景","人物","换鞋"},button.name);
                    if(viewIndex>=0)button.onClick.AddListener(()=>story.CameraControls.SetView(viewIndex));
                    int character=System.Array.IndexOf(HomecomingCharacters.Names,button.name);
                    if(character>=0)button.onClick.AddListener(()=>story.SelectCharacter(character));
                }
            }
        }
        public static HomecomingMobileUI Create(HomecomingScene scene)
        {
            var root=new GameObject("移动端操作",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(scene.transform,false);
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=10;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            var ui=root.AddComponent<HomecomingMobileUI>(); ui.story=scene; ui.Build(); return ui;
        }
        RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor; rect.pivot=pivot; rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        Text Label(RectTransform parent,string text,int size)
        {
            var rect=Rect("文字",parent,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero); rect.anchorMax=Vector2.one;
            var label=rect.gameObject.AddComponent<Text>(); label.font=font; label.text=text; label.fontSize=size;
            label.color=new Color(.96f,.96f,.93f); label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false;
            return label;
        }
        Image Button(string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size,UnityAction action,out Text label)
        {
            var rect=Rect(name,safeRoot,anchor,pivot,position,size);
            var image=rect.gameObject.AddComponent<Image>(); image.color=Idle; image.sprite=buttonSprite; image.type=Image.Type.Sliced;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.onClick.AddListener(action);
            var colors=button.colors; colors.highlightedColor=new Color(.9f,1,1); colors.pressedColor=new Color(.65f,.8f,.75f); button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None}; label=Label(rect,name,30); return image;
        }
        void Build()
        {
            font=Resources.Load<Font>("Homecoming/Fonts/NotoSansCJKsc-Regular");
            if(!font) font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="圆角触控按钮"};
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                var corner=new Vector2(Mathf.Max(16-x,x-47),Mathf.Max(16-y,y-47)); corner=Vector2.Max(corner,Vector2.zero);
                pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(16.5f-corner.magnitude));
            }
            texture.SetPixels(pixels); texture.Apply();
            buttonSprite=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
            safeRoot=Rect("安全区域",transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
            safeRoot.anchorMax=Vector2.one;
            Button("开始",Vector2.zero,Vector2.zero,new Vector2(30,30),new Vector2(150,128),story.TogglePlayback,out playLabel);
            Button("重播",Vector2.zero,Vector2.zero,new Vector2(194,30),new Vector2(150,128),story.Replay,out _);
            string[] names={"全景","人物","换鞋"};
            for(int i=0;i<3;i++)
            {
                int index=i;
                views[i]=Button(names[i],Vector2.right,Vector2.right,new Vector2(-30-(2-i)*138,30),new Vector2(124,128),()=>story.CameraControls.SetView(index),out _);
            }
            for(int i=0;i<HomecomingCharacters.Names.Length;i++)
            {
                int index=i;
                characters[i]=Button(HomecomingCharacters.Names[i],Vector2.up,Vector2.up,new Vector2(30+i*142,-26),new Vector2(130,92),()=>story.SelectCharacter(index),out _);
            }
            var hintRect=Rect("触控提示",safeRoot,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-24),new Vector2(720,48));
            hint=Label(hintRect,Application.isMobilePlatform?"单指滑动转动视角 · 双指缩放":"拖动转动视角 · 滚轮缩放",24);
            var shadow=hint.gameObject.AddComponent<Shadow>(); shadow.effectColor=new Color(0,0,0,.8f); shadow.effectDistance=new Vector2(1,-2);
            hintUntil=Time.unscaledTime+7;
            if(!EventSystem.current)
            {
                var events=new GameObject("触控事件",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
            }
            UpdateSafeArea(Screen.safeArea,new Vector2Int(Screen.width,Screen.height));
        }
        void OnDestroy()
        {
            if(!buttonSprite) return;
            var texture=buttonSprite.texture;
            if(Application.isPlaying) { Destroy(buttonSprite); Destroy(texture); }
            else { DestroyImmediate(buttonSprite); DestroyImmediate(texture); }
        }
        public void UpdateSafeArea(Rect area,Vector2Int screen)
        {
            if(screen.x<=0 || screen.y<=0) return;
            previousSafeArea=area; previousScreen=screen;
            safeRoot.anchorMin=new Vector2(area.xMin/screen.x,area.yMin/screen.y);
            safeRoot.anchorMax=new Vector2(area.xMax/screen.x,area.yMax/screen.y);
            safeRoot.offsetMin=safeRoot.offsetMax=Vector2.zero;
        }
        void Update()
        {
            if(!story || !story.CameraControls || !safeRoot || !playLabel || !hint) return;
            var screen=new Vector2Int(Screen.width,Screen.height);
            if(Screen.safeArea!=previousSafeArea || screen!=previousScreen) UpdateSafeArea(Screen.safeArea,screen);
            playLabel.text=story.Finished?"再播放":!story.Started?"开始":story.Paused?"继续":"暂停";
            for(int i=0;i<3;i++) if(views[i]) views[i].color=story.CameraControls.ViewIndex==i?Selected:Idle;
            for(int i=0;i<characters.Length;i++) if(characters[i])characters[i].color=story.CharacterIndex==i?Selected:Idle;
            hint.gameObject.SetActive(Time.unscaledTime<hintUntil);
        }
    }
}
