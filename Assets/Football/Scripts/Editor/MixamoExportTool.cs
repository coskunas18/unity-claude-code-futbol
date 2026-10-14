using System.IO;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.EditorTools
{
    /// <summary>
    /// Prepares an unrigged character model for Mixamo's auto-rigger and exports it as FBX.
    /// Mixamo expects a single mesh in T-pose, facing +Z, Y up, feet on the ground, with textures that travel with it.
    /// The tool bakes orientation and size into a new mesh, extracts the textures to PNG files, gives the model a
    /// URP Lit material and writes an FBX with embedded textures outside Assets (so Unity does not import it).
    /// </summary>
    public static class MixamoExportTool
    {
        public const string ExportFolder = "Exports/Mixamo";

        /// <param name="sourceModelPath">Asset path of the imported model, e.g. a .glb.</param>
        /// <param name="assetName">Base name for the generated mesh, material, textures and FBX.</param>
        /// <param name="heightMeters">Standing height the model is scaled to.</param>
        /// <param name="yawDegrees">Rotation about Y that turns the model's face toward +Z.</param>
        /// <returns>Absolute path of the exported FBX.</returns>
        public static string Export(string sourceModelPath, string assetName, float heightMeters, float yawDegrees)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelPath);
            if (source == null)
                throw new FileNotFoundException($"No model at {sourceModelPath}");
            var sourceFilter = source.GetComponentInChildren<MeshFilter>();
            var sourceRenderer = source.GetComponentInChildren<MeshRenderer>();
            if (sourceFilter == null || sourceRenderer == null)
                throw new System.InvalidOperationException("Model must contain one MeshFilter and MeshRenderer");

            string folder = Path.GetDirectoryName(sourceModelPath)?.Replace('\\', '/');
            string textureFolder = $"{folder}/{assetName}_Textures";
            FootballAssetPaths.EnsureFolder(textureFolder);

            Mesh mesh = BakeMesh(sourceFilter.sharedMesh, heightMeters, yawDegrees, assetName);
            string meshPath = $"{folder}/{assetName}_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            Material material = BuildMaterial(sourceRenderer.sharedMaterial, textureFolder, assetName);

            var temp = new GameObject(assetName);
            try
            {
                temp.AddComponent<MeshFilter>().sharedMesh = mesh;
                temp.AddComponent<MeshRenderer>().sharedMaterial = material;

                Directory.CreateDirectory(ExportFolder);
                string fbxPath = Path.GetFullPath(Path.Combine(ExportFolder, assetName + ".fbx"));
                var options = new ExportModelOptions { ExportFormat = ExportFormat.Binary, EmbedTextures = true };
                string written = ModelExporter.ExportObject(fbxPath, temp, options);
                if (string.IsNullOrEmpty(written))
                    throw new System.InvalidOperationException("FBX export failed; see the Console");
                AssetDatabase.SaveAssets();
                return written;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>Copies the mesh with rotation and scale applied and the lowest vertex moved to y = 0.</summary>
        static Mesh BakeMesh(Mesh source, float heightMeters, float yawDegrees, string assetName)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Bounds bounds = source.bounds;
            float scale = heightMeters / bounds.size.y;
            Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);

            float minY = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = yaw * ((vertices[i] - new Vector3(bounds.center.x, 0f, bounds.center.z)) * scale);
                minY = Mathf.Min(minY, vertices[i].y);
            }
            for (int i = 0; i < vertices.Length; i++)
                vertices[i].y -= minY;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = yaw * normals[i];

            var mesh = new Mesh
            {
                name = assetName,
                indexFormat = source.indexFormat,
                vertices = vertices,
                normals = normals,
                uv = source.uv,
                triangles = source.triangles
            };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>URP Lit material using the source's colour and normal maps, saved as real PNG files.</summary>
        static Material BuildMaterial(Material source, string textureFolder, string assetName)
        {
            Texture baseColor = FindTexture(source, "baseColorTexture", "_BaseMap", "_MainTex");
            Texture normal = FindTexture(source, "normalTexture", "_BumpMap");

            Texture2D baseColorAsset = baseColor != null
                ? SaveAsPng(baseColor, $"{textureFolder}/{assetName}_BaseColor.png", isNormal: false)
                : null;
            Texture2D normalAsset = normal != null
                ? SaveAsPng(normal, $"{textureFolder}/{assetName}_Normal.png", isNormal: true)
                : null;

            string path = $"{Path.GetDirectoryName(textureFolder)?.Replace('\\', '/')}/{assetName}_Lit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = assetName + "_Lit" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", baseColorAsset);
            material.SetColor("_BaseColor", Color.white);
            // Fabric and skin: matte, non-metallic. The source's packed ORM map does not match URP's layout.
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.25f);
            if (normalAsset != null)
            {
                material.SetTexture("_BumpMap", normalAsset);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture FindTexture(Material material, params string[] propertyNames)
        {
            foreach (string name in propertyNames)
            {
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return material.GetTexture(name);
            }
            return null;
        }

        /// <summary>Reads a texture through the GPU (works for non-readable imported textures) and writes a PNG asset.</summary>
        static Texture2D SaveAsPng(Texture texture, string assetPath, bool isNormal)
        {
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32,
                isNormal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, isNormal);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            File.WriteAllBytes(assetPath, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
