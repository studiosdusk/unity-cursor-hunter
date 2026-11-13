using System;
using System.IO;
using CursorHunter.Progression.ResearchPrototype;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CursorHunter.Progression.Editor
{
    public static class ResearchPrototypeSceneBuilder
    {
        private const string Folder = "Assets/CursorHunter/Progression/ResearchPrototype";
        private const string ScenePath = "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity";
        private const string Pack = "Assets/DownLoadAssets/Casual_GUI/GUI Pro-SimpleCasual/";
        private const string IconFolder = Pack + "ResourcesData/Sprites/Demo/Demo_Icon/";
        private const string ResearchIcons = "Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/256/";
        private static TMP_FontAsset font;

        [MenuItem("Tools/Cursor Hunter/Research Prototype/Create Test Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Test scene already exists; refusing to overwrite: " + ScenePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/CursorHunter/Progression/Runtime/Fonts/NanumGothic.ttf");
            font = TMP_FontAsset.CreateFontAsset(sourceFont);
            font.name = "ResearchPrototype NanumGothic";
            AssetDatabase.CreateAsset(font, Folder + "/ResearchPrototypeFont.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
            font.TryAddCharacters("초급 공격력 연구범위간격치명타 확률강화피해보스0123456789 /배초%연구 비용최대 랭크무료가넷테스트 (실제 저장 및 전투 미적용)→");
            EditorUtility.SetDirty(font);
            var defs = new ResearchDefinition[6];
            string[] ids = {"cursor.attack", "cursor.radius", "cursor.interval", "cursor.critical", "cursor.critical_damage", "cursor.boss_damage"};
            string[] icons = {"Gear_Weapons_Sword_01.png", "UI_ETC_Target_01.png", "Stat_AttackSpeed_01.png", "Stat_CriticalChance_01.png", "Stat_Critical_01.png", "UI_Play_Skull_03_Red.png"};
            float[][] values = {new[]{2f,4f,7f,10f,14f},new[]{.1f,.2f,.3f,.4f,.5f},new[]{-.08f,-.16f,-.24f,-.32f},new[]{5f,10f,15f,20f,25f},new[]{.1f,.2f,.3f},new[]{.01f,.25f,.45f,.7f,1f}};
            long[] prices = {100,250,500,1000,2000};
            for (int i=0;i<defs.Length;i++)
            {
                var d=ScriptableObject.CreateInstance<ResearchDefinition>();
                d.name=ids[i]; d.stableId=ids[i]; d.presentation.textKey="research."+ids[i]; d.stat=(ResearchStat)i; d.presentation.valueDisplay = i == 1 ? ResearchValueDisplay.RadiusRatio : i == 3 ? ResearchValueDisplay.PercentPoints : i == 5 ? ResearchValueDisplay.FractionPercent : ResearchValueDisplay.Number;
                d.icon=AssetDatabase.LoadAssetAtPath<Sprite>(ResearchIcons+icons[i]);
                if (d.icon == null) throw new InvalidOperationException("Missing icon: " + icons[i]);
                d.ranks=new ResearchRank[values[i].Length];
                for(int j=0;j<d.ranks.Length;j++) d.ranks[j]=new ResearchRank { levelDelta=values[i][j] - (j == 0 ? 0 : values[i][j-1]), garnetCost=prices[j] };
                AssetDatabase.CreateAsset(d, Folder+"/"+ids[i]+".asset"); defs[i]=d;
            }
            var canvasGO=new GameObject("ResearchPrototype_Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGO.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGO.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            var map=canvasGO.AddComponent<ResearchTestMap>();
            var bg=Image("Background",canvasGO.transform,Vector2.zero,new Vector2(1920,1080),null,new Color(.035f,.075f,.13f)); bg.raycastTarget=true; bg.rectTransform.anchorMin=Vector2.zero; bg.rectTransform.anchorMax=Vector2.one; bg.rectTransform.sizeDelta=Vector2.zero;
            Text("ScreenTitle",canvasGO.transform,new Vector2(-770,440),new Vector2(450,75),"초급 연구",42,TextAlignmentOptions.Left);
            Text("TestLabel",canvasGO.transform,new Vector2(-430,440),new Vector2(350,52),"테스트 (실제 저장 및 전투 미적용)",18,TextAlignmentOptions.Left);
            Text("WalletLabel",canvasGO.transform,new Vector2(650,440),new Vector2(110,55),"가넷",24,TextAlignmentOptions.Right);
            map.wallet=Text("TestWallet",canvasGO.transform,new Vector2(760,440),new Vector2(120,55),"10,000",26,TextAlignmentOptions.Right);
            var board=Rect("ResearchBoard",canvasGO.transform,Vector2.zero,new Vector2(900,760));
            var navigation=canvasGO.AddComponent<ResearchMapNavigation>();
            navigation.viewport=(RectTransform)canvasGO.transform; navigation.board=board; navigation.map=map; map.navigation=navigation;
            var lines=Rect("EditableLinks",board,Vector2.zero,new Vector2(900,760));
            var nodeRoot=Rect("EditableNodes",board,Vector2.zero,new Vector2(900,760));
            Vector2[] positions={new Vector2(0,165),new Vector2(145,85),new Vector2(145,-85),new Vector2(0,-165),new Vector2(-145,-85),new Vector2(-145,85)};
            var frameSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Pack+"ResourcesData/Sprites/Components/Frame/Frame_Custom/BasicFrame_Rectangle01_l_White.png");
            if (frameSprite == null) throw new InvalidOperationException("Missing node frame sprite");
            map.nodes=new ResearchTestNode[12];
            for(int i=0;i<6;i++)
            {
                Vector2 inner=i==0 ? Vector2.zero : positions[i];
                Vector2 outer=i==0 ? positions[i] : positions[i]*2f;
                var line=CreateLink(lines,"Link_"+ids[i]+".next",inner,outer);
                var first=CreateNode(nodeRoot,ids[i]+".start",inner,defs[i],map,frameSprite);
                var second=CreateNode(nodeRoot,ids[i]+".next",outer,defs[i],map,frameSprite);
                second.prerequisites=new[]{first}; second.requiredPrerequisites=1; second.incomingLine=line.gameObject;
                map.nodes[i]=first; map.nodes[i+6]=second;
            }
            for (int i=1;i<6;i++)
            {
                map.nodes[i].prerequisites=new[]{map.nodes[0]}; map.nodes[i].requiredPrerequisites=1;
                map.nodes[i].incomingLine=CreateLink(lines,"Link_"+ids[i]+".start",Vector2.zero,positions[i]).gameObject;
            }
            foreach (var node in map.nodes) PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Prefabs/Prefabs_DemoScene_Panels/Dark/Tooltip.prefab");
            if(prefab==null) throw new InvalidOperationException("Dark Tooltip prefab not found");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,canvasGO.transform);
            root.name="Tooltip_Dark_Prefab";
            var tooltipGroup=root.AddComponent<CanvasGroup>(); tooltipGroup.blocksRaycasts=false;
            map.tooltipRoot=root;
            var rr=(RectTransform)root.transform; rr.anchorMin=rr.anchorMax=new Vector2(.5f,.5f); rr.sizeDelta=Vector2.zero; rr.anchoredPosition=Vector2.zero;
            root.transform.Find("ScreenDim").gameObject.SetActive(false);
            var popup=(RectTransform)root.transform.Find("Popup"); popup.anchoredPosition=new Vector2(560,-20); popup.sizeDelta=new Vector2(490,304);
            map.tooltipPanel=popup;
            foreach(Transform child in popup) if(child.name!="TopFrame_Dark") child.gameObject.SetActive(false);
            var header=(RectTransform)popup.Find("TopFrame_Dark"); header.anchorMin=new Vector2(0,1); header.anchorMax=new Vector2(1,1); header.sizeDelta=new Vector2(-48,58); header.anchoredPosition=new Vector2(0,-43);
            var roundedSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Pack+"ResourcesData/Sprites/Components/Frame/Frame_Demo_Dark/BasicFrame_Rectangle02_s_Navy.png");
            if (roundedSprite == null) throw new InvalidOperationException("Missing rounded Dark frame");
            var headerImage=header.GetComponent<UnityEngine.UI.Image>(); headerImage.sprite=roundedSprite; headerImage.type=UnityEngine.UI.Image.Type.Sliced; headerImage.enabled=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(header);
            PrefabUtility.RecordPrefabInstancePropertyModifications(headerImage);
            foreach(Transform child in header) child.gameObject.SetActive(false);
            map.title=Text("ResearchTitle",header,Vector2.zero,new Vector2(450,55),"",25,TextAlignmentOptions.Center);
            map.title.enableAutoSizing=true; map.title.fontSizeMin=16; map.title.fontSizeMax=25;
            var bandSprite=roundedSprite;
            var band=Image("EffectBand",popup,new Vector2(0,15),new Vector2(454,114),bandSprite,Color.white);
            band.type=UnityEngine.UI.Image.Type.Sliced; band.raycastTarget=false; band.transform.SetAsFirstSibling();
            map.effectDescription=Text("EffectDescription",popup,new Vector2(0,49),new Vector2(430,30),"",20,TextAlignmentOptions.Center);
            map.effectDescription.enableAutoSizing=true; map.effectDescription.fontSizeMin=14; map.effectDescription.fontSizeMax=20;
            map.beforeValue=Text("CurrentValue",popup,new Vector2(-110,-1),new Vector2(150,50),"0",32,TextAlignmentOptions.Center);
            map.afterValue=Text("NextValue",popup,new Vector2(110,-1),new Vector2(150,50),"2",32,TextAlignmentOptions.Center); map.afterValue.color=new Color(.65f,.91f,.75f);
            var a=Image("ArrowIcon",popup,new Vector2(0,-1),new Vector2(28,28),AssetDatabase.LoadAssetAtPath<Sprite>(ResearchIcons+"UI_Common_Arrow_04_Blue_Right.png"),Color.white); a.preserveAspect=true; a.raycastTarget=false; map.arrow=a.gameObject;
            var separator=Image("Separator",popup,new Vector2(0,-58),new Vector2(446,2),null,new Color(.27f,.36f,.48f)); separator.raycastTarget=false;
            map.costLabel=Text("CostLabel",popup,new Vector2(-113,-88),new Vector2(150,40),"연구 비용",22,TextAlignmentOptions.Center);
            var gem=Image("GarnetIcon",popup,new Vector2(65,-88),new Vector2(36,36),AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Gem_Pentagon_Red.png"),Color.white); gem.preserveAspect=true; gem.raycastTarget=false; map.gemIcon=gem.gameObject;
            map.costValue=Text("CostValue",popup,new Vector2(132,-88),new Vector2(100,40),"100",23,TextAlignmentOptions.Right);
            map.rankInfo=Text("SelectedRank",popup,new Vector2(0,-125),new Vector2(130,28),"0 / 5",18,TextAlignmentOptions.Center);
            var es=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new InvalidOperationException("Failed to save scene");
            AssetDatabase.SaveAssets();
            Debug.Log("RESEARCH_PROTOTYPE_CREATED " + ScenePath);
        }
        private static Image CreateLink(Transform parent,string name,Vector2 start,Vector2 end)
        {
            var delta=end-start;
            var line=Image(name,parent,(start+end)/2,new Vector2(7,Mathf.Max(1,delta.magnitude-92)),null,new Color(.13f,.65f,.95f));
            line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(-delta.x,delta.y)*Mathf.Rad2Deg);
            line.raycastTarget=false;
            return line;
        }
        private static ResearchTestNode CreateNode(Transform parent,string id,Vector2 pos,ResearchDefinition definition,ResearchTestMap map,Sprite sprite)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CursorHunter/Progression/Prefabs/ResearchNode.prefab");
            if (prefab == null) throw new InvalidOperationException("Missing ResearchNode prefab");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            instance.name="Node_"+id;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            var rect=(RectTransform)instance.transform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f); rect.anchoredPosition=pos;
            var node=instance.GetComponent<ResearchTestNode>();
            node.placementId=id; node.definition=definition; node.map=map;
            node.frame.sprite=sprite; node.frame.color=map.unpurchasedColor; node.icon.sprite=definition.icon;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node.frame);
            PrefabUtility.RecordPrefabInstancePropertyModifications(node.icon);
            return node;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        { var go=new GameObject(name,typeof(RectTransform)); var r=(RectTransform)go.transform; r.SetParent(parent,false); r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.anchoredPosition=pos; r.sizeDelta=size; return r; }
        private static Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Sprite sprite,Color color)
        { var r=Rect(name,parent,pos,size); var img=r.gameObject.AddComponent<Image>(); img.sprite=sprite; img.color=color; return img; }
        private static TextMeshProUGUI Text(string name,Transform parent,Vector2 pos,Vector2 size,string value,float sizeFont,TextAlignmentOptions align)
        { var r=Rect(name,parent,pos,size); var t=r.gameObject.AddComponent<TextMeshProUGUI>(); t.font=font; t.fontStyle=FontStyles.Bold; t.text=value; t.fontSize=sizeFont; t.alignment=align; t.color=Color.white; t.raycastTarget=false; return t; }
    }
}
