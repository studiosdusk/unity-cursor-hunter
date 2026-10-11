using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Standalone visual/load check using fixed snapshots, with no App or save services.</summary>
    [RequireComponent(typeof(CombatRunController))]
    public sealed class CursorHitEffectSandbox : MonoBehaviour
    {
        [SerializeField] private CursorHitEffectManager effectPrefab;
        [SerializeField] private MonsterCombatTarget monsterPrefab;
        private CombatRunController _combat;
        private CursorHitEffectManager _effects;
        private Transform _targets;
        private CircleCollider2D _attack;
        private Sprite _bodySprite;
        private int _runSequence;
        private int _targetCount = 8;
        private bool _autoHit = true;

        private void Start()
        {
            _combat = GetComponent<CombatRunController>();
            _effects = Instantiate(effectPrefab, transform);
            _bodySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            var attackObject = new GameObject("SandboxAttackArea");
            attackObject.transform.SetParent(transform, false);
            _attack = attackObject.AddComponent<CircleCollider2D>();
            _attack.isTrigger = true;
            _attack.radius = 20f;
            Restart();
        }

        private void Restart()
        {
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            if (_targets != null)
            {
                _targets.gameObject.SetActive(false);
                Destroy(_targets.gameObject);
            }
            _targets = new GameObject("SandboxTargets").transform;
            _targets.SetParent(transform, false);
            float cooldown = _targetCount == 80 ? 0.05f : 0.8f;
            _combat.StartRun(new RunRequest("hit-sandbox-" + ++_runSequence, 30f),
                new CombatSnapshot(1L, 1f, cooldown, 1, 25f, 1f, true, cooldown));
            int columns = _targetCount == 80 ? 10 : 4;
            for (int i = 0; i < _targetCount; i++)
            {
                MonsterCombatTarget target = Instantiate(monsterPrefab, _targets);
                target.transform.position = _targetCount == 80
                    ? new Vector3(-5.4f + i % columns * 1.2f, -4f + i / columns, 0f)
                    : new Vector3(-3f + i % columns * 2f, -1.5f + i / columns * 2f, 0f);
                var body = new GameObject("SandboxBody");
                body.transform.SetParent(target.transform.Find("VisualRoot"), false);
                body.transform.localPosition = Vector3.up * 0.5f;
                body.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
                var renderer = body.AddComponent<SpriteRenderer>();
                renderer.sprite = _bodySprite;
                renderer.color = new Color(0.35f, 0.45f, 0.4f);
                target.Initialize(new SpawnSnapshot("sandbox", "sandbox", 100000L, 1f, 1, 80, 0L),
                    _combat.CurrentRunId);
            }
        }

        private void LateUpdate()
        {
            if (_autoHit && _combat != null) _combat.TryAttack(_attack, false);
        }

        private void OnGUI()
        {
            if (_combat == null) return;
            GUILayout.BeginArea(new Rect(12f, 12f, 430f, 180f), GUI.skin.box);
            GUILayout.Label("Cursor hit VFX | fixed snapshot | no save data");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("8 targets")) { _targetCount = 8; Restart(); }
            if (GUILayout.Button("80 targets / 0.05s")) { _targetCount = 80; Restart(); }
            if (GUILayout.Button("Restart")) Restart();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _autoHit = GUILayout.Toggle(_autoHit, "Auto hit");
            if (GUILayout.Button("Hit once")) _combat.TryAttack(_attack, false);
            if (GUILayout.Button(_combat.IsPaused ? "Resume" : "Pause"))
            {
                if (_combat.IsPaused) _combat.ResumeRun();
                else _combat.PauseRun();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Active {_effects.ActiveCount}/256 | Peak {_effects.PeakActiveCount}");
            GUILayout.Label($"Emitted {_effects.EmittedCount} | Suppressed {_effects.SuppressedCount}");
            GUILayout.Label($"Remaining {_combat.RemainingSeconds:0.0}s");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (_bodySprite != null) Destroy(_bodySprite);
        }
    }
}
