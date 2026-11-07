using System.Collections.Generic;
using DarwinPark.Core;
using DarwinPark.Entities.Ecology;
using DarwinPark.Genetics;
using UnityEngine;
using UnityEngine.UI;
namespace DarwinPark.UI
{
    public partial class GeneInspectorUI
    {
        private ColonyResource _currentCorpse;private GenomeEvaluation _corpseEvaluation;
        public ColonyResource CurrentCorpse=>_currentCorpse;
        // ★ 新增：使用原GenePanel、摘要、基因行与ScrollRect；不实例化带模拟组件的隐藏实体。
        public void ShowCorpseInfo(ColonyResource corpse)
        {
            if(corpse==null||corpse.State.kind!=ColonyResourceKind.Corpse||!EnsurePanel())return;
            HideEntityInfo();WorldResourceInspector.Instance?.Hide();_currentCorpse=corpse;_panel.SetActive(true);_panel.transform.SetAsLastSibling();_nameInput.readOnly=true;_nameInput.SetTextWithoutNotify((string.IsNullOrEmpty(corpse.State.sourceName)?"尸体":corpse.State.sourceName)+" · 已死亡");_favBtn.interactable=false;
            _rowRefresh.Clear();foreach(Transform child in _contentParent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var summary=UIFactory.NewText("BaseInfo",_contentParent,"",14,TextAnchor.UpperLeft);summary.gameObject.AddComponent<LayoutElement>().minHeight=198;summary.enabled=false;_baseInfo=summary;
            var refresh=EntitySummaryView.BuildCorpse(summary.transform,corpse);_rowRefresh.Add(refresh);refresh();EntityEditPopup.AddEntry(summary.transform,null,corpse);
            var archive=corpse.State.source;_corpseEvaluation=archive?.Evaluate();
            Heading("CoreTitle","核心基因");
            if(archive==null){Heading("MissingArchive","旧存档未记录生前基因");return;}
            var inspected=GeneFailureReasons.Inspect(archive.genome,_corpseEvaluation);var required=new[]{"gene_growth","gene_reproduction","gene_nutrition"};var extra=new List<GeneEvaluation>();
            foreach(var id in required){bool first=true;foreach(var evaluation in inspected)if(evaluation.Definition.Id==id){if(first){CreateGeneRow(evaluation,null,_contentParent);first=false;}else extra.Add(evaluation);}if(first)Heading("Missing","缺失 ("+id+")");}
            Heading("MutTitle","扩展基因");foreach(var evaluation in inspected)if(System.Array.IndexOf(required,evaluation.Definition.Id)<0)extra.Add(evaluation);foreach(var evaluation in extra)CreateGeneRow(evaluation,null,_contentParent);
        }
        private void Heading(string name,string text)
        {var title=UIFactory.NewText(name,_contentParent,"<color=#99AFBF><b>"+text+"</b></color>",14,TextAnchor.UpperLeft);title.gameObject.AddComponent<LayoutElement>().preferredHeight=25;}
        private void RefreshCorpse()
        {
            if(_currentCorpse==null||!_currentCorpse.isActiveAndEnabled||_currentCorpse.State.energy<=0||!WorldRuntimeGate.Ready){HideEntityInfo();return;}
            if(_panel!=null&&_panel.activeSelf)foreach(var refresh in _rowRefresh)refresh();
        }
    }
}
