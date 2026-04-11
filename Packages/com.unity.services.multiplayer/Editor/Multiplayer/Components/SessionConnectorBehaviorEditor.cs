using Unity.Services.Multiplayer.Components;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.Multiplayer.Editor.Components
{
    [CustomEditor(typeof(SessionConnectorBehaviour))]
    class SessionConnectorBehaviorEditor : UnityEditor.Editor
    {
        SerializedProperty _SessionConnectorProperty;
        Foldout _foldout;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            _SessionConnectorProperty = serializedObject.FindProperty("SessionConnector");

            _foldout = new Foldout();
            var toggle = _foldout.Q<Toggle>();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var connector = root.Query<PropertyField>()
                .Where(p => p.bindingPath == _SessionConnectorProperty.propertyPath)
                .First();

            connector.RegisterValueChangeCallback(OnPropertyChanged);

            toggle
                .Query<VisualElement>()
                .Class("unity-foldout__input")
                .Build()
                .First()
                .Add(connector);

            if (_SessionConnectorProperty.objectReferenceValue != null)
            {
                CreateSessionConnector();
            }

            root.Add(_foldout);

            return root;

            void OnPropertyChanged(SerializedPropertyChangeEvent evt)
            {
                if (evt.changedProperty.name != _SessionConnectorProperty.name)
                {
                    return;
                }

                if (evt.target is VisualElement element)
                {
                    CreateSessionConnector();
                }
            }
        }

        private void CreateSessionConnector()
        {
            _foldout.Clear();
            var box = new Box();
            var inspector =
                new InspectorElement(new SerializedObject(_SessionConnectorProperty.objectReferenceValue));
            box.Add(inspector);
            _foldout.Add(box);
        }
    }
}
