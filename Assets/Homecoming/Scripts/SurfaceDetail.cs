using UnityEngine;

namespace Homecoming
{
    public static class SurfaceDetail
    {
        public static Texture2D Weave(string name,bool wood)
        {
            const int size=128; var image=new Texture2D(size,size,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float noise=Mathf.PerlinNoise(x*.071f,y*.025f);
                float grain=wood?Mathf.Sin(x*.5f+Mathf.PerlinNoise(x*.017f,y*.013f)*9)*.055f:(Mathf.Sin(x*Mathf.PI*.5f)*Mathf.Sin(y*Mathf.PI*.5f))*.035f;
                float value=.91f+noise*.10f+grain; pixels[y*size+x]=new Color(value,value,value);
            }
            image.SetPixels(pixels); image.Apply(); return image;
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
