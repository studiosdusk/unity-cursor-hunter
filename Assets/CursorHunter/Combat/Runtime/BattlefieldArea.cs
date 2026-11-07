using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>The walkable top of the map, in map-local coordinates. All spatial
    /// multipliers are presentation/runtime conversions; snapshots stay unscaled.</summary>
    [DisallowMultipleComponent]
    public sealed class BattlefieldArea : MonoBehaviour
    {
        [SerializeField] private Vector2 halfSize = new Vector2(18.382f, 10.4895f);
        [SerializeField, Min(0f)] private float groundMargin = 0.25f;
        [SerializeField, Range(0.1f, 1.5f)] private float monsterScale = 0.62f;
        [SerializeField, Range(0.1f, 1.5f)] private float golemScale = 0.5f;
        [SerializeField, Range(0.1f, 1.5f)] private float smallMonsterScale = 0.8f;
        [SerializeField, Range(0.1f, 1.5f)] private float cursorScale = 0.65f;
        [SerializeField, Range(0.1f, 1.5f)] private float skillScale = 0.65f;
        [SerializeField, Range(0.1f, 1.5f)] private float magicBallScale = 0.55f;
        [SerializeField, Range(0.1f, 1.5f)] private float petScale = 0.58f;
        [SerializeField, Range(0.1f, 1.5f)] private float motionScale = 0.7f;
        [SerializeField, Range(0.1f, 1.5f)] private float damageTextScale = 0.85f;

        private readonly Vector2[] _corners = new Vector2[4];
        private readonly Vector2[] _normals = new Vector2[4];
        private readonly float[] _distances = new float[4];
        private Matrix4x4 _matrix;
        private Vector2 _cachedHalfSize;
        private bool _cached;
        private Rect _bounds;

        public float CursorScale => cursorScale;
        public float SkillScale => skillScale;
        public float MagicBallScale => magicBallScale;
        public float PetScale => petScale;
        public float MotionScale => motionScale;
        public float GroundMargin => groundMargin;
        public float DamageTextScale => damageTextScale;
        public Rect WorldBounds { get { Refresh(); return _bounds; } }

        public float MonsterScale(string monsterId) => monsterId == "monster.08"
            ? golemScale : monsterId == "monster.02" ? smallMonsterScale : monsterScale;

        // Sprites remain upright. The footprint is at the feet, not at the head.
        public float MonsterInset(SpawnSnapshot snapshot) => groundMargin +
            snapshot.HitAreaWidth * MonsterScale(snapshot.MonsterId) * 0.5f;

        private void Refresh()
        {
            Matrix4x4 matrix = transform.localToWorldMatrix;
            if (_cached && matrix == _matrix && halfSize == _cachedHalfSize) return;
            _cached = true;
            _matrix = matrix;
            _cachedHalfSize = halfSize;
            _corners[0] = transform.TransformPoint(new Vector3(0f, halfSize.y));
            _corners[1] = transform.TransformPoint(new Vector3(halfSize.x, 0f));
            _corners[2] = transform.TransformPoint(new Vector3(0f, -halfSize.y));
            _corners[3] = transform.TransformPoint(new Vector3(-halfSize.x, 0f));
            Vector2 center = transform.position;
            Vector2 min = _corners[0], max = min;
            for (int i = 0; i < 4; i++)
            {
                Vector2 edge = _corners[(i + 1) % 4] - _corners[i];
                Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
                if (Vector2.Dot(normal, _corners[i] - center) < 0f) normal = -normal;
                _normals[i] = normal;
                _distances[i] = Vector2.Dot(normal, _corners[i]);
                min = Vector2.Min(min, _corners[i]);
                max = Vector2.Max(max, _corners[i]);
            }
            _bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public float Clearance(Vector2 point)
        {
            Refresh();
            float distance = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
                distance = Mathf.Min(distance, _distances[i] - Vector2.Dot(_normals[i], point));
            return distance;
        }

        public bool Fits(float inset) => Clearance(transform.position) > Mathf.Max(0f, inset) + 0.001f;
        public bool Contains(Vector2 point, float inset = 0f) => Clearance(point) >= inset - 0.0001f;

        private Vector2 InsetCorner(int index, float inset)
        {
            int previous = (index + 3) % 4;
            Vector2 a = _normals[previous], b = _normals[index];
            float da = _distances[previous] - inset, db = _distances[index] - inset;
            float determinant = a.x * b.y - a.y * b.x;
            if (Mathf.Abs(determinant) < 0.000001f) return transform.position;
            return new Vector2((da * b.y - a.y * db) / determinant,
                (a.x * db - da * b.x) / determinant);
        }

        public Vector2 Sample(float u, float v, float inset = 0f)
        {
            Refresh();
            if (!Fits(inset)) return transform.position;
            Vector2 top = InsetCorner(0, inset);
            return top + Mathf.Clamp01(u) * (InsetCorner(1, inset) - top) +
                Mathf.Clamp01(v) * (InsetCorner(3, inset) - top);
        }

        public Vector2 Clamp(Vector2 point, float inset = 0f)
        {
            Refresh();
            if (Contains(point, inset)) return point;
            if (!Fits(inset)) return transform.position;
            Vector2 closest = transform.position;
            float best = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = InsetCorner(i, inset), b = InsetCorner((i + 1) % 4, inset);
                Vector2 edge = b - a;
                Vector2 candidate = a + edge * Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
                float distance = (point - candidate).sqrMagnitude;
                if (distance < best) { best = distance; closest = candidate; }
            }
            return closest;
        }

        /// <summary>First wall along a segment, including a wall at its endpoint.</summary>
        public bool Trace(Vector2 start, Vector2 displacement, float inset,
            out float fraction, out Vector2 normal)
        {
            Refresh();
            fraction = 1f;
            normal = Vector2.zero;
            bool hit = false;
            for (int i = 0; i < 4; i++)
            {
                float approach = Vector2.Dot(_normals[i], displacement);
                if (approach <= 0.000001f) continue;
                float t = (_distances[i] - inset - Vector2.Dot(_normals[i], start)) / approach;
                if (t > 1f + 0.000001f) continue;
                t = Mathf.Max(0f, t);
                if (t < fraction - 0.000001f) { fraction = t; normal = _normals[i]; hit = true; }
                else if (Mathf.Abs(t - fraction) <= 0.000001f) { normal += _normals[i]; hit = true; }
            }
            normal.Normalize();
            return hit;
        }

        public Vector2 MoveReflected(Vector2 position, ref Vector2 velocity, float seconds, float inset)
        {
            position = Clamp(position, inset);
            if (!Fits(inset)) return position;
            for (int i = 0; i < 64 && seconds > 0.000001f; i++)
            {
                Vector2 delta = velocity * seconds;
                bool hit = Trace(position, delta, inset, out float t, out Vector2 normal);
                position += delta * t;
                if (!hit) break;
                velocity = Vector2.Reflect(velocity, normal);
                position = Clamp(position - normal * 0.0001f, inset);
                seconds *= 1f - t;
            }
            return Clamp(position, inset);
        }

        /// <summary>Vertical strip fully inside the four sloping edges.</summary>
        public bool TryGetVerticalSpan(float x, float halfWidth, out float minY, out float maxY)
        {
            Refresh();
            minY = float.NegativeInfinity;
            maxY = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 n = _normals[i];
                float available = _distances[i] - groundMargin - n.x * x - Mathf.Abs(n.x) * halfWidth;
                if (n.y > 0.000001f) maxY = Mathf.Min(maxY, available / n.y);
                else if (n.y < -0.000001f) minY = Mathf.Max(minY, available / n.y);
                else if (available < 0f) return false;
            }
            return maxY > minY && !float.IsInfinity(maxY) && !float.IsInfinity(minY);
        }

        /// <summary>The two ground axes, following the actual map transform.</summary>
        public Vector2 GetGroundAxis(int axis)
        {
            Refresh();
            return (_corners[axis == 0 ? 0 : 2] - _corners[3]).normalized;
        }

        /// <summary>Clip a finite-width beam against all four ground edges.
        /// Both endpoints and all four rectangle corners stay on the grass.</summary>
        public bool TryGetBeamSegment(Vector2 point, Vector2 direction, float width,
            out Vector2 start, out Vector2 end)
        {
            Refresh();
            start = end = point;
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) ||
                !float.IsFinite(width) || width < 0f || direction.sqrMagnitude < 0.000001f)
                return false;
            direction.Normalize();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float min = float.NegativeInfinity, max = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 n = _normals[i];
                float available = _distances[i] - groundMargin - Vector2.Dot(n, point) -
                    Mathf.Abs(Vector2.Dot(n, perpendicular)) * width * 0.5f;
                float along = Vector2.Dot(n, direction);
                if (along > 0.000001f) max = Mathf.Min(max, available / along);
                else if (along < -0.000001f) min = Mathf.Max(min, available / along);
                else if (available < -0.00001f) return false;
            }
            if (!float.IsFinite(min) || !float.IsFinite(max) || max - min <= 0.001f)
                return false;
            start = point + direction * min;
            end = point + direction * max;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Refresh();
            Gizmos.color = Color.cyan;
            for (int i = 0; i < 4; i++) Gizmos.DrawLine(_corners[i], _corners[(i + 1) % 4]);
        }
    }
}
