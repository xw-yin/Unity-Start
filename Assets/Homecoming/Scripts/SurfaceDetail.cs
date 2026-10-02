using UnityEngine;

namespace Homecoming
{
    public static class SurfaceDetail
    {
        public static Texture2D Weave(string name,bool wood)
        {
            const int size=128; var image=new Texture2D(size,size,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float noise=Mathf.PerlinNoise(x*.071f,y*.025f);
                float grain=wood?Mathf.Sin(x*.5f+Mathf.PerlinNoise(x*.017f,y*.013f)*9)*.025f:(Mathf.Sin(x*Mathf.PI*.5f)*Mathf.Sin(y*Mathf.PI*.5f))*.018f;
                float value=.94f+noise*.06f+grain; pixels[y*size+x]=new Color(value,value,value);
            }
            image.SetPixels(pixels); image.Apply(); return image;
        }
        public static Mesh Leaf(float length,float width)
        {
            const int rows=12;
            var vertices=new Vector3[(rows+1)*3]; var uv=new Vector2[vertices.Length]; var triangles=new int[rows*12]; int index=0;
            for(int i=0;i<=rows;i++)
            {
                float t=(float)i/rows,half=width*.5f*Mathf.Sin(t*Mathf.PI);
                float bend=length*(.16f*Mathf.Sin(t*Mathf.PI)-.12f*t*t);
                for(int j=0;j<3;j++) { int n=i*3+j; vertices[n]=new Vector3(t*length,bend+(j==1?half*.18f:0),(j-1)*half); uv[n]=new Vector2(t,j*.5f); }
                if(i==rows) continue;
                for(int j=0;j<2;j++) { int n=i*3+j; triangles[index++]=n; triangles[index++]=n+1; triangles[index++]=n+3; triangles[index++]=n+1; triangles[index++]=n+4; triangles[index++]=n+3; }
            }
            var mesh=new Mesh{name="弧形叶片",vertices=vertices,uv=uv,triangles=triangles}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        public static Mesh Curtain(string name,float width,float height)
        {
            const int rows=20,columns=36;
            var v=new Vector3[(rows+1)*(columns+1)]; var uv=new Vector2[v.Length]; var tri=new int[rows*columns*6]; int index=0;
            for(int j=0;j<=rows;j++) for(int i=0;i<=columns;i++)
            {
                float u=(float)i/columns,t=(float)j/rows;
                int n=j*(columns+1)+i;
                v[n]=new Vector3((u-.5f)*width*(.86f+.14f*t),height*t-.016f*Mathf.Cos(u*8*Mathf.PI)*(1-t),.033f*Mathf.Cos(u*8*Mathf.PI)); uv[n]=new Vector2(u,t);
                if(j<rows&&i<columns) { int b=n+columns+1; tri[index++]=n; tri[index++]=b; tri[index++]=n+1; tri[index++]=n+1; tri[index++]=b; tri[index++]=b+1; }
            }
            var mesh=new Mesh{name=name,vertices=v,uv=uv,triangles=tri}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
