using UnityEngine;
using System.Linq;

namespace Homecoming
{
    public sealed class HomecomingShoeGrip
    {
        readonly HomecomingHandPose palms;
        readonly Quaternion[] handRotation=new Quaternion[2];
        readonly bool[] hasHandRotation=new bool[2];
        readonly Transform[] feet=new Transform[2];
        readonly Transform[] calves=new Transform[2];
        readonly Vector3[] origins=new Vector3[2],heels=new Vector3[2];
        readonly Quaternion[] rest=new Quaternion[2];
        readonly Quaternion[] calfRest=new Quaternion[2];
        readonly bool longBoots;
        readonly bool ankleBoots;
        readonly bool openToeFootwear;
        readonly Vector3[][] bootVertices=new Vector3[2][];
        readonly SkinnedMeshRenderer[] skins=new SkinnedMeshRenderer[2];
        readonly SkinnedMeshRenderer[] feetSkins=new SkinnedMeshRenderer[2];
        readonly int[][] footVertices=new int[2][];
        readonly int[][] mouthVertices=new int[2][];
        public bool FittedLongBoots=>longBoots&&(originalBoot[0]||openToeFootwear);
        readonly int[] cuffVertices=new int[2];
        readonly bool[] originalBoot=new bool[2];
        readonly Vector3[][] sourceVertices=new Vector3[2][];
        readonly BoneWeight[][] sourceWeights=new BoneWeight[2][];
        readonly Matrix4x4[][] bindposes=new Matrix4x4[2][];
        readonly Transform[][] sourceBones=new Transform[2][];
        readonly Vector3[][] relaxedVertices=new Vector3[2][];
        readonly HomecomingBootSnapshot[] snapshots=new HomecomingBootSnapshot[2];
        readonly bool[] captured=new bool[2];
        readonly Vector3[] extractionCuff=new Vector3[2];
        readonly Quaternion[] extractionRotation=new Quaternion[2];
        public bool AnatomicalExtraction { get; private set; }
        public HomecomingShoeGrip(CharacterRig rig,HomecomingHandPose handPose)
        {
            palms=handPose;longBoots=rig.longBoots;
            openToeFootwear=rig.openToeFootwear;
            ankleBoots=rig.footwearKind==CharacterRig.FootwearKind.AnkleBoot;
            calves[0]=rig.leftCalf;calves[1]=rig.rightCalf;
            calfRest[0]=calves[0].rotation;calfRest[1]=calves[1].rotation;
            foreach(var bone in rig.GetComponentsInChildren<Transform>(true))
            {
                if(bone.name=="J_Bip_L_Foot") feet[0]=bone;
                if(bone.name=="J_Bip_R_Foot") feet[1]=bone;
            }
            if(!rig.importedSource) return;
            var mesh=new Mesh();
            for(int side=0;side<2;side++)
            {
                feetSkins[side]=rig.hosiery[side];
                float FootWeight(int index,float weight)=>feetSkins[side].bones[index].name.Contains("Foot")||feetSkins[side].bones[index].name.Contains("Toe")?weight:0;
                footVertices[side]=feetSkins[side].sharedMesh.boneWeights.Select((w,i)=>(w,i)).Where(p=>FootWeight(p.w.boneIndex0,p.w.weight0)+FootWeight(p.w.boneIndex1,p.w.weight1)+FootWeight(p.w.boneIndex2,p.w.weight2)+FootWeight(p.w.boneIndex3,p.w.weight3)>.5f).Select(p=>p.i).ToArray();
                var skin=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                skins[side]=skin;originalBoot[side]=skin.sharedMesh.name.StartsWith("OriginalYenneferBoot");
                sourceVertices[side]=skin.sharedMesh.vertices;sourceWeights[side]=skin.sharedMesh.boneWeights;bindposes[side]=skin.sharedMesh.bindposes;
                sourceBones[side]=skin.bones;
                foreach(var w in sourceWeights[side])
                    if((skin.bones[w.boneIndex0]==calves[side]&&w.weight0>.1f)||(skin.bones[w.boneIndex1]==calves[side]&&w.weight1>.1f)||(skin.bones[w.boneIndex2]==calves[side]&&w.weight2>.1f)||(skin.bones[w.boneIndex3]==calves[side]&&w.weight3>.1f))AnatomicalExtraction=true;
                skin.BakeMesh(mesh);
                var vertices=mesh.vertices; var bounds=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);
                foreach(var v in vertices) bounds.Encapsulate(skin.transform.TransformPoint(v));
                var origin=bounds.center; origin.y=bounds.min.y;
                origins[side]=feet[side].InverseTransformPoint(origin); rest[side]=feet[side].rotation;
                var ground=(side==0?rig.leftGroundShoe:rig.rightGroundShoe).GetComponent<MeshFilter>().sharedMesh;
                bootVertices[side]=ground.vertices;
                relaxedVertices[side]=ground.vertices;
                var wanted=longBoots||ankleBoots?new Vector3(HandSide(side)==0?ground.bounds.max.x-.008f:ground.bounds.min.x+.008f,ground.bounds.max.y-.02f,ground.bounds.min.z+.055f):new Vector3(0,ground.bounds.max.y*.75f,ground.bounds.max.z-.018f);
                float distance=float.MaxValue;
                var points=ground.vertices;
                var cuff=skin.sharedMesh.uv2;
                for(int i=0;i<points.Length;i++)
                {
                    if(originalBoot[side]&&cuff[i].x<.5f)continue;
                    var v=points[i];
                    var delta=v-wanted; float score=delta.x*delta.x*3+delta.y*delta.y+delta.z*delta.z;
                    if(score<distance) { distance=score; heels[side]=v;cuffVertices[side]=i; }
                }
                mouthVertices[side]=FindMouth(ground,heels[side],originalBoot[side]);
            }
            if(Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh);
        }
        // Reach shortly after settling into the seat. These are authored action
        // times (the walk clock remaps arrival to action 7), so they stay tied
        // to the arrival instead of waiting on the old longer walk duration.
        public static float Start(int side)=>side==0?9:14.5f;
        public static float Detach(int side)=>Start(side)+2;
        public float Cleared(int side)=>Start(side)+(longBoots?(originalBoot[side]||openToeFootwear?3.4f:3.05f):2.8f);
        public static float Release(int side)=>Start(side)+4.4f;
        public Vector3 ExtractionAxis(int side)=>(captured[side]?extractionRotation[side]:AttachedRotation(side))*Vector3.up;
        static float Ease(float t,float a,float b) { float u=Mathf.Clamp01((t-a)/(b-a)); return u*u*u*(u*(u*6-15)+10); }
        Quaternion AttachedRotation(int side)=>longBoots?calves[side].rotation*Quaternion.Inverse(calfRest[side]):feet[side].rotation*Quaternion.Inverse(rest[side]);
        public Vector3 HeelOnFoot(int side)=>AnatomicalExtraction||originalBoot[side]?SkinnedCuff(side):feet[side].position+AttachedRotation(side)*(rest[side]*origins[side]+heels[side]);
        Vector3 SkinnedCuff(int side)
        {
            var skin=skins[side];int index=cuffVertices[side];
            var v=sourceVertices[side][index];var w=sourceWeights[side][index];var bind=bindposes[side];
            Vector3 Point(int bone)=>(sourceBones[side][bone].localToWorldMatrix*bind[bone]).MultiplyPoint3x4(v);
            return Point(w.boneIndex0)*w.weight0+Point(w.boneIndex1)*w.weight1+Point(w.boneIndex2)*w.weight2+Point(w.boneIndex3)*w.weight3;
        }
        public Matrix4x4 AttachedMatrix(int side)=>Matrix4x4.TRS(HeelOnFoot(side)-AttachedRotation(side)*heels[side],AttachedRotation(side),Vector3.one);
        public Vector3 GroundOrigin(int side)=>longBoots
            ?new Vector3(HandSide(side)==0?(AnatomicalExtraction?(originalBoot[side]?-.90f:-.79f):-.64f):(AnatomicalExtraction?(originalBoot[side]?-1.60f:-1.71f):-1.86f),.012f,AnatomicalExtraction?1.13f:1.23f)
            :new Vector3(side==0?-.86f:-1.64f,.012f,1.08f);
        void CaptureExtraction(int side,float action)
        {
            if(!AnatomicalExtraction)return;
            if(action<Detach(side)){captured[side]=false;return;}
            if(captured[side])return;
            captured[side]=true;extractionCuff[side]=HeelOnFoot(side);extractionRotation[side]=AttachedRotation(side);
            // The first wrist solve must already use the currently bent sole.
            // Waiting for PlaceProxy to capture it can change the floor target
            // after the hand has been solved on that same frame.
            var boneMatrices=new Matrix4x4[sourceBones[side].Length];
            for(int b=0;b<boneMatrices.Length;b++)boneMatrices[b]=sourceBones[side][b].localToWorldMatrix*bindposes[side][b];
            var points=new Vector3[sourceVertices[side].Length];var inverse=Quaternion.Inverse(extractionRotation[side]);
            var origin=extractionCuff[side]-extractionRotation[side]*heels[side];
            for(int i=0;i<points.Length;i++)
            {
                var p=sourceVertices[side][i];var w=sourceWeights[side][i];
                var world=boneMatrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+boneMatrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+boneMatrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+boneMatrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                points[i]=inverse*(world-origin);
            }
            bootVertices[side]=points;
        }
        public Quaternion Rotation(int side,float action)
        {
            CaptureExtraction(side,action);
            return Quaternion.Slerp(longBoots?(captured[side]?extractionRotation[side]:AttachedRotation(side)):Quaternion.identity,Quaternion.Euler(0,HandSide(side)==0?-90:90,0),Ease(action,Cleared(side),Release(side)));
        }
        float Fold(int side,float action)
        {
            float loaded=originalBoot[side]?.45f:openToeFootwear?.45f:.0875f;
            float empty=originalBoot[side]?.65f:openToeFootwear?.5f:.35f;
            return loaded*Ease(action,Detach(side),Cleared(side))+(empty-loaded)*Ease(action,Cleared(side),Release(side));
        }
        public Vector3 GroundHeel(int side)
        {
            float bottom=0;if(snapshots[side]){bottom=float.MaxValue;foreach(var v in bootVertices[side])bottom=Mathf.Min(bottom,v.y);}
            return GroundOrigin(side)-Vector3.up*bottom+Quaternion.Euler(0,HandSide(side)==0?-90:90,0)*heels[side];
        }
        public int HandSide(int footwearSide)=>longBoots?1-footwearSide:footwearSide;
        public int FootwearSide(int handSide)=>longBoots?1-handSide:handSide;
        public void SetHandRotation(int side,Quaternion rotation){handRotation[side]=rotation;hasHandRotation[side]=true;}
        Quaternion GripRotation(int side,float action)=>hasHandRotation[side]?handRotation[side]:palms.BootRotation(HandSide(side),Rotation(side,action));
        public Vector3 WristTarget(int side,float action)
        {
            CaptureExtraction(side,action);
            if(snapshots[side])
            {
                snapshots[side].UpdateShape(skins[side],extractionRotation[side],extractionCuff[side],heels[side],relaxedVertices[side],Ease(action,Cleared(side),Release(side)),Fold(side,action),Mathf.InverseLerp(Detach(side),Cleared(side),action));
                bootVertices[side]=snapshots[side].Points;
            }
            float start=Start(side);
            if(longBoots||ankleBoots)
            {
                var axis=(captured[side]?extractionRotation[side]:AttachedRotation(side))*Vector3.up;
                // The fitted leg also withdraws through the opening. Share the
                // clearance between that motion and the hand's pull rather than
                // requiring the arm to travel almost the whole shaft length.
                float clearance=AnatomicalExtraction?Mathf.Max(.34f,skins[side].sharedMesh.bounds.size.y-(originalBoot[side]?.24f:openToeFootwear?.18f:.10f)):longBoots?.50f:.22f;
                // Share the soft DLC boot's exit with the withdrawing leg.
                // Pulling its cuff to ankle height exceeded a seated arm's reach.
                if(originalBoot[side])clearance=Mathf.Min(clearance,.43f);
                if(longBoots)clearance+=.015f;
                float clearTime=Cleared(side)-start;
                var cuff=(captured[side]?extractionCuff[side]:HeelOnFoot(side))-axis*clearance*Ease(action,start+2,start+clearTime);
                cuff=Vector3.Lerp(cuff,GroundHeel(side),Ease(action,start+clearTime,Release(side)));
                // Carry the freed boot along a lifted arc before easing it down.
                float carry=Ease(action,start+clearTime,Release(side));
                cuff+=Vector3.up*.14f*Mathf.Sin(Mathf.PI*carry);
                // Move the extracted boot around the returning foot, rather
                // than carrying its captured toe box through that foot.
                if(longBoots)cuff+=Vector3.right*(HandSide(side)==0?1:-1)*.16f*Mathf.Sin(Mathf.PI*carry);
                if(action>=Detach(side)&&!(originalBoot[side]&&action<=Detach(side)+.00001f))
                {
                    var turn=Rotation(side,action);float bottom=float.MaxValue;
                    foreach(var v in bootVertices[side]) bottom=Mathf.Min(bottom,(cuff+turn*(v-heels[side])).y);
                    if(bottom<.012f)cuff.y+=.012f-bottom;
                }
                return palms.Wrist(HandSide(side),HomecomingHandPose.Hold.Boot,cuff,GripRotation(side,action));
            }
            var grip=HeelOnFoot(side)+new Vector3(0,-.025f,.17f)*Ease(action,start+2,start+2.8f);
            grip=Vector3.Lerp(grip,GroundHeel(side),Ease(action,start+2.8f,Release(side)));
            return palms.Wrist(HandSide(side),HomecomingHandPose.Hold.Boot,grip,GripRotation(side,action));
        }
        public Vector3 HandContact(int side)=>palms.Contact(HandSide(side),HomecomingHandPose.Hold.Boot);
        public void PlaceProxy(Transform proxy,int side,float action)
        {
            CaptureExtraction(side,action);
            if((AnatomicalExtraction||originalBoot[side])&&action>=Detach(side))
            {
                var snapshot=proxy.GetComponent<HomecomingBootSnapshot>();
                if(!snapshot)snapshot=proxy.gameObject.AddComponent<HomecomingBootSnapshot>();
                if(snapshots[side]!=snapshot)snapshot.ResetCapture();
                snapshots[side]=snapshot;
                snapshot.UpdateShape(skins[side],extractionRotation[side],extractionCuff[side],heels[side],relaxedVertices[side],Ease(action,Cleared(side),Release(side)),Fold(side,action),Mathf.InverseLerp(Detach(side),Cleared(side),action));
                bootVertices[side]=snapshot.Points;
            }
            if(action<Release(side))
            {
                proxy.rotation=Rotation(side,action);
                // The shoe follows its independently planned path, not the
                // solved fingertips. ContactError can now reveal a failed reach.
                var turn=GripRotation(side,action);
                var plannedContact=WristTarget(side,action)-palms.Wrist(HandSide(side),HomecomingHandPose.Hold.Boot,Vector3.zero,turn);
                proxy.position=plannedContact-proxy.rotation*heels[side];
            }
            else
            {
                float bottom=snapshots[side]?proxy.GetComponent<MeshFilter>().sharedMesh.bounds.min.y:0;
                proxy.SetPositionAndRotation(GroundOrigin(side)-Vector3.up*bottom,Quaternion.Euler(0,HandSide(side)==0?-90:90,0));
            }
        }
        public float ContactError(Transform proxy,int side)=>Vector3.Distance(proxy.TransformPoint(heels[side]),HandContact(side));
        public Vector3 ProxyContact(Transform proxy,int side)=>proxy.TransformPoint(heels[side]);
        public int[] OriginalFootVertices(int side)=>footVertices[side];
        public Vector3 ProxyLip(Transform proxy,int side)
        {
            var vertices=proxy.GetComponent<MeshFilter>().sharedMesh.vertices;
            var point=heels[side];point.y=mouthVertices[side].Max(i=>vertices[i].y);
            return proxy.TransformPoint(point);
        }
        public float FootExitClearance(Transform proxy,int side)=>proxy.GetComponent<HomecomingBootSnapshot>().FootClearance(feetSkins[side],footVertices[side],mouthVertices[side],heels[side]);
        static int[] FindMouth(Mesh template,Vector3 grip,bool openBoundary)
        {
            var positions=template.vertices;
            if(!openBoundary){float top=positions.Max(p=>p.y);return Enumerable.Range(0,positions.Length).Where(i=>positions[i].y>=top-.018f).ToArray();}
            var welded=new System.Collections.Generic.Dictionary<Vector3Int,int>();
            var points=new System.Collections.Generic.List<Vector3>();var ids=new int[positions.Length];
            for(int i=0;i<positions.Length;i++)
            {
                var p=positions[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
                if(!welded.TryGetValue(key,out int id)){id=points.Count;welded.Add(key,id);points.Add(p);}ids[i]=id;
            }
            var edges=new System.Collections.Generic.Dictionary<(int,int),int>();var tris=template.triangles;
            void Edge(int a,int b){var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out int count);edges[key]=count+1;}
            for(int i=0;i<tris.Length;i+=3){Edge(ids[tris[i]],ids[tris[i+1]]);Edge(ids[tris[i+1]],ids[tris[i+2]]);Edge(ids[tris[i+2]],ids[tris[i]]);}
            var adjacency=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<int>>();
            void Link(int a,int b){if(!adjacency.TryGetValue(a,out var list)){list=new System.Collections.Generic.List<int>();adjacency[a]=list;}list.Add(b);}
            foreach(var edge in edges)if(edge.Value==1){Link(edge.Key.Item1,edge.Key.Item2);Link(edge.Key.Item2,edge.Key.Item1);}
            var remaining=new System.Collections.Generic.HashSet<int>(adjacency.Keys);System.Collections.Generic.HashSet<int> best=null;float score=float.MaxValue;
            while(remaining.Count>0)
            {
                int first=remaining.First();remaining.Remove(first);var part=new System.Collections.Generic.HashSet<int>{first};var queue=new System.Collections.Generic.Queue<int>();queue.Enqueue(first);
                while(queue.Count>0)foreach(int next in adjacency[queue.Dequeue()])if(remaining.Remove(next)){part.Add(next);queue.Enqueue(next);}
                if(part.Count<8)continue;var bounds=new Bounds(points[first],Vector3.zero);var centre=Vector3.zero;
                foreach(int id in part){bounds.Encapsulate(points[id]);centre+=points[id];}centre/=part.Count;
                if(bounds.size.x<.04f||bounds.size.z<.035f||centre.y<.3f)continue;
                float distance=(centre-grip).sqrMagnitude;if(distance<score){score=distance;best=part;}
            }
            if(best==null)throw new System.InvalidOperationException("Original boot mouth boundary could not be identified");
            return Enumerable.Range(0,ids.Length).Where(i=>best.Contains(ids[i])).ToArray();
        }
    }
}
