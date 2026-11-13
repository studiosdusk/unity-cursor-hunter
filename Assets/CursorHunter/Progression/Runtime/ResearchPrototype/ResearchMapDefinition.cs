using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CursorHunter.Progression.ResearchPrototype
{
    [Serializable]
    public sealed class ResearchMapPlacement
    {
        public string id;
        public ResearchDefinition definition;
        public Vector2 position;
        public ResearchCost[] costs = Array.Empty<ResearchCost>();
        public List<string> prerequisites = new List<string>();
        public int requiredPrerequisites;
    }

    [CreateAssetMenu(menuName = "Cursor Hunter/Research/Map Version")]
    public sealed class ResearchMapDefinition : ScriptableObject
    {
        public string versionId;
        public string description;
        [HideInInspector] public bool isolatedTest;
        public List<ResearchMapPlacement> nodes = new List<ResearchMapPlacement>();

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(versionId)) errors.Add("버전 이름을 입력해 주세요.");
            if (nodes == null || nodes.Count == 0) { errors.Add("노드를 하나 이상 추가해 주세요."); return errors; }
            var byId = new Dictionary<string, ResearchMapPlacement>(StringComparer.Ordinal);
            var uniqueEffects = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null) { errors.Add("비어 있는 노드 데이터가 있습니다."); continue; }
                if (string.IsNullOrWhiteSpace(node.id) || byId.ContainsKey(node.id))
                { errors.Add("노드 ID가 비어 있거나 중복됩니다: " + node.id); continue; }
                byId.Add(node.id, node);
                if (node.definition == null) { errors.Add(node.id + ": 연구가 지정되지 않았습니다. 노드를 우클릭하고 '연구 주입'에서 연구를 선택해 주세요."); continue; }
                if (!node.definition.TryValidate(out _)) errors.Add(node.id + ": 연구 SO의 표시 정보 또는 효과를 확인해 주세요.");
                var game = node.definition.gameplay;
                if (game != null && (game.kind == ResearchEffectKind.SkillUnlock || game.kind == ResearchEffectKind.MonsterUnlock ||
                    game.kind == ResearchEffectKind.MonsterProduction || game.kind == ResearchEffectKind.FieldDuration) &&
                    !uniqueEffects.Add(game.kind + ":" + game.targetId))
                    errors.Add(node.id + ": 같은 대상의 해금 또는 설정 연구는 한 번만 배치할 수 있습니다.");
                if (node.costs == null || node.costs.Length != node.definition.ranks.Length)
                    errors.Add(node.id + $": 비용 행이 {node.costs?.Length ?? 0}개입니다. 최대 레벨 {node.definition.ranks.Length}개에 맞게 연구 종류를 다시 선택하고 비용을 설정해 주세요.");
                else for (int i = 0; i < node.costs.Length; i++)
                {
                    if (!ResearchDefinition.IsSupportedCurrency(node.costs[i].currencyId))
                        errors.Add(node.id + $": {i + 1}레벨의 재화를 선택해 주세요. 무료라도 재화를 선택하고 비용을 0으로 입력해야 합니다.");
                    if (node.costs[i].amount < 0)
                        errors.Add(node.id + $": {i + 1}레벨 비용이 {node.costs[i].amount}입니다. 0 이상의 수량을 입력해 주세요.");
                }
            }
            foreach (var node in byId.Values)
            {
                var dependencies = node.prerequisites ?? new List<string>();
                if (node.requiredPrerequisites < (dependencies.Count == 0 ? 0 : 1) || node.requiredPrerequisites > dependencies.Count)
                    errors.Add(node.id + $": 선행 노드 {dependencies.Count}개 중 필요한 수가 {node.requiredPrerequisites}개입니다. 연결이 있으면 1~{dependencies.Count}, 없으면 0으로 설정해 주세요.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in dependencies)
                {
                    if (string.IsNullOrWhiteSpace(id)) errors.Add(node.id + ": 선행 노드가 지정되지 않은 연결을 삭제해 주세요.");
                    else if (id == node.id) errors.Add(node.id + ": 자기 자신으로 연결할 수 없습니다. 해당 연결을 삭제해 주세요.");
                    else if (!byId.ContainsKey(id)) errors.Add(node.id + ": 선행 노드 '" + id + "'를 찾을 수 없습니다. 해당 연결을 삭제해 주세요.");
                    else if (!seen.Add(id)) errors.Add(node.id + ": 선행 노드 '" + id + "'에 중복 연결되어 있습니다. 중복 연결을 삭제해 주세요.");
                }
            }
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            var path = new List<string>();
            List<string> cycle = null;
            bool Visit(string id)
            {
                if (visited.Contains(id)) return false;
                if (!visiting.Add(id)) { int index = path.IndexOf(id); cycle = path.Skip(index).Concat(new[] { id }).ToList(); return true; }
                path.Add(id);
                foreach (string prerequisite in byId[id].prerequisites ?? new List<string>())
                    if (prerequisite != null && byId.ContainsKey(prerequisite) && Visit(prerequisite)) return true;
                path.RemoveAt(path.Count - 1); visiting.Remove(id); visited.Add(id); return false;
            }
            if (byId.Keys.Any(Visit)) errors.Add("선행 연결이 순환합니다: " + string.Join(" → ", cycle) + ". 연결을 하나 이상 삭제해 주세요.");
            foreach (var node in byId.Values)
            {
                var game = node.definition?.gameplay;
                if (game == null) continue;
                ResearchEffectKind unlockKind;
                if (game.kind == ResearchEffectKind.SkillStat) unlockKind = ResearchEffectKind.SkillUnlock;
                else if (game.kind == ResearchEffectKind.MonsterProduction && game.targetId != "monster.01") unlockKind = ResearchEffectKind.MonsterUnlock;
                else continue;
                var unlock = byId.Values.FirstOrDefault(x => x.definition?.gameplay != null &&
                    x.definition.gameplay.kind == unlockKind && x.definition.gameplay.targetId == game.targetId);
                if (unlock == null)
                {
                    errors.Add(node.id + ": 대상 해금 연구가 없습니다. 해당 스킬 또는 몬스터 해금 노드를 배치해 주세요.");
                    continue;
                }
                var visitingWithoutUnlock = new HashSet<string>();
                var memo = new Dictionary<string, bool>();
                bool ReachableWithoutUnlock(string id)
                {
                    if (id == unlock.id || !byId.TryGetValue(id, out var current)) return false;
                    if (memo.TryGetValue(id, out bool cached)) return cached;
                    if (!visitingWithoutUnlock.Add(id)) return false;
                    var prerequisites = current.prerequisites ?? new List<string>();
                    int count = prerequisites.Count(ReachableWithoutUnlock);
                    visitingWithoutUnlock.Remove(id);
                    bool reachable = prerequisites.Count == 0 || count >= current.requiredPrerequisites;
                    memo[id] = reachable;
                    return reachable;
                }
                if (ReachableWithoutUnlock(node.id))
                    errors.Add(node.id + ": 대상 해금 연구를 구매하지 않고 도달할 수 있습니다. 해금 노드가 필수 선행 조건이 되도록 연결해 주세요.");
            }
            return errors;
        }
    }
}
