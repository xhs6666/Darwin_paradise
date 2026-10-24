using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities;
using DarwinPark.Genetics;

namespace DarwinPark.UI
{
    public class EntitySpawnPanel : MonoBehaviour
    {
        public GameObject DefaultEntityPrefab;

        private Transform _templateGridParent;

        private void Start()
        {
            BuildUI();
            EntityTemplateManager.OnTemplatesChanged += RefreshTemplates;
            RefreshTemplates();
        }

        private void OnDestroy()
        {
            EntityTemplateManager.OnTemplatesChanged -= RefreshTemplates;
        }

        private void BuildUI()
        {
            // ================= 左侧：生成与收藏 =================
            var leftSec = UIFactory.NewRect("LeftSec", transform);
            var lrt = leftSec.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(0, 1);
            lrt.offsetMin = new Vector2(10, 5);
            lrt.offsetMax = new Vector2(80, -5);    // 宽度 70

            var leftVlg = leftSec.AddComponent<VerticalLayoutGroup>();
            leftVlg.spacing = 10; // 两个按钮之间的间距
            leftVlg.childAlignment = TextAnchor.MiddleCenter;
            leftVlg.childControlHeight = false;
            leftVlg.childControlWidth = false; // 让按钮保持 70x70

            // 1. 默认生成槽 (70x70)
            CreateSpawnSlot(leftSec.transform, "默认生成\n点击或拖拽", DefaultEntityPrefab, null);

            // 2. 收藏列表按钮 (70x70)
            CreateFavoritesButton(leftSec.transform);

            // ================= 分隔线 1 =================
            CreateDivider(transform, 90);

            // ================= 中间：模板区域 (两行四列) =================
            var templateSec = UIFactory.NewRect("TemplateSec", transform);
            var trt = templateSec.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(0, 1);
            trt.offsetMin = new Vector2(100, 5);
            // 宽度 = 4列 * 70 + 3个间距 * 10 = 310
            trt.offsetMax = new Vector2(100 + 310, -5);

            var grid = templateSec.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(70, 70); // ★ 模板 70x70
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            _templateGridParent = templateSec.transform;
        }

        private void CreateSpawnSlot(Transform parent, string label, GameObject prefab, Genome customGenome)
        {
            var slotGO = UIFactory.NewRect("SpawnSlot", parent, typeof(Image), typeof(EntitySpawnerUI));
            var rt = slotGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(70, 70);

            slotGO.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.35f, 0.8f);
            var spawner = slotGO.GetComponent<EntitySpawnerUI>();
            spawner.entityPrefab = prefab;
            spawner.CustomGenome = customGenome;

            var txt = UIFactory.NewText("Txt", slotGO.transform, label, 12, TextAnchor.MiddleCenter); // 字号稍微小一点适应 70x70
            UIFactory.Stretch(txt.rectTransform);
        }

        private void CreateFavoritesButton(Transform parent)
        {
            var favBtnGO = UIFactory.NewRect("FavBtn", parent, typeof(Image), typeof(Button));
            var frt = favBtnGO.GetComponent<RectTransform>();
            frt.sizeDelta = new Vector2(70, 70);
            favBtnGO.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.3f, 0.9f);

            var favBtn = favBtnGO.GetComponent<Button>();
            favBtn.onClick.AddListener(() => {
                if (EntityFavoritesManager.Instance != null)
                {
                    EntityFavoritesManager.Instance.TogglePanel();
                }
                else Log.Error(Log.Tag.UI, "收藏列表不可用：场景缺少 EntityFavoritesManager");
            });

            var favTxt = UIFactory.NewText("Txt", favBtnGO.transform, "收藏\n列表", 12, TextAnchor.MiddleCenter);
            UIFactory.Stretch(favTxt.rectTransform);
        }

        private void RefreshTemplates()
        {
            if (_templateGridParent == null) return;

            foreach (Transform child in _templateGridParent) Destroy(child.gameObject);

            for (int i = 0; i < 8; i++)
            {
                if (i < EntityTemplateManager.Templates.Count)
                {
                    CreateTemplateButton(EntityTemplateManager.Templates[i]);
                }
                else
                {
                    var emptyGO = UIFactory.NewRect("EmptyTpl", _templateGridParent, typeof(Image));
                    emptyGO.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.3f);
                }
            }
        }

        private void CreateTemplateButton(EntityTemplateData tpl)
        {
            var btnGO = UIFactory.NewRect("Tpl_" + tpl.templateName, _templateGridParent, typeof(Image), typeof(Button), typeof(EntitySpawnerUI));
            var img = btnGO.GetComponent<Image>();

            var sprite = UIIconRegistry.Get(tpl.iconId);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.2f, 0.35f, 0.5f, 0.9f);
            }

            var textGO = UIFactory.NewText("Tooltip", btnGO.transform, tpl.templateName, 12, TextAnchor.MiddleCenter);
            UIFactory.Stretch(textGO.rectTransform);
            textGO.gameObject.SetActive(false);

            var trigger = btnGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => { textGO.gameObject.SetActive(true); });
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener((data) => { textGO.gameObject.SetActive(false); });
            trigger.triggers.Add(exit);

            var spawner = btnGO.GetComponent<EntitySpawnerUI>();
            spawner.entityPrefab = DefaultEntityPrefab;
            spawner.CustomGenome = ConvertToGenome(tpl.genes);
        }

        private Genome ConvertToGenome(List<GeneInstanceData> genes)
        {
            var genome = new Genome();
            foreach (var g in genes)
            {
                genome.Add(GeneInstance.Create(g.definitionId, g.value, g.tier));
            }
            return genome;
        }

        private void CreateDivider(Transform parent, float xOffset)
        {
            var div = UIFactory.NewRect("Divider", parent, typeof(Image));
            var rt = div.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.1f);
            rt.anchorMax = new Vector2(0, 0.9f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(xOffset, 0);
            rt.offsetMax = new Vector2(xOffset + 2, 0);
            div.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
        }
    }
}