using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;

namespace VF.Inspector {
    public class VRCFuryComponentEditor<T> : Editor where T : VRCFuryComponent {

        /*
        public override bool UseDefaultMargins() {
            return false;
        }
        
        private Action detachChanges = null;

        private void Detach() {
            if (detachChanges != null) detachChanges();
            detachChanges = null;
        }

        private void CreateHeaderOverlay(VisualElement el) {
            Detach();
            
            var inspectorRoot = el.parent?.parent;
            if (inspectorRoot == null) return;

            var header = inspectorRoot.Children().ToList()
                .FirstOrDefault(c => c.name.EndsWith("Header"));
            var footer = inspectorRoot.Children().ToList()
                .FirstOrDefault(c => c.name.EndsWith("Footer"));
            if (header != null) header.style.display = DisplayStyle.None;
            if (footer != null) footer.style.display = DisplayStyle.None;

            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    height = 21,
                    borderTopWidth = 1,
                    borderTopColor = Color.black,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f),
                }
            };
            VRCFuryEditorUtils.HoverHighlight(row);
            var normalLabelColor = new Color(0.05f, 0.05f, 0.05f);
            var label = new Label("VRCF") {
                style = {
                    color = new Color(0.8f, 0.4f, 0f),
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0,
                    paddingLeft = 10,
                    paddingRight = 7,
                    backgroundColor = normalLabelColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    flexShrink = 1,
                }
            };
            row.Add(label);

            row.Add(new VisualElement {
                style = {
                    borderLeftColor = normalLabelColor,
                    borderTopColor = normalLabelColor,
                    borderLeftWidth = 5,
                    borderTopWidth = 10,
                    borderRightWidth = 5,
                    borderBottomWidth = 10,
                }
            });

            var name = new Label("Toggle") {
                style = {
                    //color = Color.white,
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 5
                }
            };
            row.Add(name);
            void ContextMenu(Vector2 pos) {
                var displayMethod = typeof(EditorUtility)
                    .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Where(method => method.Name == "DisplayObjectContextMenu")
                    .Where(method => method.GetParameters()[1].ParameterType.IsArray)
                    .First();
                displayMethod?.Invoke(null, new object[] {
                    new Rect(pos.x, pos.y, 0.0f, 0.0f),
                    targets,
                    0
                });
            }
            row.RegisterCallback<MouseUpEvent>(e => {
                if (e.button == 0) {
                    Debug.Log("Toggle " + target);
                    e.StopImmediatePropagation();
                    InternalEditorUtility.SetIsInspectorExpanded(target, !InternalEditorUtility.GetIsInspectorExpanded(target));
                }
                if (e.button == 1) {
                    e.StopImmediatePropagation();
                    ContextMenu(e.mousePosition);
                }
            });
            {
                var up = new Label("↑") {
                    style = {
                        width = 16,
                        height = 16,
                        marginTop = 2,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    }
                };
                up.RegisterCallback<MouseUpEvent>(e => {
                    if (e.button != 0) return;
                    e.StopImmediatePropagation();
                    ComponentUtility.MoveComponentUp(target as Component);
                });
                row.Add(up);
                VRCFuryEditorUtils.HoverHighlight(up);
            }
            {
                var menu = new Image {
                    image = EditorGUIUtility.FindTexture("_Menu@2x"),
                    scaleMode = ScaleMode.StretchToFill,
                    style = {
                        width = 16,
                        height = 16,
                        marginTop = 2,
                        marginRight = 4,
                    }
                };
                menu.RegisterCallback<MouseUpEvent>(e => {
                    if (e.button != 0) return;
                    e.StopImmediatePropagation();
                    ContextMenu(e.mousePosition);
                });
                row.Add(menu);
                VRCFuryEditorUtils.HoverHighlight(menu);
            }

            detachChanges = () => {
                if (header != null) header.style.display = DisplayStyle.Flex;
                if (footer != null) footer.style.display = DisplayStyle.Flex;
                row.parent.Remove(row);
            };

            inspectorRoot.Insert(0, row);
        }
        */

        private GameObject dummyObject;

        public sealed override VisualElement CreateInspectorGUI() {
            try {
                return CreateInspectorGUIUnsafe();
            } catch (Exception e) {
                Debug.LogException(new Exception("Failed to render editor", e));
                return VRCFuryEditorUtils.Error("Failed to render editor (see unity console)");
            }
        }

        private VisualElement CreateInspectorGUIUnsafe() {
            if (!(target is UnityEngine.Component c)) {
                return VRCFuryEditorUtils.Error("This isn't a component?");
            }
            if (!(c is T v)) {
                return VRCFuryEditorUtils.Error("Unexpected type?");
            }

            var loadError = v.GetBrokenMessage();
            if (loadError != null) {
                return VRCFuryEditorUtils.Error(
                    $"This VRCFury component failed to load ({loadError}). It's likely that your VRCFury is out of date." +
                    " Please try Tools -> VRCFury -> Update VRCFury. If this doesn't help, let us know on the " +
                    " discord at https://vrcfury.com/discord");
            }
            
            var isInstance = PrefabUtility.IsPartOfPrefabInstance(v);
            var assetPath = AssetDatabase.GetAssetPath(v);
            if (assetPath == null) assetPath = ImmutablePrefabFixer.GetEditingPrefabPath();
            var isImmutable = assetPath != null && PreSaveVerifier.IsImmutableVrcf(assetPath);

            var container = new VisualElement();
            container.styleSheets.Add(VRCFuryEditorUtils.GetResource<StyleSheet>("VRCFuryStyle.uss"));

            container.Add(CreateOverrideLabel());

            if (isInstance) {
                // We prevent users from adding overrides on prefabs, because it does weird things (at least in unity 2019)
                // when you apply modifications to an object that lives within a SerializedReference. Some properties not overridden
                // will just be thrown out randomly, and unity will dump a bunch of errors.
                var baseFury = PrefabUtility.GetCorrespondingObjectFromOriginalSource(v);
                container.Add(CreatePrefabInstanceLabel(baseFury));
            } else if (isImmutable) {
                container.Add(CreateImmutableLabel());
            }

            VisualElement body;
            if (isImmutable || isInstance) {
                var copy = CopyComponent(v);
                copy.Upgrade();
                copy.gameObjectOverride = v.gameObject;
                var copySo = new SerializedObject(copy);
                body = CreateEditor(copySo, copy);
                body.SetEnabled(false);
                // TODO: This doesn't seem to work and it binds back to the original all the time
                //body.Bind(copySo);
            } else {
                v.Upgrade();
                serializedObject.Update();
                body = CreateEditor(serializedObject, v);
            }
            
            container.Add(body);

            /*
            el.RegisterCallback<AttachToPanelEvent>(e => {
                CreateHeaderOverlay(el);
            });
            el.RegisterCallback<DetachFromPanelEvent>(e => {
                Detach();
            });
            el.style.marginBottom = 4;
            */
            return container;
        }

        private C CopyComponent<C>(C original) where C : UnityEngine.Component {
            OnDestroy();
            var originalObject = original.gameObject;
            var id = originalObject
                .GetComponents(original.GetType())
                .Select((el, i) => (el, i))
                .First(x => x.el == original)
                .i;
            dummyObject = Instantiate(originalObject);
            dummyObject.SetActive(false);
            dummyObject.hideFlags |= HideFlags.HideAndDontSave;
            foreach (Transform child in dummyObject.transform) {
                DestroyImmediate(child.gameObject);
            }
            var copy = (C)dummyObject.GetComponents(original.GetType())
                .ElementAt(id);
            foreach (var other in dummyObject.GetComponents(typeof(UnityEngine.Component))) {
                if (other is Transform || other == copy) continue;
                DestroyImmediate(other);
            }
            return copy;
        }

        public void OnDestroy() {
            if (dummyObject) {
                DestroyImmediate(dummyObject);
            }
        }

        public virtual VisualElement CreateEditor(SerializedObject serializedObject, T target) {
            return new VisualElement();
        }
        
        private VisualElement CreateOverrideLabel() {
            var baseText = "The VRCFury features in this prefab are overridden on this instance. Please revert them!" +
                           " If you apply, it may corrupt data in the changed features.";
            var overrideLabel = VRCFuryEditorUtils.Error(baseText);
            overrideLabel.style.display = DisplayStyle.None;

            double lastCheck = 0;
            void CheckOverride() {
                if (this == null) return; // The editor was deleted
                var vrcf = (VRCFuryComponent)target;
                var now = EditorApplication.timeSinceStartup;
                if (lastCheck < now - 1) {
                    lastCheck = now;
                    var mods = VRCFPrefabFixer.GetModifications(vrcf);
                    var isModified = mods.Count > 0;
                    overrideLabel.style.display = isModified ? DisplayStyle.Flex : DisplayStyle.None;
                    if (isModified) {
                        overrideLabel.text = baseText + "\n\n" + string.Join(", ", mods.Select(m => m.propertyPath));
                    }
                }
                EditorApplication.delayCall += CheckOverride;
            }
            CheckOverride();

            return overrideLabel;
        }
        
        private VisualElement CreateImmutableLabel() {
            var text = "Changes are not allowed to this file, because they would be lost when you upgrade VRCFury.\n\n" +
                "If you want to change where menu items go, add a VRCFury Move Menu Item component to your avatar root instead.\n\n" +
                "If you want to change other things, create your own copy of the file somewhere else in your project.";
            var label = new Label {
                text = text,
                style = {
                    paddingTop = 5,
                    paddingBottom = 5,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.Normal,
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 0,
                    borderBottomRightRadius = 0,
                    marginTop = 5,
                    marginLeft = 20,
                    marginRight = 20,
                    marginBottom = 0,
                    borderTopWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 0
                }
            };
            VRCFuryEditorUtils.Padding(label, 5);
            VRCFuryEditorUtils.BorderColor(label, Color.black);
            return label;
        }
        
        private VisualElement CreatePrefabInstanceLabel(UnityEngine.Component parent) {
            var label = new Button(() => AssetDatabase.OpenAsset(parent)) {
                text = "You are viewing a prefab instance\nClick here to edit VRCFury on the base prefab",
                style = {
                    paddingTop = 5,
                    paddingBottom = 5,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.Normal,
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 0,
                    borderBottomRightRadius = 0,
                    marginTop = 5,
                    marginLeft = 20,
                    marginRight = 20,
                    marginBottom = 0,
                    borderTopWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 0
                }
            };
            VRCFuryEditorUtils.Padding(label, 5);
            VRCFuryEditorUtils.BorderColor(label, Color.black);
            return label;
        }
    }
}
