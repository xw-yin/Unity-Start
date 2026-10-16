using UnityEngine;

namespace Homecoming
{
    // A small, bounded secondary-motion solver for the split skirt. The waist
    // stays pinned; independent hem springs lag behind the animated pelvis.
    public sealed class HomecomingClothDynamics : MonoBehaviour
    {
        CharacterRig rig;
        Mesh source, mesh;
        Vector3[] rest, normals, vertices, deformedNormals, sittingDelta;
        readonly Vector2[] displacement = new Vector2[6], velocity = new Vector2[6];
        Vector3 lastPosition;
        Quaternion lastRotation;
        float lastTime = -1;
        int sittingShape, hipIndex;
        Matrix4x4 hipBindPose;
        Transform leftFoot, rightFoot;
        Renderer seat;
        public float MaximumDisplacement { get; private set; }
        public float MaximumSeatCorrection { get; private set; }
        public Mesh SourceMesh => source;

        void Initialize(CharacterRig character)
        {
            rig = character;
            var cushion=rig.transform.parent?rig.transform.parent.Find("换鞋凳软垫"):null;
            if(cushion)seat=cushion.GetComponent<Renderer>();
            source = rig.skirt.sharedMesh;
            mesh = Instantiate(source);
            mesh.name = source.name + " (dynamic cloth)";
            mesh.MarkDynamic();
            rest = source.vertices; normals = source.normals;
            vertices = new Vector3[rest.Length]; deformedNormals = new Vector3[rest.Length];
            sittingShape = mesh.GetBlendShapeIndex("Sitting");
            sittingDelta=new Vector3[rest.Length];
            if(sittingShape>=0)source.GetBlendShapeFrameVertices(sittingShape,0,sittingDelta,null,null);
            hipIndex = System.Array.IndexOf(rig.skirt.bones, rig.hips);
            hipBindPose = hipIndex >= 0 ? source.bindposes[hipIndex] : Matrix4x4.identity;
            foreach(var bone in rig.GetComponentsInChildren<Transform>())
            {
                if(bone.name == "J_Bip_L_Foot") leftFoot = bone;
                if(bone.name == "J_Bip_R_Foot") rightFoot = bone;
            }
            var bounds = mesh.bounds; bounds.Expand(.14f); mesh.bounds = bounds;
            rig.skirt.sharedMesh = mesh;
        }

        public void Sample(CharacterRig character, float time)
        {
            if (!character.longBoots || !character.skirt) return;
            if (!mesh) Initialize(character);
            if (lastTime == time) return;
            float dt = time - lastTime;
            bool reset = lastTime < 0 || dt < 0 || dt > .25f;
            Vector3 position = rig.hips.position;
            Quaternion rotation = rig.hips.rotation;
            float sitting = sittingShape >= 0 ? rig.skirt.GetBlendShapeWeight(sittingShape) / 100 : 0;
            Vector3 localSpeed = reset ? Vector3.zero : rig.skirt.transform.InverseTransformVector((position - lastPosition) / Mathf.Max(dt, .0001f));
            float turnSpeed = reset ? 0 : Mathf.DeltaAngle(lastRotation.eulerAngles.y, rotation.eulerAngles.y) / Mathf.Max(dt, .0001f);
            if (reset) for (int i = 0; i < 6; i++) { displacement[i] = Vector2.zero; velocity[i] = Vector2.zero; }
            float activity = Mathf.Clamp01(localSpeed.magnitude * 2 + Mathf.Abs(turnSpeed) / 100);
            float limit = Mathf.Lerp(.055f, .020f, sitting);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(dt, .25f) * 120));
            float step = reset ? 0 : dt / steps;
            for (int i = 0; i < 6; i++)
            {
                float phase = i * 1.17f;
                Vector2 target = new Vector2(-localSpeed.x * .035f - turnSpeed * .00015f, -localSpeed.z * .035f);
                if (time > 1.5f) target += new Vector2(Mathf.Sin(time * 5.4f + phase), Mathf.Sin(time * 6.1f + phase)) * (.002f + .012f * activity);
                target = Vector2.ClampMagnitude(target, limit);
                for (int j = 0; j < steps; j++)
                {
                    // Semi-implicit fixed substeps keep low frame rates stable.
                    velocity[i] += (105 * (target - displacement[i]) - 13 * velocity[i]) * step;
                    displacement[i] = Vector2.ClampMagnitude(displacement[i] + velocity[i] * step, limit);
                }
            }
            var hipMatrix = hipIndex >= 0 ? rig.hips.localToWorldMatrix * hipBindPose : rig.skirt.transform.localToWorldMatrix;
            var inverseHip = hipMatrix.inverse;
            MaximumDisplacement = 0;MaximumSeatCorrection=0;
            var seatBounds=seat?seat.bounds:new Bounds();
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                float free = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.93f, .54f, p.y));
                bool front = p.z > .10f;
                float column = Mathf.Clamp((p.x + .18f) / .36f * 2, 0, 2);
                int a = Mathf.Min(1, Mathf.FloorToInt(column)), group = front ? 0 : 3;
                Vector2 sway = Vector2.Lerp(displacement[group + a], displacement[group + a + 1], column - a);
                float fold = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.93f, .85f, p.y));
                Quaternion bend = Quaternion.Euler((front ? -70 : 60) * fold * sitting, 0, 0);
                Vector3 offset = bend * new Vector3(sway.x, Mathf.Sin(time * 5 + p.x * 17) * sway.magnitude * .15f, sway.y) * free;
                // Use the imported corrective, not a reconstructed approximation:
                // the authored coat fold has changed since the first procedural draft.
                Vector3 folded=p+sittingDelta[i]*sitting;
                Vector3 worldBase = hipMatrix.MultiplyPoint3x4(folded);
                Vector3 world = worldBase + hipMatrix.MultiplyVector(offset);
                Constrain(ref world, worldBase, rig.leftThigh.position, rig.leftCalf.position, .075f);
                Constrain(ref world, worldBase, rig.rightThigh.position, rig.rightCalf.position, .075f);
                if(leftFoot) Constrain(ref world, worldBase, rig.leftCalf.position, leftFoot.position, .052f);
                if(rightFoot) Constrain(ref world, worldBase, rig.rightCalf.position, rightFoot.position, .052f);
                // Inertial cloth travel and static contact correction have different
                // meanings; keep the secondary-motion stability metric independent.
                MaximumDisplacement=Mathf.Max(MaximumDisplacement,inverseHip.MultiplyVector(world-worldBase).magnitude);
                var beforeSeat=world;
                // A seated coat rests on the real cushion, including during the
                // approach. Its free back panels must not sink through a raised seat.
                if(seat&&sitting>0&&free>0)
                {
                    var bounds=seatBounds;
                    if(world.x>bounds.min.x&&world.x<bounds.max.x&&world.z>bounds.min.z&&world.z<bounds.max.z&&world.y>bounds.min.y&&world.y<bounds.max.y+.003f)
                        world.y=Mathf.Lerp(world.y,bounds.max.y+.003f,Mathf.Clamp01(sitting*4));
                }
                offset = inverseHip.MultiplyVector(world - worldBase);
                vertices[i] = p + offset;
                deformedNormals[i] = Quaternion.Euler(-sway.y * free * 80, 0, sway.x * free * 80) * normals[i];
                MaximumSeatCorrection=Mathf.Max(MaximumSeatCorrection,Vector3.Distance(world,beforeSeat));
            }
            mesh.vertices = vertices; mesh.normals = deformedNormals;
            lastPosition = position; lastRotation = rotation; lastTime = time;
        }

        static void Constrain(ref Vector3 point, Vector3 origin, Vector3 a, Vector3 b, float radius)
        {
            Vector3 axis = b - a;
            Vector3 center = a + axis * Mathf.Clamp01(Vector3.Dot(point - a, axis) / Mathf.Max(axis.sqrMagnitude, .00001f));
            Vector3 delta = point - center;
            if (delta.sqrMagnitude >= radius * radius) return;
            Vector3 oldCenter = a + axis * Mathf.Clamp01(Vector3.Dot(origin - a, axis) / Mathf.Max(axis.sqrMagnitude, .00001f));
            float oldDistance = Vector3.Distance(origin, oldCenter);
            // Reject a new inward displacement instead of pushing it out along
            // another direction, which can amplify motion at the knee bend.
            if (delta.magnitude < oldDistance) point = origin;
        }

        void OnDestroy()
        {
            if (rig && rig.skirt && rig.skirt.sharedMesh == mesh) rig.skirt.sharedMesh = source;
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
