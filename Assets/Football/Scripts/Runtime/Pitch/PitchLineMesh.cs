using System.Collections.Generic;
using Football.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Pitch
{
    /// <summary>
    /// Builds the pitch markings as one flat mesh from <see cref="PitchDimensions"/>, so the painted lines and
    /// the rule code can never disagree. Every distance is measured to the outer edge of a line, as in the Laws.
    /// </summary>
    public static class PitchLineMesh
    {
        const int CircleSegments = 128;

        public static Mesh Build(float height = 0.01f)
        {
            var builder = new Builder(height);
            float w = PitchDimensions.LineWidth;
            float hw = w * 0.5f;
            float L = PitchDimensions.HalfLength;
            float W = PitchDimensions.HalfWidth;

            // Touchlines and goal lines.
            builder.Strip(new Vector2(-L, W - hw), new Vector2(L, W - hw), w);
            builder.Strip(new Vector2(-L, -W + hw), new Vector2(L, -W + hw), w);
            builder.Strip(new Vector2(L - hw, -W + w), new Vector2(L - hw, W - w), w);
            builder.Strip(new Vector2(-L + hw, -W + w), new Vector2(-L + hw, W - w), w);

            // Halfway line, centre circle and spot.
            builder.Strip(new Vector2(0f, -W + w), new Vector2(0f, W - w), w);
            builder.Ring(Vector2.zero, PitchDimensions.CentreCircleRadius, w, 0f, 360f);
            builder.Disc(Vector2.zero, PitchDimensions.CentreSpotRadius);

            foreach (int side in new[] { -1, 1 })
            {
                Box(builder, side, PitchDimensions.PenaltyAreaDepth, PitchDimensions.PenaltyAreaWidth);
                Box(builder, side, PitchDimensions.GoalAreaDepth, PitchDimensions.GoalAreaWidth);

                var spot = new Vector2(side * (L - PitchDimensions.PenaltySpotDistance), 0f);
                builder.Disc(spot, PitchDimensions.CentreSpotRadius);

                // Penalty arc: the part of the 9.15 m circle around the spot outside the penalty area.
                float inside = PitchDimensions.PenaltyAreaDepth - PitchDimensions.PenaltySpotDistance;
                float halfAngle = Mathf.Acos(inside / PitchDimensions.PenaltyArcRadius) * Mathf.Rad2Deg;
                float towardCentre = side > 0 ? 180f : 0f;
                builder.Ring(spot, PitchDimensions.PenaltyArcRadius, w, towardCentre - halfAngle, towardCentre + halfAngle);

                // Corner arcs, a quarter circle into the field at each corner.
                foreach (int lateral in new[] { -1, 1 })
                {
                    var corner = new Vector2(side * L, lateral * W);
                    float start = Mathf.Atan2(-lateral, -side) * Mathf.Rad2Deg - 45f;
                    builder.Ring(corner, PitchDimensions.CornerArcRadius, w, start, start + 90f);
                }
            }

            return builder.ToMesh("PitchLines");
        }

        /// <summary>Penalty or goal area: two lines out from the goal line and one parallel to it.</summary>
        static void Box(Builder builder, int side, float depth, float width)
        {
            float w = PitchDimensions.LineWidth;
            float hw = w * 0.5f;
            float L = PitchDimensions.HalfLength;
            float goalLineInner = side * (L - w);
            float frontCentre = side * (L - depth + hw);
            float halfWidth = width * 0.5f;

            builder.Strip(new Vector2(goalLineInner, halfWidth - hw), new Vector2(frontCentre - side * hw, halfWidth - hw), w);
            builder.Strip(new Vector2(goalLineInner, -halfWidth + hw), new Vector2(frontCentre - side * hw, -halfWidth + hw), w);
            builder.Strip(new Vector2(frontCentre, -halfWidth), new Vector2(frontCentre, halfWidth), w);
        }

        sealed class Builder
        {
            readonly float height;
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int> triangles = new List<int>();

            public Builder(float height)
            {
                this.height = height;
            }

            Vector3 P(Vector2 xz) => new Vector3(xz.x, height, xz.y);

            /// <summary>Straight line of the given width centred on segment a–b (x, z).</summary>
            public void Strip(Vector2 a, Vector2 b, float width)
            {
                Vector2 along = (b - a).normalized;
                Vector2 across = new Vector2(-along.y, along.x) * (width * 0.5f);
                Quad(P(a - across), P(a + across), P(b + across), P(b - across));
            }

            /// <summary>Arc whose OUTER edge has the given radius.</summary>
            public void Ring(Vector2 centre, float outerRadius, float width, float startDegrees, float endDegrees)
            {
                float innerRadius = outerRadius - width;
                int segments = Mathf.Max(4, Mathf.CeilToInt(CircleSegments * Mathf.Abs(endDegrees - startDegrees) / 360f));
                for (int i = 0; i < segments; i++)
                {
                    float a0 = Mathf.Lerp(startDegrees, endDegrees, i / (float)segments) * Mathf.Deg2Rad;
                    float a1 = Mathf.Lerp(startDegrees, endDegrees, (i + 1) / (float)segments) * Mathf.Deg2Rad;
                    var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                    var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                    Quad(P(centre + d0 * innerRadius), P(centre + d0 * outerRadius),
                        P(centre + d1 * outerRadius), P(centre + d1 * innerRadius));
                }
            }

            public void Disc(Vector2 centre, float radius)
            {
                const int segments = 24;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i / (float)segments * Mathf.PI * 2f;
                    float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                    Triangle(P(centre), P(centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius),
                        P(centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius));
                }
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            /// <summary>Adds a triangle wound so its front face points up.</summary>
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                if (Vector3.Cross(b - a, c - a).y < 0f)
                    (b, c) = (c, b);
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                var normals = new Vector3[vertices.Count];
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = Vector3.up;
                mesh.normals = normals;
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
