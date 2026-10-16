using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Homecoming.Editor
{
    [InitializeOnLoad]
    public static class HomecomingMotionReview
    {
        static HomecomingMotionReview() { EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(!File.Exists("Artifacts/review-motion.request") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if(EditorApplication.isPlaying) { EditorApplication.isPlaying=false; return; }
            File.Delete("Artifacts/review-motion.request");
            try { Run(); File.WriteAllText("Artifacts/Motion-review-result.txt","PASS"); }
            catch(System.Exception e) { Debug.LogException(e); File.WriteAllText("Artifacts/Motion-review-result.txt",e.ToString()); }
        }
        public static void Run()
        {
            HomecomingSetup.CreateScene();
            var story=Object.FindAnyObjectByType<HomecomingScene>();
            // Scene serialization replaces temporary meshes; evaluate a fresh runtime actor.
            story.ReloadCharacter();
            var rig=story.GetComponentInChildren<CharacterRig>();
            var timelineFlags=BindingFlags.NonPublic|BindingFlags.Instance;
            var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",timelineFlags).GetValue(story);
            motion.Sample(0);
            if(rig.transform.position.z<3.8f) throw new System.Exception("Initial character must be outside the door");
            if(!Resources.Load<HomecomingWalkCycle>("Homecoming/WalkCycle")) throw new System.Exception("Retargeted walking animation is missing");
            var bones=new[]{rig.hips,rig.leftCalf,rig.rightCalf,rig.leftElbow,rig.rightElbow};
            var door=story.transform.Find("入户门转轴");
            var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.enabled).ToArray();
            var visible=new System.Collections.Generic.Dictionary<SkinnedMeshRenderer,bool[]>();
            foreach(var skin in skins)
            {
                var mask=new bool[skin.sharedMesh.vertexCount];
                var texture=skin.sharedMaterial.GetTexture("_BaseMap");
                var path=AssetDatabase.GetAssetPath(texture);
                Texture2D pixels=null;
                if(File.Exists(path)) { pixels=new Texture2D(2,2); pixels.LoadImage(File.ReadAllBytes(path)); }
                var uv=skin.sharedMesh.uv;
                for(int i=0;i<mask.Length;i++) mask[i]=!pixels || pixels.GetPixelBilinear(uv[i].x,uv[i].y).a>=skin.sharedMaterial.GetFloat("_Cutoff");
                visible.Add(skin,mask);
                var used=new bool[mask.Length];foreach(int index in skin.sharedMesh.triangles)used[index]=true;
                for(int i=0;i<mask.Length;i++)mask[i]&=used[i];
                if(pixels) Object.DestroyImmediate(pixels);
            }
            var cushion=story.GetComponentsInChildren<Renderer>().Single(x=>x.name=="换鞋凳软垫").bounds;
            var baked=new Mesh();
            var previous=new Vector3[bones.Length]; float maximum=0; string worst="";
            typeof(HomecomingScene).GetField("started",timelineFlags).SetValue(story,true);
            for(int frame=0;frame<=33*60;frame++)
            {
                typeof(HomecomingScene).GetField("elapsed",timelineFlags).SetValue(story,frame/60f);
                typeof(HomecomingScene).GetMethod("Update",timelineFlags).Invoke(story,null);
                door.localRotation=Quaternion.Euler(0,HomecomingScene.DoorAngle(frame/60f),0);
                for(int i=0;i<bones.Length;i++)
                {
                    float jump=Vector3.Distance(previous[i],bones[i].position);
                    if(frame>0 && jump>maximum) { maximum=jump; worst="frame="+frame+" bone="+bones[i].name; }
                    previous[i]=bones[i].position;
                }
                for(int side=0;side<2;side++)
                {
                    float action=motion.ActionTime(frame/60f);
                    float start=HomecomingShoeGrip.Start(side);
                    if(action>start+1.7f&&action<start+1.98f)
                    {
                        float error=Vector3.Distance(motion.Shoes.HandContact(side),motion.Shoes.HeelOnFoot(side));
                        if(error>.018f) throw new System.Exception("Hand missed shoe heel: side="+side+" time="+frame/60f+" error="+error);
                    }
                    if(action>HomecomingShoeGrip.Release(side)-.1f&&action<HomecomingShoeGrip.Release(side))
                    {
                        float error=Vector3.Distance(motion.Shoes.HandContact(side),motion.Shoes.GroundHeel(side));
                        if(error>.025f) throw new System.Exception("Hand cannot set shoe down: side="+side+" time="+frame/60f+" error="+error);
                    }
                }
                if(rig.hips.position.y<.535f) throw new System.Exception("Pelvis below cushion clearance");
                if(frame%6==0)
                {
                    foreach(var skin in skins)
                    {
                        if(!skin.gameObject.activeInHierarchy) continue;
                        skin.BakeMesh(baked);
                        var vertices=baked.vertices;
                        for(int vi=0;vi<vertices.Length;vi++)
                        {
                            if(!visible[skin][vi]) continue;
                            var p=skin.transform.TransformPoint(vertices[vi]);
                            if(p.z>3.425f&&p.z<3.575f&&p.y>.05f&&p.y<2.62f&&(p.x<-2.12f||p.x>-.78f))
                                throw new System.Exception("Wall penetration at "+frame/60f+"s: "+skin.name+" "+p);
                            var inDoor=door.InverseTransformPoint(p);
                            if(inDoor.x>.015f&&inDoor.x<1.195f&&inDoor.y>.05f&&inDoor.y<2.45f&&Mathf.Abs(inDoor.z)<.035f)
                                throw new System.Exception("Door penetration at "+frame/60f+"s: "+skin.name+" "+p);
                            if(p.x>cushion.min.x+.02f&&p.x<cushion.max.x-.02f&&p.z>cushion.min.z+.02f&&p.z<cushion.max.z-.02f&&p.y>cushion.min.y+.02f&&p.y<cushion.max.y-.02f)
                                throw new System.Exception("Bench penetration at "+frame/60f+"s: "+skin.name+" "+p+" bone="+skin.bones[skin.sharedMesh.boneWeights[vi].boneIndex0].name);
                            if(p.y<-.012f) throw new System.Exception("Floor penetration at "+frame/60f+"s: "+skin.name+" "+p+" weight="+skin.sharedMesh.boneWeights[vi].boneIndex0+" bone="+skin.bones[skin.sharedMesh.boneWeights[vi].boneIndex0].name);
                        }
                    }
                }
            }
            Object.DestroyImmediate(baked);
            if(maximum>.06f) throw new System.Exception("Discontinuous motion: "+maximum+" "+worst);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
            var camera=story.GetComponentInChildren<Camera>();
            int n=0;
            foreach(float t in new[]{0f,6f,10f,12.8f,13.5f,14.5f,15.3f,16f,18.8f,19.5f,20.5f,21.3f,22f,25f,28f})
            {
                typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,t);
                typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
                camera.transform.position=new Vector3(.0f,1.55f,-1.6f);
                camera.transform.LookAt(new Vector3(-1.35f,.75f,1.35f)); camera.fieldOfView=43;
                HomecomingSetup.Capture(camera,"Artifacts/Motion-"+(n++)+".png",640,640);
            }
            typeof(HomecomingScene).GetMethod("ResetStory",flags).Invoke(story,null);
            Debug.Log("MOTION REVIEW PASSED: 1981 frames, maximum joint travel/frame="+maximum);
        }
        public static void RenderPreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            var story=Object.FindAnyObjectByType<HomecomingScene>();
            story.Build();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
            var camera=story.GetComponentInChildren<Camera>();
            Directory.CreateDirectory("Artifacts/MotionFrames");
            for(int frame=0;frame<232;frame++)
            {
                typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,frame/8f);
                typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
                camera.transform.position=new Vector3(0,1.55f,-1.6f);
                camera.transform.LookAt(new Vector3(-1.35f,.75f,1.35f)); camera.fieldOfView=43;
                HomecomingSetup.Capture(camera,"Artifacts/MotionFrames/"+frame.ToString("D3")+".png",480,480);
            }
            Debug.Log("MOTION PREVIEW RENDERED: 232 frames.");
        }
    }
}
