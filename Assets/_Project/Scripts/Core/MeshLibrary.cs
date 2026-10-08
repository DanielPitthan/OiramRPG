using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oiram.Core
{
    /// <summary>
    /// Malhas do jogo (primitivas da Unity e formas geradas por código), todas com normais suavizadas no UV3 —
    /// o contorno toon (casco invertido) usa essas normais para não abrir nas quinas.
    /// </summary>
    public static class MeshLibrary
    {
        /// <summary>O editor troca isto para salvar as malhas como assets ao "assar" as cenas.</summary>
        public static Func<Mesh, Mesh> Persist;

        static readonly Dictionary<string, Mesh> cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static void ClearCache() => cache.Clear();

        /// <summary>Malha única de uma cena (terreno, decoração): no editor vira asset; em runtime fica como está.</summary>
        public static Mesh Store(Mesh mesh) => Persist != null ? Persist(mesh) : mesh;

        static Mesh Cached(string name, Func<Mesh> build)
        {
            if (cache.TryGetValue(name, out var mesh) && mesh != null) return mesh;
            mesh = build();
            mesh.name = name;
            BakeOutlineNormals(mesh);
            mesh.RecalculateBounds();
            if (Persist != null) mesh = Persist(mesh);
            cache[name] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ primitivas

        public static Mesh Get(PrimitiveType type) => Cached("Prim_" + type, () =>
        {
            var go = GameObject.CreatePrimitive(type);
            var copy = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(go);
            return copy;
        });

        /// <summary>Quad 1×1 virado para +Z (partículas, céu, sombras suaves). Sem contorno.</summary>
        public static Mesh Quad => Get(PrimitiveType.Quad);

        // ------------------------------------------------------------------ formas geradas

        /// <summary>Caixa 1×1×1 com quinas arredondadas (raio em unidades da caixa).</summary>
        public static Mesh RoundedBox(float radius = 0.12f, int segments = 3) =>
            Cached($"RoundBox_{radius * 100:0}_{segments}", () => BuildRoundedBox(Mathf.Clamp(radius, 0.01f, 0.49f), Mathf.Max(1, segments)));

        /// <summary>Cone com base em y = −0.5 (raio 0.5) e ponta em y = +0.5. Com 4 lados vira pirâmide (telhado).</summary>
        public static Mesh Cone(int sides = 16, bool smooth = true) => Cached($"Cone_{sides}_{(smooth ? 1 : 0)}", () => BuildCone(Mathf.Max(3, sides), smooth));

        /// <summary>Icosfera de raio 0.5. Facetada = visual low-poly (folhagem, pedras, cristais).</summary>
        public static Mesh Icosphere(int subdivisions = 1, bool faceted = true, float jitter = 0f, int seed = 0) =>
            Cached($"Ico_{subdivisions}_{(faceted ? 1 : 0)}_{jitter * 100:0}_{seed}", () => BuildIcosphere(Mathf.Clamp(subdivisions, 0, 3), faceted, jitter, seed));

        /// <summary>Gota/chama: esfera esticada para cima com ponta (raio 0.5, altura 1).</summary>
        public static Mesh Drop(int segments = 14) => Cached($"Drop_{segments}", () => BuildDrop(Mathf.Max(6, segments)));

        // ------------------------------------------------------------------ construção

        static Mesh BuildRoundedBox(float r, int seg)
        {
            // Amostras por eixo: concentradas nas quinas (arco), nada no meio das faces.
            var axis = new List<float>();
            for (int k = 0; k <= seg; k++) axis.Add(-0.5f + r - r * Mathf.Cos(k / (float)seg * Mathf.PI * 0.5f));
            for (int k = seg; k >= 0; k--) axis.Add(0.5f - r + r * Mathf.Cos(k / (float)seg * Mathf.PI * 0.5f));

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var fn in faceNormals)
            {
                // Base ortonormal da face (u × v = normal, para o winding ficar certo).
                Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(fn, u);
                u = Vector3.Cross(v, fn);
                int start = vertices.Count;
                int n = axis.Count;
                for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var p = fn * 0.5f + u * axis[i] + v * axis[j];
                    var inner = new Vector3(Mathf.Clamp(p.x, -0.5f + r, 0.5f - r), Mathf.Clamp(p.y, -0.5f + r, 0.5f - r), Mathf.Clamp(p.z, -0.5f + r, 0.5f - r));
                    var dir = p - inner;
                    var normal = dir.sqrMagnitude > 1e-8f ? dir.normalized : fn;
                    vertices.Add(inner + normal * r);
                    normals.Add(normal);
                }
                for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    int a = start + i * n + j, b = a + 1, c = a + n, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            FixWinding(mesh);
            return mesh;
        }

        static Mesh BuildCone(int sides, bool smooth)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var apex = new Vector3(0f, 0.5f, 0f);
            float slope = 0.5f; // raio / altura
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                Vector3 n0, n1, nApex;
                if (smooth)
                {
                    n0 = new Vector3(Mathf.Cos(a0), slope, Mathf.Sin(a0)).normalized;
                    n1 = new Vector3(Mathf.Cos(a1), slope, Mathf.Sin(a1)).normalized;
                    nApex = (n0 + n1).normalized;
                }
                else
                {
                    var flat = Vector3.Cross(apex - p0, p1 - p0).normalized;
                    var outward = new Vector3(p0.x + p1.x, 0f, p0.z + p1.z);
                    if (Vector3.Dot(flat, outward) < 0f) flat = -flat;
                    n0 = n1 = nApex = flat;
                }
                int s = vertices.Count;
                vertices.Add(p0); vertices.Add(apex); vertices.Add(p1);
                normals.Add(n0); normals.Add(nApex); normals.Add(n1);
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                // Base.
                int b = vertices.Count;
                vertices.Add(new Vector3(0f, -0.5f, 0f)); vertices.Add(p1); vertices.Add(p0);
                normals.Add(Vector3.down); normals.Add(Vector3.down); normals.Add(Vector3.down);
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            FixWinding(mesh);
            return mesh;
        }

        static Mesh BuildIcosphere(int subdivisions, bool faceted, float jitter, int seed)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (midpoints.TryGetValue(key, out int m)) return m;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    midpoints[key] = points.Count - 1;
                    return points.Count - 1;
                }
                var next = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            if (jitter > 0f)
            {
                var rng = new System.Random(seed);
                for (int i = 0; i < points.Count; i++)
                    points[i] *= 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
            }
            for (int i = 0; i < points.Count; i++) points[i] *= 0.5f;

            var mesh = new Mesh();
            if (faceted)
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var triangles = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    var a = points[faces[i]]; var b = points[faces[i + 1]]; var c = points[faces[i + 2]];
                    var n = Vector3.Cross(b - a, c - a).normalized;
                    if (Vector3.Dot(n, a + b + c) < 0f) n = -n;
                    int s = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c);
                    normals.Add(n); normals.Add(n); normals.Add(n);
                    triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                }
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
            }
            else
            {
                mesh.SetVertices(points);
                mesh.SetTriangles(faces, 0);
            }
            FixWinding(mesh);
            if (!faceted) mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh BuildDrop(int segments)
        {
            int rings = segments;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;            // 0 = base, 1 = ponta
                float theta = v * Mathf.PI;
                float radius = Mathf.Sin(theta) * 0.5f * (1f - 0.55f * v * v);
                float y = -Mathf.Cos(theta) * 0.5f;
                y = y < 0f ? y * 0.6f - 0.2f : y * 1.1f - 0.2f;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = r * stride + s, b = a + 1, c = a + stride, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
                triangles.Add(a); triangles.Add(d); triangles.Add(b);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            FixWinding(mesh);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>Garante cada triângulo voltado para fora do centro (todas as formas aqui são "estreladas").</summary>
        static void FixWinding(Mesh mesh)
        {
            var v = mesh.vertices;
            var tris = mesh.triangles;
            var center = mesh.bounds.center;
            bool changed = false;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = v[tris[i]]; var b = v[tris[i + 1]]; var c = v[tris[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) >= 0f) continue;
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                changed = true;
            }
            if (changed) mesh.triangles = tris;
        }

        /// <summary>Média das normais de todos os vértices na mesma posição, guardada no UV3 (usada só pelo contorno).</summary>
        public static void BakeOutlineNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }
            var sums = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Key(vertices[i]);
                sums[key] = sums.TryGetValue(key, out var s) ? s + normals[i] : normals[i];
            }
            var smooth = new List<Vector3>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var n = sums[Key(vertices[i])];
                smooth.Add(n.sqrMagnitude > 1e-8f ? n.normalized : normals[i]);
            }
            mesh.SetUVs(3, smooth);
        }
    }
}
