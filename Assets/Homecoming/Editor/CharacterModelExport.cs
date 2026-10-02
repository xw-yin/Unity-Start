using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class CharacterModelExport
    {
        // OBJ is a neutral-pose interchange model. The Unity prefab retains articulation and skin data.
        public static void Save(CharacterRig rig,string directory)
        {
            Directory.CreateDirectory(directory);
            var geometry=new StringBuilder("# Xia: original stylized adult character\nmtllib Xia.mtl\n");
            var materials=new StringBuilder(); var names=new Dictionary<Material,string>(); int offset=1,part=0;
            foreach(var renderer in rig.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh=null; bool temporary=false;
                if(renderer is SkinnedMeshRenderer skin) { mesh=new Mesh(); skin.BakeMesh(mesh); temporary=true; }
                else { var filter=renderer.GetComponent<MeshFilter>(); if(filter) mesh=filter.sharedMesh; }
                if(!mesh) continue;
                var material=renderer.sharedMaterial;
                if(!names.TryGetValue(material,out string materialName))
                {
                    materialName="Material_"+names.Count; names.Add(material,materialName); Color color=material.color;
                    materials.AppendLine("newmtl "+materialName); materials.AppendLine("Kd "+F(color.r)+" "+F(color.g)+" "+F(color.b)); materials.AppendLine("Ks 0.08 0.08 0.08\nNs 24\n");
                }
                geometry.AppendLine("o Part_"+part++); geometry.AppendLine("usemtl "+materialName);
                var matrix=rig.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                foreach(var vertex in mesh.vertices) { var v=matrix.MultiplyPoint3x4(vertex); geometry.AppendLine("v "+F(v.x)+" "+F(v.y)+" "+F(-v.z)); }
                foreach(var tex in mesh.uv) geometry.AppendLine("vt "+F(tex.x)+" "+F(tex.y));
                foreach(var normal in mesh.normals) { var n=matrix.MultiplyVector(normal).normalized; geometry.AppendLine("vn "+F(n.x)+" "+F(n.y)+" "+F(-n.z)); }
                var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3) geometry.AppendLine("f "+Index(indices[i]+offset)+" "+Index(indices[i+2]+offset)+" "+Index(indices[i+1]+offset));
                offset+=mesh.vertexCount;
                if(temporary) Object.DestroyImmediate(mesh);
            }
            File.WriteAllText(Path.Combine(directory,"Xia.obj"),geometry.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory,"Xia.mtl"),materials.ToString(),new UTF8Encoding(false));
        }
        static string F(float value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
        static string Index(int value)=>value+"/"+value+"/"+value;
    }
}
