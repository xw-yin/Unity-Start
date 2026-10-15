using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class BootExtractionReview
    {
        const string Key="Homecoming.BootExtractionReview";
        [Serializable] class Surface { public float[] vertices,normals,boundary,uv;public int[] triangles;public string alphaTexture;public float cutoff; }
        [Serializable] class Side { public Surface boot,sock,lace;public string mesh;public float[] basis,relaxed,weights,skinMatrices,worldToBoot,minimum,maximum,cuff,lip,axis;public int[] footVertices,boneIndices;public bool masked,restMasked,openFoot;public float heel,ball,soleMax,clearAction; }
        [Serializable] class Frame { public int character,frame;public float elapsed,action;public Side[] sides; }
        public static void RebuildAndRun()
        {
            WitcherCharacterImport.Import("Triss");HomecomingFootwearReview.Run();
            HomecomingSetup.CreateScene();SessionState.SetBool(Key,true);
            SessionState.SetInt("Homecoming.ContinuousReview.First",0);SessionState.SetInt("Homecoming.ContinuousReview.Last",6);
            HomecomingContinuousReview.Run();
        }
        public static void Run(){SessionState.SetBool(Key,true);SessionState.SetInt("Homecoming.ContinuousReview.First",0);SessionState.SetInt("Homecoming.ContinuousReview.Last",6);HomecomingContinuousReview.Run();}
        public static void TrissOnly(){SessionState.SetBool(Key,true);SessionState.SetInt("Homecoming.ContinuousReview.First",2);SessionState.SetInt("Homecoming.ContinuousReview.Last",2);HomecomingContinuousReview.Run();}
        public static void Pair(){SessionState.SetBool(Key,true);SessionState.SetInt("Homecoming.ContinuousReview.First",0);SessionState.SetInt("Homecoming.ContinuousReview.Last",2);HomecomingContinuousReview.Run();}
        public static void FastPair(){SessionState.SetBool(Key,true);HomecomingContinuousReview.FastPair();}
        public static void FastAll(){SessionState.SetBool(Key,true);HomecomingContinuousReview.FastAll();}
        public static void ContactSource()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Dense",true);
            SessionState.SetString(Key+".Output","Artifacts/SeatBootReview/contact-source");
            HomecomingContinuousReview.FastPair();
        }
        public static void RebuildTrissOnly(){WitcherCharacterImport.Import("Triss");HomecomingSetup.CreateScene();TrissOnly();}
        public static void FinishEvidence()
        {
            HomecomingFootwearReview.Run();HomecomingGarmentReview.Run();TrissModelReview.ReviewCloth();TrissModelReview.Probe();BlenderModelBridge.ExportDLCOutfits();
            Debug.Log("BOOT EXTRACTION FINAL EVIDENCE COMPLETE");
        }
        static float[] Values(Vector3 p)=>new[]{p.x,p.y,p.z};
        static Surface Read(Mesh mesh,Transform transform)=>new Surface{vertices=mesh.vertices.SelectMany(v=>Values(transform.TransformPoint(v))).ToArray(),normals=mesh.normals.SelectMany(v=>Values(transform.TransformDirection(v))).ToArray(),triangles=mesh.triangles,boundary=mesh.uv4.Select(v=>v.y-v.x).ToArray(),uv=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray()};
        static Surface Read(SkinnedMeshRenderer skin)
        {
            var mesh=new Mesh();skin.BakeMesh(mesh);var result=Read(mesh,skin.transform);UnityEngine.Object.DestroyImmediate(mesh);
            var mat=skin.sharedMaterial;
            if(mat.HasProperty("_SourceAlphaClip")&&mat.GetFloat("_SourceAlphaClip")>.5f){result.alphaTexture=Path.GetFullPath(AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")));result.cutoff=mat.GetFloat("_SourceCutoff");}
            return result;
        }
        public static void Capture(HomecomingScene story,CharacterRig rig,HomecomingMotion motion,int character,int frame)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(frame>=840)
                for(int side=0;side<2;side++)if(Mathf.Abs(motion.HeelHeight(side)-motion.BallHeight(side))>.008f||motion.HeelHeight(side)>.035f)throw new Exception("Barefoot heel is not planted: "+character+" heel="+motion.HeelHeight(side)+" ball="+motion.BallHeight(side));
            if(character!=0&&character!=2)return;
            int shot=Mathf.RoundToInt(motion.StoryTime(frame/30f)*30);
            float action=motion.ActionTime(frame/30f);
            bool dense=SessionState.GetBool(Key+".Dense",false)&&((action>=11.95f&&action<=motion.Shoes.Cleared(0)+.1f)||(action>=17.95f&&action<=motion.Shoes.Cleared(1)+.1f));
            if(!dense&&!new[]{240,300,360,389,390,396,405,414,420,432,435,450,570,585,594,600,612,630,690,720,750,780,840,900,960}.Contains(shot))return;
            var output=new Frame{character=character,frame=shot,elapsed=frame/30f,action=action,sides=new Side[2]};
            for(int side=0;side<2;side++)
            {
                Surface boot;
                if(action>=HomecomingShoeGrip.Detach(side)){var heldProxy=story.transform.Find("脱下的通勤鞋").GetChild(side);boot=Read(heldProxy.GetComponent<MeshFilter>().sharedMesh,heldProxy);}
                else boot=Read((side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>());
                if(Mathf.Abs(action-HomecomingShoeGrip.Detach(side))<.00001f)
                {
                    var worn=Read((side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>());
                    float maximum=0;
                    for(int i=0;i<boot.vertices.Length;i+=3)
                        maximum=Mathf.Max(maximum,Vector3.Distance(new Vector3(boot.vertices[i],boot.vertices[i+1],boot.vertices[i+2]),new Vector3(worn.vertices[i],worn.vertices[i+1],worn.vertices[i+2])));
                    Debug.Log("BOOT TRANSFER GEOMETRY: character="+character+" side="+side+" maximum world vertex change="+maximum);
                }
                if(boot.vertices.Where((v,i)=>i%3==1).Min()<-.002f)throw new Exception("Boot sole penetrates the floor: "+character+" / "+frame+" / "+side);
                bool dressed=rig.dressedLegs!=null&&rig.dressedLegs.Length==2&&action<HomecomingShoeGrip.Detach(side);
                var skin=dressed?rig.dressedLegs[side]:rig.hosiery[side];var properties=new MaterialPropertyBlock();skin.GetPropertyBlock(properties);
                var lace=rig.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s=>s.name=="YenneferOriginalLace"+(side==0?"Left":"Right"));
                var matrix=properties.GetMatrix("_BootWorldToLocal");
                var proxy=story.transform.Find("脱下的通勤鞋").GetChild(side);
                float FootWeight(int bone,float weight)=>skin.bones[bone].name.Contains("Foot")||skin.bones[bone].name.Contains("Toe")?weight:0;
                var footVertices=skin.sharedMesh.boneWeights.Select((w,i)=>(w,i)).Where(p=>FootWeight(p.w.boneIndex0,p.w.weight0)+FootWeight(p.w.boneIndex1,p.w.weight1)+FootWeight(p.w.boneIndex2,p.w.weight2)+FootWeight(p.w.boneIndex3,p.w.weight3)>.5f).Select(p=>p.i).ToArray();
                var original=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>().sharedMesh;
                output.sides[side]=new Side{boot=boot,sock=Read(skin),mesh=original.name,basis=original.vertices.SelectMany(v=>Values(v)).ToArray(),relaxed=(side==0?rig.leftGroundShoe:rig.rightGroundShoe).GetComponent<MeshFilter>().sharedMesh.vertices.SelectMany(v=>Values(v)).ToArray(),lace=character==0?Read(dressed?rig.dressedHosiery[side]:lace):null,worldToBoot=Enumerable.Range(0,16).Select(i=>matrix[i/4,i%4]).ToArray(),minimum=Values(properties.GetVector("_BootBoundsMin")),maximum=Values(properties.GetVector("_BootBoundsMax")),cuff=Values(motion.Shoes.ProxyContact(proxy,side)),axis=Values(proxy.up),footVertices=footVertices,clearAction=motion.Shoes.Cleared(side),masked=properties.GetFloat("_BootMask")>.5f,restMasked=properties.GetFloat("_BootRestMask")>.5f,openFoot=properties.GetFloat("_OpenFootSoleMask")>.5f,soleMax=properties.GetFloat("_BootSoleMax"),heel=motion.HeelHeight(side),ball=motion.BallHeight(side)};
                var originalSkin=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                output.sides[side].boneIndices=original.boneWeights.SelectMany(w=>new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3}).ToArray();
                output.sides[side].weights=original.boneWeights.SelectMany(w=>new[]{w.weight0,w.weight1,w.weight2,w.weight3}).ToArray();
                output.sides[side].skinMatrices=originalSkin.bones.Select((b,i)=>b.localToWorldMatrix*original.bindposes[i]).SelectMany(m=>Enumerable.Range(0,16).Select(i=>m[i/4,i%4])).ToArray();
                if(action>=HomecomingShoeGrip.Detach(side))
                {
                    output.sides[side].lip=Values(motion.Shoes.ProxyLip(proxy,side));
                    output.sides[side].footVertices=motion.Shoes.OriginalFootVertices(side);
                }
            }
            string folder=SessionState.GetString(Key+".Output","Artifacts/FootwearMotionReview")+"/"+character;Directory.CreateDirectory(folder+"/Geometry");File.WriteAllText(folder+"/Geometry/"+shot+".json",JsonUtility.ToJson(output));
            if(HomecomingContinuousReview.Fast)return;
            var camera=story.CameraControls.GetComponent<Camera>();var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var aim=rig.transform.position+new Vector3(0,.80f,0);camera.transform.position=aim+new Vector3(.08f,.07f,-2.7f);camera.transform.LookAt(aim);camera.fieldOfView=38;
            HomecomingContinuousReview.CaptureGpu(camera,folder+"/"+frame+"-front.png");
            aim=rig.transform.position+new Vector3(0,.36f,-.03f);camera.transform.position=aim+new Vector3(1.1f,.06f,-.70f);camera.transform.LookAt(aim);camera.fieldOfView=39;
            HomecomingContinuousReview.CaptureGpu(camera,folder+"/"+frame+"-feet.png");
            camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
        }
    }
}
