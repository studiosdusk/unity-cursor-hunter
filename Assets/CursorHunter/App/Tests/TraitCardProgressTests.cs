using System;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class TraitCardProgressTests
    {
        private static readonly string[] Ids = { "a", "b", "c", "d" };

        [Test]
        public void AllFourCardsCostTenPointsAndExhaustWithoutDuplicates()
        {
            var progress = new TraitCardProgress(Ids, new TraitCardProgressData { points = 10 }, _ => 0, _ => true);
            Assert.That(progress.Prepare(), Is.True);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(progress.Level, Is.EqualTo(i));
                Assert.That(progress.RevealCost, Is.EqualTo(i + 1));
                Assert.That(progress.CandidateIds.Count, Is.EqualTo(Math.Min(3, 4 - i)));
                Assert.That(progress.CandidateIds.Distinct().Count(), Is.EqualTo(progress.CandidateIds.Count));
                Assert.That(progress.CandidateIds.Intersect(progress.OwnedIds), Is.Empty);
                Assert.That(progress.Select(progress.CandidateIds[0]), Is.False, "Locked cards cannot be selected.");
                Assert.That(progress.Reveal(), Is.True);
                long remaining = progress.Points;
                Assert.That(progress.Reveal(), Is.False, "Reveal only spends once.");
                Assert.That(progress.Level, Is.EqualTo(i));
                string id = progress.CandidateIds[0];
                Assert.That(progress.Select(id), Is.True);
                Assert.That(progress.Points, Is.EqualTo(remaining), "Selection is free.");
                Assert.That(progress.Select(id), Is.False);
            }
            Assert.That(progress.Complete, Is.True);
            Assert.That(progress.Points, Is.Zero);
            Assert.That(progress.CandidateIds, Is.Empty);
            Assert.That(progress.Reveal(), Is.False);
        }

        [Test]
        public void FailedSaveLeavesBothRevealAndSelectionUnchanged()
        {
            bool fail = true;
            var progress = new TraitCardProgress(Ids, new TraitCardProgressData { points = 2 }, _ => 0, _ => !fail);
            Assert.That(progress.Prepare(), Is.False);
            Assert.That(progress.CandidateIds, Is.Empty);
            fail = false; Assert.That(progress.Prepare(), Is.True);
            fail = true; Assert.That(progress.Reveal(), Is.False);
            Assert.That(progress.Points, Is.EqualTo(2));
            Assert.That(progress.Revealed, Is.False);
            fail = false; Assert.That(progress.Reveal(), Is.True);
            var candidates = progress.CandidateIds.ToArray();
            fail = true; Assert.That(progress.Select(candidates[0]), Is.False);
            Assert.That(progress.Level, Is.Zero);
            Assert.That(progress.Points, Is.EqualTo(1));
            Assert.That(progress.Revealed, Is.True);
            Assert.That(progress.CandidateIds, Is.EqualTo(candidates));
        }

        [Test]
        public void RestoringRevealedDraftNeverChargesOrRerolls()
        {
            TraitCardProgressData saved = null;
            var first = new TraitCardProgress(Ids, new TraitCardProgressData { points = 3 }, _ => 0,
                next => { saved = next; return true; });
            first.Prepare(); first.Reveal();
            var restored = new TraitCardProgress(Ids, JsonUtility.FromJson<TraitCardProgressData>(JsonUtility.ToJson(saved)),
                _ => throw new Exception("Must not reroll a saved draft."), _ => true);
            Assert.That(restored.Prepare(), Is.True);
            Assert.That(restored.Revealed, Is.True);
            Assert.That(restored.Points, Is.EqualTo(2));
            Assert.That(restored.CandidateIds, Is.EqualTo(first.CandidateIds));
            Assert.That(restored.Reveal(), Is.False);
        }

        [Test]
        public void InsufficientPointsAndUnknownSelectionDoNotSave()
        {
            int writes = 0;
            var progress = new TraitCardProgress(Ids, new TraitCardProgressData(), _ => 0,
                _ => { writes++; return true; });
            progress.Prepare();
            Assert.That(progress.Reveal(), Is.False);
            progress.AddLevelPoints(3);
            Assert.That(progress.Reveal(), Is.True);
            Assert.That(progress.Points, Is.EqualTo(2));
            Assert.That(progress.Select("not-in-draft"), Is.False);
            Assert.That(writes, Is.EqualTo(2));
        }

        [Test]
        public void UnselectedCardCanReappearButSelectedCardCannot()
        {
            var progress = new TraitCardProgress(Ids, new TraitCardProgressData { points = 4 }, _ => 0, _ => true);
            progress.Prepare(); progress.Reveal(); progress.Select("a");
            Assert.That(progress.CandidateIds, Is.EquivalentTo(new[] { "b", "c", "d" }));
        }

        [Test]
        public void ResetClearsPointsAndOwnershipAndPersistsANewLockedDraft()
        {
            TraitCardProgressData saved = null;
            var progress = new TraitCardProgress(Ids, new TraitCardProgressData
            {
                points = 12, ownedIds = new List<string>(Ids)
            }, _ => 0, next => { saved = next; return true; });

            Assert.That(progress.Reset(), Is.True);
            Assert.That(progress.Points, Is.Zero);
            Assert.That(progress.Level, Is.Zero);
            Assert.That(progress.RevealCost, Is.EqualTo(1));
            Assert.That(progress.Revealed, Is.False);
            Assert.That(progress.Complete, Is.False);
            Assert.That(progress.CanReveal, Is.False);
            Assert.That(progress.CandidateIds, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(JsonUtility.ToJson(progress.Capture())));
            Assert.That(progress.Reset(), Is.True);
            Assert.That(progress.Points, Is.Zero, "Repeating a reset must not refund points.");
        }

        [Test]
        public void FailedResetPreservesThePaidDraftAndOwnedCards()
        {
            var progress = new TraitCardProgress(Ids, new TraitCardProgressData
            {
                points = 5, revealed = true,
                ownedIds = new List<string> { "a" }, candidateIds = new List<string> { "b", "c", "d" }
            }, _ => 0, _ => false);
            string original = JsonUtility.ToJson(progress.Capture());
            Assert.That(progress.Reset(), Is.False);
            Assert.That(JsonUtility.ToJson(progress.Capture()), Is.EqualTo(original));
        }

        [Test]
        public void InvalidSavedDraftIsRejectedInsteadOfSilentlyReplaced()
        {
            Assert.Throws<ArgumentException>(() => new TraitCardProgress(Ids,
                new TraitCardProgressData { ownedIds = new List<string> { "a" }, candidateIds = new List<string> { "a" } },
                _ => 0, _ => true));
        }
    }
}
