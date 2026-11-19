using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.App
{
    public enum CheatAction
    {
        ResetTraitCards = 0,
        ResetResearch = 1,
        ResetRelics = 2,
        AddAllCurrencies = 3,
        ClearAllCurrencies = 4,
        IncreaseUserLevel = 5,
        ResetAllProgress = 6
    }

    [Serializable]
    public sealed class CheatRow
    {
        [Tooltip("고유 ID. 중복되거나 비어 있으면 버튼을 표시하지 않습니다.")] public string id;
        public CheatAction action;
        [Min(0), Tooltip("모든 재화 지급 동작에서 각 잔액에 더할 수량.")] public long amount;
        public bool enabled = true;
    }

    /// <summary>Inspector-editable ordered table of available test cheats.</summary>
    [CreateAssetMenu(menuName = "Cursor Hunter/Test/Cheat Table")]
    public sealed class CheatTable : ScriptableObject
    {
        [SerializeField] private List<CheatRow> entries = new List<CheatRow>();
        public IReadOnlyList<CheatRow> Entries => entries;
    }
}
