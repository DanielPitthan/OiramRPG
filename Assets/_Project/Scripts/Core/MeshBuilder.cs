using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oiram.Core
{
    /// <summary>
    /// Monta uma malha com cores por vértice (terreno, decoração espalhada) — milhares de peças viram um único objeto.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector3> normals = new();
        readonly List<Vector3> smooth = new();
        readonly List<Color> colors = new();
        readonly List<int> triangles = new();

        public int VertexCount => vertices.Count;

        /// <summary>Quad a-b-c-d (em volta), virado para <paramref name="normal"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color ca, Color cb, Color cc, Color cd)
        {
            int s = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
                smooth.Add(normal);
            }
            colors.Add(ca); colors.Add(cb); colors.Add(cc); colors.Add(cd);
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
            if (flip)
            {
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 1);
                triangles.Add(s); triangles.Add(s + 3); triangles.Add(s + 2);
            }
            else
            {
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color) => Quad(a, b, c, d, normal, color, color, color, color);

        /// <summary>Copia <paramref name="mesh"/> transformada, pintada de <paramref name="color"/> (mantém as normais do contorno).</summary>
        public void AddMesh(Mesh mesh, Matrix4x4 matrix, Color color)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv3 = new List<Vector3>();
            mesh.GetUVs(3, uv3);
            var normalMatrix = matrix.inverse.transpose;
            int s = vertices.Count;
            for (int i = 0; i < v.Length; i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(v[i]));
                var nn = normalMatrix.MultiplyVector(n[i]).normalized;
                normals.Add(nn);
                smooth.Add(i < uv3.Count ? normalMatrix.MultiplyVector(uv3[i]).normalized : nn);
                colors.Add(color);
            }
            foreach (int t in mesh.triangles) triangles.Add(s + t);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            // Cores de vértice não passam pela conversão sRGB → linear dos materiais: converte aqui.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < colors.Count; i++)
                {
                    var c = colors[i];
                    var lin = c.linear;
                    lin.a = c.a;
                    colors[i] = lin;
                }
            mesh.SetColors(colors);
            mesh.SetUVs(3, smooth);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
