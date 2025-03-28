using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Feature {
    /** This builder is responsible for creating a fake head bone, and moving
     * objects onto it, if those objects should be visible in first person.
     */
    public class FakeHeadBuilder : FeatureBuilder {

        private HashSet<GameObject> objectsEligibleForFakeHead = new HashSet<GameObject>();

        public void MarkEligible(GameObject obj) {
            objectsEligibleForFakeHead.Add(obj);
        }

        [FeatureBuilderAction(FeatureOrder.FakeHeadBuilder)]
        public void Apply() {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
                return;
            }

            var head = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Head);
            if (!head) return;
            
            var objectsForFakeHead = objectsEligibleForFakeHead
                .Where(obj => obj.transform.parent == head.transform)
                .ToList();
            if (objectsForFakeHead.Count == 0) return;
            
            var mover = GetBuilder<ObjectMoveBuilder>();
            var vrcfAlwaysVisibleHead = GameObjects.Create("vrcfAlwaysVisibleHead", head.transform.parent, useTransformFrom: head.transform);
            
            var p = vrcfAlwaysVisibleHead.AddComponent<ParentConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = head.transform,
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;

            foreach (var obj in objectsForFakeHead) {
                mover.Move(obj, vrcfAlwaysVisibleHead.gameObject);
            }
        }
    }
}
