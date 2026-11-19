using System;
using System.Collections.Generic;

namespace CursorHunter.Progression
{
    [Serializable]
    public sealed class TraitCardProgressData
    {
        public long points;
        public bool revealed;
        public List<string> ownedIds = new List<string>();
        public List<string> candidateIds = new List<string>();

        public TraitCardProgressData Copy() => new TraitCardProgressData
        {
            points = points, revealed = revealed,
            ownedIds = new List<string>(ownedIds ?? new List<string>()),
            candidateIds = new List<string>(candidateIds ?? new List<string>())
        };
    }

    /// <summary>New, non-repeatable card draft. Save the complete next state before publishing it.</summary>
    public sealed class TraitCardProgress
    {
        private readonly List<string> catalog;
        private readonly Func<int, int> randomIndex;
        private readonly Func<TraitCardProgressData, bool> save;
        private TraitCardProgressData state;

        public TraitCardProgress(IEnumerable<string> ids, TraitCardProgressData initial,
            Func<int, int> randomIndex, Func<TraitCardProgressData, bool> save)
        {
            catalog = new List<string>(ids ?? throw new ArgumentNullException(nameof(ids)));
            if (catalog.Count == 0 || !Unique(catalog)) throw new ArgumentException("Invalid trait card catalog.");
            this.randomIndex = randomIndex ?? throw new ArgumentNullException(nameof(randomIndex));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            Restore(initial ?? new TraitCardProgressData());
        }

        public long Points => state.points;
        public int Level => state.ownedIds.Count;
        public long RevealCost => (long)Level + 1;
        public bool Revealed => state.revealed;
        public bool Complete => state.ownedIds.Count == catalog.Count;
        public bool CanReveal => !Complete && !Revealed && state.candidateIds.Count > 0 && Points >= RevealCost;
        public IReadOnlyList<string> OwnedIds => state.ownedIds.AsReadOnly();
        public IReadOnlyList<string> CandidateIds => state.candidateIds.AsReadOnly();
        public TraitCardProgressData Capture() => state.Copy();

        public void Restore(TraitCardProgressData value)
        {
            var next = value.Copy();
            if (next.points < 0 || !Unique(next.ownedIds) || !Unique(next.candidateIds) ||
                next.candidateIds.Count > 3 || (next.revealed && next.candidateIds.Count == 0))
                throw new ArgumentException("Invalid trait card progress.");
            foreach (string id in next.ownedIds)
                if (!catalog.Contains(id)) throw new ArgumentException("Missing owned trait card: " + id);
            foreach (string id in next.candidateIds)
                if (!catalog.Contains(id) || next.ownedIds.Contains(id))
                    throw new ArgumentException("Invalid trait card candidate: " + id);
            state = next;
        }

        // Called inside the existing experience/reward transaction, which saves or rolls this back.
        public void AddLevelPoints(int levels)
        {
            if (levels <= 0) return;
            state.points = state.points > long.MaxValue - levels ? long.MaxValue : state.points + levels;
        }

        // Called as part of a larger saved cheat transaction; the caller rolls back on failure.
        internal void SetPointsForTesting(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            state.points = value;
        }

        public bool Prepare()
        {
            if (Complete || state.candidateIds.Count > 0) return true;
            var next = state.Copy();
            Deal(next);
            return Commit(next);
        }

        public bool Reveal()
        {
            if (!CanReveal) return false;
            var next = state.Copy();
            next.points -= RevealCost;
            next.revealed = true;
            return Commit(next);
        }

        public bool Select(string id)
        {
            if (!Revealed || !state.candidateIds.Contains(id) || state.ownedIds.Contains(id)) return false;
            var next = state.Copy();
            next.ownedIds.Add(id);
            next.revealed = false;
            Deal(next);
            return Commit(next);
        }

        public bool Reset()
        {
            var next = new TraitCardProgressData();
            Deal(next);
            return Commit(next);
        }

        private void Deal(TraitCardProgressData next)
        {
            var pool = new List<string>(catalog);
            pool.RemoveAll(next.ownedIds.Contains);
            next.candidateIds.Clear();
            while (pool.Count > 0 && next.candidateIds.Count < 3)
            {
                int index = randomIndex(pool.Count);
                if (index < 0 || index >= pool.Count) throw new InvalidOperationException("Invalid random index.");
                next.candidateIds.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        private bool Commit(TraitCardProgressData next)
        {
            if (!save(next.Copy())) return false;
            state = next;
            return true;
        }

        private static bool Unique(List<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return false;
            return true;
        }
    }
}
