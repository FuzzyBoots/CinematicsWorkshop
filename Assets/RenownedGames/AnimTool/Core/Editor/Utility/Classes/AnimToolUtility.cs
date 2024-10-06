/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RenownedGames.AnimTool
{
    public static class AnimToolUtility
    {
        public static void ModifyImportedAnimation(string path, Avatar avatar, string clipName, AnimProperty properties)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (modelImporter != null)
            {
                SetImportedAnimationAvatar(ref modelImporter, avatar);
                ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;
                if (clipAnimations != null && clipAnimations.Length > 0)
                {
                    ModelImporterClipAnimation clipAnimation = clipAnimations[0];
                    clipAnimation.name = clipName;
                    SetImportedAnimationClipProperties(ref clipAnimation, properties);
                    clipAnimations[0] = clipAnimation;
                    modelImporter.clipAnimations = clipAnimations;
                }
                modelImporter.SaveAndReimport();
            }
        }

        public static void ModifyImportedAnimationAvatar(string path, Avatar avatar)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (modelImporter != null)
            {
                SetImportedAnimationAvatar(ref modelImporter, avatar);
                modelImporter.SaveAndReimport();
            }
        }

        public static void ModifyImportedAnimationClipProperties(string path, AnimProperty properties)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (modelImporter != null)
            {
                ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;
                if (clipAnimations != null && clipAnimations.Length > 0)
                {
                    ModelImporterClipAnimation clipAnimation = clipAnimations[0];
                    SetImportedAnimationClipProperties(ref clipAnimation, properties);
                    clipAnimations[0] = clipAnimation;
                    modelImporter.clipAnimations = clipAnimations;
                    modelImporter.SaveAndReimport();
                }
            }
        }

        public static void RenameImportedAnimationClip(string path, string name)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
            if (modelImporter != null)
            {
                ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;
                if (clipAnimations != null && clipAnimations.Length > 0)
                {
                    ModelImporterClipAnimation clipAnimation = clipAnimations[0];
                    RenameImportedAnimationClip(ref clipAnimation, name);
                    clipAnimations[0] = clipAnimation;
                    modelImporter.clipAnimations = clipAnimations;
                    modelImporter.SaveAndReimport();
                }
            }
        }

        public static void ExecuteRenameFunctions(string input, out string result, params RenameOperation[] functions)
        {
            result = input;
            if (functions != null && functions.Length > 0)
            {
                for (int i = 0; i < functions.Length; i++)
                {
                    RenameOperation function = functions[i];
                    if (function != null)
                    {
                        function.Execute(ref result);
                    }
                }
            }
        }

        public static bool TryGetRelativePath(string absolutePath, out string relativePath)
        {
            if (!string.IsNullOrEmpty(absolutePath) && absolutePath.Contains("Assets"))
            {
                relativePath = absolutePath.Remove(0, absolutePath.IndexOf("Assets"));
                return true;
            }
            relativePath = null;
            return false;
        }

        public static void SetImportedAnimationAvatar(ref ModelImporter modelImporter, Avatar avatar)
        {
            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            modelImporter.sourceAvatar = avatar;
        }

        public static void RenameImportedAnimationClip(ref ModelImporterClipAnimation clipAnimation, string name)
        {
            clipAnimation.name = name;
            clipAnimation.takeName = name;
        }

        public static void SetImportedAnimationClipProperties(ref ModelImporterClipAnimation clipAnimation, AnimProperty properties)
        {
            clipAnimation.loopTime = properties.LoopTime();
            clipAnimation.loopPose = properties.LoopPose();
            clipAnimation.cycleOffset = properties.GetCycleOffset();

            clipAnimation.lockRootRotation = properties.LockRotation();
            switch (properties.GetRotation())
            {
                case AnimProperty.Rotation.Original:
                    clipAnimation.keepOriginalOrientation = true;
                    break;
                case AnimProperty.Rotation.BodyOrintation:
                    clipAnimation.keepOriginalOrientation = false;
                    break;
            }
            clipAnimation.rotationOffset = properties.GetRotationOffset();

            clipAnimation.lockRootHeightY = properties.LockPositionY();
            switch (properties.GetPositionY())
            {
                case AnimProperty.PositionY.Original:
                    clipAnimation.keepOriginalPositionY = true;
                    break;
                case AnimProperty.PositionY.CenterOfMass:
                    clipAnimation.keepOriginalPositionY = false;
                    clipAnimation.heightFromFeet = false;
                    break;
                case AnimProperty.PositionY.Feet:
                    clipAnimation.heightFromFeet = true;
                    break;
            }
            clipAnimation.heightOffset = properties.GetPositionYOffset();

            clipAnimation.lockRootPositionXZ = properties.LockPositionXZ();
            switch (properties.GetPositionXZ())
            {
                case AnimProperty.PositionXZ.Original:
                    clipAnimation.keepOriginalPositionXZ = true;
                    break;
                case AnimProperty.PositionXZ.CenterOfMass:
                    clipAnimation.keepOriginalPositionXZ = false;
                    break;
            }
        }

        public static Type[] FindAllRenameFunctionTypes()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] types = assembly.GetTypes();
            return types
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(RenameOperation)))
                .ToArray();
        }

        public static RenameOperation[] GetAllRenameFunctions()
        {
            Type[] types = FindAllRenameFunctionTypes();
            return types
                .Select(rf => (RenameOperation)Activator.CreateInstance(rf))
                .ToArray();
        }

        public static string GetOperationLabel(SerializedProperty property)
        {
            string[] baseTypeAndAssemblyName = property.managedReferenceFullTypename.Split(' ');
            Type type = Type.GetType(baseTypeAndAssemblyName[1]);
            OperationContentAttribute operationContentAttribute = Attribute.GetCustomAttribute(type, typeof(OperationContentAttribute)) as OperationContentAttribute;
            return operationContentAttribute?.label ?? type.Name;
        }

        public static OperationContentAttribute GetOperationContentAttribute(RenameOperation operation)
        {
            Type type = operation.GetType();
            OperationContentAttribute operationContentAttribute = Attribute.GetCustomAttribute(type, typeof(OperationContentAttribute)) as OperationContentAttribute;
            return operationContentAttribute;
        }
    }
}