using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Plugin {
    public class ParamSmoothingPlugin : FeaturePlugin {
        public VFAFloat Smooth(string name, VFAFloat target, float smoothing, bool useAcceleration = true) {
            if (smoothing <= 0) return target;
            if (smoothing > 0.999) smoothing = 0.999f;

            var adjustmentExponent = 0.1f;
            smoothing = (float)Math.Pow(smoothing, adjustmentExponent);
            
            var fx = GetFx();
            var speedParam = fx.NewFloat($"{name}/Speed", def: smoothing);

            var output = Smooth_($"{name}/Pass1", target, speedParam);
            if (useAcceleration) output = Smooth_($"{name}/Pass2", output, speedParam);
            return output;
        }

        private VFAFloat Smooth_(string name, VFAFloat target, VFAFloat speedParam) {
            var fx = GetFx();

            var output = fx.NewFloat(name);
            
            // These clips drive the output param to certain values
            var minClip = fx.NewClip($"{output.Name()}-1");
            minClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, -1f));
            var maxClip = fx.NewClip($"{output.Name()}1");
            maxClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, 1f));

            // Maintain tree - keeps the current value
            var maintainTree = fx.NewBlendTree($"{output.Name()}_do_not_change");
            maintainTree.blendType = BlendTreeType.Simple1D;
            maintainTree.useAutomaticThresholds = false;
            maintainTree.blendParameter = output.Name();
            maintainTree.AddChild(minClip, -1);
            maintainTree.AddChild(maxClip, 1);

            // Target tree - uses the target (input) value
            var targetTree = fx.NewBlendTree($"{output.Name()}_lock_to_{target.Name()}");
            targetTree.blendType = BlendTreeType.Simple1D;
            targetTree.useAutomaticThresholds = false;
            targetTree.blendParameter = target.Name();
            targetTree.AddChild(minClip, -1);
            targetTree.AddChild(maxClip, 1);

            //The following two trees merge the update and the maintain tree together. The smoothParam controls 
            //how much from either tree should be applied during each tick
            var smoothTree = fx.NewBlendTree($"{output.Name()}_smooth_to_{target.Name()}");
            smoothTree.blendType = BlendTreeType.Simple1D;
            smoothTree.useAutomaticThresholds = false;
            smoothTree.blendParameter = speedParam.Name();
            smoothTree.AddChild(targetTree, 0);
            smoothTree.AddChild(maintainTree, 1);

            var layer = fx.NewLayer("Smoothing " + name);
            layer.NewState("Smooth").WithAnimation(smoothTree);
            return output;
        }

        public VFAFloat SetValueWithConditions(
            string name,
            float minPossible, float maxPossible,
            params (VFAFloat,VFACondition)[] targets
        ) {
            var fx = GetFx();

            var output = fx.NewFloat(name);
            
            // These clips drive the output param to certain values
            var minClip = fx.NewClip($"{output.Name()}Max");
            minClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, maxPossible));
            var maxClip = fx.NewClip($"{output.Name()}Min");
            maxClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, minPossible));

            Motion GenerateTargetTree(VFAFloat target) {
                // Target tree - uses the target (input) value
                var targetTree = fx.NewBlendTree($"{output.Name()}_set_to_{target.Name()}");
                targetTree.blendType = BlendTreeType.Simple1D;
                targetTree.useAutomaticThresholds = false;
                targetTree.blendParameter = target.Name();
                targetTree.AddChild(minClip, maxPossible);
                targetTree.AddChild(maxClip, minPossible);
                return targetTree;
            }

            var layer = fx.NewLayer("SetConditional " + name);

            VFAState.FakeAnyState(targets.Select(target => {
                if (target.Item1 == null) {
                    return (layer.NewState("Maintain").WithAnimation(GenerateTargetTree(output)), target.Item2);
                }
                return (layer.NewState("Target " + target.Item1.Name()).WithAnimation(GenerateTargetTree(target.Item1)), target.Item2);
            }).ToArray());

            return output;
        }

        public VFACondition GreaterThan(VFAFloat a, VFAFloat b, bool orEqualTo = false) {
            var fx = GetFx();
            var bIsWinning = fx.NewFloat("comparison");
            var layer = fx.NewLayer($"{a.Name()} vs {b.Name()}");
            var tree = IsBWinningTree(a, b, bIsWinning);
            layer.NewState($"{a.Name()} vs {b.Name()}").WithAnimation(tree);
            if (orEqualTo) return bIsWinning.IsGreaterThan(0.5f).Not();
            return bIsWinning.IsLessThan(0.5f);
        }

        public Motion IsBWinningTree(VFAFloat a, VFAFloat b, VFAFloat bWinning) {
            var fx = GetFx();
            var bWinningClip = fx.NewClip("vs1");
            bWinningClip.SetCurve("", typeof(Animator), bWinning.Name(), AnimationCurve.Constant(0, 0, 1f));
            var aWinningClip = fx.NewClip("vs0");
            aWinningClip.SetCurve("", typeof(Animator), bWinning.Name(), AnimationCurve.Constant(0, 0, 0f));
            var tree = fx.NewBlendTree($"{a.Name()} vs {b.Name()}");
            tree.useAutomaticThresholds = false;
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.AddChild(aWinningClip, new Vector2(1f, 0));
            tree.AddChild(bWinningClip, new Vector2(0, 1f));
            tree.blendParameter = a.Name();
            tree.blendParameterY = b.Name();
            return tree;
        }

        public VFAFloat Map(string name, VFAFloat input, float inMin, float inMax, float outMin, float outMax) {
            var fx = GetFx();

            var output = fx.NewFloat(name);

            // These clips drive the output param to certain values
            var minClip = fx.NewClip(output.Name() + "Min");
            minClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, outMin));
            var maxClip = fx.NewClip(output.Name() + "Max");
            maxClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, outMax));

            var tree = fx.NewBlendTree($"{input.Name()}_map_{inMin}_{inMax}_to_{outMin}_{outMax}");
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            tree.blendParameter = input.Name();
            if (inMin < inMax) {
                tree.AddChild(minClip, inMin);
                tree.AddChild(maxClip, inMax);
            } else {
                tree.AddChild(maxClip, inMax);
                tree.AddChild(minClip, inMin);
            }

            var layer = fx.NewLayer($"Map {input.Name()} from {inMin}-{inMax} to {outMin}-{outMax}");
            layer.NewState($"Run").WithAnimation(tree);

            return output;
        }
    }
}
