using System.Collections.Generic;
using Football.Core;
using Football.Pitch;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.EditorTools
{
    /// <summary>
    /// Creates the phase 1 pitch: striped grass, markings, goals with nets and corner flags.
    /// Visual only: ball physics reads the goal frame from <see cref="PitchDimensions"/>, so no colliders are made.
    /// </summary>
    public static class PitchBuilder
    {
        const string GeneratedFolder = FootballAssetPaths.Root + "/Generated";
        const int StripeCount = 12;

        public sealed class Materials
        {
            public Material GrassLight;
            public Material GrassDark;
            public Material Surround;
            public Material Lines;
            public Material GoalFrame;
            public Material Net;
            public Material FlagPole;
            public Material Flag;
        }

        public static Materials CreateMaterials() => new Materials
        {
            GrassLight = FootballAssetPaths.GetOrCreateMaterial("Grass_Light", new Color(0.30f, 0.56f, 0.22f), 0.15f),
            GrassDark = FootballAssetPaths.GetOrCreateMaterial("Grass_Dark", new Color(0.25f, 0.49f, 0.18f), 0.15f),
            Surround = FootballAssetPaths.GetOrCreateMaterial("Surround", new Color(0.22f, 0.40f, 0.17f), 0.1f),
            Lines = FootballAssetPaths.GetOrCreateMaterial("PitchLines", new Color(0.93f, 0.93f, 0.9f), 0.05f),
            GoalFrame = FootballAssetPaths.GetOrCreateMaterial("GoalFrame", Color.white, 0.6f),
            Net = FootballAssetPaths.GetOrCreateMaterial("Net", new Color(0.95f, 0.95f, 0.95f, 0.28f), 0.1f, transparent: true),
            FlagPole = FootballAssetPaths.GetOrCreateMaterial("FlagPole", new Color(0.9f, 0.9f, 0.9f), 0.4f),
            Flag = FootballAssetPaths.GetOrCreateMaterial("Flag", new Color(1f, 0.85f, 0.1f), 0.2f, doubleSided: true)
        };

        public static GameObject Build(Materials materials)
        {
            FootballAssetPaths.EnsureFolder(GeneratedFolder);
            var root = new GameObject("Pitch");

            var grass = new GameObject("Grass", typeof(MeshFilter), typeof(MeshRenderer));
            grass.transform.SetParent(root.transform, false);
            grass.GetComponent<MeshFilter>().sharedMesh = SaveMesh(BuildGrassMesh(), "PitchGrass");
            var grassRenderer = grass.GetComponent<MeshRenderer>();
            grassRenderer.sharedMaterials = new[] { materials.GrassLight, materials.GrassDark, materials.Surround };
            grassRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var lines = new GameObject("Lines", typeof(MeshFilter), typeof(MeshRenderer));
            lines.transform.SetParent(root.transform, false);
            lines.GetComponent<MeshFilter>().sharedMesh = SaveMesh(PitchLineMesh.Build(), "PitchLines");
            var lineRenderer = lines.GetComponent<MeshRenderer>();
            lineRenderer.sharedMaterial = materials.Lines;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;

            foreach (int side in new[] { -1, 1 })
                BuildGoal(root.transform, side, materials);

            foreach (int side in new[] { -1, 1 })
            foreach (int lateral in new[] { -1, 1 })
                BuildCornerFlag(root.transform, side, lateral, materials);

            return root;
        }

        /// <summary>Mown stripes across the pitch (submeshes 0 and 1) and the run-off around it (submesh 2).</summary>
        static Mesh BuildGrassMesh()
        {
            float L = PitchDimensions.HalfLength;
            float W = PitchDimensions.HalfWidth;
            float outerX = L + PitchDimensions.RunOff;
            float outerZ = W + PitchDimensions.RunOff;

            var vertices = new List<Vector3>();
            var stripes = new[] { new List<int>(), new List<int>() };
            var surround = new List<int>();

            void Quad(List<int> target, float x0, float x1, float z0, float z1)
            {
                int start = vertices.Count;
                vertices.Add(new Vector3(x0, 0f, z0));
                vertices.Add(new Vector3(x0, 0f, z1));
                vertices.Add(new Vector3(x1, 0f, z1));
                vertices.Add(new Vector3(x1, 0f, z0));
                target.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            float stripeLength = PitchDimensions.Length / StripeCount;
            for (int i = 0; i < StripeCount; i++)
                Quad(stripes[i % 2], -L + i * stripeLength, -L + (i + 1) * stripeLength, -W, W);

            Quad(surround, -outerX, outerX, W, outerZ);
            Quad(surround, -outerX, outerX, -outerZ, -W);
            Quad(surround, -outerX, -L, -W, W);
            Quad(surround, L, outerX, -W, W);

            var mesh = new Mesh { name = "PitchGrass", subMeshCount = 3 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(stripes[0], 0);
            mesh.SetTriangles(stripes[1], 1);
            mesh.SetTriangles(surround, 2);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++)
                normals[i] = Vector3.up;
            mesh.normals = normals;
            var uvs = new Vector2[vertices.Count];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = new Vector2(vertices[i].x, vertices[i].z) * 0.1f;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void BuildGoal(Transform parent, int side, Materials materials)
        {
            var goal = new GameObject(side > 0 ? "Goal_East" : "Goal_West");
            goal.transform.SetParent(parent, false);

            float diameter = PitchDimensions.PostRadius * 2f;
            float postHeight = PitchDimensions.PostTopHeight;
            foreach (int lateral in new[] { -1, 1 })
            {
                Vector3 basePosition = PitchDimensions.PostBase(side, lateral);
                var post = Primitive(PrimitiveType.Cylinder, lateral > 0 ? "Post_North" : "Post_South", goal.transform, materials.GoalFrame);
                post.transform.localPosition = basePosition + Vector3.up * (postHeight * 0.5f);
                post.transform.localScale = new Vector3(diameter, postHeight * 0.5f, diameter);
            }

            float barLength = PitchDimensions.GoalWidth + 2f * diameter;
            var bar = Primitive(PrimitiveType.Cylinder, "Crossbar", goal.transform, materials.GoalFrame);
            bar.transform.localPosition = new Vector3(PitchDimensions.PostBase(side, 1).x, PitchDimensions.CrossbarCentreHeight, 0f);
            bar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bar.transform.localScale = new Vector3(diameter, barLength * 0.5f, diameter);

            // Net panels match the planes the ball simulator uses.
            float halfWidth = PitchDimensions.GoalWidth * 0.5f + PitchDimensions.LineWidth;
            float height = PitchDimensions.GoalHeight + PitchDimensions.LineWidth;
            float depth = PitchDimensions.GoalDepth;
            float front = PitchDimensions.HalfLength;
            float midX = side * (front + depth * 0.5f);

            NetPanel(goal.transform, "Net_Back", materials.Net,
                new Vector3(side * (front + depth), height * 0.5f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(halfWidth * 2f, height));
            NetPanel(goal.transform, "Net_North", materials.Net,
                new Vector3(midX, height * 0.5f, halfWidth), Quaternion.identity, new Vector2(depth, height));
            NetPanel(goal.transform, "Net_South", materials.Net,
                new Vector3(midX, height * 0.5f, -halfWidth), Quaternion.identity, new Vector2(depth, height));
            NetPanel(goal.transform, "Net_Roof", materials.Net,
                new Vector3(midX, height, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(depth, halfWidth * 2f));
        }

        static void NetPanel(Transform parent, string name, Material material, Vector3 position, Quaternion rotation, Vector2 size)
        {
            var panel = Primitive(PrimitiveType.Quad, name, parent, material);
            panel.transform.SetLocalPositionAndRotation(position, rotation);
            panel.transform.localScale = new Vector3(size.x, size.y, 1f);
            panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildCornerFlag(Transform parent, int side, int lateral, Materials materials)
        {
            const float poleHeight = 1.5f;
            var flag = new GameObject($"CornerFlag_{(side > 0 ? "E" : "W")}{(lateral > 0 ? "N" : "S")}");
            flag.transform.SetParent(parent, false);
            flag.transform.localPosition = new Vector3(side * PitchDimensions.HalfLength, 0f, lateral * PitchDimensions.HalfWidth);

            var pole = Primitive(PrimitiveType.Cylinder, "Pole", flag.transform, materials.FlagPole);
            pole.transform.localPosition = Vector3.up * (poleHeight * 0.5f);
            pole.transform.localScale = new Vector3(0.03f, poleHeight * 0.5f, 0.03f);

            var cloth = Primitive(PrimitiveType.Quad, "Flag", flag.transform, materials.Flag);
            cloth.transform.localPosition = new Vector3(-side * 0.2f, poleHeight - 0.15f, 0f);
            cloth.transform.localScale = new Vector3(0.4f, 0.3f, 1f);
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            string path = $"{GeneratedFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
