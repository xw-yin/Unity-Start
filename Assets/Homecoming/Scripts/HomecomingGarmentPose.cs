using System;
using UnityEngine;

namespace Homecoming
{
    // Reversible DCC correctives on the source topology, before cloth reads it.
    public static class HomecomingGarmentPose
    {
        [Serializable] class Shape { public string name; public float[] delta; }
        [Serializable] class Package { public string character,mesh; public int vertexCount; public float[] baseVertices; public Shape[] shapes; }
        public static Mesh Create(CharacterRig rig,Mesh source)
        {
            string actor=rig.name.Replace("(Clone)","").Trim();
            var asset=Resources.Load<TextAsset>("Homecoming/Poses/"+actor+"Dress");
            if(!asset)return null;
            var data=JsonUtility.FromJson<Package>(asset.text);
            if(data.character!=actor||data.mesh!=source.name)return null;
            var rest=source.vertices;
            if(data.vertexCount!=rest.Length||data.baseVertices.Length!=rest.Length*3)
                throw new InvalidOperationException("Authored dress topology changed; re-export the source");
            for(int i=0;i<rest.Length;i++)
                if((rest[i]-new Vector3(data.baseVertices[i*3],data.baseVertices[i*3+1],data.baseVertices[i*3+2])).sqrMagnitude>1e-12f)
                    throw new InvalidOperationException("Authored dress source geometry changed; re-export the source");
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name;mesh.hideFlags=HideFlags.DontSave;
            mesh.ClearBlendShapes();
            var delta=new Vector3[rest.Length];var normals=new Vector3[rest.Length];var tangents=new Vector3[rest.Length];
            var seated=new Vector3[rest.Length];int sitting=source.GetBlendShapeIndex("Sitting");
            if(sitting>=0)source.GetBlendShapeFrameVertices(sitting,0,seated,null,null);
            var temporary=new Mesh{vertices=rest,triangles=source.triangles};
            var posed=new Vector3[rest.Length];
            for(int i=0;i<posed.Length;i++)posed[i]=rest[i]+seated[i];
            temporary.vertices=posed;temporary.RecalculateNormals();var seatedNormals=temporary.normals;
            for(int shape=0;shape<source.blendShapeCount;shape++)
            {
                string name=source.GetBlendShapeName(shape);source.GetBlendShapeFrameVertices(shape,0,delta,normals,tangents);
                var authored=Array.Find(data.shapes,s=>s.name==name);
                if(authored!=null)
                {
                    if(authored.delta.Length!=rest.Length*3)throw new InvalidOperationException("Invalid dress corrective");
                    for(int i=0;i<delta.Length;i++){delta[i]=new Vector3(authored.delta[i*3],authored.delta[i*3+1],authored.delta[i*3+2]);posed[i]=rest[i]+seated[i]+delta[i];}
                    temporary.vertices=posed;temporary.RecalculateNormals();var targetNormals=temporary.normals;
                    for(int i=0;i<normals.Length;i++){normals[i]=targetNormals[i]-seatedNormals[i];tangents[i]=Vector3.zero;}
                }
                mesh.AddBlendShapeFrame(name,100,delta,normals,tangents);
            }
            if(Application.isPlaying)UnityEngine.Object.Destroy(temporary);else UnityEngine.Object.DestroyImmediate(temporary);
            return mesh;
        }
    }
}
