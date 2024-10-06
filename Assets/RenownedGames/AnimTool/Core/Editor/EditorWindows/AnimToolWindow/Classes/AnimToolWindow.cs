/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using RenownedGames.ExLibEditor.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RenownedGames.AnimTool
{
    public sealed class AnimToolWindow : EditorWindow
    {
        private static class Contents
        {
            public static readonly GUIContent Header = new GUIContent("AnimTool");
            public static readonly GUIContent Path = new GUIContent("Path");
            public static readonly GUIContent Option = new GUIContent("Options");
            public static readonly GUIContent ScanButton = new GUIContent("Scan");
            public static readonly GUIContent ApplyButton = new GUIContent("Apple");
            public static readonly GUIContent MainTabSection = new GUIContent("Main");
            public static readonly GUIContent RenameTabSection = new GUIContent("Rename");
            public static readonly GUIContent SettingsTabSection = new GUIContent("Settings");
        }

        private static class Styles
        {
            public static GUIStyle Title
            {
                get
                {
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.fontSize = 20;
                    style.fontStyle = FontStyle.Bold;
                    style.alignment = TextAnchor.MiddleCenter;
                    return style;
                }
            }

            public static GUIStyle Placeholder
            {
                get
                {
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.fontStyle = FontStyle.Italic;
                    style.fontSize = 11;
                    style.normal.textColor = Color.gray;
                    return style;
                }
            }
        }

        private static class Colors
        {
            public static Color LineColor
            {
                get
                {
                    if (EditorGUIUtility.isProSkin)
                        return new Color(0.1f, 0.1f, 0.1f, 1.0f);
                    else
                        return new Color(0.55f, 0.55f, 0.55f, 1.0f);
                }
            }
            public static Color ScanResultBackground
            {
                get
                {
                    if(EditorGUIUtility.isProSkin)
                        return new Color(0.185f, 0.185f, 0.185f, 1.0f);
                    else
                        return new Color(0.68f, 0.68f, 0.68f, 1.0f);
                }
            }
        }

        private struct ScanResult
        {
            private Object asset;
            private string path;
            private string currentClipName;
            private string newClipName;
            private bool exclude;

            public ScanResult(Object asset, string path, string currentClipName, string newClipName, bool exclude)
            {
                this.asset = asset;
                this.path = path;
                this.currentClipName = currentClipName;
                this.newClipName = newClipName;
                this.exclude = exclude;
            }

            #region [Getter / Setter]
            public Object GetAsset()
            {
                return asset;
            }

            public void SetAsset(Object value)
            {
                asset = value;
            }

            public string GetPath()
            {
                return path;
            }

            public void SetPath(string value)
            {
                path = value;
            }

            public string GetCurrentClipName()
            {
                return currentClipName;
            }

            public void SetCurrentClipName(string value)
            {
                currentClipName = value;
            }

            public string GetNewClipName()
            {
                return newClipName;
            }

            public void SetNewClipName(string value)
            {
                newClipName = value;
            }

            public bool Exclude()
            {
                return exclude;
            }

            public void Exclude(bool value)
            {
                exclude = value;
            }
            #endregion
        }

        [Flags]
        private enum ScanOptions
        {
            None = 0,
            SubFolders = 1 << 0,
            MixamoOnly = 1 << 1,
            WithMesh = 1 << 2,
            All = ~0
        }

        [SerializeField]
        private Avatar avatar;

        [SerializeReference]
        private RenameOperation[] operations;

        [SerializeField]
        private AnimProperty animProperty;

        private SerializedObject serializedObject;
        private SerializedProperty serializedAvatar;
        private SerializedProperty serializedOperations;
        private SerializedProperty serializedAnimProperty;
        private ReorderableList reorderableList;

        private bool isProcessing;
        private int activeToolbarID;
        private float progressValue;
        private string progressMessage;
        private string path;
        private string searchText;
        private CancellationTokenSource cancellationToken;
        private ScanOptions scanOptions;
        private GUISplitView splitView;
        private Vector2 scanResultScrollPos;
        private Vector2 operationsScrollPos;
        private List<ScanResult> scanResults;
        private List<bool> scanResultFoldouts;

        /// <summary>
        /// Called when the object becomes enabled and active.
        /// </summary>
        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            serializedAvatar = serializedObject.FindProperty("avatar");
            serializedOperations = serializedObject.FindProperty("operations");
            serializedAnimProperty = serializedObject.FindProperty("animProperty");
            InitializeReorderableList(serializedObject, serializedOperations);

            activeToolbarID = 1;
            path = "Assets/";
            scanOptions = ScanOptions.SubFolders;
            searchText = string.Empty;
            scanResults = new List<ScanResult>();
            scanResultFoldouts = new List<bool>();
            splitView = new GUISplitView(GUISplitView.Direction.Horizontal, 0.3f, 0.257f, 0.5f);
        }

        /// <summary>
        /// Initialize operation reorderable list.
        /// </summary>
        private void InitializeReorderableList(SerializedObject serializedObject, SerializedProperty list)
        {
            reorderableList = new ReorderableList(serializedObject, list, true, false, true, true);

            reorderableList.footerHeight = 0.0f;

            reorderableList.displayAdd = false;

            reorderableList.displayRemove = false;

            reorderableList.showDefaultBackground = false;

            reorderableList.headerHeight = 1.0f;

            reorderableList.drawElementCallback = (Rect position, int index, bool isActive, bool isFocused) =>
            {
                Event currentGUIEvent = Event.current;
                if (currentGUIEvent.type == EventType.MouseDown && currentGUIEvent.button == 1 && position.Contains(currentGUIEvent.mousePosition))
                {
                    GenericMenu genericMenu = new GenericMenu();
                    genericMenu.AddItem(new GUIContent("Delete"), false, () =>
                    {
                        ReorderableList.defaultBehaviours.DoRemoveButton(reorderableList);
                        serializedObject.ApplyModifiedProperties();
                    });
                    genericMenu.ShowAsContext();
                }

                SerializedProperty operation = list.GetArrayElementAtIndex(index);

                Rect labelPosition = new Rect(position.x - 2, position.y + 1, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(labelPosition, AnimToolUtility.GetOperationLabel(operation), EditorStyles.boldLabel);

                if (operation.hasVisibleChildren)
                {
                    Rect foldoutPosition = new Rect(position.xMax - (operation.isExpanded ? 8 : 10), position.y + 1, 15, EditorGUIUtility.singleLineHeight);
                    if (operation.isExpanded)
                        GUI.matrix = Matrix4x4.identity;
                    else
                        GUIUtility.RotateAroundPivot(180, foldoutPosition.center);
                    operation.isExpanded = EditorGUI.Foldout(foldoutPosition, operation.isExpanded, GUIContent.none, true);
                    GUI.matrix = Matrix4x4.identity;

                    if (operation.isExpanded)
                    {
                        position.y += EditorGUIUtility.singleLineHeight;
                        foreach (SerializedProperty element in GetVisibleChildren(operation))
                        {
                            Rect elementPosition = new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(element, true));
                            EditorGUI.PropertyField(elementPosition, element);
                            position.y += elementPosition.height + EditorGUIUtility.standardVerticalSpacing;
                        }
                    }
                }
            };

            reorderableList.drawNoneElementCallback = (rect) => { };

            reorderableList.elementHeightCallback = (int index) =>
            {
                SerializedProperty element = list.GetArrayElementAtIndex(index);
                float height = 20;
                if (element.isExpanded && element.hasVisibleChildren)
                {
                    foreach (SerializedProperty child in GetChildren(element))
                    {
                        height += EditorGUI.GetPropertyHeight(child) + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
                return height;
            };

            reorderableList.onMouseDragCallback = (list) =>
            {
                for (int i = 0; i < serializedOperations.arraySize; i++)
                {
                    serializedOperations.GetArrayElementAtIndex(i).isExpanded = false;
                }
            };

            reorderableList.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
            {
                if (serializedOperations.arraySize > 0)
                {
                    Rect backPosition = new Rect(rect.x, rect.y + 1, rect.width, rect.height - 1);
                    ReorderableList.defaultBehaviours.DrawElementBackground(backPosition, index, isActive, isFocused, true);

                    if (Event.current.type == EventType.Repaint)
                    {
                        GUIStyle style = new GUIStyle(ReorderableList.defaultBehaviours.elementBackground);
                        style.fixedHeight = rect.height;
                        style.Draw(rect, false, isActive, isActive, isFocused);
                    }

                    Rect separator = new Rect(rect.x, rect.yMin, rect.width, 1.0f);
                    EditorGUI.DrawRect(separator, Colors.LineColor);

                    if (index == serializedOperations.arraySize - 1)
                    {
                        Rect separator1 = new Rect(rect.x, rect.yMax, rect.width, 1.0f);
                        EditorGUI.DrawRect(separator1, Colors.LineColor);
                    }
                }
            };
        }

        /// <summary>
        /// Called for rendering and handling GUI events.
        /// </summary>
        private void OnGUI()
        {
            splitView.BeginSplitView();
            GUILayout.BeginVertical();
            OnLeftSideGUI();
            GUILayout.EndVertical();
            splitView.Split();
            GUILayout.BeginVertical();
            OnRightSideGUI();
            GUILayout.EndVertical();
            splitView.EndSplitView();
        }

        /// <summary>
        /// Called when close the window.
        /// </summary>
        private void OnDestroy()
        {
            if(cancellationToken != null)
            {
                cancellationToken.Cancel();
            }
        }

        /// <summary>
        /// Called for rendering and handling left side GUI.
        /// </summary>
        private void OnLeftSideGUI()
        {
            Rect toolbarPosition = GUILayoutUtility.GetRect(0, 20);

            Rect mainButtonPosition = new Rect(toolbarPosition.x, toolbarPosition.y, 45, toolbarPosition.height);
            if (GUI.Button(mainButtonPosition, Contents.MainTabSection, EditorStyles.toolbarButton))
            {
                activeToolbarID = 1;
            }

            Rect renameButtonPosition = new Rect(mainButtonPosition.xMax, mainButtonPosition.y, 60, toolbarPosition.height);
            if (GUI.Button(renameButtonPosition, Contents.RenameTabSection, EditorStyles.toolbarButton))
            {
                activeToolbarID = 2;
            }

            Rect settingsButtonPosition = new Rect(renameButtonPosition.xMax, renameButtonPosition.y, 65, toolbarPosition.height);
            if (GUI.Button(settingsButtonPosition, Contents.SettingsTabSection, EditorStyles.toolbarButton))
            {
                activeToolbarID = 3;
            }

            if (Event.current.type == EventType.Repaint)
            {
                GUIStyle toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
                switch (activeToolbarID)
                {
                    case 1:
                        toolbarButtonStyle.Draw(mainButtonPosition, Contents.MainTabSection, false, true, false, false);
                        break;
                    case 2:
                        toolbarButtonStyle.Draw(renameButtonPosition, Contents.RenameTabSection, false, true, false, false);
                        break;
                    case 3:
                        toolbarButtonStyle.Draw(settingsButtonPosition, Contents.SettingsTabSection, false, true, false, false);
                        break;
                }
            }

            DrawSingleLineLayout();

            switch (activeToolbarID)
            {
                case 1:
                    OnMainTabGUI();
                    break;
                case 2:
                    float width = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 50;
                    OnRenameTabGUI();
                    EditorGUIUtility.labelWidth = width;
                    break;
                case 3:
                    float width2 = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 120;
                    OnSettingsTabGUI();
                    EditorGUIUtility.labelWidth = width2;
                    break;
            }
        }

        /// <summary>
        /// Called for rendering and handling right side GUI.
        /// </summary>
        private void OnRightSideGUI()
        {
            Rect searchFieldPosition = GUILayoutUtility.GetRect(0, 20);
            searchFieldPosition = new Rect(searchFieldPosition.x + 3, searchFieldPosition.y + 2, searchFieldPosition.width - 5, EditorGUIUtility.singleLineHeight);
            searchText = EditorGUI.TextField(searchFieldPosition, searchText, EditorStyles.toolbarSearchField);

            if (string.IsNullOrEmpty(searchText))
            {
                Rect placeholderPosition = new Rect(searchFieldPosition.x + 12, searchFieldPosition.y - 1, searchFieldPosition.width, searchFieldPosition.height);
                GUI.Label(placeholderPosition, "Animation name...", Styles.Placeholder);
            }

            DrawSingleLineLayout();

            Rect backgroundPosition = GUILayoutUtility.GetRect(0, 0);
            backgroundPosition = new Rect(splitView.GetResizeHandlePosition().xMax, backgroundPosition.y, backgroundPosition.width, position.height);
            EditorGUI.DrawRect(backgroundPosition, Colors.ScanResultBackground);

            Event eventCurrent = Event.current;
            if (eventCurrent.type == EventType.MouseDown && eventCurrent.button == 1 && backgroundPosition.Contains(eventCurrent.mousePosition))
            {
                GenericMenu contextMenu = new GenericMenu();
                if (scanResults.Count > 0)
                {
                    contextMenu.AddItem(new GUIContent("Clear"), false, scanResults.Clear);
                }
                else
                {
                    contextMenu.AddDisabledItem(new GUIContent("Clear"));
                }
                contextMenu.ShowAsContext();
            }

            if (scanResults.Count == 0)
            {
                Rect messagePosition = GUILayoutUtility.GetRect(0, position.height - 20);
                GUI.Label(messagePosition, "The results will appear after the scanning process is executed.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                GUILayout.BeginVertical();
                scanResultScrollPos = GUILayout.BeginScrollView(scanResultScrollPos);
                for (int i = 0; i < scanResults.Count; i++)
                {
                    ScanResult scanResult = scanResults[i];
                    if (!scanResult.GetAsset().name.ToLower().Contains(searchText.ToLower()))
                    {
                        continue;
                    }

                    Color storedColor = GUI.contentColor;
                    if (scanResult.Exclude())
                    {
                        GUI.contentColor = Color.red;
                    }
                    scanResultFoldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(scanResultFoldouts[i], scanResult.GetAsset().name);
                    GUI.contentColor = storedColor;
                    if (scanResultFoldouts[i])
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        scanResult.SetAsset(EditorGUILayout.ObjectField("Animation", scanResult.GetAsset(), typeof(Object), false));
                        scanResult.SetPath(EditorGUILayout.TextField("Path", scanResult.GetPath()));
                        scanResult.SetCurrentClipName(EditorGUILayout.TextField("Original Clip Name", scanResult.GetCurrentClipName()));
                        EditorGUI.EndDisabledGroup();
                        scanResult.SetNewClipName(EditorGUILayout.TextField("New Clip Name", scanResult.GetNewClipName()));
                        scanResult.Exclude(EditorGUILayout.Toggle("Exclude", scanResult.Exclude()));
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    scanResults[i] = scanResult;
                    GUILayout.Space(1);
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            if (scanResults != null && scanResults.Count > 0)
            {
                for (int i = 0; i < scanResults.Count; i++)
                {
                    ScanResult scanResult = scanResults[i];
                    string newClipName = scanResult.GetAsset().name;
                    if (operations != null && operations.Length > 0)
                    {
                        AnimToolUtility.ExecuteRenameFunctions(scanResult.GetAsset().name, out newClipName, operations);
                    }
                    scanResult.SetNewClipName(newClipName);
                    scanResults[i] = scanResult;
                }
            }
        }

        /// <summary>
        /// Called when active main tab.
        /// </summary>
        private void OnMainTabGUI()
        {
            Rect pathPosition = GUILayoutUtility.GetRect(0, 22);

            Rect pathLabelPosition = new Rect(pathPosition.x, pathPosition.y + 2, 35, EditorGUIUtility.singleLineHeight);
            GUI.Label(pathLabelPosition, Contents.Path);

            Rect pathFieldPosition = new Rect(pathLabelPosition.xMax + 30, pathLabelPosition.y, pathPosition.width - pathLabelPosition.width - 50, pathLabelPosition.height);
            path = EditorGUI.TextField(pathFieldPosition, path);

            Rect pathButtonPosition = new Rect(pathFieldPosition.xMax + 1.0f, pathLabelPosition.y + 1.0f, 20, pathLabelPosition.height);
            if (GUI.Button(pathButtonPosition, EditorGUIUtility.IconContent("Folder Icon"), "IconButton"))
            {
                const string ROOT_PATH = "Assets/";
                path = EditorUtility.OpenFolderPanel("Choose folder with animations...", ROOT_PATH, ROOT_PATH);
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    path = path.Remove(0, path.IndexOf(ROOT_PATH));
                }
                else
                {
                    EditorUtility.DisplayDialog("AnimTool", "Select a folder from the current project.", "Ok");
                    path = ROOT_PATH;
                    GUI.FocusControl(string.Empty);
                }
            }

            Rect optionPosition = GUILayoutUtility.GetRect(0, 22);
            Rect optionLabelPosition = new Rect(optionPosition.x, optionPosition.y, 50, EditorGUIUtility.singleLineHeight);
            GUI.Label(optionLabelPosition, Contents.Option);

            Rect optionFieldPosition = new Rect(optionLabelPosition.xMax + 15, optionPosition.y, optionPosition.width - optionLabelPosition.width - 17, EditorGUIUtility.singleLineHeight);
            scanOptions = (ScanOptions)EditorGUI.EnumFlagsField(optionFieldPosition, scanOptions);

            Rect linePosition = GUILayoutUtility.GetRect(0, 3);
            linePosition = new Rect(linePosition.x - 3, linePosition.y, linePosition.width + 6, 1);
            EditorGUI.DrawRect(linePosition, Colors.LineColor);

            if (isProcessing)
            {
                Rect progressBarPosition = GUILayoutUtility.GetRect(0, 20);
                progressBarPosition.x += 2;
                progressBarPosition.width -= 4;
                EditorGUI.ProgressBar(progressBarPosition, progressValue, progressMessage);
            }
            

            Rect buttonsPosition = GUILayoutUtility.GetRect(0, 18);
            Rect scanButtonPosition = new Rect(buttonsPosition.xMin + 5, buttonsPosition.y + EditorGUIUtility.standardVerticalSpacing, (buttonsPosition.width / 2) - 7, buttonsPosition.height);
            EditorGUI.BeginDisabledGroup(isProcessing || string.IsNullOrEmpty(path));
            if (GUI.Button(scanButtonPosition, Contents.ScanButton))
            {
                ScanDirectory();
            }
            EditorGUI.EndDisabledGroup();

            Rect applyButtonPosition = new Rect(buttonsPosition.xMax - scanButtonPosition.width - 5, scanButtonPosition.y, scanButtonPosition.width, buttonsPosition.height);
            EditorGUI.BeginDisabledGroup(isProcessing || scanResults.Count == 0);
            if (GUI.Button(applyButtonPosition, Contents.ApplyButton))
            {
                ModifyAnimations();
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Called when active rename tab.
        /// </summary>
        private void OnRenameTabGUI()
        {
            serializedObject.Update();
            Rect toolbarPosition = GUILayoutUtility.GetRect(0, 16);
            Rect plusButtonPosition = new Rect(toolbarPosition.xMax - 16, toolbarPosition.y, 16, 16);
            if (GUI.Button(plusButtonPosition, EditorGUIUtility.IconContent("Toolbar Plus@2x"), "IconButton"))
            {
                DropdownOperations(plusButtonPosition);
            }

            if (serializedOperations.arraySize > 0)
            {
                operationsScrollPos = GUILayout.BeginScrollView(operationsScrollPos);
                Rect listPosition = GUILayoutUtility.GetRect(0, reorderableList.GetHeight());
                Rect paddingListPosition = new Rect(listPosition.x, listPosition.y - 3, listPosition.width, listPosition.height);
                reorderableList.DoList(paddingListPosition);
                GUILayout.EndScrollView();
            }
            else
            {
                Rect labelPosition = GUILayoutUtility.GetRect(0, position.height - 38);
                GUI.Label(labelPosition, "Add Operations...", EditorStyles.centeredGreyMiniLabel);
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Called when active settings tab.
        /// </summary>
        private void OnSettingsTabGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedAvatar);
            EditorGUILayout.PropertyField(serializedAnimProperty);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draw single line.
        /// </summary>
        private void DrawSingleLineLayout()
        {
            Rect bottomLinePosition = GUILayoutUtility.GetRect(0, 1);
            bottomLinePosition.height = 1;
            EditorGUI.DrawRect(bottomLinePosition, Colors.LineColor);
        }

        /// <summary>
        /// Generic menu to add new rename operations.
        /// </summary>
        /// <param name="position"></param>
        private void DropdownOperations(Rect position)
        {
            RenameOperation[] allOperations = AnimToolUtility.GetAllRenameFunctions();
            GenericMenu operationMenu = new GenericMenu();
            if (allOperations != null && allOperations.Length > 0)
            {
                for (int i = 0; i < allOperations.Length; i++)
                {
                    RenameOperation operation = allOperations[i];
                    OperationContentAttribute operationContentAttribute = AnimToolUtility.GetOperationContentAttribute(operation);
                    string content = operationContentAttribute?.path ?? operation.GetType().Name;
                    operationMenu.AddItem(new GUIContent(content), false, () =>
                    {
                        int elementIndex = serializedOperations.arraySize;
                        serializedOperations.arraySize++;
                        serializedOperations.GetArrayElementAtIndex(elementIndex).managedReferenceValue = operation.Clone();
                        serializedObject.ApplyModifiedProperties();
                    });
                }
            }
            operationMenu.DropDown(position);
        }

        /// <summary>
        /// Scan selected directory async.
        /// </summary>
        private async void ScanDirectory()
        {
            if (!Directory.Exists(path))
            {
                EditorUtility.DisplayDialog("AnimTool", "Incorrent path...", "Ok");
                return;
            }

            scanResults = new List<ScanResult>();
            scanResultFoldouts = new List<bool>();
            bool subFolder = (scanOptions & ScanOptions.SubFolders) != 0;
            string[] paths = Directory.GetFiles(path, "*.fbx", subFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            if (paths == null || paths.Length == 0)
            {
                EditorUtility.DisplayDialog("AnimTool", "There are no animations on this path.", "Ok");
                return;
            }

            int index = 0;
            float progress = 0;
            float count = paths.Length;

            isProcessing = true;
            cancellationToken = new CancellationTokenSource();
            while (index < count)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                string assetPath = paths[index];
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset != null)
                {
                    bool isMesh = false;
                    Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                    for (int j = 0; j < subAssets.Length; j++)
                    {
                        Object subAsset = subAssets[j];
                        if (subAsset is Mesh)
                        {
                            isMesh = true;
                            break;
                        }
                    }

                    bool mixamoOnly = (scanOptions & ScanOptions.MixamoOnly) != 0;
                    bool withMesh = (scanOptions & ScanOptions.WithMesh) != 0;
                    if (!isMesh || withMesh)
                    {
                        ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                        if (modelImporter != null)
                        {
                            ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;
                            if (clipAnimations != null && clipAnimations.Length > 0)
                            {
                                ModelImporterClipAnimation clipAnimation = clipAnimations[0];

                                const string MIXAMO_CLIP_NAME = "mixamo.com";
                                if (mixamoOnly && clipAnimation.name != MIXAMO_CLIP_NAME)
                                {
                                    continue;
                                }

                                string currentClipName = clipAnimation.name;
                                AnimToolUtility.ExecuteRenameFunctions(asset.name, out string newClipName, operations);
                                scanResults.Add(new ScanResult(asset, assetPath, currentClipName, newClipName, false));
                                scanResultFoldouts.Add(false);

                                float completed = index + 1;
                                progress = Mathf.Lerp(completed, count, Mathf.InverseLerp(completed, count, progress));
                                progressMessage = assetPath;
                                progressValue = progress / count;

                                await Task.Delay(10);
                                Repaint();
                            }
                        }
                    }
                }
                await Task.Yield();
                index++;
            }
            cancellationToken = null;
            isProcessing = false;
            Repaint();
        }

        /// <summary>
        /// Modify scanned animations async.
        /// </summary>
        private async void ModifyAnimations()
        {
            List<ScanResult> scanResultsToOverride = scanResults.Where(r => !r.Exclude()).ToList();

            if (scanResultsToOverride == null || scanResultsToOverride.Count == 0)
            {
                return;
            }

            int index = 0;
            float progress = 0;
            float count = scanResultsToOverride.Count;

            isProcessing = true;
            cancellationToken = new CancellationTokenSource();
            while (index < count)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ScanResult scanResult = scanResultsToOverride[index];
                float completed = index + 1;
                progress = Mathf.Lerp(completed, count, Mathf.InverseLerp(completed, count, progress));
                progressMessage = $"Applying: {Path.GetFileNameWithoutExtension(scanResult.GetPath())}";
                progressValue = progress / count;
                AnimToolUtility.ModifyImportedAnimation(scanResult.GetPath(), avatar, scanResult.GetNewClipName(), animProperty);
                index++;

                await Task.Yield();
            }
            cancellationToken = null;
            isProcessing = false;
            Repaint();

            EditorUtility.DisplayDialog("AnimTool: Success!", "All scanned animations has been modified.", "Ok");
        }

        #region [Static Methods]
        [MenuItem("Renowned Games/AnimTool/AnimTool Window", false, 80)]
        public static void Open()
        {
            AnimToolWindow window = GetWindow<AnimToolWindow>(false);
            window.titleContent = new GUIContent("AnimTool");

            Vector2 size = new Vector2(800, 450);
            window.minSize = size;
            window.maxSize = size;
            window.MoveToCenter();
            window.Show();
        }

        /// <summary>
        /// Gets all children of SerializedProperty at 1 level depth.
        /// </summary>
        /// <param name="serializedProperty">Parent SerializedProperty.</param>
        /// <returns>Collection of SerializedProperty children.</returns>
        public static IEnumerable<SerializedProperty> GetChildren(SerializedProperty serializedProperty)
        {
            SerializedProperty currentProperty = serializedProperty.Copy();
            SerializedProperty nextSiblingProperty = serializedProperty.Copy();
            {
                nextSiblingProperty.Next(false);
            }

            if (currentProperty.Next(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;

                    yield return currentProperty;
                }
                while (currentProperty.Next(false));
            }
        }

        /// <summary>
        /// Gets visible children of SerializedProperty at 1 level depth.
        /// </summary>
        /// <param name="serializedProperty">Parent SerializedProperty.</param>
        /// <returns>Collection of SerializedProperty children.</returns>
        public static IEnumerable<SerializedProperty> GetVisibleChildren(SerializedProperty serializedProperty)
        {
            SerializedProperty currentProperty = serializedProperty.Copy();
            SerializedProperty nextSiblingProperty = serializedProperty.Copy();
            {
                nextSiblingProperty.NextVisible(false);
            }

            if (currentProperty.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;

                    yield return currentProperty;
                }
                while (currentProperty.NextVisible(false));
            }
        }
        #endregion
    }
}