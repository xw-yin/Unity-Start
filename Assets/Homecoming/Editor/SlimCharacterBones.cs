using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Trims each character SkinnedMeshRenderer's bone list down to the bones that
    // actually carry weight, and remaps mesh boneWeights / bindposes consistently.
    //
    // Why: the importer puts the full skeleton (200-394 bones) into every SMR.
    // The custom per-frame systems (HomecomingMotion.Sole, HomecomingGarmentDynamics,
    // HomecomingFootContact) build one matrix per listed bone, so unused bones are
    // pure per-frame CPU cost on iOS.
    //
    // Pipeline order: run this AFTER "Homecoming/Blender/应用保存的接缝和法线修正",
    // so refinement-assigned weights are included in the used set. Re-run it after
    // any Blender re-import (re-import regenerates full bone lists).
    //
    // The tool also remaps DCC/Blender/refinements.json bone indices and refreshes
    // its fingerprints (the fingerprint covers bindposes.Length), keeping a
    // refinements.json.pre-slim-bak backup on first run.
    //
    // Safety: only bones with exactly 0 total weight are removed, so skinning math
    // is bit-identical. Runtime code only indexes bones via boneWeights or looks
    // transforms up by identity, never by absolute index.
    public static class SlimCharacterBones
    {
        const string PrefabDir = "Assets/Homecoming/Resources/Homecoming";
        const string RefinementsPath = "DCC/Blender/refinements.json";

        [Serializable] public class SparseEdits { public List<SparseSurface> surfaces; }
        [Serializable] public class SparseSurface { public string path; public int vertexCount; public string fingerprint; public List<SparseVertex> vertices; }
        [Serializable] public class SparseVertex { public int index; public float[] normal; public int[] bones; public float[] weights; }

        sealed class MeshPlan
        {
            public Mesh mesh;
            public string assetPath;
            public List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            public int[] oldToNew; // length = old bindpose count, -1 = removed
            public int newCount;
        }

        [MenuItem("Homecoming/性能/裁剪角色未使用骨骼")]
        public static void Run()
        {
            var plans = BuildPlans();
            if (plans.Count == 0)
            {
                EditorUtility.DisplayDialog("裁剪角色未使用骨骼", "所有网格的骨骼列表已是最简，无需裁剪。", "确定");
                return;
            }
            var totalBefore = plans.Sum(p => p.mesh.bindposes.Length * p.renderers.Count);
            var totalAfter = plans.Sum(p => p.newCount * p.renderers.Count);
            var lines = plans
                .OrderByDescending(p => (p.mesh.bindposes.Length - p.newCount) * p.renderers.Count)
                .Select(p => $"{System.IO.Path.GetFileName(p.assetPath)}: {p.mesh.bindposes.Length} -> {p.newCount} bones x {p.renderers.Count} renderers");
            var msg = $"裁剪后每 SMR 只保留有权重的骨骼。\n\n" +
                      string.Join("\n", lines.Take(25)) +
                      (lines.Count() > 25 ? $"\n... (+{lines.Count() - 25} more)" : "") +
                      $"\n\n骨骼槽位总量: {totalBefore} -> {totalAfter} (约 {(1 - (double)totalAfter / totalBefore):P0} 减少)\n" +
                      "refinements.json 的骨骼索引与指纹将同步更新（首次运行自动备份）。\n\n确定执行吗？";
            if (!EditorUtility.DisplayDialog("裁剪角色未使用骨骼", msg, "执行", "取消")) return;
            ApplyPlans(plans);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlimCharacterBones] done: bone slots {totalBefore} -> {totalAfter}. " +
                      "请运行 Homecoming/Editor 的连续复查（961 帧 x 7 角色）验证外观无变化。");
        }

        static List<MeshPlan> BuildPlans()
        {
            var prefabPaths = Directory.GetFiles(PrefabDir, "*.prefab", SearchOption.TopDirectoryOnly);
            var byMesh = new Dictionary<string, MeshPlan>();
            foreach (var prefabPath in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!prefab) continue;
                foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = smr.sharedMesh;
                    if (!mesh) continue;
                    var assetPath = AssetDatabase.GetAssetPath(mesh);
                    if (string.IsNullOrEmpty(assetPath)) continue;
                    if (!byMesh.TryGetValue(assetPath, out var plan))
                    {
                        plan = new MeshPlan { mesh = mesh, assetPath = assetPath };
                        byMesh[assetPath] = plan;
                    }
                    if (!plan.renderers.Contains(smr)) plan.renderers.Add(smr);
                }
            }
            var plans = new List<MeshPlan>();
            var refinements = LoadRefinements();
            foreach (var plan in byMesh.Values)
            {
                var weights = plan.mesh.boneWeights;
                var used = new HashSet<int>();
                foreach (var w in weights)
                {
                    if (w.weight0 > 0) used.Add(w.boneIndex0);
                    if (w.weight1 > 0) used.Add(w.boneIndex1);
                    if (w.weight2 > 0) used.Add(w.boneIndex2);
                    if (w.weight3 > 0) used.Add(w.boneIndex3);
                }
                // Refinement vertices carry absolute bone indices; include them so a
                // future re-application stays valid after remap.
                var surface = refinements?.surfaces?.FirstOrDefault(s => s.path == plan.assetPath);
                if (surface != null)
                    foreach (var v in surface.vertices)
                        foreach (var b in v.bones) used.Add(b);
                int oldCount = plan.mesh.bindposes.Length;
                if (used.Any(b => b < 0 || b >= oldCount))
                    throw new Exception($"Bone index out of range in {plan.assetPath}");
                var keep = used.OrderBy(i => i).ToArray();
                if (keep.Length == oldCount) continue; // nothing to trim
                var oldToNew = Enumerable.Repeat(-1, oldCount).ToArray();
                for (int i = 0; i < keep.Length; i++) oldToNew[keep[i]] = i;
                plan.oldToNew = oldToNew;
                plan.newCount = keep.Length;
                plans.Add(plan);
            }
            return plans;
        }

        static void ApplyPlans(List<MeshPlan> plans)
        {
            var refinements = LoadRefinements();
            bool refinementsDirty = false;
            // Backup refinements.json once, before first mutation.
            if (refinements != null && plans.Any(p => SurfaceHasRefinement(refinements, p.assetPath)) && !File.Exists(RefinementsPath + ".pre-slim-bak"))
                File.Copy(RefinementsPath, RefinementsPath + ".pre-slim-bak");

            foreach (var plan in plans)
            {
                var mesh = plan.mesh;
                var oldWeights = mesh.boneWeights;
                var newWeights = new BoneWeight[oldWeights.Length];
                for (int i = 0; i < oldWeights.Length; i++)
                {
                    var w = oldWeights[i];
                    newWeights[i] = new BoneWeight
                    {
                        boneIndex0 = plan.oldToNew[w.boneIndex0], weight0 = w.weight0,
                        boneIndex1 = plan.oldToNew[w.boneIndex1], weight1 = w.weight1,
                        boneIndex2 = plan.oldToNew[w.boneIndex2], weight2 = w.weight2,
                        boneIndex3 = plan.oldToNew[w.boneIndex3], weight3 = w.weight3,
                    };
                    if (newWeights[i].boneIndex0 < 0 || newWeights[i].boneIndex1 < 0 ||
                        newWeights[i].boneIndex2 < 0 || newWeights[i].boneIndex3 < 0)
                        throw new Exception($"Remap missed a used bone in {plan.assetPath}");
                }
                var oldBinds = mesh.bindposes;
                var newBinds = new Matrix4x4[plan.newCount];
                var newBonesPerRenderer = new Dictionary<SkinnedMeshRenderer, Transform[]>();
                for (int i = 0; i < plan.oldToNew.Length; i++)
                    if (plan.oldToNew[i] >= 0) newBinds[plan.oldToNew[i]] = oldBinds[i];
                foreach (var smr in plan.renderers)
                {
                    var oldBones = smr.bones;
                    if (oldBones.Length != plan.oldToNew.Length)
                        throw new Exception($"Bone list length mismatch on {smr.name} ({oldBones.Length} vs {plan.oldToNew.Length})");
                    var nb = new Transform[plan.newCount];
                    for (int i = 0; i < oldBones.Length; i++)
                        if (plan.oldToNew[i] >= 0) nb[plan.oldToNew[i]] = oldBones[i];
                    if (nb.Any(b => !b)) throw new Exception($"Null bone after remap on {smr.name}");
                    newBonesPerRenderer[smr] = nb;
                }
                mesh.boneWeights = newWeights;
                mesh.bindposes = newBinds;
                EditorUtility.SetDirty(mesh);
                foreach (var kv in newBonesPerRenderer) { kv.Key.bones = kv.Value; EditorUtility.SetDirty(kv.Key); }

                var surface = refinements?.surfaces?.FirstOrDefault(s => s.path == plan.assetPath);
                if (surface != null)
                {
                    foreach (var v in surface.vertices)
                        for (int k = 0; k < v.bones.Length; k++)
                        {
                            v.bones[k] = plan.oldToNew[v.bones[k]];
                            if (v.bones[k] < 0) throw new Exception($"Refinement bone not in used set: {plan.assetPath}");
                        }
                    surface.fingerprint = Fingerprint(mesh);
                    refinementsDirty = true;
                }
            }
            if (refinementsDirty)
                File.WriteAllText(RefinementsPath, JsonUtility.ToJson(refinements, true), new UTF8Encoding(false));
        }

        static bool SurfaceHasRefinement(SparseEdits e, string path) =>
            e.surfaces != null && e.surfaces.Any(s => s.path == path);

        static SparseEdits LoadRefinements() =>
            File.Exists(RefinementsPath)
                ? JsonUtility.FromJson<SparseEdits>(File.ReadAllText(RefinementsPath))
                : null;

        // Same algorithm as BlenderModelBridge.Fingerprint (vertexCount, vertices,
        // triangles, bindposes.Length). Must be recomputed after bindposes change.
        static string Fingerprint(Mesh mesh)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(mesh.vertexCount);
                    foreach (var v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    var triangles = mesh.triangles; writer.Write(triangles.Length);
                    foreach (int index in triangles) writer.Write(index);
                    writer.Write(mesh.bindposes.Length);
                }
                using (var hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
