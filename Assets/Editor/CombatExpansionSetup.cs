using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class CombatExpansionSetup
{
    [MenuItem("Tools/Underdog/Prepare Combat Expansion")]
    public static void Prepare()
    {
        try
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var managers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WaveManager>(true)).ToArray();
            if(managers.Length!=1)throw new InvalidOperationException("Abra a partida com um unico WaveManager.");
            Validate(managers[0]);
            string targets=scene.path+"\n"+string.Join("\n",new[]{managers[0].basico,managers[0].rapido,managers[0].distante}.Select(p=>AssetDatabase.GetAssetPath(p.gameObject)));
            if(!EditorUtility.DisplayDialog("Preparar expansao do combate", "Criar backups e configurar estes arquivos?\n"+targets+"\n\nControllers copiados, mascara superior do Mage, especiais e materiais/rajada. A cena sera salva com o catalogo de buffs. Camera, arena, HUD existente e composicao das ondas serao preservados.","Criar backups e configurar","Cancelar"))return;
            Debug.Log("Expansao configurada. Assets e backups: "+Aplicar(managers[0])+". Inicie nova sessao de Play Mode.");
        }
        catch(Exception error){Debug.LogException(error);}
    }
    static void Validate(WaveManager manager)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!Application.unityVersion.StartsWith("2022.3."))throw new InvalidOperationException("Execute fora do Play Mode na Unity 2022.3.");
        if(manager==null||!manager.gameObject.scene.IsValid()||string.IsNullOrEmpty(manager.gameObject.scene.path)||manager.gameObject.scene.isDirty)
            throw new InvalidOperationException("Salve a cena atual antes de criar o backup.");
        if(manager.jogador==null||manager.jogador.GetComponent<PlayerCombat>()==null)throw new InvalidOperationException("PlayerCombat da partida ausente.");
        var prefabs=new[]{manager.basico,manager.rapido,manager.distante};
        if(prefabs.Any(p=>p==null)||prefabs.Distinct().Count()!=3)throw new InvalidOperationException("Referencie os tres prefabs distintos da partida.");
        foreach(var ai in prefabs)
        {
            string path=AssetDatabase.GetAssetPath(ai.gameObject);
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||path.Contains("/Assets_Externos/")||!path.EndsWith(".prefab")||ai.transform.parent!=null)
                throw new InvalidOperationException("Use prefabs de gameplay fora dos assets KayKit originais: "+path);
            if(!ai.usarNavMesh||ai.GetComponent<NavMeshAgent>()==null)throw new InvalidOperationException("Prefab precisa preservar sua navegacao NavMesh: "+path);
            var animator=ai.GetComponentInChildren<Animator>(true);
            var controller=animator!=null?animator.runtimeAnimatorController as AnimatorController:null;
            if(controller==null||Find(controller.layers[0].stateMachine,"Attack")==null)throw new InvalidOperationException("Controller/estado Attack nao suportado: "+path);
            if(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).Any(EditorUtility.IsDirty)||ai.GetComponentsInChildren<Component>(true).Any(c=>c!=null&&EditorUtility.IsDirty(c)))
                throw new InvalidOperationException("Salve as alteracoes pendentes do prefab/Animator: "+path);
        }
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null&&stage.scene.isDirty)throw new InvalidOperationException("Salve ou feche o Prefab Mode.");
        if(manager.distante.projetil==null||!AssetDatabase.GetAssetPath(manager.distante.projetil.gameObject).EndsWith(".prefab"))throw new InvalidOperationException("Projetil basico do Mage ausente ou nao e prefab.");
        if(Shader.Find("Universal Render Pipeline/Unlit")==null)throw new InvalidOperationException("Shader URP Unlit indisponivel.");
    }
    static AnimatorState Find(AnimatorStateMachine machine,string name)=>machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name);
    static void Exit(AnimatorState state,AnimatorState destination)
    {
        if(state.transitions.Any(t=>t.destinationState==destination))return;
        var transition=state.AddTransition(destination);transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=0.05f;
    }
    static AnimationClip CopyClip(AnimationClip source,string path,bool loop=false)
    {
        if(source==null)throw new InvalidOperationException("Clip real de ataque ausente.");
        var clip=UnityEngine.Object.Instantiate(source);clip.name=Path.GetFileNameWithoutExtension(path);
        AnimationUtility.SetAnimationEvents(clip,new AnimationEvent[0]);
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
        AssetDatabase.CreateAsset(clip,path);return clip;
    }
    static AnimationClip KayKitClip(string name)
    {
        foreach(string guid in AssetDatabase.FindAssets("Rig_Medium"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))continue;
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>c.name==name);
            if(clip!=null)return clip;
        }
        return null;
    }
    static void Configure(EnemyAI ai,string folder,string label,AnimationClip dodge)
    {
        var animator=ai.GetComponentInChildren<Animator>(true);
        string path=folder+"/"+label+".controller";
        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),path))throw new IOException("Falha ao copiar Controller de "+label);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        var machine=controller.layers[0].stateMachine;
        var attack=Find(machine,"Attack");var source=attack.motion as AnimationClip;
        var events=source!=null?AnimationUtility.GetAnimationEvents(source):new AnimationEvent[0];
        ai.pontoImpactoEspecial=events.Length>0?events[0].time:0.4f;
        if(!controller.parameters.Any(p=>p.name=="SpecialRate"))controller.AddParameter(new AnimatorControllerParameter{name="SpecialRate",type=AnimatorControllerParameterType.Float,defaultFloat=1});
        if(ai.ataqueDistancia)
        {
            var backwards=KayKitClip("Walking_Backwards");var left=KayKitClip("Running_Strafe_Left");var right=KayKitClip("Running_Strafe_Right");
            var locomotion=Find(machine,"Locomotion");var oldTree=locomotion!=null?locomotion.motion as BlendTree:null;
            if(backwards!=null&&left!=null&&right!=null&&oldTree!=null&&oldTree.children.Length>=2)
            {
                foreach(string parameter in new[]{"MoveX","MoveZ"})if(!controller.parameters.Any(p=>p.name==parameter))controller.AddParameter(parameter,AnimatorControllerParameterType.Float);
                var tree=new BlendTree{name="MageDirectional",blendType=BlendTreeType.SimpleDirectional2D,blendParameter="MoveX",blendParameterY="MoveZ",useAutomaticThresholds=false};
                AssetDatabase.AddObjectToAsset(tree,controller);
                tree.AddChild(oldTree.children[0].motion,Vector2.zero);tree.AddChild(oldTree.children[1].motion,Vector2.up);
                tree.AddChild(CopyClip(backwards,folder+"/MageBackward.anim",true),Vector2.down);
                tree.AddChild(CopyClip(left,folder+"/MageLeft.anim",true),Vector2.left);
                tree.AddChild(CopyClip(right,folder+"/MageRight.anim",true),Vector2.right);locomotion.motion=tree;
            }
            var mask=new AvatarMask{name="SkeletonMageUpperBody"};var transforms=animator.GetComponentsInChildren<Transform>(true);
            mask.transformCount=transforms.Length;int active=0;
            for(int i=0;i<transforms.Length;i++)
            {
                string bone=AnimationUtility.CalculateTransformPath(transforms[i],animator.transform);mask.SetTransformPath(i,bone);
                bool upper=bone.Contains("/spine")||bone.EndsWith("spine");mask.SetTransformActive(i,upper);if(upper)active++;
            }
            if(active==0){UnityEngine.Object.DestroyImmediate(mask);Debug.LogWarning("Rig sem spine: Mage usa alternativa de corpo inteiro em movimento.");ai.camadaAtaque=0;}
            else
            {
                AssetDatabase.CreateAsset(mask,folder+"/MageUpperBody.mask");int layer=Array.FindIndex(controller.layers,l=>l.name=="Upper Body");
                if(layer<0){controller.AddLayer("Upper Body");layer=controller.layers.Length-1;}
                var layers=controller.layers;layers[layer].avatarMask=mask;layers[layer].defaultWeight=0;controller.layers=layers;
                var upper=layers[layer].stateMachine;var empty=Find(upper,"Empty")??upper.AddState("Empty");upper.defaultState=empty;empty.writeDefaultValues=false;
                var cast=Find(upper,"Attack")??upper.AddState("Attack");cast.motion=source;cast.speed=attack.speed;cast.writeDefaultValues=false;Exit(cast,empty);
                machine=upper;ai.camadaAtaque=layer;
            }
        }
        var special=Find(machine,"Special")??machine.AddState("Special");special.motion=CopyClip(source,folder+"/"+label+"Special.anim");special.writeDefaultValues=false;
        special.speedParameter="SpecialRate";special.speedParameterActive=true;
        var rest=Find(machine,ai.camadaAtaque>0?"Empty":"Locomotion");if(rest==null)throw new InvalidOperationException("Locomotion/Empty ausente.");Exit(special,rest);
        if(!ai.ataqueDistancia&&ai.Tipo==EnemyAI.Comportamento.Flanqueador&&dodge!=null)
        {
            var dash=Find(machine,"Dash")??machine.AddState("Dash");dash.motion=CopyClip(dodge,folder+"/RogueDash.anim");dash.speed=dodge.length/Mathf.Max(0.05f,ai.GetComponent<EnemySpecialAttack>().investida.duracao);dash.writeDefaultValues=false;Exit(dash,rest);
        }
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
    }
    static Material WarningMaterial(string folder)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Rogue Warning"};
        material.SetColor("_BaseColor",new Color(1,0.08f,0.04f,0.4f));material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);
        material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;AssetDatabase.CreateAsset(material,folder+"/RogueWarning.mat");return material;
    }
    static Projectile Burst(Projectile source,string folder)
    {
        string path=folder+"/MageBurst.prefab";if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.gameObject),path))throw new IOException("Falha ao copiar projetil.");
        var contents=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var renderer=contents.GetComponentInChildren<Renderer>();
            if(renderer!=null&&renderer.sharedMaterial!=null)
            {
                var material=new Material(renderer.sharedMaterial){name="Arcane fan"};if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",new Color(0.8f,0.3f,2,1));
                if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",new Color(1.2f,0.4f,3));AssetDatabase.CreateAsset(material,folder+"/MageBurst.mat");
                foreach(var item in contents.GetComponentsInChildren<Renderer>())item.sharedMaterial=material;
            }
            PrefabUtility.SaveAsPrefabAsset(contents,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(contents);}
        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Projectile>();
    }
    public static string Aplicar(WaveManager manager)
    {
        Validate(manager);var scene=manager.gameObject.scene;
        if(!AssetDatabase.IsValidFolder("Assets/Generated"))AssetDatabase.CreateFolder("Assets","Generated");
        string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/CombatExpansion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));AssetDatabase.CreateFolder("Assets/Generated",Path.GetFileName(folder));
        string[] paths=new[]{manager.basico,manager.rapido,manager.distante}.Select(p=>AssetDatabase.GetAssetPath(p.gameObject)).ToArray();
        if(!AssetDatabase.CopyAsset(scene.path,folder+"/SceneBefore.unity"))throw new IOException("Falha no backup da cena.");
        for(int i=0;i<3;i++)if(!AssetDatabase.CopyAsset(paths[i],folder+"/Before_"+i+".prefab"))throw new IOException("Falha no backup dos prefabs.");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
        try
        {
            var material=WarningMaterial(folder);var burst=Burst(manager.distante.projetil,folder);var dodge=KayKitClip("Dodge_Forward");
            for(int i=0;i<3;i++)
            {
                var contents=PrefabUtility.LoadPrefabContents(paths[i]);
                try
                {
                    var ai=contents.GetComponent<EnemyAI>();var special=contents.GetComponent<EnemySpecialAttack>();if(special==null)special=contents.AddComponent<EnemySpecialAttack>();
                    if(contents.GetComponent<CombatDisplacement>()==null)contents.AddComponent<CombatDisplacement>();
                    special.materialAviso=material;if(i==2)special.projetilRajada=burst;
                    Configure(ai,folder,i==0?"Warrior":i==1?"Rogue":"Mage",dodge);ai.moverAoAtacar=true;
                    if(PrefabUtility.SaveAsPrefabAsset(contents,paths[i])==null)throw new IOException("Falha ao salvar prefab.");
                }
                finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            Undo.RecordObject(manager,"Configure combat expansion");
            if(manager.melhorias==null)manager.melhorias=manager.GetComponent<PlayerBuffs>()??Undo.AddComponent<PlayerBuffs>(manager.gameObject);
            var combat=manager.jogador.GetComponent<PlayerCombat>();Undo.RecordObject(combat,"Upgrade magic explosion");
            if(Mathf.Approximately(combat.explosao.dano,35))combat.explosao.dano=50;
            EditorUtility.SetDirty(combat);EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Falha ao salvar cena configurada.");Undo.CollapseUndoOperations(undo);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            for(int i=0;i<3;i++){File.Copy(folder+"/Before_"+i+".prefab",paths[i],true);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceUpdate);}
            File.Copy(folder+"/SceneBefore.unity",scene.path,true);AssetDatabase.ImportAsset(scene.path,ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(scene.path);throw;
        }
        return folder;
    }
}
