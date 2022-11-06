using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Inspector;
using VF.Model;

namespace VF.Menu {
    public class DpsAttachMigration {
        public static void Run(GameObject avatarObj) {
            var messages = Migrate(avatarObj, true);
            if (string.IsNullOrWhiteSpace(messages)) {
                EditorUtility.DisplayDialog(
                    "DPSAttach Migrator",
                    "Failed to find DPSAttach on selected avatar",
                    "Ok"
                );
                return;
            }
            
            var doIt = EditorUtility.DisplayDialog(
                "DPSAttach Migrator",
                messages + "\n\nContinue?",
                "Yes, Do it!",
                "Cancel"
            );
            if (!doIt) return;

            Migrate(avatarObj, false);
            
            EditorUtility.DisplayDialog(
                "DPSAttach Migrator",
                "Done! You can now delete any holes / rings you don't wish to use (DPSAttach had... a lot of them)",
                "Ok"
            );
        }

        private static string Migrate(GameObject avatarObj, bool dryRun) {
            var dryRunMigrate = new List<string>();

            var oldParentsToDelete = new HashSet<GameObject>();
            foreach (var light in avatarObj.GetComponentsInChildren<Light>(true)) {
                var isHole = OGBOrificeEditor.IsHole(light);
                var isRing = OGBOrificeEditor.IsRing(light);
                if (!isHole && !isRing) continue;
                var parent = light.transform.parent;
                if (!parent) continue;
                if (oldParentsToDelete.Contains(parent.gameObject)) continue;
                var constraint = parent.gameObject.GetComponent<ParentConstraint>();
                if (constraint == null) continue;
                if (constraint.sourceCount < 2) continue;
                oldParentsToDelete.Add(parent.gameObject);
                
                for (var i = 0; i < constraint.sourceCount; i++) {
                    var source = constraint.GetSource(i);
                    var t = source.sourceTransform;
                    if (t == null) continue;
                    var obj = t.gameObject;
                    var name = obj.name;
                    var id = name.IndexOf("(");
                    if (id >= 0) name = name.Substring(id+1);
                    id = name.IndexOf(")");
                    if (id >= 0) name = name.Substring(0, id);

                    var fullName = (isHole ? "Hole" : "Ring") + " (" + name + ")";

                    dryRunMigrate.Add(obj.name + " -> " + fullName);
                    if (!dryRun) {
                        var ogb = obj.GetComponent<OGBOrifice>();
                        if (ogb == null) {
                            ogb = obj.AddComponent<OGBOrifice>();
                            obj.transform.Rotate(90, 0, 0);
                        }
                        ogb.addLight = isHole ? AddLight.Hole : AddLight.Ring;
                        ogb.name = name;
                        ogb.addMenuItem = true;
                        obj.name = fullName;
                        ogb.enableHandTouchZone = name.ToLower().Contains("vagina") || name.ToLower().Contains("anus");
                    }
                }
            }
            
            if (dryRunMigrate.Count == 0) return "";

            var deletions = AvatarCleaner.Cleanup(avatarObj,
                perform: !dryRun,
                ShouldRemoveObj: obj => {
                    return obj.name == "GUIDES_DELETE"
                           || oldParentsToDelete.Contains(obj);
                },
                ShouldRemoveAsset: asset => {
                    if (asset == null) return false;
                    var path = AssetDatabase.GetAssetPath(asset);
                    if (path == null) return false;
                    var lower = path.ToLower();
                    if (lower.Contains("dps_attach")) return true;
                    return false;
                },
                ShouldRemoveLayer: layer => {
                    return layer == "DPS_Holes"
                           || layer == "DPS_Rings"
                           || layer == "HotDog";
                },
                ShouldRemoveParam: param => {
                    return param == "DPS_Hole"
                           || param == "DPS_Ring"
                           || param == "HotDog";
                }
            );

            return "These objects will be converted to OGB holes/rings:\n"
                   + string.Join("\n", dryRunMigrate)
                   + "\n\nThese objects will be deleted:\n"
                   + string.Join("\n", deletions);
        }
    }
}