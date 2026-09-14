using System;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CursorHunter.App.Tests
{
    public sealed class RunSessionTests
    {
        [Test]
        public void NewSessionStartsIdle()
        {
            RunSession session = new RunSession();

            Assert.That(session.State, Is.EqualTo(RunState.Idle));
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void StartEntersStartingUntilRuntimeParticipantsCommit()
        {
            RunSession session = new RunSession();

            Assert.That(session.Start(CreateRequest("run-start")), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Starting));
            Assert.That(session.IsActive, Is.True);

            Assert.That(session.CommitStart(), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Running));
        }

        [Test]
        public void StartCannotBeCalledTwiceForOneSession()
        {
            RunSession session = new RunSession();
            RunRequest request = CreateRequest("run-duplicate");

            Assert.That(session.Start(request), Is.True);
            Assert.That(session.Start(request), Is.False);
            Assert.That(session.CommitStart(), Is.True);
            Assert.That(session.Start(request), Is.False);
        }

        [Test]
        public void PauseAndResumeOnlyWorkFromTheirMatchingStates()
        {
            RunSession session = StartRunning("run-pause");

            Assert.That(session.Pause(), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Paused));
            Assert.That(session.Pause(), Is.False);

            Assert.That(session.Resume(), Is.True);
            Assert.That(session.State, Is.EqualTo(RunState.Running));
            Assert.That(session.Resume(), Is.False);
        }

        [Test]
        public void CompleteAndAbortAreMutuallyExclusive()
        {
            RunSession completed = StartRunning("run-complete");
            int completedEvents = 0;
            int abortedEvents = 0;
            completed.Completed += () => completedEvents++;
            completed.Aborted += () => abortedEvents++;

            Assert.That(
                completed.Complete(
                    RunEndReason.TimeExpired,
                    RunSettlementPolicy.Eligible),
                Is.True);
            Assert.That(completed.State, Is.EqualTo(RunState.Completed));
            Assert.That(completed.Abort(
                    RunEndReason.UserExit,
                    RunSettlementPolicy.Eligible),
                Is.False);
            Assert.That(completedEvents, Is.EqualTo(1));
            Assert.That(abortedEvents, Is.EqualTo(0));

            RunSession aborted = StartRunning("run-abort");
            completedEvents = 0;
            abortedEvents = 0;
            aborted.Completed += () => completedEvents++;
            aborted.Aborted += () => abortedEvents++;

            Assert.That(
                aborted.Abort(
                    RunEndReason.UserExit,
                    RunSettlementPolicy.Eligible),
                Is.True);
            Assert.That(aborted.State, Is.EqualTo(RunState.Aborted));
            Assert.That(aborted.Complete(
                    RunEndReason.TimeExpired,
                    RunSettlementPolicy.Eligible),
                Is.False);
            Assert.That(completedEvents, Is.EqualTo(0));
            Assert.That(abortedEvents, Is.EqualTo(1));
        }

        [Test]
        public void ResetAbortsWithoutPublishingCompleted()
        {
            RunSession session = StartRunning("run-reset");
            int completedEvents = 0;
            int abortedEvents = 0;
            session.Completed += () => completedEvents++;
            session.Aborted += () => abortedEvents++;

            Assert.That(
                session.Abort(
                    RunEndReason.Reset,
                    RunSettlementPolicy.Discard),
                Is.True);

            Assert.That(session.State, Is.EqualTo(RunState.Aborted));
            Assert.That(session.EndReason, Is.EqualTo(RunEndReason.Reset));
            Assert.That(
                session.SettlementPolicy,
                Is.EqualTo(RunSettlementPolicy.Discard));
            Assert.That(completedEvents, Is.EqualTo(0));
            Assert.That(abortedEvents, Is.EqualTo(1));
        }

        [Test]
        public void TerminalSessionCannotBeStartedAgain()
        {
            RunSession session = StartRunning("run-terminal");
            Assert.That(
                session.Complete(
                    RunEndReason.ManualComplete,
                    RunSettlementPolicy.Eligible),
                Is.True);

            Assert.That(session.Start(CreateRequest("run-new")), Is.False);
        }

        [Test]
        public void RequestCarriesIdentityVersionsModeBossAndSeed()
        {
            RunId runId = new RunId("run-contract");
            RunRequest request = new RunRequest(
                runId,
                3,
                11,
                RunMode.Boss,
                "boss.first",
                123456789UL,
                60f);

            Assert.That(request.IsValid, Is.True);
            Assert.That(request.RunId, Is.EqualTo(runId));
            Assert.That(request.SchemaVersion, Is.EqualTo(3));
            Assert.That(request.BalanceVersion, Is.EqualTo(11));
            Assert.That(request.Mode, Is.EqualTo(RunMode.Boss));
            Assert.That(request.BossId, Is.EqualTo("boss.first"));
            Assert.That(request.Seed, Is.EqualTo(123456789UL));
            Assert.That(request.DurationSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void RunIdFactoryProducesDistinctIds()
        {
            RunId first = RunId.Create();
            RunId second = RunId.Create();

            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void RunIdRegistryRejectsDuplicateLogicalRuns()
        {
            RunIdRegistry registry = new RunIdRegistry();
            RunId runId = new RunId("run-registry");

            Assert.That(registry.TryClaim(runId), Is.True);
            Assert.That(registry.TryClaim(runId), Is.False);
            Assert.That(registry.Contains(runId), Is.True);
        }

        [Test]
        public void CoordinatorRollsBackCombatWhenSpawnerCannotStart()
        {
            GameObject gameObject = new GameObject("RunCoordinatorRollbackTest");
            RunCoordinator coordinator = null;
            try
            {
                CombatRunController combat =
                    gameObject.AddComponent<CombatRunController>();
                MonsterSpawner spawner =
                    gameObject.AddComponent<MonsterSpawner>();
                coordinator = new RunCoordinator(combat, spawner);

                RunRequest request = CreateRequest("run-rollback");
                bool started = coordinator.Start(
                    request,
                    new CombatSnapshot(10L, 1f, 0f, 1),
                    new SpawnPlan(
                        new SpawnSnapshot(
                            "monster.slime",
                            "missing-prefab",
                            30L,
                            1.5f,
                            1,
                            1,
                            3L),
                        null),
                    out string failureReason);

                Assert.That(started, Is.False);
                Assert.That(failureReason, Is.Not.Empty);
                Assert.That(combat.IsRunActive, Is.False);
                Assert.That(combat.IsRunning, Is.False);
                Assert.That(coordinator.State, Is.EqualTo(RunState.Aborted));

                Assert.That(
                    coordinator.Start(
                        request,
                        new CombatSnapshot(10L, 1f, 0f, 1),
                        new SpawnPlan(
                            new SpawnSnapshot(
                                "monster.slime",
                                "missing-prefab",
                                30L,
                                1.5f,
                                1,
                                1,
                                3L),
                            null),
                        out failureReason),
                    Is.False);
                Assert.That(failureReason, Does.Contain("already been used"));
            }
            finally
            {
                coordinator?.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TargetRejectsForeignRunDamageAndUnregistersImmediately()
        {
            GameObject gameObject = new GameObject("RunTargetOwnershipTest");
            try
            {
                WalkerStumpTarget target =
                    gameObject.AddComponent<WalkerStumpTarget>();
                RunId ownerRunId = new RunId("run-owner");
                RunId foreignRunId = new RunId("run-foreign");

                target.Initialize(
                    new SpawnSnapshot(
                        "monster.slime",
                        "walker_stump",
                        30L,
                        1.5f,
                        1,
                        1,
                        3L),
                    ownerRunId);

                Assert.That(target.IsRegistered, Is.True);
                Assert.That(target.IsActive, Is.True);
                Assert.That(target.BelongsTo(ownerRunId), Is.True);
                Assert.That(target.BelongsTo(foreignRunId), Is.False);

                Assert.That(
                    target.ApplyDamage(
                        foreignRunId,
                        10L,
                        out _,
                        out _),
                    Is.False);
                Assert.That(target.CurrentHealth, Is.EqualTo(30L));

                Assert.That(
                    target.ApplyDamage(
                        ownerRunId,
                        10L,
                        out long effectiveDamage,
                        out bool killed),
                    Is.True);
                Assert.That(effectiveDamage, Is.EqualTo(10L));
                Assert.That(killed, Is.False);

                target.Unregister();
                Assert.That(target.IsRegistered, Is.False);
                Assert.That(target.IsActive, Is.False);
                Assert.That(
                    target.ApplyDamage(
                        ownerRunId,
                        10L,
                        out _,
                        out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SpawnerReturnsAStableStatusForAnInvalidRequest()
        {
            GameObject gameObject = new GameObject("SpawnerStartResultTest");
            try
            {
                gameObject.AddComponent<CombatRunController>();
                MonsterSpawner spawner = gameObject.AddComponent<MonsterSpawner>();

                SpawnStartResult result = spawner.StartRun(
                    CreateRequest("run-invalid-spawn"),
                    null);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(
                    result.Status,
                    Is.EqualTo(SpawnStartStatus.InvalidRequest));
                Assert.That(result.Message, Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TargetAdapterSupportsNestedAnimatorAndMissingCollider()
        {
            GameObject root = new GameObject("GenericMonsterRoot");
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform);
            visual.AddComponent<SpriteRenderer>();
            visual.AddComponent<Animator>();

            try
            {
                WalkerStumpTarget target =
                    root.AddComponent<WalkerStumpTarget>();
                target.Initialize(
                    new SpawnSnapshot(
                        "monster.generic",
                        "generic_prefab",
                        30L,
                        1f,
                        1,
                        1,
                        1L),
                    new RunId("run-generic-target"));

                Collider2D collider = target.EnsureCombatCollider();

                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.gameObject, Is.SameAs(root));
                Assert.That(target.IsActive, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void AuthoredMonsterPrefabsCanUseTheGenericTargetAdapter()
        {
            MonsterDefinition slimeDefinition =
                AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                    "Assets/CursorHunter/Data/Resources/MonsterDefinitions/SlimeDefinition.asset");
            if (slimeDefinition == null)
            {
                Assert.Ignore("Slime MonsterDefinition is not imported.");
            }

            Assert.That(slimeDefinition.Prefab, Is.Not.Null);

            const string prefabRoot =
                "Assets/DownLoadAssets/MonsterAsset/2D Minimal-EnemyMonster/" +
                "EnemyMonster 2/Prefabs";
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { prefabRoot });
            Array.Sort(prefabGuids, StringComparer.Ordinal);

            if (prefabGuids.Length == 0)
            {
                Assert.Ignore("Authored monster prefabs are not imported.");
            }

            foreach (string prefabGuid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Assert.Ignore(
                        $"Authored monster prefab is not imported: {prefabPath}");
                }

                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    WalkerStumpTarget target =
                        instance.AddComponent<WalkerStumpTarget>();
                    RunId runId = new RunId("run-prefab-adapter");
                    target.Initialize(
                        new SpawnSnapshot(
                            "monster.generic",
                            "generic_prefab",
                            30L,
                            1f,
                            1,
                            1,
                            1L),
                        runId);

                    Assert.That(target.IsActive, Is.True, prefabPath);
                    Assert.That(
                        target.EnsureCombatCollider(),
                        Is.Not.Null,
                        prefabPath);
                    Assert.That(
                        target.ApplyDamage(
                            runId,
                            1L,
                            out _,
                            out bool hitKilled),
                        Is.True,
                        prefabPath);
                    Assert.That(hitKilled, Is.False, prefabPath);
                    Assert.That(
                        target.ApplyDamage(
                            runId,
                            29L,
                            out _,
                            out bool killed),
                        Is.True,
                        prefabPath);
                    Assert.That(killed, Is.True, prefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }
#endif

        [Test]
        public void RunResultRejectsNegativeCountersInsteadOfNormalizingThem()
        {
            RunRequest request = CreateRequest("run-counter");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RunResult(
                    request,
                    RunEndReason.NumericOverflow,
                    RunSettlementPolicy.Discard,
                    1f,
                    0,
                    -1L,
                    0L));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RunResult(
                    request,
                    RunEndReason.NumericOverflow,
                    RunSettlementPolicy.Discard,
                    1f,
                    0,
                    0L,
                    -1L));
        }

        [Test]
        public void ExactDurationRequestsCompletionAndStopsRunning()
        {
            GameObject gameObject = new GameObject("CombatRunControllerTest");
            try
            {
                CombatRunController controller =
                    gameObject.AddComponent<CombatRunController>();
                int completionRequests = 0;
                RunEndReason requestedReason = RunEndReason.Unknown;
                controller.CompletionRequested += reason =>
                {
                    completionRequests++;
                    requestedReason = reason;
                };

                RunRequest request = CreateRequest("run-boundary", 1f);
                Assert.That(
                    controller.StartRun(
                        request,
                        new CombatSnapshot(10L, 1f, 0f, 1)),
                    Is.True);

                controller.AdvanceTime(0.999f);
                Assert.That(controller.IsRunning, Is.True);
                Assert.That(completionRequests, Is.EqualTo(0));

                controller.AdvanceTime(0.001f);
                Assert.That(controller.IsRunning, Is.False);
                Assert.That(completionRequests, Is.EqualTo(1));
                Assert.That(
                    requestedReason,
                    Is.EqualTo(RunEndReason.TimeExpired));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PausedCombatClockDoesNotAdvanceUntilResumed()
        {
            GameObject gameObject = new GameObject("CombatPauseTest");
            try
            {
                CombatRunController controller =
                    gameObject.AddComponent<CombatRunController>();
                int completionRequests = 0;
                controller.CompletionRequested += _ => completionRequests++;

                RunRequest request = CreateRequest("run-pause-clock", 1f);
                Assert.That(
                    controller.StartRun(
                        request,
                        new CombatSnapshot(10L, 1f, 0f, 1)),
                    Is.True);

                controller.AdvanceTime(0.5f);
                Assert.That(controller.ElapsedSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(controller.PauseRun(), Is.True);

                controller.AdvanceTime(1f);
                Assert.That(controller.ElapsedSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(completionRequests, Is.EqualTo(0));

                Assert.That(controller.ResumeRun(), Is.True);
                controller.AdvanceTime(0.5f);
                Assert.That(completionRequests, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static RunSession StartRunning(string runId)
        {
            RunSession session = new RunSession();
            Assert.That(session.Start(CreateRequest(runId)), Is.True);
            Assert.That(session.CommitStart(), Is.True);
            return session;
        }

        private static RunRequest CreateRequest(
            string runId,
            float durationSeconds = 60f)
        {
            return new RunRequest(
                new RunId(runId),
                1,
                1,
                RunMode.NormalField,
                string.Empty,
                42UL,
                durationSeconds);
        }
    }

    public sealed class SeededRandomTests
    {
        [Test]
        public void SameSeedProducesTheSameSequence()
        {
            SeededRandom first = new SeededRandom(99UL);
            SeededRandom second = new SeededRandom(99UL);

            for (int index = 0; index < 20; index++)
            {
                Assert.That(
                    first.NextFloat(-1f, 1f),
                    Is.EqualTo(second.NextFloat(-1f, 1f)));
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentSequences()
        {
            SeededRandom first = new SeededRandom(99UL);
            SeededRandom second = new SeededRandom(100UL);
            bool anyDifferent = false;

            for (int index = 0; index < 20; index++)
            {
                if (first.NextFloat(0f, 1f) != second.NextFloat(0f, 1f))
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.That(anyDifferent, Is.True);
        }
    }
}
