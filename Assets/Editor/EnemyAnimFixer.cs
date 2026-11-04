// Ilagay sa: Assets/Editor/EnemyAnimFixer.cs
//
// Menu:
//   Haunted House > Enemy > Animation: DIAGNOSE (report lang)   -> walang babaguhin, ililista lang ang problema
//   Haunted House > Enemy > Animation: DIAGNOSE + FIX           -> aayusin ang kaya nitong ayusin
//
// Piliin muna ang enemy sa Hierarchy (o kahit wala, kukunin ang unang EnemyAI sa scene).
//
// Ano ang chine-check:
//   - may Animator ba, naka-enable ba, may Controller ba
//   - Avatar, Root Motion (nag-aaway sa NavMeshAgent), Culling Mode
//   - BINABASA ang EnemyAI.cs para makita kung anong parameter/state ang tinatawag nito
//     (SetBool / SetFloat / SetTrigger / SetInteger / Play / CrossFade) at kino-compare sa Controller:
//       * kung magkahawig lang ang pangalan (hal. "Chasing" vs "IsChasing") -> ire-rename ang parameter sa controller,
//         pati ang mga transition at blend tree na gumagamit nito
//       * kung wala talaga -> dadagdagan ng parameter
//   - GetComponent<Animator>() sa EnemyAI pero nasa child ang Animator (null ang makukuha)
//   - Animator field ng EnemyAI na walang laman
//   - mga clip: legacy, hindi naka-loop (idle/walk/run), rig na hindi tugma
//   - Controller: layer weight 0, state na walang clip, state speed 0, unreachable state, transition na lilipat agad
//   - kung wala talagang code ang EnemyAI na gumagalaw ng Animator -> maglalagay ng EnemyAnimDriver
//
// Bago baguhin ang controller, gumagawa ito ng kopya: <pangalan>_BACKUP.controller

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyAnimFixer
{
    static List<string> log;
    static int nOk, nFixed, nWarn, nProblem;
    static bool backedUp;

    static void Ok(string m) { nOk++; log.Add("   OK        " + m); }
    static void Fixed(string m) { nFixed++; log.Add("   NAYOS     " + m); }
    static void Warn(string m) { nWarn++; log.Add("   BABALA    " + m); }
    static void Problem(string m) { nProblem++; log.Add("   PROBLEMA  " + m); }

    [MenuItem("Haunted House/Enemy/Animation: DIAGNOSE (report lang)")]
    static void Diagnose() { Run(false); }

    [MenuItem("Haunted House/Enemy/Animation: DIAGNOSE + FIX")]
    static void DiagnoseAndFix() { Run(true); }

    // =====================================================================
    static void Run(bool fix)
    {
        if (fix && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Enemy animation", "Itigil muna ang Play mode bago mag-FIX.", "OK");
            return;
        }

        log = new List<string>();
        nOk = nFixed = nWarn = nProblem = 0;
        backedUp = false;

        GameObject enemy = FindEnemy();
        if (enemy == null)
        {
            EditorUtility.DisplayDialog("Enemy animation",
                "Walang EnemyAI sa scene. Piliin ang enemy sa Hierarchy at subukan ulit.", "OK");
            return;
        }

        var ai = enemy.GetComponent<EnemyAI>();
        if (ai == null) ai = enemy.GetComponentInChildren<EnemyAI>(true);

        log.Add("Enemy: " + HierarchyPath(enemy.transform) + (fix ? "   [DIAGNOSE + FIX]" : "   [report lang]"));

        CheckScripts(enemy);

        Animator anim = FindAnimator(enemy, fix);
        if (anim != null) CheckAnimator(anim, enemy, ai, fix);

        Finish(enemy, fix);
    }

    static GameObject FindEnemy()
    {
        var sel = Selection.activeGameObject;
        if (sel != null && sel.scene.IsValid())
        {
            var a = sel.GetComponentInParent<EnemyAI>();
            return a != null ? a.gameObject : sel;
        }
        var all = FindInScene<EnemyAI>();
        return all.Count > 0 ? all[0].gameObject : null;
    }

    static void CheckScripts(GameObject enemy)
    {
        foreach (var mb in enemy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
            {
                Warn("May Missing Script sa enemy. Tanggalin sa Inspector (Remove Component).");
                continue;
            }
            if (mb.GetType().Name == "EnemyController")
                Warn("May EnemyController sa enemy: nag-aaway ito sa EnemyAI (ino-override ang destination). Alisin ito.");
        }
        if (enemy.GetComponentInChildren<NavMeshAgent>(true) == null)
            Warn("Walang NavMeshAgent sa enemy.");
    }

    // =====================================================================
    //  ANIMATOR
    // =====================================================================
    static Animator FindAnimator(GameObject enemy, bool fix)
    {
        var anims = enemy.GetComponentsInChildren<Animator>(true);

        if (anims.Length == 0)
        {
            Problem("Walang Animator component sa enemy o sa mga child nito.");
            if (!fix) return null;

            var smr = enemy.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null)
            {
                Problem("Walang SkinnedMeshRenderer (model na may buto) na mapaglalagyan ng Animator. Ilagay ang model bilang child ng enemy.");
                return null;
            }
            Transform top = smr.transform;
            if (top != enemy.transform)
                while (top.parent != null && top.parent != enemy.transform) top = top.parent;
            var added = Undo.AddComponent<Animator>(top.gameObject);
            Fixed("Naglagay ng Animator sa " + HierarchyPath(top) + ". Kailangan pang i-assign ang Controller.");
            return added;
        }

        Animator best = anims[0];
        foreach (var a in anims)
            if (a.runtimeAnimatorController != null) { best = a; break; }

        if (anims.Length > 1)
            Warn(anims.Length + " Animator ang nasa enemy. Ginagamit ko ang nasa " + HierarchyPath(best.transform) +
                 ". Isa lang dapat (alisin ang sobra).");
        return best;
    }

    static void CheckAnimator(Animator anim, GameObject enemy, EnemyAI ai, bool fix)
    {
        log.Add("Animator: " + HierarchyPath(anim.transform));

        // ---- naka-enable? ----
        if (!anim.enabled)
        {
            if (fix) { Undo.RecordObject(anim, "Enable Animator"); anim.enabled = true; Touch(anim); Fixed("Naka-disable ang Animator, in-enable ko."); }
            else Problem("Naka-disable ang Animator component.");
        }
        else Ok("Naka-enable ang Animator.");

        if (!anim.gameObject.activeInHierarchy) Warn("Naka-disable ang object ng Animator.");

        // ---- controller ----
        if (anim.runtimeAnimatorController == null)
        {
            Problem("Walang Animator Controller na naka-assign.");
            if (fix) TryAssignController(anim, enemy);
        }

        var rac = anim.runtimeAnimatorController;
        AnimatorController ac = rac as AnimatorController;
        var ov = rac as AnimatorOverrideController;
        if (ac == null && ov != null) ac = ov.runtimeAnimatorController as AnimatorController;

        if (rac == null) log.Add("   (hindi na maipagpapatuloy ang ibang check ng controller kasi wala pa)");
        else if (ac == null) Warn("Hindi ko ma-edit ang controller na ito (hindi AnimatorController asset).");
        else Ok("Controller: " + ac.name);

        // ---- mga clip ng controller ----
        var clips = new List<KeyValuePair<string, AnimationClip>>();
        if (ac != null)
            foreach (var cs in AllStates(ac))
                CollectClips(cs.state.motion, cs.state.name, clips);

        bool anyHuman = false;
        foreach (var kv in clips) if (kv.Value != null && kv.Value.isHumanMotion) anyHuman = true;

        // ---- avatar ----
        CheckAvatar(anim, anyHuman, fix);

        // ---- rig ng model vs clips ----
        CheckRigs(anim, clips);

        // ---- root motion ----
        if (anim.applyRootMotion && enemy.GetComponentInChildren<NavMeshAgent>(true) != null)
        {
            if (fix)
            {
                Undo.RecordObject(anim, "Root Motion off");
                anim.applyRootMotion = false;
                Touch(anim);
                Fixed("Apply Root Motion ay naka-on kasabay ng NavMeshAgent (nag-aaway sila). In-off ko.");
            }
            else Problem("Apply Root Motion ay naka-on kasabay ng NavMeshAgent. I-off.");
        }
        else Ok("Root Motion ayos.");

        // ---- culling ----
        if (anim.cullingMode != AnimatorCullingMode.AlwaysAnimate)
        {
            if (fix)
            {
                Undo.RecordObject(anim, "Culling Mode");
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Touch(anim);
                Fixed("Culling Mode = Always Animate (para tuloy ang animation kahit wala sa screen).");
            }
            else Warn("Culling Mode ay hindi Always Animate.");
        }

        // ---- EnemyAI source at references ----
        Usage u = null;
        if (ai != null)
        {
            u = ScanSource(ai);
            CheckScriptAnimatorAccess(anim, ai, u, fix);
            FixAnimatorRefs(ai, anim, fix);
        }
        else Warn("Walang EnemyAI sa napiling object, hindi ko mababasa ang code.");

        // ---- parameters at structure ----
        if (ac != null)
        {
            if (u != null) CompareParams(ac, u, fix);
            CheckController(ac, u, fix);
            CheckClips(clips, fix);
        }

        // ---- walang code na gumagalaw ng animator? ----
        if (ai != null && u != null && u.prm.Count == 0 && u.states.Count == 0)
            HandleNoAnimatorCode(enemy, anim, u, fix);
    }

    static void TryAssignController(Animator anim, GameObject enemy)
    {
        var guids = AssetDatabase.FindAssets("t:AnimatorController");
        var cands = new List<string>();
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.EndsWith("_BACKUP.controller")) continue;
            cands.Add(p);
        }

        var strong = new List<string>();
        string en = enemy.name.ToLower();
        foreach (var p in cands)
        {
            string n = Path.GetFileNameWithoutExtension(p).ToLower();
            if (n.Contains("enemy") || n.Contains("monster") || n.Contains("ghost") || (en.Length > 2 && n.Contains(en)))
                strong.Add(p);
        }

        if (strong.Count == 1)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(strong[0]);
            Undo.RecordObject(anim, "Assign Controller");
            anim.runtimeAnimatorController = ctrl;
            Touch(anim);
            Fixed("Na-assign ang controller na " + strong[0] + " (siya lang ang mukhang pang-enemy).");
            return;
        }

        string list = "";
        var show = strong.Count > 1 ? strong : cands;
        for (int i = 0; i < show.Count && i < 8; i++) list += "\n      - " + show[i];
        Problem("Hindi ko mapili ang controller nang sigurado. I-drag mo ang tamang Controller sa Animator. Mga nahanap:" +
                (list.Length > 0 ? list : " wala"));
    }

    static void CheckAvatar(Animator anim, bool anyHuman, bool fix)
    {
        if (anim.avatar == null)
        {
            var smr = anim.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Avatar found = null;
            string modelPath = null;
            if (smr != null && smr.sharedMesh != null)
            {
                modelPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!string.IsNullOrEmpty(modelPath))
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                        if (o is Avatar) { found = (Avatar)o; break; }
            }

            if (found != null && found.isValid)
            {
                if (fix)
                {
                    Undo.RecordObject(anim, "Assign Avatar");
                    anim.avatar = found;
                    Touch(anim);
                    Fixed("Walang Avatar ang Animator, nilagay ko ang galing sa model: " + modelPath);
                }
                else if (anyHuman) Problem("Walang Avatar pero Humanoid ang mga clip. Avatar na nahanap: " + modelPath);
                else Warn("Walang Avatar sa Animator (nahanap: " + modelPath + ").");
            }
            else if (anyHuman) Problem("Humanoid ang mga clip pero walang Avatar ang Animator at wala akong mahanap sa model.");
            else Ok("Walang Avatar (Generic rig, pwede basta tugma ang pangalan ng mga buto).");
            return;
        }

        if (!anim.avatar.isValid) Problem("Hindi valid ang Avatar na naka-assign (" + anim.avatar.name + "). Ayusin ang Rig ng model.");
        else if (anyHuman && !anim.avatar.isHuman) Problem("Humanoid ang mga clip pero Generic ang Avatar ng model. Itakda ang Rig = Humanoid sa model.");
        else Ok("Avatar: " + anim.avatar.name);
    }

    static string RigOf(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        return mi == null ? null : mi.animationType.ToString();
    }

    static void CheckRigs(Animator anim, List<KeyValuePair<string, AnimationClip>> clips)
    {
        var smr = anim.GetComponentInChildren<SkinnedMeshRenderer>(true);
        string modelPath = (smr != null && smr.sharedMesh != null) ? AssetDatabase.GetAssetPath(smr.sharedMesh) : null;
        string modelRig = RigOf(modelPath);

        if (modelRig == "None" || modelRig == "Legacy")
            Problem("Ang Rig (Animation Type) ng model ay '" + modelRig + "'. Gawing Generic o Humanoid (Model > Rig tab).");

        var done = new HashSet<string>();
        foreach (var kv in clips)
        {
            if (kv.Value == null) continue;
            string p = AssetDatabase.GetAssetPath(kv.Value);
            if (!done.Add(p)) continue;
            string r = RigOf(p);
            if (r == null || modelRig == null) continue;
            if (r != modelRig)
                Problem("Magkaiba ang Rig ng model (" + modelRig + ") at ng clip source (" + r + "): " + p);
        }
    }

    // =====================================================================
    //  BASA NG ENEMYAI.CS
    // =====================================================================
    class Usage
    {
        public Dictionary<string, AnimatorControllerParameterType> prm = new Dictionary<string, AnimatorControllerParameterType>();
        public HashSet<string> states = new HashSet<string>();
        public bool mentionsAnimator, getComponentSame, getComponentChildren, usesLayerWeight;
        public string path;
    }

    static Usage ScanSource(EnemyAI ai)
    {
        var u = new Usage();
        var ms = MonoScript.FromMonoBehaviour(ai);
        if (ms == null) return u;
        u.path = AssetDatabase.GetAssetPath(ms);

        string src = ms.text;
        src = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
        src = Regex.Replace(src, @"//[^\n]*", "");

        // mga string variable (const, field) at ang aktwal na halaga sa Inspector
        var strVar = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(src, @"(?:string|int)\s+(\w+)\s*=\s*(?:Animator\.StringToHash\(\s*)?""([^""]+)"""))
            strVar[m.Groups[1].Value] = m.Groups[2].Value;
        foreach (Match m in Regex.Matches(src, @"(\w+)\s*=\s*Animator\.StringToHash\(\s*""([^""]+)""\s*\)"))
            strVar[m.Groups[1].Value] = m.Groups[2].Value;

        var so = new SerializedObject(ai);
        var it = so.GetIterator();
        while (it.NextVisible(true))
            if (it.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(it.stringValue))
                strVar[it.name] = it.stringValue;

        u.mentionsAnimator = Regex.IsMatch(src, @"\bAnimator\b");
        u.getComponentSame = Regex.IsMatch(src, @"GetComponent\s*<\s*Animator\s*>");
        u.getComponentChildren = Regex.IsMatch(src, @"GetComponentInChildren\s*<\s*Animator\s*>");
        u.usesLayerWeight = src.Contains("SetLayerWeight");

        foreach (Match m in Regex.Matches(src, @"\.(SetBool|SetFloat|SetInteger|SetTrigger|ResetTrigger)\s*\(\s*(?:""([^""]+)""|(\w+))"))               {
            string name = m.Groups[2].Success ? m.Groups[2].Value : null;
            if (name == null && !strVar.TryGetValue(m.Groups[3].Value, out name)) continue;

            string kind = m.Groups[1].Value;
            AnimatorControllerParameterType type =
                kind == "SetBool" ? AnimatorControllerParameterType.Bool :
                kind == "SetFloat" ? AnimatorControllerParameterType.Float :
                kind == "SetInteger" ? AnimatorControllerParameterType.Int :
                AnimatorControllerParameterType.Trigger;
            if (!u.prm.ContainsKey(name)) u.prm[name] = type;
        }

        foreach (Match m in Regex.Matches(src, @"\.(Play|CrossFade|CrossFadeInFixedTime)\s*\(\s*(?:""([^""]+)""|(\w+))"))
        {
            string name = m.Groups[2].Success ? m.Groups[2].Value : null;
            if (name == null && !strVar.TryGetValue(m.Groups[3].Value, out name)) continue;
            u.states.Add(name);
        }
        return u;
    }

    static void CheckScriptAnimatorAccess(Animator anim, EnemyAI ai, Usage u, bool fix)
    {
        if (!(u.getComponentSame && !u.getComponentChildren && anim.gameObject != ai.gameObject))
        {
            Ok("Nakukuha ng EnemyAI ang Animator.");
            return;
        }

        Problem("Gumagamit ang EnemyAI ng GetComponent<Animator>() pero nasa child na object ang Animator (" +
                HierarchyPath(anim.transform) + "). Null ang makukuha kaya walang gagalaw.");

        if (!fix || string.IsNullOrEmpty(u.path)) return;
        if (!EditorUtility.DisplayDialog("Ayusin ang EnemyAI.cs?",
                "Papalitan ko ang GetComponent<Animator>() ng GetComponentInChildren<Animator>() sa:\n" + u.path +
                "\n\nMagkakaroon ng kopya sa labas ng Assets folder (project root). Tuloy?", "Sige", "Huwag"))
        {
            Warn("Hindi pinalitan ang EnemyAI.cs (ikaw na ang magpalit ng GetComponent<Animator>() ng GetComponentInChildren<Animator>()).");
            return;
        }

        string full = Path.GetFullPath(u.path);
        string original = File.ReadAllText(full);
        string bak = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "EnemyAI_backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".cs.txt");
        File.WriteAllText(bak, original);
        string patched = Regex.Replace(original, @"GetComponent\s*<\s*Animator\s*>\s*\(\s*\)", "GetComponentInChildren<Animator>()");
        File.WriteAllText(full, patched);
        AssetDatabase.ImportAsset(u.path);
        Fixed("Pinalitan ang EnemyAI.cs (GetComponentInChildren<Animator>). Kopya: " + bak);
    }

    static void FixAnimatorRefs(EnemyAI ai, Animator anim, bool fix)
    {
        var so = new SerializedObject(ai);
        var it = so.GetIterator();
        bool changed = false;
        while (it.NextVisible(true))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (it.type != "PPtr<$Animator>") continue;

            var cur = it.objectReferenceValue as Animator;
            if (cur == anim) { Ok("Field '" + it.name + "' ng EnemyAI ay nakaturo sa tamang Animator."); continue; }
            if (cur == null)
            {
                if (fix) { it.objectReferenceValue = anim; changed = true; Fixed("Walang laman ang field '" + it.name + "' ng EnemyAI, nilagay ko ang Animator."); }
                else Problem("Walang laman ang field '" + it.name + "' ng EnemyAI.");
            }
            else Warn("Ang field '" + it.name + "' ng EnemyAI ay nakaturo sa ibang Animator (" + HierarchyPath(cur.transform) + ").");
        }
        if (changed)
        {
            so.ApplyModifiedProperties();
            Touch(ai);
        }
    }

    static void HandleNoAnimatorCode(GameObject enemy, Animator anim, Usage u, bool fix)
    {
        if (!u.mentionsAnimator)
            Problem("Walang code ang EnemyAI na gumagalaw ng Animator (walang SetBool/SetFloat/SetTrigger/Play). Hindi gagalaw ang animation kahit tama ang controller.");
        else
            Warn("May Animator sa EnemyAI pero hindi ko mabasa kung anong parameter ang itinatakda nito. Tingnan mano-mano ang mga pangalan.");

        var driverType = System.Type.GetType("EnemyAnimDriver, Assembly-CSharp");
        if (driverType == null)
        {
            Warn("Ilagay ang EnemyAnimDriver.cs sa Assets/Scripts (ito ang gagalaw ng Speed/Walk/Run ayon sa galaw ng enemy), tapos i-run ulit ang FIX.");
            return;
        }
        if (enemy.GetComponent(driverType) != null) { Ok("May EnemyAnimDriver na."); return; }
        if (!fix) { Warn("Maglalagay ng EnemyAnimDriver kapag nag-FIX."); return; }

        var comp = Undo.AddComponent(enemy, driverType);
        var so = new SerializedObject(comp);
        var p = so.FindProperty("animator");
        if (p != null) { p.objectReferenceValue = anim; so.ApplyModifiedProperties(); }
        Fixed("Naglagay ng EnemyAnimDriver sa enemy: ginagalaw nito ang mga parameter na may pangalang speed/move/walk/run/chase ayon sa bilis ng enemy.");
    }

    // =====================================================================
    //  PARAMETERS
    // =====================================================================
    static void CompareParams(AnimatorController ac, Usage u, bool fix)
    {
        foreach (var kv in u.prm)
        {
            string name = kv.Key;
            var type = kv.Value;

            var ps = ac.parameters;
            AnimatorControllerParameter existing = null;
            foreach (var p in ps) if (p.name == name) { existing = p; break; }

            if (existing != null)
            {
                if (existing.type == type) Ok("Parameter '" + name + "' (" + type + ") tugma.");
                else Problem("Parameter '" + name + "': " + type + " ang gamit ng EnemyAI pero " + existing.type + " sa controller.");
                continue;
            }

            // wala: hanapin ang kahawig na hindi ginagamit ng script
            var cands = new List<string>();
            foreach (var q in ps)
                if (q.type == type && !u.prm.ContainsKey(q.name) && Similar(q.name, name))
                    cands.Add(q.name);

            if (cands.Count == 1)
            {
                if (!fix) { Problem("Walang parameter na '" + name + "' sa controller (may kahawig: '" + cands[0] + "')."); continue; }
                Backup(ac);
                RenameParam(ac, cands[0], name);
                Fixed("Ni-rename ang parameter '" + cands[0] + "' -> '" + name + "' (pati mga transition) para tumugma sa EnemyAI.");
            }
            else if (fix)
            {
                Backup(ac);
                Undo.RecordObject(ac, "Add parameter");
                ac.AddParameter(name, type);
                EditorUtility.SetDirty(ac);
                Fixed("Dinagdag ang parameter '" + name + "' (" + type + "). Wala pang transition na gumagamit nito, i-connect mo sa Animator window.");
            }
            else Problem("Walang parameter na '" + name + "' (" + type + ") sa controller, pero ito ang tinatawag ng EnemyAI.");
        }

        // mga state na tinatawag ng Play / CrossFade
        var stateNames = new HashSet<string>();
        foreach (var cs in AllStates(ac)) stateNames.Add(cs.state.name);
        foreach (var s in u.states)
        {
            if (stateNames.Contains(s)) Ok("State '" + s + "' nasa controller.");
            else Problem("Tinatawag ng EnemyAI ang state '" + s + "' pero wala sa controller. Mga state: " + string.Join(", ", new List<string>(stateNames).ToArray()));
        }

        // mga parameter na ginagamit ng transitions pero hindi sine-set ng script
        var inTransitions = new HashSet<string>();
        foreach (var t in AllTransitions(ac))
            foreach (var c in t.conditions) inTransitions.Add(c.parameter);
        foreach (var bt in AllBlendTrees(ac)) { inTransitions.Add(bt.blendParameter); inTransitions.Add(bt.blendParameterY); }

        foreach (var p in ac.parameters)
            if (inTransitions.Contains(p.name) && !u.prm.ContainsKey(p.name))
                Warn("Parameter '" + p.name + "' (" + p.type + ") ay ginagamit ng controller pero hindi kailanman itinatakda ng EnemyAI. Kung ito ang nagpapagalaw ng animation, hindi iyon gagana.");
    }

    static string Norm(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.ToLower()) if (char.IsLetterOrDigit(c)) sb.Append(c);
        string r = sb.ToString();
        foreach (var pre in new[] { "is", "has", "can" })
            if (r.StartsWith(pre) && r.Length > pre.Length + 2) { r = r.Substring(pre.Length); break; }
        return r;
    }

    static bool Similar(string a, string b)
    {
        string na = Norm(a), nb = Norm(b);
        if (na == nb) return true;
        return na.Length >= 4 && nb.Length >= 4 && (na.Contains(nb) || nb.Contains(na));
    }

    static void RenameParam(AnimatorController ac, string oldName, string newName)
    {
        Undo.RecordObject(ac, "Rename parameter");
        var ps = ac.parameters;
        for (int i = 0; i < ps.Length; i++) if (ps[i].name == oldName) ps[i].name = newName;
        ac.parameters = ps;

        foreach (var t in AllTransitions(ac))
        {
            var cs = t.conditions;
            bool ch = false;
            for (int j = 0; j < cs.Length; j++)
                if (cs[j].parameter == oldName) { cs[j].parameter = newName; ch = true; }
            if (ch) { t.conditions = cs; EditorUtility.SetDirty(t); }
        }
        foreach (var bt in AllBlendTrees(ac))
        {
            if (bt.blendParameter == oldName) bt.blendParameter = newName;
            if (bt.blendParameterY == oldName) bt.blendParameterY = newName;
            EditorUtility.SetDirty(bt);
        }
        EditorUtility.SetDirty(ac);
    }

    static void Backup(AnimatorController ac)
    {
        if (backedUp) return;
        backedUp = true;
        string path = AssetDatabase.GetAssetPath(ac);
        if (string.IsNullOrEmpty(path)) return;
        string bak = path.Replace(".controller", "_BACKUP.controller");
        if (AssetDatabase.LoadAssetAtPath<Object>(bak) == null)
        {
            AssetDatabase.CopyAsset(path, bak);
            log.Add("   (gumawa ng kopya ng controller: " + bak + ")");
        }
    }

    // =====================================================================
    //  CONTROLLER
    // =====================================================================
    static void CheckController(AnimatorController ac, Usage u, bool fix)
    {
        // ---- layers ----
        var layers = ac.layers;
        bool layersChanged = false;
        for (int i = 1; i < layers.Length; i++)
        {
            if (layers[i].defaultWeight > 0f) continue;
            bool scriptControls = u != null && u.usesLayerWeight;
            if (fix && !scriptControls)
            {
                Backup(ac);
                layers[i].defaultWeight = 1f;
                layersChanged = true;
                Fixed("Layer '" + layers[i].name + "' ay may Weight 0 kaya walang epekto. Ginawang 1.");
            }
            else Warn("Layer '" + layers[i].name + "' ay may Weight 0" + (scriptControls ? " (mukhang kinokontrol ng script)." : "."));
        }
        if (layersChanged) { Undo.RecordObject(ac, "Layer weight"); ac.layers = layers; EditorUtility.SetDirty(ac); }

        // ---- states ----
        var destinations = new HashSet<AnimatorState>();
        foreach (var t in AllTransitions(ac))
        {
            var st = t as AnimatorStateTransition;
            if (st != null && st.destinationState != null) destinations.Add(st.destinationState);
            var tr = t as AnimatorTransition;
            if (tr != null && tr.destinationState != null) destinations.Add(tr.destinationState);
        }
        foreach (var layer in ac.layers) AddDefaults(layer.stateMachine, destinations);

        foreach (var cs in AllStates(ac))
        {
            var s = cs.state;

            if (s.motion == null) Warn("State '" + s.name + "' ay walang animation clip.");

            if (!s.speedParameterActive && Mathf.Approximately(s.speed, 0f))
            {
                if (fix) { Backup(ac); Undo.RecordObject(s, "State speed"); s.speed = 1f; EditorUtility.SetDirty(s); Fixed("State '" + s.name + "' ay may Speed 0 (nakatigil). Ginawang 1."); }
                else Problem("State '" + s.name + "' ay may Speed 0, kaya nakatigil ang animation.");
            }

            if (s.speedParameterActive)
            {
                var ps = ac.parameters;
                for (int i = 0; i < ps.Length; i++)
                    if (ps[i].name == s.speedParameter && Mathf.Approximately(ps[i].defaultFloat, 0f))
                    {
                        if (fix)
                        {
                            Backup(ac);
                            ps[i].defaultFloat = 1f;
                            ac.parameters = ps;
                            Fixed("Ang speed multiplier '" + s.speedParameter + "' ng state '" + s.name + "' ay 0 ang default (nakatigil). Ginawang 1.");
                        }
                        else Problem("Ang speed multiplier '" + s.speedParameter + "' ng state '" + s.name + "' ay 0 ang default, kaya nakatigil.");
                    }
            }

            if (!destinations.Contains(s) && (ac.layers.Length > 0) && !(AnyPlayCall(u, s.name)))
                Warn("State '" + s.name + "' ay walang transition na papunta rito (hindi ito mapupuntahan).");

            foreach (var t in s.transitions)
                if (!t.hasExitTime && t.conditions.Length == 0)
                    Warn("Transition ng '" + s.name + "' -> '" + (t.destinationState != null ? t.destinationState.name : "?") +
                         "' ay walang condition at walang Exit Time (lilipat agad).");
        }
    }

    static bool AnyPlayCall(Usage u, string stateName)
    {
        return u != null && u.states.Contains(stateName);
    }

    static void AddDefaults(AnimatorStateMachine sm, HashSet<AnimatorState> set)
    {
        if (sm.defaultState != null) set.Add(sm.defaultState);
        foreach (var child in sm.stateMachines) AddDefaults(child.stateMachine, set);
    }

    // =====================================================================
    //  CLIPS
    // =====================================================================
    static readonly string[] LoopWords = { "idle", "walk", "run", "chase", "patrol", "search", "crawl", "move", "wander", "sprint" };

    static void CheckClips(List<KeyValuePair<string, AnimationClip>> clips, bool fix)
    {
        var seen = new HashSet<AnimationClip>();
        foreach (var kv in clips)
        {
            var clip = kv.Value;
            if (clip == null || !seen.Add(clip)) continue;

            string path = AssetDatabase.GetAssetPath(clip);
            bool standalone = path.EndsWith(".anim");

            if (clip.legacy)
            {
                if (standalone && fix)
                {
                    clip.legacy = false;
                    EditorUtility.SetDirty(clip);
                    Fixed("Clip '" + clip.name + "' ay Legacy (hindi gumagana sa Animator). Ginawang hindi legacy.");
                }
                else Problem("Clip '" + clip.name + "' ay Legacy, hindi ito gumagana sa Animator. " +
                             (standalone ? "" : "Palitan ang Rig ng source na FBX (" + path + ") sa Generic/Humanoid."));
            }

            string sn = (kv.Key + " " + clip.name).ToLower();
            bool wantsLoop = false;
            foreach (var w in LoopWords) if (sn.Contains(w)) { wantsLoop = true; break; }

            if (wantsLoop && !clip.isLooping)
            {
                if (standalone && fix)
                {
                    var s = AnimationUtility.GetAnimationClipSettings(clip);
                    s.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, s);
                    EditorUtility.SetDirty(clip);
                    Fixed("Clip '" + clip.name + "' (state '" + kv.Key + "') ay hindi naka-loop kaya isang beses lang tumatakbo. In-on ang Loop Time.");
                }
                else Warn("Clip '" + clip.name + "' (state '" + kv.Key + "') ay hindi naka-loop. " +
                          (standalone ? "" : "Piliin ang FBX (" + path + "), Animation tab, i-check ang Loop Time ng clip na iyon, Apply."));
            }
        }
    }

    // =====================================================================
    //  MGA TULONG
    // =====================================================================
    static List<ChildAnimatorState> AllStates(AnimatorController ac)
    {
        var res = new List<ChildAnimatorState>();
        foreach (var layer in ac.layers) CollectStates(layer.stateMachine, res);
        return res;
    }

    static void CollectStates(AnimatorStateMachine sm, List<ChildAnimatorState> res)
    {
        foreach (var cs in sm.states) res.Add(cs);
        foreach (var child in sm.stateMachines) CollectStates(child.stateMachine, res);
    }

    static List<AnimatorTransitionBase> AllTransitions(AnimatorController ac)
    {
        var res = new List<AnimatorTransitionBase>();
        foreach (var layer in ac.layers) CollectTransitions(layer.stateMachine, res);
        return res;
    }

    static void CollectTransitions(AnimatorStateMachine sm, List<AnimatorTransitionBase> res)
    {
        foreach (var cs in sm.states)
            foreach (var t in cs.state.transitions) res.Add(t);
        foreach (var t in sm.anyStateTransitions) res.Add(t);
        foreach (var t in sm.entryTransitions) res.Add(t);
        foreach (var child in sm.stateMachines) CollectTransitions(child.stateMachine, res);
    }

    static List<BlendTree> AllBlendTrees(AnimatorController ac)
    {
        var res = new List<BlendTree>();
        foreach (var cs in AllStates(ac)) CollectBlendTrees(cs.state.motion, res);
        return res;
    }

    static void CollectBlendTrees(Motion m, List<BlendTree> res)
    {
        var bt = m as BlendTree;
        if (bt == null) return;
        res.Add(bt);
        foreach (var c in bt.children) CollectBlendTrees(c.motion, res);
    }

    static void CollectClips(Motion m, string stateName, List<KeyValuePair<string, AnimationClip>> res)
    {
        if (m == null) return;
        var clip = m as AnimationClip;
        if (clip != null) { res.Add(new KeyValuePair<string, AnimationClip>(stateName, clip)); return; }
        var bt = m as BlendTree;
        if (bt != null)
            foreach (var c in bt.children) CollectClips(c.motion, stateName, res);
    }

    static void Touch(Object o)
    {
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    static List<T> FindInScene<T>() where T : Component
    {
        var res = new List<T>();
        foreach (var c in Resources.FindObjectsOfTypeAll<T>())
        {
            if (c == null || EditorUtility.IsPersistent(c)) continue;
            if (c.hideFlags != HideFlags.None) continue;
            var sc = c.gameObject.scene;
            if (!sc.IsValid() || !sc.isLoaded) continue;
            res.Add(c);
        }
        return res;
    }

    static string HierarchyPath(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static void Finish(GameObject enemy, bool fix)
    {
        if (fix)
        {
            EditorSceneManager.MarkSceneDirty(enemy.scene);
            AssetDatabase.SaveAssets();
        }

        string header = "[EnemyAnimFixer] " + nOk + " OK, " + nFixed + " nayos, " + nWarn + " babala, " + nProblem + " problema\n";
        Debug.Log(header + string.Join("\n", log.ToArray()), enemy);

        string msg = nOk + " OK\n" + nFixed + " naayos\n" + nWarn + " babala\n" + nProblem + " problema\n\n" +
                     "Buong listahan nasa Console." +
                     (fix && nFixed > 0 ? "\nI-save ang scene (Ctrl+S) at subukan sa Play." : "") +
                     (!fix && nProblem > 0 ? "\nGamitin ang DIAGNOSE + FIX para ayusin ang kaya." : "");
        EditorUtility.DisplayDialog("Enemy animation", msg, "OK");
    }
}
