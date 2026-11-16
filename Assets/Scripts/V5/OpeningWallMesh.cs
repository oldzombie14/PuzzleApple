using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    // Unit wall with one rectangular aperture. Adjacent cells share exact coordinates
    // and only exposed edges receive thickness faces.
    internal static class OpeningWallMesh
    {
        public static Mesh Build(float widthFraction, float bottom, float top, bool inwardForward)
        {
            var x = new[] { -.5f, -widthFraction/2, widthFraction/2, .5f };
            var y = new[] { -.5f, bottom-.5f, top-.5f, .5f };
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var inside = new List<int>();
            var outside = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> indices)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a,b,c,d });
                uv.AddRange(new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up });
                indices.AddRange(new[] { i,i+1,i+2,i,i+2,i+3 });
            }
            for (int col=0; col<3; col++)
            for (int row=0; row<3; row++)
            {
                if ((col==1 && row==1) || y[row]==y[row+1]) continue;
                var a = new Vector3(x[col],y[row],-.5f);
                var b = new Vector3(x[col+1],y[row],-.5f);
                var c = new Vector3(x[col+1],y[row+1],-.5f);
                var d = new Vector3(x[col],y[row+1],-.5f);
                var z = Vector3.forward;
                Quad(d,c,b,a,inwardForward ? outside : inside);
                Quad(a+z,b+z,c+z,d+z,inwardForward ? inside : outside);
                if (col==0 || (col==2 && row==1)) Quad(a,a+z,d+z,d,outside);
                if (col==2 || (col==0 && row==1)) Quad(b,c,c+z,b+z,outside);
                if (y[row]==-.5f || (col==1 && row==2)) Quad(a,b,b+z,a+z,outside);
                if (row==2 || (col==1 && row==0)) Quad(d,d+z,c+z,c,outside);
            }
            var mesh = new Mesh { name="Wall with rectangular opening" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uv);
            mesh.subMeshCount=2;
            mesh.SetTriangles(inside,0);
            mesh.SetTriangles(outside,1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
