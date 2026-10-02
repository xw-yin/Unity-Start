using UnityEngine;
using R = Homecoming.ModelMesh.Ring;

namespace Homecoming
{
    /// <summary>Original stylized adult character: mesh surfaces, articulated limbs and a weighted skirt.</summary>
    public sealed class XiaModelBuilder
    {
        readonly Material skin,hair,blouse,skirt,leather,pink,white,iris,pupil,hairLight,trim,sole,blush;
        CharacterRig rig;
        public XiaModelBuilder(Material skin,Material hair,Material blouse,Material skirt,Material leather,Material pink)
        {
            this.skin=skin; this.hair=hair; this.blouse=blouse; this.skirt=skirt; this.leather=leather; this.pink=pink;
            blouse.SetTexture("_BaseMap",SurfaceDetail.Weave("针织开衫纹理",false));
            skirt.SetTexture("_BaseMap",SurfaceDetail.Weave("半裙织物纹理",false));
            white=Material("眼白与衬衣",new Color(.98f,.95f,.88f)); iris=Material("琥珀色虹膜",new Color(.38f,.16f,.07f),.65f);
            pupil=Material("瞳孔与睫毛",new Color(.025f,.014f,.022f)); hairLight=Material("发丝高光",new Color(.29f,.14f,.075f));
            trim=Material("针织罗纹",new Color(.70f,.53f,.35f)); sole=Material("鞋底橡胶",new Color(.12f,.095f,.08f));
            blush=Material("淡玫瑰腮红",new Color(.89f,.60f,.52f));
        }
        static Material Material(string name,Color color,float smoothness=.25f)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name}; m.color=color; m.SetFloat("_Smoothness",smoothness);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*.055f); return m;
        }
        Transform Joint(string name,Vector3 p,Transform parent)
        {
            var g=new GameObject(name); g.transform.SetParent(parent,false); g.transform.localPosition=p; return g.transform;
        }
        GameObject Loft(string name,R[] profile,Material material,Transform parent,Vector3 p=default,int sides=40,float pleat=0)
            => ModelMesh.Object(name,ModelMesh.Loft(name,profile,sides,pleat),material,parent,p);
        GameObject Oval(string name,Vector3 radius,Vector3 p,Material material,Transform parent)
            => ModelMesh.Object(name,ModelMesh.Ellipsoid(name,radius),material,parent,p);
        GameObject Rounded(string name,Vector3 size,Vector3 p,float rounding,Material material,Transform parent)
            => ModelMesh.Object(name,ModelMesh.RoundedBox(name,size,rounding),material,parent,p);
        GameObject Line(string name,Vector3[] points,float radius,Material material,Transform parent)
        {
            var radii=new float[points.Length]; for(int i=0;i<radii.Length;i++) radii[i]=radius;
            return ModelMesh.Object(name,ModelMesh.Tube(name,points,radii),material,parent,Vector3.zero);
        }
        public CharacterRig Build(Transform parent)
        {
            var root=Joint("小夏 · 日系卡通角色",new Vector3(-1.45f,0,2.82f),parent);
            rig=root.gameObject.AddComponent<CharacterRig>();
            rig.hips=Joint("Hips",new Vector3(0,.91f,0),root);
            rig.spine=Joint("Spine",new Vector3(0,.10f,0),rig.hips);
            Loft("腰腹 · 连续拓扑",new[]{new R(-.12f,.14f,.092f),new R(0,.15f,.105f),new R(.10f,.125f,.083f),new R(.20f,.14f,.088f)},skin,rig.hips);
            Loft("开衫 · 收腰衣身",new[]{new R(-.025f,.143f,.101f),new R(0,.145f,.10f),new R(.10f,.135f,.089f),new R(.23f,.166f,.106f,-.006f),new R(.33f,.180f,.109f),new R(.40f,.195f,.099f),new R(.44f,.177f,.083f),new R(.47f,.095f,.063f),new R(.485f,.063f,.048f)},blouse,rig.spine);
            Loft("开衫下摆 · 罗纹",new[]{new R(-.033f,.145f,.104f),new R(-.012f,.146f,.104f),new R(.006f,.146f,.103f)},trim,rig.spine);
            Loft("衬衣领口",new[]{new R(.436f,.065f,.052f),new R(.481f,.061f,.046f),new R(.493f,.057f,.043f)},white,rig.spine);
            Loft("脖颈",new[]{new R(.44f,.053f,.043f),new R(.48f,.043f,.039f),new R(.55f,.047f,.042f),new R(.58f,.054f,.043f)},skin,rig.spine);
            var collarLeft=Rounded("左衬衣领",new Vector3(.071f,.067f,.017f),new Vector3(-.055f,.435f,-.072f),.006f,white,rig.spine); collarLeft.transform.localEulerAngles=new Vector3(0,0,-25);
            var collarRight=Rounded("右衬衣领",new Vector3(.071f,.067f,.017f),new Vector3(.055f,.435f,-.072f),.006f,white,rig.spine); collarRight.transform.localEulerAngles=new Vector3(0,0,25);
            Line("开衫门襟",new[]{new Vector3(0,-.01f,-.108f),new Vector3(0,.12f,-.096f),new Vector3(0,.26f,-.118f),new Vector3(0,.37f,-.106f),new Vector3(0,.44f,-.074f)},.009f,trim,rig.spine);
            for(int i=0;i<5;i++) Oval("珍珠纽扣",new Vector3(.010f,.010f,.006f),new Vector3(0,.025f+i*.075f,-(i<2?.106f:.119f)),white,rig.spine);
            for(int side=-1;side<=1;side+=2)
            {
                Line("开衫肩线",new[]{new Vector3(side*.065f,.465f,-.024f),new Vector3(side*.115f,.442f,-.066f),new Vector3(side*.182f,.409f,-.051f)},.0024f,trim,rig.spine);
                Rounded("开衫口袋",new Vector3(.068f,.069f,.009f),new Vector3(side*.089f,.07f,-.079f),.005f,blouse,rig.spine);
            }
            BuildHead();
            rig.leftArm=BuildArm(-1,out rig.leftElbow); rig.rightArm=BuildArm(1,out rig.rightElbow);
            rig.leftThigh=BuildLeg(-1,out rig.leftCalf,out rig.leftShoe,out rig.leftSlipper);
            rig.rightThigh=BuildLeg(1,out rig.rightCalf,out rig.rightShoe,out rig.rightSlipper);
            BuildSkirt();
            rig.bag=Rounded("通勤手提包",new Vector3(.28f,.245f,.105f),new Vector3(.035f,-.46f,.03f),.025f,leather,rig.rightElbow);
            Line("皮包提手",new[]{new Vector3(-.09f,.10f,-.025f),new Vector3(-.075f,.20f,-.025f),new Vector3(0,.235f,-.025f),new Vector3(.075f,.20f,-.025f),new Vector3(.09f,.10f,-.025f)},.012f,leather,rig.bag.transform);
            Rounded("包盖",new Vector3(.268f,.093f,.018f),new Vector3(0,.05f,-.061f),.012f,leather,rig.bag.transform);
            Rounded("包扣",new Vector3(.033f,.035f,.009f),new Vector3(0,.005f,-.074f),.004f,trim,rig.bag.transform);
            rig.leftSlipper.SetActive(false); rig.rightSlipper.SetActive(false);
            return rig;
        }
        void BuildHead()
        {
            rig.head=Joint("Head",new Vector3(0,.675f,0),rig.spine);
            var faceProfile=new[]{new R(-.135f,.022f,.026f,-.005f),new R(-.121f,.059f,.053f,-.002f),new R(-.095f,.087f,.076f),new R(-.058f,.108f,.091f),new R(-.02f,.121f,.098f),new R(.023f,.124f,.103f),new R(.068f,.117f,.099f),new R(.111f,.094f,.080f),new R(.145f,.047f,.043f),new R(.157f,.001f,.001f)};
            var face=Loft("脸部 · 下颌与面颊",faceProfile,skin,rig.head,default,64).GetComponent<MeshFilter>().sharedMesh;
            var faceVertices=face.vertices;
            for(int i=0;i<faceVertices.Length;i++)
            {
                var p=faceVertices[i];
                if(p.z<0) { float nose=Mathf.Exp(-Mathf.Pow(p.x/.012f,2)-Mathf.Pow((p.y+.032f)/.028f,2)); p.z-=.018f*nose; faceVertices[i]=p; }
            }
            face.vertices=faceVertices; face.RecalculateNormals(); ModelMesh.SmoothSeam(face,65,(faceProfile.Length-1)*3+1); face.RecalculateBounds();
            ModelMesh.Object("分层发帽",ModelMesh.HairCap(),hair,rig.head,Vector3.zero);
            foreach(int side in new[]{-1,1})
            {
                Oval("耳朵",new Vector3(.022f,.034f,.013f),new Vector3(side*.119f,-.018f,.002f),skin,rig.head);
                Oval("耳内",new Vector3(.010f,.023f,.007f),new Vector3(side*.134f,-.018f,-.008f),pink,rig.head);
                Oval("小耳钉",new Vector3(.006f,.006f,.004f),new Vector3(side*.134f,-.043f,-.014f),white,rig.head);
                var eye=Joint(side<0?"Eye.L":"Eye.R",new Vector3(side*.052f,.009f,-.097f),rig.head); eye.localEulerAngles=new Vector3(0,-side*12,side*3);
                Oval("杏仁眼白",new Vector3(.035f,.020f,.009f),Vector3.zero,white,eye);
                Oval("虹膜",new Vector3(.0155f,.0175f,.004f),new Vector3(0,0,-.0085f),iris,eye);
                Oval("瞳孔",new Vector3(.007f,.012f,.002f),new Vector3(0,0,-.013f),pupil,eye);
                Oval("眼睛高光",new Vector3(.005f,.0055f,.002f),new Vector3(-.006f,.008f,-.018f),white,eye);
                Oval("眼睛小高光",new Vector3(.0022f,.0022f,.002f),new Vector3(.006f,-.008f,-.018f),white,eye);
                Line("上眼线",new[]{new Vector3(-.034f,0,-.005f),new Vector3(-.022f,.016f,-.010f),new Vector3(0,.020f,-.010f),new Vector3(.024f,.016f,-.009f),new Vector3(.036f,.003f,-.004f)},.0024f,pupil,eye);
                Line("下眼睑",new[]{new Vector3(-.029f,-.010f,-.005f),new Vector3(0,-.019f,-.009f),new Vector3(.027f,-.010f,-.006f)},.0012f,blush,eye);
                for(int i=0;i<2;i++) Line("外眼角睫毛",new[]{new Vector3(side*(.024f+i*.005f),.014f-i*.003f,-.010f),new Vector3(side*(.031f+i*.005f),.022f-i*.003f,-.011f)},.0014f,pupil,eye);
                Line("眉毛",new[]{new Vector3(side*.027f,.044f,-.096f),new Vector3(side*.048f,.049f,-.100f),new Vector3(side*.076f,.044f,-.084f)},.0025f,hair,rig.head);
                var cheek=Oval("淡粉腮红",new Vector3(.019f,.006f,.0007f),new Vector3(side*.079f,-.042f,-.077f),blush,rig.head); cheek.transform.localEulerAngles=new Vector3(0,-side*35,0);
                // Layered side locks and individual curves preserve a silhouette from every angle.
                for(int j=0;j<3;j++)
                {
                    float x=side*(.101f+j*.011f);
                    var points=new[]{new Vector3(x,.107f,.002f+j*.012f),new Vector3(side*(.128f+j*.008f),.025f,-.025f+j*.021f),new Vector3(side*(.132f+j*.007f),-.10f,.011f+j*.022f),new Vector3(side*(.118f+j*.008f),-.23f,.034f+j*.018f),new Vector3(side*.11f,-.29f,.047f+j*.020f)};
                    ModelMesh.Object("鬓发分束",ModelMesh.Tube("鬓发分束",points,new[]{.018f,.022f,.022f,.017f,.001f},12),j==1?hairLight:hair,rig.head,Vector3.zero);
                }
            }
            Line("微笑唇线",new[]{new Vector3(-.024f,-.075f,-.086f),new Vector3(-.012f,-.080f,-.091f),new Vector3(0,-.081f,-.093f),new Vector3(.012f,-.080f,-.091f),new Vector3(.024f,-.075f,-.086f)},.0016f,blush,rig.head);
            // Sweep bangs diagonally across the forehead instead of a solid sphere.
            for(int i=0;i<8;i++)
            {
                float x=-.095f+i*.025f;
                var points=new[]{new Vector3(x*.35f,.161f,-.016f),new Vector3(x*.75f,.127f,-.072f),new Vector3(x,.093f,-.102f),new Vector3(x+.016f,.055f+Mathf.Abs(x)*.26f,-.106f)};
                ModelMesh.Object("侧分刘海",ModelMesh.Tube("侧分刘海",points,new[]{.012f,.016f,.017f,.002f},12),i%3==1?hairLight:hair,rig.head,Vector3.zero);
            }
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*.023f;
                ModelMesh.Object("后发束",ModelMesh.Tube("后发束",new[]{new Vector3(x,.07f,.119f),new Vector3(x*1.2f,-.055f,.131f),new Vector3(x*.8f,-.19f,.145f),new Vector3(x+.017f,-.28f,.12f)},new[]{.026f,.028f,.025f,.002f},14),i%2==0?hair:hairLight,rig.head,Vector3.zero);
            }
            Rounded("发夹",new Vector3(.044f,.011f,.013f),new Vector3(.115f,.052f,-.051f),.004f,trim,rig.head).transform.localEulerAngles=new Vector3(0,-40,-25);
        }
        Transform BuildArm(int side,out Transform elbow)
        {
            var shoulder=Joint(side<0?"UpperArm.L":"UpperArm.R",new Vector3(side*.188f,.395f,0),rig.spine);
            Loft("开衫上袖",new[]{new R(-.26f,.046f,.044f),new R(-.235f,.049f,.045f),new R(-.17f,.057f,.052f),new R(-.08f,.068f,.064f),new R(-.015f,.077f,.067f),new R(.021f,.045f,.046f)},blouse,shoulder);
            elbow=Joint(side<0?"Forearm.L":"Forearm.R",new Vector3(0,-.25f,0),shoulder);
            Loft("开衫下袖",new[]{new R(-.19f,.035f,.034f),new R(-.17f,.037f,.035f),new R(-.10f,.044f,.039f),new R(0,.047f,.043f),new R(.017f,.047f,.043f)},blouse,elbow);
            Loft("袖口罗纹",new[]{new R(-.197f,.0355f,.0345f),new R(-.175f,.036f,.035f),new R(-.160f,.0365f,.035f)},trim,elbow);
            var hand=Joint(side<0?"Hand.L":"Hand.R",new Vector3(0,-.211f,-.003f),elbow);
            Loft("手腕和手掌",new[]{new R(-.070f,.025f,.013f),new R(-.055f,.032f,.016f),new R(-.020f,.028f,.018f),new R(.015f,.026f,.025f)},skin,hand,default,24);
            for(int i=0;i<4;i++)
            {
                float length=new[]{.055f,.066f,.060f,.047f}[i],x=(i-1.5f)*.015f;
                Line("手指",new[]{new Vector3(x,-.058f,-.001f),new Vector3(x,-.075f,-.005f),new Vector3(x,-.058f-length*.65f,-.018f),new Vector3(x,-.058f-length,-.019f)},.007f,skin,hand);
            }
            Line("拇指",new[]{new Vector3(side*.025f,-.025f,0),new Vector3(side*.041f,-.055f,-.014f),new Vector3(side*.035f,-.074f,-.024f)},.009f,skin,hand);
            return shoulder;
        }
        Transform BuildLeg(int side,out Transform calf,out GameObject shoe,out GameObject slipper)
        {
            var thigh=Joint(side<0?"Thigh.L":"Thigh.R",new Vector3(side*.095f,-.07f,0),rig.hips);
            calf=Joint(side<0?"Shin.L":"Shin.R",new Vector3(0,-.39f,0),thigh);
            var legMesh=ModelMesh.Loft("连续腿部蒙皮",new[]{new R(-.765f,.026f,.031f),new R(-.73f,.028f,.033f),new R(-.64f,.035f,.041f,.011f),new R(-.54f,.047f,.051f,.012f),new R(-.45f,.05f,.048f),new R(-.39f,.046f,.047f),new R(-.33f,.057f,.058f),new R(-.20f,.066f,.069f),new R(-.085f,.080f,.081f),new R(.023f,.078f,.078f)},40);
            var vertices=legMesh.vertices; var weights=new BoneWeight[vertices.Length];
            for(int i=0;i<vertices.Length;i++) { float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.34f,-.44f,vertices[i].y)); weights[i]=new BoneWeight{boneIndex0=0,weight0=1-blend,boneIndex1=1,weight1=blend}; }
            legMesh.boneWeights=weights; legMesh.bindposes=new[]{thigh.worldToLocalMatrix*thigh.localToWorldMatrix,calf.worldToLocalMatrix*thigh.localToWorldMatrix};
            var leg=Joint("腿部 · 连续蒙皮网格",Vector3.zero,thigh).gameObject.AddComponent<SkinnedMeshRenderer>(); leg.sharedMesh=legMesh; leg.bones=new[]{thigh,calf}; leg.rootBone=thigh; leg.localBounds=new Bounds(new Vector3(0,-.3f,0),Vector3.one*1.7f); ModelMesh.ApplyMaterial(leg,skin);
            Oval("脚",new Vector3(.041f,.033f,.096f),new Vector3(0,-.407f,-.044f),skin,calf);
            shoe=BuildFootwear("棕色乐福鞋",new Vector3(0,-.42f,-.047f),leather,calf,false);
            slipper=BuildFootwear("珊瑚粉拖鞋",new Vector3(0,-.42f,-.047f),pink,calf,true);
            return thigh;
        }
        void BuildSkirt()
        {
            var mesh=ModelMesh.Loft("百褶裙 · 带权重",new[]{new R(-.30f,.230f,.155f),new R(-.275f,.229f,.154f),new R(-.20f,.214f,.144f),new R(-.11f,.195f,.131f),new R(-.02f,.165f,.114f),new R(.054f,.148f,.105f)},64,.065f,16);
            // Bind in character-local space; thigh weights lift the front hem while sitting.
            var weights=new BoneWeight[mesh.vertexCount]; var vertices=mesh.vertices;
            for(int i=0;i<weights.Length;i++)
            {
                float down=Mathf.InverseLerp(-.02f,-.30f,vertices[i].y),front=Mathf.InverseLerp(.07f,-.075f,vertices[i].z);
                float blend=down*front*.06f; weights[i]=new BoneWeight{boneIndex0=0,weight0=1-blend,boneIndex1=vertices[i].x<0?1:2,weight1=blend};
            }
            mesh.boneWeights=weights;
            var delta=new Vector3[mesh.vertexCount];
            for(int i=0;i<delta.Length;i++)
            {
                float down=Mathf.InverseLerp(.015f,-.30f,vertices[i].y),front=Mathf.InverseLerp(.12f,-.12f,vertices[i].z);
                delta[i]=new Vector3(0,.17f*down*front,-.26f*down*front);
            }
            mesh.AddBlendShapeFrame("Sitting",100,delta,null,null);
            mesh.bindposes=new[]{rig.hips.worldToLocalMatrix*rig.hips.localToWorldMatrix,rig.leftThigh.worldToLocalMatrix*rig.hips.localToWorldMatrix,rig.rightThigh.worldToLocalMatrix*rig.hips.localToWorldMatrix};
            var obj=new GameObject("百褶裙 · 蒙皮网格"); obj.transform.SetParent(rig.hips,false);
            rig.skirt=obj.AddComponent<SkinnedMeshRenderer>(); rig.skirt.sharedMesh=mesh; rig.skirt.bones=new[]{rig.hips,rig.leftThigh,rig.rightThigh}; rig.skirt.rootBone=rig.hips;
            rig.skirt.localBounds=new Bounds(Vector3.zero,Vector3.one*2); ModelMesh.ApplyMaterial(rig.skirt,skirt);
            Loft("裙腰带",new[]{new R(.015f,.151f,.108f),new R(.04f,.151f,.108f),new R(.056f,.150f,.107f)},skirt,rig.hips);
            Rounded("裙腰搭扣",new Vector3(.035f,.026f,.006f),new Vector3(.057f,.033f,-.099f),.003f,trim,rig.hips);
        }
        public GameObject BuildFootwear(string name,Vector3 position,Material material,Transform parent,bool slipper)
        {
            var root=Joint(name,position,parent).gameObject;
            Rounded("独立鞋底",new Vector3(.102f,.027f,.225f),new Vector3(0,-.007f,-.008f),.012f,sole,root.transform);
            if(slipper)
            {
                Rounded("拖鞋软垫",new Vector3(.095f,.018f,.210f),new Vector3(0,.009f,-.008f),.008f,material,root.transform);
                // A curved open-ended strap, leaving heel and toes visible.
                const int count=20; var points=new Vector3[count]; var radii=new float[count];
                for(int i=0;i<count;i++) { float a=i*Mathf.PI/(count-1); points[i]=new Vector3(-.046f*Mathf.Cos(a),.016f+.042f*Mathf.Sin(a),-.031f); radii[i]=.026f; }
                ModelMesh.Object("开放拖鞋鞋面",ModelMesh.Tube("开放鞋面",points,radii,12),material,root.transform,Vector3.zero);
            }
            else
            {
                Oval("乐福鞋鞋面",new Vector3(.049f,.041f,.109f),new Vector3(0,.022f,-.012f),material,root.transform);
                Oval("鞋口内衬",new Vector3(.034f,.012f,.047f),new Vector3(0,.051f,.036f),sole,root.transform);
                Rounded("鞋面横带",new Vector3(.096f,.015f,.034f),new Vector3(0,.056f,-.037f),.005f,material,root.transform);
                Line("鞋头缝线",new[]{new Vector3(-.036f,.039f,-.037f),new Vector3(-.032f,.048f,-.087f),new Vector3(0,.054f,-.108f),new Vector3(.032f,.048f,-.087f),new Vector3(.036f,.039f,-.037f)},.0012f,trim,root.transform);
                Rounded("鞋跟",new Vector3(.080f,.03f,.067f),new Vector3(0,-.025f,.065f),.005f,sole,root.transform);
            }
            return root;
        }
    }
}
