using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// Applies the project's high-fidelity baseline before the first scene is
    /// loaded. Unity can retain the editor's last low quality selection during
    /// Play Mode, so the prototype otherwise appears softer than a build.
    /// Resolution remains user-controlled; this class only removes the
    /// accidental low-quality render settings.
    /// </summary>
    public static class DisplayQualityBootstrap
    {
        private const int PreferredQualityIndex = 5;
        private const int PreferredAntiAliasing = 4;
        private const int PreferredFrameRate = 60;
        private const int PreferredMipmapLimit = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyBaseline()
        {
            try
            {
                int qualityCount = QualitySettings.names == null
                    ? 0
                    : QualitySettings.names.Length;
                if (qualityCount > 0)
                {
                    QualitySettings.SetQualityLevel(
                        Mathf.Clamp(PreferredQualityIndex, 0, qualityCount - 1),
                        true);
                }

                if (QualitySettings.antiAliasing < PreferredAntiAliasing)
                {
                    QualitySettings.antiAliasing = PreferredAntiAliasing;
                }

                // Keep full-resolution texture mips at run time. A quality
                // preset can be changed by the editor or by a previous scene.
                QualitySettings.globalTextureMipmapLimit = PreferredMipmapLimit;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.resolutionScalingFixedDPIFactor = 1f;
                // A previous Editor session or a platform quality preset can
                // leave dynamic-resolution buffers below the native back
                // buffer. Keep the prototype at 1:1 so a high-resolution
                // window is not rendered into a smaller intermediate target.
                ScalableBufferManager.ResizeBuffers(1f, 1f);
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = PreferredFrameRate;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"Display quality baseline could not be applied: {exception.Message}");
            }
        }
    }
}
