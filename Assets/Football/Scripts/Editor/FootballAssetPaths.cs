using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.EditorTools
{
    /// <summary>Asset locations and get-or-create helpers shared by the scene builders.</summary>
    public static class FootballAssetPaths
    {
        public const string Root = "Assets/Football";
        public const string Materials = Root + "/Materials";
        public const string Data = Root + "/Data";
        public const string Scenes = Root + "/Scenes";

        const string LitShader = "Universal Render Pipeline/Lit";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>URP Lit material. Existing materials are updated so re-running the builder applies new colours.</summary>
        public static Material GetOrCreateMaterial(string name, Color color, float smoothness,
            bool transparent = false, bool doubleSided = false)
        {
            EnsureFolder(Materials);
            string path = $"{Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(LitShader);
                if (shader == null)
                    throw new System.InvalidOperationException($"Shader '{LitShader}' not found. Is URP installed and active?");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (transparent || doubleSided)
                material.SetFloat("_Cull", (float)CullMode.Off);

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
