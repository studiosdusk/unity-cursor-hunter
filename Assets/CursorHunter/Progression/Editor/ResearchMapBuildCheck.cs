using System;
using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace CursorHunter.Progression.Editor
{
    internal sealed class ResearchMapBuildCheck : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (scene.path != ResearchMapEditorService.MainScene) return;
            foreach (var map in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)))
            {
                try
                {
                    ResearchEffectCalculator.ValidateMap(map.nodes);
                    foreach (var node in map.nodes)
                        for (int i = 0; i < node.definition.ranks.Length; i++)
                            if (!node.TryGetCost(i, out _)) throw new InvalidOperationException(node.placementId + "의 비용이 올바르지 않습니다.");
                    if (map.appliedVersion == null) continue; // Legacy scene prior to explicit conversion.
                    var errors = ResearchMapEditorService.Validate(map.appliedVersion);
                    if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
                    string sceneHash = ResearchMapEditorService.Fingerprint(map);
                    if (sceneHash != map.appliedSceneFingerprint || sceneHash != ResearchMapEditorService.FingerprintDefinition(map.appliedVersion))
                        throw new InvalidOperationException("씬과 적용된 맵 버전이 다릅니다. 연구 맵 에디터에서 씬에 적용하고 씬을 저장해 주세요.");
                }
                catch (Exception e) { throw new BuildFailedException("연구 맵 검증 실패: " + e.Message); }
            }
        }
    }
}
