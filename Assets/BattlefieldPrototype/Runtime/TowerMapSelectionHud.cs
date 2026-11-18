using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        RectTransform mapSelection;
        TowerLayoutPreview mapPreview;
        Text mapDescription;
        readonly Button[] mapChoices = new Button[TowerMapCatalog.Count];
        void CreateMapSelection(RectTransform card)
        {
            mapSelection = Panel(card,"Battlefield selection",40,316,920,180);
            var obj = new GameObject("Tower layout preview",typeof(RectTransform),typeof(TowerLayoutPreview)); obj.transform.SetParent(mapSelection,false);
            mapPreview = obj.GetComponent<TowerLayoutPreview>(); mapPreview.Battle = battle; mapPreview.material = overlay; mapPreview.raycastTarget = false;
            BattleUiStyle.Place(mapPreview.rectTransform,14,10,220,148);
            Label(mapSelection,"Layout legend","BLUE: yours / RED: enemy / GRAY: ruin",10,14,162,224,13,BattleUiStyle.Muted);
            for (int i = 0; i < mapChoices.Length; i++)
            {
                var kind = (TowerMapKind)i;
                mapChoices[i] = Button(mapSelection,"Map " + kind,TowerMapCatalog.Name(kind).ToUpperInvariant() + "\n" + TowerMapCatalog.Spawns(kind).Count + " towers / " + TowerMapCatalog.WallHeight(kind) + "m walls",
                    252 + i%3*214,14+i/3*48,200,40,()=>{battle.SelectMap(kind); Refresh();});
                mapChoices[i].GetComponentInChildren<Text>().fontSize = 14;
            }
            var random = Button(mapSelection,"Random battlefield","RANDOM MAP",680,62,200,40,()=>
            { battle.SelectMap((TowerMapKind)(((int)battle.Map+Random.Range(1,TowerMapCatalog.Count))%TowerMapCatalog.Count)); Refresh(); });
            random.GetComponentInChildren<Text>().fontSize = 14;
            mapDescription = Label(mapSelection,"Map description","",15,252,110,640,60,Color.white);
        }
        void RefreshMapSelection(bool atHome)
        {
            mapSelection.gameObject.SetActive(atHome);
            ((RectTransform)modalTitle.transform.parent).sizeDelta = new Vector2(1000, world ? 760 : 630);
            BattleUiStyle.Place(modalCopy.rectTransform,40,180,920,world ? (atHome ? 120 : 330) : atHome ? 125 : 245);
            modalCopy.fontSize = world ? 28 : atHome ? 20 : 22;
            BattleUiStyle.Place(resultStats.rectTransform,40,world ? 580 : atHome ? 499 : 435,920,world ? 72 : atHome ? 35 : 70);
            resultStats.fontSize = world ? 23 : atHome ? 15 : 17;
            foreach (var button in new[] { newMatch, home, homeGuide })
            {
                if (button == null) continue;
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, world ? -680 : -537);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 54);
                button.GetComponentInChildren<Text>().fontSize = world ? 26 : 20;
            }
            mapSelection.sizeDelta = new Vector2(920, world ? 244 : 180);
            for (int i = 0; i < mapChoices.Length; i++)
            {
                BattleUiStyle.Place((RectTransform)mapChoices[i].transform,252+i%3*214,14+i/3*(world ? 76 : 48),200,world ? 64 : 40);
                mapChoices[i].GetComponentInChildren<Text>().fontSize = world ? 21 : 14;
                var kind = (TowerMapKind)i;
                mapChoices[i].GetComponentInChildren<Text>().text = TowerMapCatalog.Name(kind).ToUpperInvariant() + "\n" +
                    (world ? $"{TowerMapCatalog.Spawns(kind).Count} TOWERS / {TowerMapCatalog.WallHeight(kind)}m" :
                    $"{TowerMapCatalog.Spawns(kind).Count} towers / {TowerMapCatalog.WallHeight(kind)}m walls");
            }
            var random = mapSelection.Find("Random battlefield").GetComponent<Button>();
            BattleUiStyle.Place((RectTransform)random.transform,680,world ? 90 : 62,200,world ? 64 : 40);
            random.GetComponentInChildren<Text>().fontSize = world ? 22 : 14;
            BattleUiStyle.Place(mapDescription.rectTransform,252,world ? 164 : 110,640,world ? 80 : 60);
            mapDescription.fontSize = world ? 22 : 15;
            if (!atHome) return;
            foreach (var button in mapChoices) button.interactable = true;
            for (int i = 0; i < mapChoices.Length; i++) mapChoices[i].image.color = (int)battle.Map == i ? new Color(.24f,.32f,.22f) : new Color(.08f,.13f,.15f);
            mapDescription.text = battle.MapName.ToUpperInvariant() + " / " + TowerMapCatalog.Spawns(battle.Map).Count + " TOWERS\n" + TowerMapCatalog.Description(battle.Map);
            if (world) mapDescription.text = TowerMapCatalog.Description(battle.Map);
            mapPreview.SetVerticesDirty();
        }
    }
}
