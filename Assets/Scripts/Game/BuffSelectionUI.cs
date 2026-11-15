using UnityEngine;
using UnityEngine.EventSystems;

public class BuffSelectionUI : MonoBehaviour
{
    [SerializeField] WaveManager manager;
    [SerializeField] GameObject panel;
    [SerializeField] UnityEngine.UI.Button[] buttons;
    [SerializeField] UnityEngine.UI.Text[] names;
    [SerializeField] UnityEngine.UI.Text[] descriptions;
    [SerializeField] UnityEngine.UI.Text applied;
    float hideAt;
    public static BuffSelectionUI Criar(WaveManager partida)
    {
        var root=new GameObject("BuffSelection",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(partida.transform,false);
        var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=110;
        var scale=root.GetComponent<UnityEngine.UI.CanvasScaler>(); scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=0.5f;
        var ui=root.AddComponent<BuffSelectionUI>(); ui.manager=partida;
        ui.panel=Object("BuffChoice",root.transform,Vector2.zero,Vector2.one);
        ui.panel.AddComponent<UnityEngine.UI.Image>().color=new Color(0.02f,0.03f,0.08f,0.9f);
        Label("Title",ui.panel.transform,"Escolha uma melhoria",34,new Vector2(0.08f,0.76f),new Vector2(0.92f,0.9f));
        Label("Hint",ui.panel.transform,"Uma escolha antes da próxima onda  •  Teclas 1, 2 ou 3",20,new Vector2(0.08f,0.1f),new Vector2(0.92f,0.2f));
        ui.buttons=new UnityEngine.UI.Button[3];ui.names=new UnityEngine.UI.Text[3];ui.descriptions=new UnityEngine.UI.Text[3];
        for(int i=0;i<3;i++)
        {
            var option=Object("Option"+(i+1),ui.panel.transform,new Vector2(0.06f+i*0.3f,0.27f),new Vector2(0.34f+i*0.3f,0.7f));
            option.AddComponent<UnityEngine.UI.Image>().color=new Color(0.12f,0.15f,0.28f,1);
            ui.buttons[i]=option.AddComponent<UnityEngine.UI.Button>();
            var colors=ui.buttons[i].colors;colors.highlightedColor=new Color(0.65f,0.75f,1);colors.selectedColor=colors.highlightedColor;ui.buttons[i].colors=colors;
            ui.names[i]=Label("Name",option.transform,"",24,new Vector2(0.05f,0.57f),new Vector2(0.95f,0.95f));
            ui.descriptions[i]=Label("Description",option.transform,"",20,new Vector2(0.06f,0.08f),new Vector2(0.94f,0.57f));
        }
        ui.applied=Label("Applied",root.transform,"",24,new Vector2(0.15f,0.84f),new Vector2(0.85f,0.96f));ui.applied.gameObject.SetActive(false);
        ui.Ligar(); ui.panel.SetActive(false);
        if(EventSystem.current==null)
        {
            var events=new GameObject("BuffEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(partida.transform,false);
        }
        return ui;
    }
    static GameObject Object(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var item=new GameObject(name,typeof(RectTransform));item.transform.SetParent(parent,false);
        var rect=item.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return item;
    }
    static UnityEngine.UI.Text Label(string name,Transform parent,string text,int size,Vector2 min,Vector2 max)
    {
        var label=Object(name,parent,min,max).AddComponent<UnityEngine.UI.Text>();
        label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=size;label.color=Color.white;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
        return label;
    }
    void Escolher0()=>manager.EscolherBuff(0);
    void Escolher1()=>manager.EscolherBuff(1);
    void Escolher2()=>manager.EscolherBuff(2);
    void Ligar()
    {
        if(buttons==null || buttons.Length!=3) return;
        buttons[0].onClick.AddListener(Escolher0);buttons[1].onClick.AddListener(Escolher1);buttons[2].onClick.AddListener(Escolher2);
    }
    void OnEnable(){Ligar();}
    void OnDisable()
    {
        if(buttons==null || buttons.Length!=3) return;
        buttons[0].onClick.RemoveListener(Escolher0);buttons[1].onClick.RemoveListener(Escolher1);buttons[2].onClick.RemoveListener(Escolher2);
    }
    public void Mostrar(PlayerBuffs buffs)
    {
        gameObject.SetActive(true);panel.SetActive(true);applied.gameObject.SetActive(false);
        for(int i=0;i<3;i++){names[i].text=$"{i+1}  {buffs.Ofertas[i].nome}";descriptions[i].text=buffs.Ofertas[i].descricao;buttons[i].interactable=true;}
        if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }
    public void Aplicada(string name)
    {
        panel.SetActive(false);applied.text="Melhoria aplicada: "+name;applied.gameObject.SetActive(true);hideAt=Time.unscaledTime+2;
    }
    public void Ocultar(){panel.SetActive(false);applied.gameObject.SetActive(false);gameObject.SetActive(false);}
    void Update()
    {
        if(manager==null) return;
        if(panel.activeSelf && manager.Situacao==WaveManager.Estado.EscolhendoBuff)
        {
            if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1)) Escolher0();
            else if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2)) Escolher1();
            else if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Keypad3)) Escolher2();
        }
        if(applied.gameObject.activeSelf && Time.unscaledTime>=hideAt) applied.gameObject.SetActive(false);
    }
}
