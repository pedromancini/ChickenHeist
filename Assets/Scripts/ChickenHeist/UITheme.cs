using System.Collections.Generic;
using UnityEngine;

// Chicken Heist interface theme: a rural night palette (dark weathered wood, cream ink, lantern amber),
// Zilla Slab for titles and Barlow for text (SIL OFL, Resources/UIFonts), rounded nine-sliced panels and
// buttons, and helpers for the HUD (cards, meters, key prompts, toasts). Every OnGUI screen sets
// GUI.skin = UITheme.Skin first, so styles copied from GUI.skin inherit the theme.
public static class UITheme
{
    public static readonly Color Ink=new Color(.94f,.90f,.81f),Muted=new Color(.72f,.68f,.60f),Faint=new Color(.52f,.50f,.45f);
    public static readonly Color Accent=new Color(.96f,.71f,.32f),AccentDark=new Color(.62f,.42f,.16f);
    public static readonly Color Danger=new Color(.90f,.38f,.28f),Good=new Color(.58f,.78f,.45f);
    public static readonly Color PanelFill=new Color(.075f,.068f,.058f,.94f),PanelSoft=new Color(.075f,.068f,.058f,.78f),Edge=new Color(.45f,.35f,.22f,.9f);
    public static readonly Color Wood=new Color(.20f,.16f,.115f),WoodHover=new Color(.29f,.22f,.15f),WoodActive=new Color(.40f,.29f,.15f);

    static GUISkin skin;static int builtFor=-1;
    static Font body,bodyBold,title;
    static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    static readonly Dictionary<string,GUIStyle> styles=new Dictionary<string,GUIStyle>();

    public static float Scale=>Mathf.Clamp(Screen.height/1000f,.82f,1.6f);
    public static int Size(float points)=>Mathf.RoundToInt(points*Scale);
    public static Font Body=>Load(ref body,"UIFonts/Barlow-Regular");
    public static Font BodyBold=>Load(ref bodyBold,"UIFonts/Barlow-SemiBold");
    public static Font TitleFont=>Load(ref title,"UIFonts/ZillaSlab-Bold");
    static Font Load(ref Font font,string path){if(font==null)font=Resources.Load<Font>(path);return font;}

    // ---------------------------------------------------------------- textures
    // Rounded rectangle, anti-aliased, with an optional inner edge line; nine-sliced through the style border.
    public static Texture2D Rounded(Color fill,Color edge,int radius=10,float edgeWidth=1.2f)
    {
        string key=fill+"|"+edge+"|"+radius+"|"+edgeWidth;
        if(textures.TryGetValue(key,out var cached) && cached!=null)return cached;
        int size=radius*2+4;var t=new Texture2D(size,size,TextureFormat.RGBA32,false){name="UI rounded",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
        var px=new Color[size*size];float r=radius;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float cx=Mathf.Clamp(x+.5f,r,size-r),cy=Mathf.Clamp(y+.5f,r,size-r);
            float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(cx,cy));
            float inside=Mathf.Clamp01(r-d+.5f);
            float edgeMix=edge.a>0?Mathf.Clamp01(edgeWidth-(r-d)+.5f):0;
            var c=Color.Lerp(fill,edge,edgeMix*edge.a);c.a=Mathf.Lerp(fill.a,1,edgeMix*edge.a)*inside;
            px[y*size+x]=c;
        }
        t.SetPixels(px);t.Apply(false,false);textures[key]=t;return t;
    }
    public static Texture2D Solid(Color c){string key="solid"+c;if(textures.TryGetValue(key,out var t) && t!=null)return t;t=new Texture2D(1,1){hideFlags=HideFlags.DontSave};t.SetPixel(0,0,c);t.Apply();textures[key]=t;return t;}

    // ---------------------------------------------------------------- skin
    public static GUISkin Skin
    {
        get
        {
            int h=Screen.height;
            if(skin!=null && builtFor==h)return skin;
            builtFor=h;styles.Clear();
            if(skin==null){skin=ScriptableObject.CreateInstance<GUISkin>();skin.hideFlags=HideFlags.DontSave;}
            var b=new RectOffset(11,11,11,11);
            skin.font=Body;
            skin.label=new GUIStyle{font=Body,fontSize=Size(18),wordWrap=true,richText=true,padding=new RectOffset(2,2,3,3),normal={textColor=Ink}};
            skin.box=new GUIStyle{font=Body,fontSize=Size(16),wordWrap=true,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(14,14,10,10),border=b,
                normal={background=Rounded(PanelFill,Edge),textColor=Ink}};
            skin.button=Button(Wood,WoodHover,WoodActive,Size(17));
            skin.toggle=new GUIStyle{font=Body,fontSize=Size(17),padding=new RectOffset(Size(36),4,Size(6),Size(6)),margin=new RectOffset(0,0,4,4),alignment=TextAnchor.MiddleLeft,
                normal={textColor=Ink},hover={textColor=Accent},onNormal={textColor=Ink},onHover={textColor=Accent},active={textColor=Accent},onActive={textColor=Accent}};
            skin.textField=new GUIStyle{font=Body,fontSize=Size(17),padding=new RectOffset(10,10,8,8),border=b,normal={background=Rounded(new Color(.04f,.035f,.03f,.95f),Edge,8),textColor=Ink},
                focused={background=Rounded(new Color(.05f,.045f,.035f,.98f),Accent,8),textColor=Ink},hover={background=Rounded(new Color(.05f,.045f,.035f,.95f),AccentDark,8),textColor=Ink}};
            skin.textArea=new GUIStyle(skin.textField){wordWrap=true};
            skin.horizontalSlider=new GUIStyle{fixedHeight=Size(8),margin=new RectOffset(4,4,Size(10),Size(10)),border=new RectOffset(5,5,4,4),normal={background=Rounded(new Color(.03f,.03f,.025f,.9f),Edge,4)}};
            skin.horizontalSliderThumb=new GUIStyle{fixedWidth=Size(20),fixedHeight=Size(20),border=new RectOffset(10,10,10,10),overflow=new RectOffset(0,0,Size(6),Size(6)),
                normal={background=Rounded(Accent,AccentDark,10,2)},hover={background=Rounded(new Color(1,.8f,.45f),AccentDark,10,2)},active={background=Rounded(new Color(1,.85f,.55f),AccentDark,10,2)}};
            skin.verticalScrollbar=new GUIStyle{fixedWidth=Size(9),margin=new RectOffset(6,2,2,2),border=new RectOffset(4,4,4,4),normal={background=Rounded(new Color(0,0,0,.35f),new Color(0,0,0,0),4)}};
            skin.verticalScrollbarThumb=new GUIStyle{fixedWidth=Size(9),border=new RectOffset(4,4,4,4),normal={background=Rounded(new Color(.45f,.36f,.24f,.9f),new Color(0,0,0,0),4)}};
            skin.verticalScrollbarUpButton=new GUIStyle{fixedHeight=0};skin.verticalScrollbarDownButton=new GUIStyle{fixedHeight=0};
            skin.horizontalScrollbar=new GUIStyle{fixedHeight=0};skin.horizontalScrollbarThumb=new GUIStyle{fixedHeight=0};
            skin.horizontalScrollbarLeftButton=new GUIStyle{fixedWidth=0};skin.horizontalScrollbarRightButton=new GUIStyle{fixedWidth=0};
            skin.scrollView=new GUIStyle{};skin.window=new GUIStyle(skin.box);
            skin.settings.cursorColor=Accent;skin.settings.selectionColor=new Color(.96f,.71f,.32f,.35f);
            return skin;
        }
    }
    public static GUIStyle Button(Color normal,Color hover,Color active,int fontSize)
    {
        var s=new GUIStyle{font=BodyBold,fontSize=fontSize,alignment=TextAnchor.MiddleCenter,wordWrap=true,padding=new RectOffset(Size(16),Size(16),Size(10),Size(10)),margin=new RectOffset(0,0,Size(4),Size(6)),
            border=new RectOffset(11,11,11,11),normal={background=Rounded(normal,Edge),textColor=Ink},hover={background=Rounded(hover,Accent),textColor=new Color(1,.93f,.78f)},
            active={background=Rounded(active,Accent),textColor=Color.white},focused={background=Rounded(normal,Edge),textColor=Ink},
            onNormal={background=Rounded(active,Accent),textColor=Color.white},onHover={background=Rounded(active,Accent),textColor=Color.white},onActive={background=Rounded(active,Accent),textColor=Color.white}};
        return s;
    }
    static Texture2D Toggle(bool on)
    {
        string key="toggle"+on;if(textures.TryGetValue(key,out var t) && t!=null)return t;
        int size=24;t=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};var px=new Color[size*size];
        var box=Rounded(new Color(.04f,.035f,.03f,.95f),on?Accent:Edge,6,1.6f);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            var c=box.GetPixelBilinear((x+.5f)/size,(y+.5f)/size);
            if(on){float d=Mathf.Max(Mathf.Abs(x-11.5f),Mathf.Abs(y-11.5f));if(d<5.5f)c=Color.Lerp(c,Accent,Mathf.Clamp01(5.5f-d));}
            px[y*size+x]=c;
        }
        t.SetPixels(px);t.Apply();textures[key]=t;return t;
    }

    // ---------------------------------------------------------------- named text styles
    public static GUIStyle Style(string name)
    {
        var _=Skin;
        if(styles.TryGetValue(name,out var s))return s;
        switch(name)
        {
            case "title":s=new GUIStyle(skin.label){font=TitleFont,fontSize=Size(40),wordWrap=false,normal={textColor=Ink}};break;
            case "heading":s=new GUIStyle(skin.label){font=TitleFont,fontSize=Size(24),normal={textColor=Ink}};break;
            case "subtitle":s=new GUIStyle(skin.label){font=Body,fontSize=Size(18),normal={textColor=Accent}};break;
            case "body":s=new GUIStyle(skin.label);break;
            case "small":s=new GUIStyle(skin.label){fontSize=Size(15),normal={textColor=Muted}};break;
            case "caption":s=new GUIStyle(skin.label){font=BodyBold,fontSize=Size(14),normal={textColor=new Color(.66f,.60f,.50f)}};break;
            case "hud":s=new GUIStyle(skin.label){font=BodyBold,fontSize=Size(17),wordWrap=false,normal={textColor=Ink}};break;
            case "hudSmall":s=new GUIStyle(skin.label){font=Body,fontSize=Size(14),wordWrap=false,normal={textColor=Muted}};break;
            case "center":s=new GUIStyle(skin.label){alignment=TextAnchor.MiddleCenter};break;
            case "key":s=new GUIStyle{font=BodyBold,fontSize=Size(15),alignment=TextAnchor.MiddleCenter,border=new RectOffset(8,8,8,8),
                normal={background=Rounded(new Color(.93f,.88f,.77f),new Color(.55f,.48f,.36f),7,1.5f),textColor=new Color(.13f,.10f,.07f)}};break;
            case "primary":s=Button(new Color(.55f,.37f,.12f),new Color(.66f,.46f,.17f),new Color(.78f,.55f,.2f),Size(18));break;
            case "tab":s=Button(new Color(.13f,.11f,.085f),WoodHover,WoodActive,Size(16));break;
            case "subtitleBand":s=new GUIStyle{font=BodyBold,fontSize=Size(24),alignment=TextAnchor.MiddleCenter,wordWrap=true,richText=true,padding=new RectOffset(Size(24),Size(24),Size(10),Size(12)),
                border=new RectOffset(11,11,11,11),normal={background=Rounded(new Color(0,0,0,.62f),new Color(0,0,0,0)),textColor=Ink}};break;
            default:s=new GUIStyle(skin.label);break;
        }
        styles[name]=s;return s;
    }

    // ---------------------------------------------------------------- drawing helpers
    public static void Panel(Rect r,Color? fill=null,Color? edge=null,int radius=12)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var tex=Rounded(fill??PanelFill,edge??Edge,radius);
        new GUIStyle{border=new RectOffset(radius+1,radius+1,radius+1,radius+1),normal={background=tex}}.Draw(r,false,false,false,false);
    }
    // Checkbox row: the box is drawn at a fixed size so the layout never stretches it.
    public static bool Check(bool value,string text,params GUILayoutOption[] options)
    {
        var style=Skin.toggle;var r=GUILayoutUtility.GetRect(new GUIContent(text),style,options);
        bool result=GUI.Toggle(r,value,text,style);
        if(Event.current.type==EventType.Repaint){float box=Size(22);GUI.DrawTexture(new Rect(r.x+Size(4),r.y+(r.height-box)*.5f,box,box),Toggle(value));}
        return result;
    }
    public static void Shadowed(Rect r,string text,GUIStyle style)
    {
        var shadow=new GUIStyle(style){normal={textColor=new Color(0,0,0,.75f)}};
        GUI.Label(new Rect(r.x+1.5f,r.y+1.5f,r.width,r.height),text,shadow);GUI.Label(r,text,style);
    }
    public static void Meter(Rect r,float value,Color fill)
    {
        Panel(r,new Color(.02f,.02f,.015f,.85f),new Color(0,0,0,0),Mathf.Max(2,(int)(r.height*.5f)));
        var inner=new Rect(r.x+1,r.y+1,Mathf.Max(0,(r.width-2)*Mathf.Clamp01(value)),r.height-2);
        if(inner.width>1)Panel(inner,fill,new Color(0,0,0,0),Mathf.Max(2,(int)(inner.height*.5f)));
    }
    // "[E] Abrir porta" prompt centred on x at height y; returns its rect.
    public static Rect KeyPrompt(float centreX,float y,string key,string action)
    {
        var text=Style("hud");var keyStyle=Style("key");
        float keyW=Mathf.Max(Size(30),keyStyle.CalcSize(new GUIContent(key)).x+Size(14));float textW=text.CalcSize(new GUIContent(action)).x;
        float h=Size(40),pad=Size(12),w=pad+keyW+Size(10)+textW+pad;
        var r=new Rect(centreX-w*.5f,y,w,h);Panel(r,PanelSoft);
        GUI.Label(new Rect(r.x+pad,r.y+(h-Size(28))*.5f,keyW,Size(28)),key,keyStyle);
        GUI.Label(new Rect(r.x+pad+keyW+Size(10),r.y,textW+4,h),action,new GUIStyle(text){alignment=TextAnchor.MiddleLeft});
        return r;
    }
    // A row of key caps with their actions ("W/S  Acelerar   A/D  Virar ..."), centred at y; optional info line below.
    public static Rect HintBar(float y,string info,params string[] keyAndAction)
    {
        var text=new GUIStyle(Style("hudSmall")){normal={textColor=Ink}};var keyStyle=Style("key");
        float gap=Size(18),pad=Size(14),h=Size(40),w=pad*2-gap;
        var widths=new float[keyAndAction.Length];
        for(int i=0;i+1<keyAndAction.Length;i+=2)
        {
            float kw=Mathf.Max(Size(30),keyStyle.CalcSize(new GUIContent(keyAndAction[i])).x+Size(12));float tw=text.CalcSize(new GUIContent(keyAndAction[i+1])).x;
            widths[i]=kw;widths[i+1]=tw;w+=kw+Size(8)+tw+gap;
        }
        float infoH=string.IsNullOrEmpty(info)?0:Size(26);
        var r=new Rect((Screen.width-w)*.5f,y,w,h+infoH);Panel(r,PanelSoft);
        float x=r.x+pad;
        for(int i=0;i+1<keyAndAction.Length;i+=2)
        {
            GUI.Label(new Rect(x,r.y+(h-Size(28))*.5f,widths[i],Size(28)),keyAndAction[i],keyStyle);x+=widths[i]+Size(8);
            GUI.Label(new Rect(x,r.y,widths[i+1]+4,h),keyAndAction[i+1],new GUIStyle(text){alignment=TextAnchor.MiddleLeft});x+=widths[i+1]+gap;
        }
        if(infoH>0)GUI.Label(new Rect(r.x,r.y+h-Size(6),r.width,infoH),info,new GUIStyle(Style("hudSmall")){alignment=TextAnchor.MiddleCenter});
        return r;
    }
    public static void Toast(string text,float alpha=1)
    {
        if(string.IsNullOrEmpty(text))return;
        var style=new GUIStyle(Style("body")){alignment=TextAnchor.MiddleCenter,fontSize=Size(18)};
        float w=Mathf.Min(Screen.width*.6f,Size(760));float h=style.CalcHeight(new GUIContent(text),w-Size(40))+Size(22);
        var r=new Rect((Screen.width-w)*.5f,Screen.height-h-Size(96),w,h);
        var old=GUI.color;GUI.color=new Color(1,1,1,alpha);Panel(r,PanelSoft);GUI.Label(new Rect(r.x+Size(20),r.y,r.width-Size(40),r.height),text,style);GUI.color=old;
    }
}
