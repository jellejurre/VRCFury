using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model.StateAction;

namespace VF.Inspector {

public class VRCFuryStateEditor {
    public static VisualElement render(
        SerializedProperty prop,
        string myLabel = null,
        int labelWidth = 100,
        string tooltip = null
    ) {
        var container = new VisualElement();
        container.AddToClassList("vfState");

        var list = prop.FindPropertyRelative("actions");

        void OnPlus() {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Object Toggle"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new ObjectToggleAction()); });
            menu.AddItem(new GUIContent("BlendShape"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new BlendShapeAction()); });
            menu.AddItem(new GUIContent("Animation Clip"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new AnimationClipAction()); });
            menu.AddItem(new GUIContent("Flipbook Frame"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new FlipbookAction()); });
            menu.AddItem(new GUIContent("Shader Inventory"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new ShaderInventoryAction()); });
            menu.AddItem(new GUIContent("Scale"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new ScaleAction()); });
            menu.AddItem(new GUIContent("Material"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new MaterialAction()); });
            menu.ShowAsContext();
        }

        var actions = prop.FindPropertyRelative("actions");

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var body = new VisualElement();

            VisualElement singleLineEditor = null;
            if (list.arraySize == 1) {
                singleLineEditor = VRCFuryEditorUtils.Prop(list.GetArrayElementAtIndex(0));
            }

            var showPlus = singleLineEditor != null || list.arraySize == 0;
            var showSingleLineEditor = singleLineEditor != null;
            var showList = singleLineEditor == null && list.arraySize > 0;

            if (showSingleLineEditor || showPlus) {
                var segments = new VisualElement();
                body.Add(segments);
                segments.style.flexDirection = FlexDirection.Row;
                segments.style.alignItems = Align.FlexStart;

                if (showSingleLineEditor) {
                    singleLineEditor.style.flexGrow = 1;
                    segments.Add(singleLineEditor);
                    var x = VRCFuryEditorUtils.Button("x", () => {
                        list.DeleteArrayElementAtIndex(0);
                        list.serializedObject.ApplyModifiedProperties();
                    });
                    x.style.flexGrow = 0;
                    x.style.flexBasis = 20;
                    segments.Add(x);
                }
                if (showPlus) {
                    var plus = VRCFuryEditorUtils.Button(singleLineEditor != null ? "+" : "Add Action +", OnPlus);
                    plus.style.flexGrow = showSingleLineEditor ? 0 : 1;
                    plus.style.flexBasis = 20;
                    segments.Add(plus);
                }
            }
            if (showList) {
                body.Add(VRCFuryEditorUtils.List(list, onPlus: OnPlus));
            }

            return VRCFuryEditorUtils.AssembleProp(myLabel, tooltip, body, false, showList, labelWidth);
        }, list, actions));

        return container;
    }
}

}
