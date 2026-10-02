using System.Collections.Generic;
using UnityEngine;

namespace Homecoming
{
    /// <summary>Editable mesh construction in metres, with normals and UVs for later texturing.</summary>
    public static class ModelMesh
    {
        public struct Ring
        {
            public float y, x, z, offset;
            public Ring(float y,float x,float z,float offset=0) { this.y=y; this.x=x; this.z=z; this.offset=offset; }
        }
        public static Mesh Loft(string name,Ring[] rings,int sides=40,float pleat=0,int folds=12)
        {
            var smooth=new List<Ring>();
            for(int j=0;j<rings.Length-1;j++) for(int k=0;k<3;k++)
            {
                float t=k/3f; var a=rings[Mathf.Max(0,j-1)]; var b=rings[j]; var c=rings[j+1]; var d=rings[Mathf.Min(rings.Length-1,j+2)];
                smooth.Add(new Ring(Mathf.Lerp(b.y,c.y,t),Mathf.Max(.0001f,Catmull(a.x,b.x,c.x,d.x,t)),Mathf.Max(.0001f,Catmull(a.z,b.z,c.z,d.z,t)),Catmull(a.offset,b.offset,c.offset,d.offset,t)));
            }
            smooth.Add(rings[rings.Length-1]); rings=smooth.ToArray();
            var v=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            for(int j=0;j<rings.Length;j++) for(int i=0;i<=sides;i++)
            {
                float a=i*2*Mathf.PI/sides;
                float ripple=1+pleat*Mathf.Cos(a*folds)*(1-Mathf.InverseLerp(rings[0].y,rings[rings.Length-1].y,rings[j].y));
                v.Add(new Vector3(Mathf.Sin(a)*rings[j].x*ripple,rings[j].y,-Mathf.Cos(a)*rings[j].z*ripple+rings[j].offset));
                uv.Add(new Vector2((float)i/sides,(float)j/(rings.Length-1)));
                if(j<rings.Length-1 && i<sides) { int n=j*(sides+1)+i,b=n+sides+1; triangles.AddRange(new[]{n,b,n+1,n+1,b,b+1}); }
            }
            for(int end=0;end<2;end++)
            {
                int row=end==0?0:rings.Length-1,center=v.Count;
                v.Add(new Vector3(0,rings[row].y,rings[row].offset)); uv.Add(new Vector2(.5f,.5f));
                for(int i=0;i<sides;i++) { int a=row*(sides+1)+i; triangles.AddRange(end==0?new[]{center,a,a+1}:new[]{center,a+1,a}); }
            }
            var mesh=Finish(name,v,uv,triangles); SmoothSeam(mesh,sides+1,rings.Length); return mesh;
        }
        public static Mesh Ellipsoid(string name,Vector3 radius,int sides=28,int rows=14)
        {
            var rings=new Ring[rows+1];
            for(int j=0;j<=rows;j++) { float a=j*Mathf.PI/rows; rings[j]=new Ring(-Mathf.Cos(a)*radius.y,Mathf.Max(.0001f,Mathf.Sin(a)*radius.x),Mathf.Max(.0001f,Mathf.Sin(a)*radius.z)); }
            return Loft(name,rings,sides);
        }
        public static Mesh RoundedBox(string name,Vector3 size,float radius,int steps=5)
        {
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var normals=new List<Vector3>(); var tri=new List<int>();
            Vector3 half=size*.5f,inner=half-Vector3.one*radius;
            Vector3[] faceNormals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var normal in faceNormals)
            {
                Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;
                Vector3 w=Vector3.Cross(normal,u); int start=vertices.Count;
                for(int y=0;y<=steps;y++) for(int x=0;x<=steps;x++)
                {
                    var p=Vector3.Scale(normal+u*(x*2f/steps-1)+w*(y*2f/steps-1),half);
                    var c=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    Vector3 n=(p-c).normalized; vertices.Add(c+n*radius); normals.Add(n); uv.Add(new Vector2((float)x/steps,(float)y/steps));
                    if(x<steps&&y<steps) { int a=start+y*(steps+1)+x,b=a+steps+1; tri.AddRange(new[]{a,a+1,b,a+1,b+1,b}); }
                }
            }
            var mesh=Finish(name,vertices,uv,tri); mesh.SetNormals(normals); return mesh;
        }
        public static Mesh Tube(string name,Vector3[] points,float[] radii,int sides=12)
        {
            var smooth=new List<Vector3>(); var smoothRadii=new List<float>();
            for(int j=0;j<points.Length-1;j++) for(int k=0;k<4;k++)
            {
                float t=k/4f; Vector3 a=points[Mathf.Max(0,j-1)],b=points[j],c=points[j+1],d=points[Mathf.Min(points.Length-1,j+2)];
                smooth.Add(new Vector3(Catmull(a.x,b.x,c.x,d.x,t),Catmull(a.y,b.y,c.y,d.y,t),Catmull(a.z,b.z,c.z,d.z,t))); smoothRadii.Add(Mathf.Lerp(radii[j],radii[j+1],t));
            }
            smooth.Add(points[points.Length-1]); smoothRadii.Add(radii[radii.Length-1]); points=smooth.ToArray(); radii=smoothRadii.ToArray();
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var tri=new List<int>();
            for(int j=0;j<points.Length;j++)
            {
                var tangent=(points[Mathf.Min(j+1,points.Length-1)]-points[Mathf.Max(0,j-1)]).normalized;
                Vector3 u=Vector3.Cross(tangent,Mathf.Abs(tangent.y)<.9f?Vector3.up:Vector3.forward).normalized,w=Vector3.Cross(tangent,u);
                for(int i=0;i<=sides;i++) { float a=i*2*Mathf.PI/sides; vertices.Add(points[j]+radii[j]*(u*Mathf.Cos(a)+w*Mathf.Sin(a))); uv.Add(new Vector2((float)i/sides,(float)j/(points.Length-1))); if(j<points.Length-1&&i<sides) { int n=j*(sides+1)+i,b=n+sides+1; tri.AddRange(new[]{n,n+1,b,n+1,b+1,b}); } }
            }
            var mesh=Finish(name,vertices,uv,tri); SmoothSeam(mesh,sides+1,points.Length); return mesh;
        }
        public static Mesh HairCap()
        {
            const int sides=64,rows=22;
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var tri=new List<int>();
            for(int j=0;j<=rows;j++) for(int i=0;i<=sides;i++)
            {
                float theta=i*2*Mathf.PI/sides;
                float front=Mathf.Max(0,Mathf.Cos(theta));
                float maxPhi=Mathf.Lerp(2.40f,.96f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.38f,front)));
                float phi=.01f+(maxPhi-.01f)*j/rows;
                float strand=1+.012f*Mathf.Sin(theta*19+phi*2);
                vertices.Add(new Vector3(.142f*Mathf.Sin(phi)*Mathf.Sin(theta)*strand,.173f*Mathf.Cos(phi),-.127f*Mathf.Sin(phi)*Mathf.Cos(theta)*strand+.006f));
                uv.Add(new Vector2((float)i/sides,(float)j/rows));
                if(i<sides&&j<rows) { int n=j*(sides+1)+i,b=n+sides+1; tri.AddRange(new[]{n,n+1,b,n+1,b+1,b}); }
            }
            return Finish("发型 · 分缝发帽",vertices,uv,tri);
        }
        public static GameObject Object(string name,Mesh mesh,Material material,Transform parent,Vector3 position)
        {
            var g=new GameObject(name); g.transform.SetParent(parent,false); g.transform.localPosition=position;
            g.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=g.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material; ApplyMaterial(renderer,material); return g;
        }
        public static void ApplyMaterial(Renderer renderer,Material material)
        {
            renderer.sharedMaterial=material;
            var properties=new MaterialPropertyBlock(); properties.SetColor("_BaseColor",material.color);
            if(material.HasProperty("_EmissionColor")) properties.SetColor("_EmissionColor",material.GetColor("_EmissionColor"));
            renderer.SetPropertyBlock(properties);
        }
        static Mesh Finish(string name,List<Vector3> vertices,List<Vector2> uv,List<int> tri)
        {
            var mesh=new Mesh{name=name}; if(vertices.Count>65535) mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(tri,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static float Catmull(float a,float b,float c,float d,float t) => .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
        public static void SmoothSeam(Mesh mesh,int columns,int rows)
        {
            var normals=mesh.normals;
            for(int j=0;j<rows;j++) { int a=j*columns,b=a+columns-1; var n=(normals[a]+normals[b]).normalized; normals[a]=normals[b]=n; }
            mesh.normals=normals;
        }
    }
}
