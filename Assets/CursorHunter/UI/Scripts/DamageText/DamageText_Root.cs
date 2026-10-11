using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "DamageText_Root")]
[DisallowMultipleComponent]
public class DamageText_Root : MonoBehaviour
{
    [FormerlySerializedAs("damageText")]
    [SerializeField] private TMP_Text normalHitText;
    [SerializeField] private TMP_Text criticalHitText;
    [SerializeField] private int sortingOrder = 300;

    // Positive Int64 needs at most 19 digits. Each pool slot owns its buffer.
    private readonly char[] _numberBuffer = new char[19];
    private TextState _normal;
    private TextState _critical;
    private TextState _selected;
    private Vector3 _authoredScale;
    private byte _opacity = 255;

    private sealed class TextState
    {
        public readonly TMP_Text Text;
        public byte[][] Alphas;
        public TextState(TMP_Text text) { Text = text; }
    }

    public bool IsConfigured => normalHitText != null && criticalHitText != null &&
        normalHitText.gameObject != criticalHitText.gameObject &&
        normalHitText.transform.IsChildOf(transform) && criticalHitText.transform.IsChildOf(transform) &&
        normalHitText.gameObject != gameObject && criticalHitText.gameObject != gameObject &&
        normalHitText.font != null && criticalHitText.font != null;

    public bool InitializeForPool()
    {
        if (_normal != null) return true;
        if (!IsConfigured) return false;

        _authoredScale = transform.localScale;
        _normal = new TextState(normalHitText);
        _critical = new TextState(criticalHitText);
        normalHitText.OnPreRenderText += CaptureNormalColors;
        criticalHitText.OnPreRenderText += CaptureCriticalColors;
        WarmText(_normal);
        WarmText(_critical);
        Hide();
        return true;
    }

    private void WarmText(TextState state)
    {
        TMP_Text text = state.Text;
        text.gameObject.SetActive(true);
        text.raycastTarget = false;
        text.vertexBufferAutoSizeReduction = false;
        Renderer renderer = text.GetComponent<Renderer>();
        if (renderer != null) renderer.sortingOrder = sortingOrder;
        // Warm all digits and maximum-length mesh buffers in both styles.
        text.SetText("1234567890123456789");
        text.ForceMeshUpdate(true);
        text.gameObject.SetActive(false);
    }

    public void Show(long damage, bool critical)
    {
        _selected = critical ? _critical : _normal;
        _opacity = 255;
        normalHitText.gameObject.SetActive(!critical);
        criticalHitText.gameObject.SetActive(critical);
        gameObject.SetActive(true);
        SetDamage(damage);
    }

    public void SetDamage(long damage)
    {
        int start = _numberBuffer.Length;
        do
        {
            _numberBuffer[--start] = (char)('0' + damage % 10L);
            damage /= 10L;
        } while (damage > 0L);

        _selected.Text.SetCharArray(_numberBuffer, start, _numberBuffer.Length - start);
        _selected.Text.ForceMeshUpdate();
    }

    public void SetPose(Vector3 worldPosition, float scale, float opacity)
    {
        transform.position = worldPosition;
        transform.localScale = _authoredScale * scale;
        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);
        if (_opacity == alpha) return;
        _opacity = alpha;
        ApplyOpacity(_selected, _selected.Text.textInfo);
        // Change vertex colors only; TMP_Text.color would dirty text layout every frame.
        _selected.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    public void Hide()
    {
        normalHitText.gameObject.SetActive(false);
        criticalHitText.gameObject.SetActive(false);
        gameObject.SetActive(false);
        transform.localScale = _authoredScale;
        _selected = null;
    }

    private void CaptureNormalColors(TMP_TextInfo info) { CaptureColors(_normal, info); }
    private void CaptureCriticalColors(TMP_TextInfo info) { CaptureColors(_critical, info); }

    private void CaptureColors(TextState state, TMP_TextInfo info)
    {
        if (state.Alphas == null || state.Alphas.Length < info.meshInfo.Length)
            state.Alphas = new byte[info.meshInfo.Length][];
        for (int mesh = 0; mesh < info.meshInfo.Length; mesh++)
        {
            Color32[] colors = info.meshInfo[mesh].colors32;
            if (colors == null) continue;
            if (state.Alphas[mesh] == null || state.Alphas[mesh].Length < colors.Length)
                state.Alphas[mesh] = new byte[colors.Length];
            for (int vertex = 0; vertex < info.meshInfo[mesh].vertexCount; vertex++)
                state.Alphas[mesh][vertex] = colors[vertex].a;
        }
        ApplyOpacity(state, info);
    }

    private void ApplyOpacity(TextState state, TMP_TextInfo info)
    {
        if (state.Alphas == null) return;
        for (int mesh = 0; mesh < info.meshInfo.Length; mesh++)
        {
            Color32[] colors = info.meshInfo[mesh].colors32;
            for (int vertex = 0; vertex < info.meshInfo[mesh].vertexCount; vertex++)
                colors[vertex].a = (byte)(state.Alphas[mesh][vertex] * _opacity / 255);
        }
    }

    private void OnDestroy()
    {
        if (normalHitText != null) normalHitText.OnPreRenderText -= CaptureNormalColors;
        if (criticalHitText != null) criticalHitText.OnPreRenderText -= CaptureCriticalColors;
    }
}
