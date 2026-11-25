using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CursorHunter.Progression.Editor
{
    /// <summary>Preserves the vendor flash as an atlas for the existing Overlay canvas.</summary>
    public static class TraitCardRevealBaker
    {
        public const string Source = "Assets/DownLoadAssets/Eric VFX Studio/Game VFX - Card Effects Collection/Prefabs/URP/FX_CardFlash_B.prefab";
        public const string Destination = "Assets/CursorHunter/Progression/Art/TraitCards/CardFlashBlue.png";
        private const int Width = 320, Height = 480, Columns = 6, Rows = 4;

        [MenuItem("Tools/Cursor Hunter/Trait Cards/Rebuild Reveal Effect")]
        public static void Bake()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Rebuilding the card flash requires a graphics device.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (prefab == null) throw new FileNotFoundException(Source);
            var scene = EditorSceneManager.NewPreviewScene();
            var previousTarget = RenderTexture.active;
            RenderTexture target = null;
            Texture2D frame = null, atlas = null;
            try
            {
                var effect = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(effect, scene);
                effect.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.useAutoRandomSeed = false;
                    particle.randomSeed = 17;
                    var main = particle.main;
                    main.loop = false;
                }
                var camera = new GameObject("Card flash bake camera", typeof(Camera)).GetComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 3.6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.enabled = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                atlas = new Texture2D(Width * Columns, Height * Rows, TextureFormat.RGB24, false);
                var root = effect.GetComponent<ParticleSystem>();
                for (int i = 0; i < Columns * Rows; i++)
                {
                    root.Simulate(.65f * i / (Columns * Rows - 1), true, true, false);
                    camera.Render();
                    RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    frame.Apply();
                    atlas.SetPixels(i % Columns * Width, (Rows - 1 - i / Columns) * Height,
                        Width, Height, frame.GetPixels());
                }
                atlas.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(Destination));
                File.WriteAllBytes(Destination, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(Destination);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (frame != null) UnityEngine.Object.DestroyImmediate(frame);
                if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("Baked vendor card flash: " + Destination);
        }
    }
}
