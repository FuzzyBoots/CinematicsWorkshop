/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using UnityEditor;
using UnityEngine;

namespace RenownedGames.AnimTool
{
    [CustomPropertyDrawer(typeof(AnimProperty))]
    sealed class AnimPropertyDrawer : PropertyDrawer
    {
        /// <summary>
        /// Override this method to make your own IMGUI based GUI for the property.
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property GUI.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">The label of this property.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty loopTime = property.FindPropertyRelative("loopTime");
            Rect loopTimePosition = new Rect(position.x, position.yMin + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(loopTimePosition, loopTime, new GUIContent("Loop Time"));

            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!loopTime.boolValue);
            SerializedProperty loopPose = property.FindPropertyRelative("loopPose");
            Rect loopPosePosition = new Rect(position.x, loopTimePosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(loopPosePosition, loopPose, new GUIContent("Loop Pose"));

            SerializedProperty cycleOffset = property.FindPropertyRelative("cycleOffset");
            Rect cycleOffsetPosition = new Rect(position.x, loopPosePosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(cycleOffsetPosition, cycleOffset, new GUIContent("Cycle Offset"));
            EditorGUI.EndDisabledGroup();


            Rect rtrPosition = new Rect(position.x, cycleOffsetPosition.yMax + 6, position.width, lineHeight);
            GUI.Label(rtrPosition, "Root Transform Rotation");

            SerializedProperty bakeRotation = property.FindPropertyRelative("lockRotation");
            Rect bakeRotationPosition = new Rect(position.x, rtrPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(bakeRotationPosition, bakeRotation, new GUIContent("Bake Into Pose"));

            SerializedProperty rotation = property.FindPropertyRelative("rotation");
            Rect rotationPosition = new Rect(position.x, bakeRotationPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(rotationPosition, rotation, new GUIContent(bakeRotation.boolValue ? "Based Upon (at Start)" : "Based Upon"));

            SerializedProperty rotationOffset = property.FindPropertyRelative("rotationOffset");
            Rect rotationOffsetPosition = new Rect(position.x, rotationPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(rotationOffsetPosition, rotationOffset, new GUIContent("Offset"));


            Rect rtryPosition = new Rect(position.x, rotationOffsetPosition.yMax + 6, position.width, lineHeight);
            GUI.Label(rtryPosition, "Root Transform Position (Y)");

            SerializedProperty bakePositionY = property.FindPropertyRelative("lockPositionY");
            Rect bakePositionYPosition = new Rect(position.x, rtryPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(bakePositionYPosition, bakePositionY, new GUIContent("Bake Into Pose"));

            SerializedProperty positionY = property.FindPropertyRelative("positionY");
            Rect positionYPosition = new Rect(position.x, bakePositionYPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(positionYPosition, positionY, new GUIContent(bakeRotation.boolValue ? "Based Upon (at Start)" : "Based Upon"));

            SerializedProperty positionYOffset = property.FindPropertyRelative("positionYOffset");
            Rect positionYOffsetPosition = new Rect(position.x, positionYPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(positionYOffsetPosition, positionYOffset, new GUIContent("Offset"));


            Rect rtrxzPosition = new Rect(position.x, positionYOffsetPosition.yMax + 6, position.width, lineHeight);
            GUI.Label(rtrxzPosition, "Root Transform Position (XZ)");

            SerializedProperty bakePositionXZ = property.FindPropertyRelative("lockPositionXZ");
            Rect bakePositionXZPosition = new Rect(position.x, rtrxzPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(bakePositionXZPosition, bakePositionXZ, new GUIContent("Bake Into Pose"));

            SerializedProperty positionXZ = property.FindPropertyRelative("positionXZ");
            Rect positionXZPosition = new Rect(position.x, bakePositionXZPosition.yMax + verticalSpacing, position.width, lineHeight);
            EditorGUI.PropertyField(positionXZPosition, positionXZ, new GUIContent(bakeRotation.boolValue ? "Based Upon (at Start)" : "Based Upon"));
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Override this method to specify how tall the GUI for this field is in pixels.
        /// </summary>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">The label of this property.</param>
        /// <returns>The height in pixels.</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 292;
        }
    }
}