using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AssetInventory
{
    public partial class IndexUI
    {
        private Vector2 _folderScrollPos;
        private Vector2 _statsScrollPos;
        private Vector2 _settingsScrollPos;

        private bool _showMaintenance;
        private bool _showLocations;
        private bool _showDiskSpace;
        private long _dbSize;
        private long _backupSize;
        private long _cacheSize;
        private long _persistedCacheSize;
        private long _previewSize;
        private string _captionTest = "-no caption created yet-";
        private bool _legacyCacheLocationFound;

        private sealed class AdditionalFoldersWrapper : ScriptableObject
        {
            public List<FolderSpec> folders = new List<FolderSpec>();
        }

        private ReorderableList FolderListControl
        {
            get
            {
                if (_folderListControl == null) InitFolderControl();
                return _folderListControl;
            }
        }

        private ReorderableList _folderListControl;

        private SerializedObject SerializedFoldersObject
        {
            get
            {
                // reference can become null on reload
                if (_serializedFoldersObject == null || _serializedFoldersObject.targetObjects.FirstOrDefault() == null) InitFolderControl();
                return _serializedFoldersObject;
            }
        }

        private SerializedObject _serializedFoldersObject;
        private SerializedProperty _foldersProperty;

        private bool _calculatingFolderSizes;
        private bool _cleanupInProgress;
        private DateTime _lastFolderSizeCalculation;

        private void InitFolderControl()
        {
            AdditionalFoldersWrapper obj = CreateInstance<AdditionalFoldersWrapper>();
            obj.folders = AssetInventory.Config.folders;

            _serializedFoldersObject = new SerializedObject(obj);
            _foldersProperty = _serializedFoldersObject.FindProperty("folders");
            _folderListControl = new ReorderableList(_serializedFoldersObject, _foldersProperty, true, true, true, true);
            _folderListControl.drawElementCallback = DrawFoldersListItems;
            _folderListControl.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Additional Folders to Index");
            _folderListControl.onAddCallback = OnAddCustomFolder;
            _folderListControl.onRemoveCallback = OnRemoveCustomFolder;
        }

        private void DrawFoldersListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            _legacyCacheLocationFound = false;
            if (index >= AssetInventory.Config.folders.Count) return;

            FolderSpec spec = AssetInventory.Config.folders[index];

            if (isFocused) _selectedFolderIndex = index;

            EditorGUI.BeginChangeCheck();
            spec.enabled = GUI.Toggle(new Rect(rect.x, rect.y, 20, EditorGUIUtility.singleLineHeight), spec.enabled, UIStyles.Content("", "Include folder when indexing"), UIStyles.toggleStyle);
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            GUI.Label(new Rect(rect.x + 20, rect.y, rect.width - 250, EditorGUIUtility.singleLineHeight), spec.location, UIStyles.entryStyle);
            GUI.Label(new Rect(rect.x + rect.width - 230, rect.y, 200, EditorGUIUtility.singleLineHeight), UIStyles.FolderTypes[spec.folderType] + (spec.folderType == 1 ? " (" + UIStyles.MediaTypes[spec.scanFor] + ")" : ""), UIStyles.entryStyle);
            if (GUI.Button(new Rect(rect.x + rect.width - 30, rect.y + 1, 30, 20), EditorGUIUtility.IconContent("Settings", "|Show/Hide Settings Tab")))
            {
                FolderSettingsUI folderSettingsUI = new FolderSettingsUI();
                folderSettingsUI.Init(spec);
                PopupWindow.Show(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), folderSettingsUI);
            }
            if (spec.location.Contains(AssetInventory.ASSET_STORE_FOLDER_NAME)) _legacyCacheLocationFound = true;
        }

        private void OnRemoveCustomFolder(ReorderableList list)
        {
            _legacyCacheLocationFound = false; // otherwise warning will not be cleared if last folder is removed
            if (_selectedFolderIndex < 0 || _selectedFolderIndex >= AssetInventory.Config.folders.Count) return;
            AssetInventory.Config.folders.RemoveAt(_selectedFolderIndex);
            AssetInventory.SaveConfig();
        }

        private void OnAddCustomFolder(ReorderableList list)
        {
            string folder = EditorUtility.OpenFolderPanel("Select folder to index", "", "");
            if (string.IsNullOrEmpty(folder)) return;

            // make absolute and conform to OS separators
            folder = Path.GetFullPath(folder);

            // special case: a relative key is already defined for the folder to be added, replace it immediately
            folder = AssetInventory.MakeRelative(folder);

            // don't allow adding Unity asset cache folders manually 
            if (folder.Contains(AssetInventory.ASSET_STORE_FOLDER_NAME))
            {
                EditorUtility.DisplayDialog("Attention", "You selected a custom Unity asset cache location. This should be done by setting the asset cache location above to custom.", "OK");
                return;
            }

            // ensure no trailing slash if root folder on Windows
            if (folder.Length > 1 && folder.EndsWith("/")) folder = folder.Substring(0, folder.Length - 1);

            FolderWizardUI wizardUI = FolderWizardUI.ShowWindow();
            wizardUI.Init(folder);
        }

        private void DrawSettingsTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            _folderScrollPos = GUILayout.BeginScrollView(_folderScrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandWidth(true));

            int labelWidth = 218;
            int cbWidth = 20;

            // invisible spacer to ensure settings are legible if all are collapsed
            EditorGUILayout.LabelField("", GUILayout.Width(labelWidth), GUILayout.Height(1));

            // folders
            EditorGUI.BeginChangeCheck();
            AssetInventory.Config.showIndexLocations = EditorGUILayout.Foldout(AssetInventory.Config.showIndexLocations, "Index Locations");
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            if (AssetInventory.Config.showIndexLocations)
            {
                BeginIndentBlock();
                UIBlock("settings.locationintro", () =>
                {
                    EditorGUILayout.LabelField("Unity stores downloads in two cache folders: one for Assets and one for content from the Unity package registry. These Unity cache folders will be your main indexing locations. Specify custom locations below to scan for Unity Packages downloaded from somewhere else than the Asset Store or for any arbitrary media files like your model or sound library you want to access.", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space();
                });

                EditorGUI.BeginChangeCheck();
                AssetInventory.Config.indexAssetStore = GUILayout.Toggle(AssetInventory.Config.indexAssetStore, "Asset Store Online", GUILayout.MaxWidth(150));
                EditorGUILayout.Space();
                AssetInventory.Config.indexAssetCache = GUILayout.Toggle(AssetInventory.Config.indexAssetCache, "Asset Store Cache", GUILayout.MaxWidth(150));

                if (AssetInventory.Config.indexAssetCache)
                {
                    UIBlock("settings.assetcachecache", () =>
                    {
                        EditorGUILayout.Space();
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.Space(16, false);
                        EditorGUILayout.LabelField(UIStyles.Content("Asset Cache Location", "How to determine where Unity stores downloaded asset packages"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.assetCacheLocationType = EditorGUILayout.Popup(AssetInventory.Config.assetCacheLocationType, _assetCacheLocationOptions, GUILayout.Width(300));
                        GUILayout.EndHorizontal();

                        switch (AssetInventory.Config.assetCacheLocationType)
                        {
                            case 0:
                                UIBlock("settings.actions.openassetcache", () =>
                                {
                                    GUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField("", GUILayout.Width(labelWidth + 16));
                                    if (GUILayout.Button("Open", GUILayout.ExpandWidth(false))) EditorUtility.RevealInFinder(AssetInventory.GetAssetCacheFolder());
                                    EditorGUILayout.LabelField(AssetInventory.GetAssetCacheFolder());
                                    GUILayout.EndHorizontal();
                                });

#if UNITY_2022_1_OR_NEWER
                                // show hint if Unity is not self-reporting the cache location
                                if (string.IsNullOrWhiteSpace(AssetStore.GetAssetCacheFolder()))
                                {
                                    GUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField("", GUILayout.Width(labelWidth));
                                    EditorGUILayout.HelpBox("If you defined a custom location for your cache folder different from the one above, either set the 'ASSETSTORE_CACHE_PATH' environment variable or select 'Custom' and enter the path there. Unity does not expose the location yet for other tools.", MessageType.Info);
                                    GUILayout.EndHorizontal();
                                }
#endif
                                break;

                            case 1:
                                GUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("", GUILayout.Width(labelWidth));
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.TextField(string.IsNullOrWhiteSpace(AssetInventory.Config.assetCacheLocation) ? "[Default] " + AssetInventory.GetAssetCacheFolder() : AssetInventory.Config.assetCacheLocation, GUILayout.ExpandWidth(true));
                                EditorGUI.EndDisabledGroup();
                                if (GUILayout.Button("Select...", GUILayout.ExpandWidth(false))) SelectAssetCacheFolder();
                                GUILayout.EndHorizontal();
                                break;
                        }
                    });
                }

                EditorGUILayout.Space();
                AssetInventory.Config.indexPackageCache = GUILayout.Toggle(AssetInventory.Config.indexPackageCache, "Package Cache", GUILayout.MaxWidth(150));

                if (AssetInventory.Config.indexPackageCache)
                {
                    UIBlock("settings.packagecache", () =>
                    {
                        EditorGUILayout.Space();
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.Space(16, false);
                        EditorGUILayout.LabelField(UIStyles.Content("Package Cache Location", "How to determine where Unity stores downloaded registry packages"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.packageCacheLocationType = EditorGUILayout.Popup(AssetInventory.Config.packageCacheLocationType, _assetCacheLocationOptions, GUILayout.Width(300));
                        GUILayout.EndHorizontal();

                        switch (AssetInventory.Config.packageCacheLocationType)
                        {
                            case 0:
                                UIBlock("settings.actions.openpackagecache", () =>
                                {
                                    GUILayout.BeginHorizontal();
                                    EditorGUILayout.LabelField("", GUILayout.Width(labelWidth + 16));
                                    if (GUILayout.Button("Open", GUILayout.ExpandWidth(false))) EditorUtility.RevealInFinder(AssetInventory.GetPackageCacheFolder());
                                    EditorGUILayout.LabelField(AssetInventory.GetPackageCacheFolder());
                                    GUILayout.EndHorizontal();
                                });
                                break;

                            case 1:
                                GUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("", GUILayout.Width(labelWidth));
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.TextField(string.IsNullOrWhiteSpace(AssetInventory.Config.packageCacheLocation) ? "[Default] " + AssetInventory.GetPackageCacheFolder() : AssetInventory.Config.packageCacheLocation, GUILayout.ExpandWidth(true));
                                EditorGUI.EndDisabledGroup();
                                if (GUILayout.Button("Select...", GUILayout.ExpandWidth(false))) SelectPackageCacheFolder();
                                GUILayout.EndHorizontal();
                                break;
                        }
                    });
                }

                EditorGUILayout.Space();
                AssetInventory.Config.indexAdditionalFolders = GUILayout.Toggle(AssetInventory.Config.indexAdditionalFolders, "Additional Folders", GUILayout.MaxWidth(150));
                if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

                if (AssetInventory.Config.indexAdditionalFolders)
                {
                    EditorGUILayout.Space();
                    BeginIndentBlock();
                    if (SerializedFoldersObject != null)
                    {
                        SerializedFoldersObject.Update();
                        FolderListControl.DoLayoutList();
                        SerializedFoldersObject.ApplyModifiedProperties();
                    }

                    if (_legacyCacheLocationFound)
                    {
                        EditorGUILayout.HelpBox("You have selected a custom asset cache location as an additional folder. This should be done using the Asset Cache Location UI above in this new version.", MessageType.Warning);
                    }

                    // relative locations
                    if (AssetInventory.UserRelativeLocations.Count > 0)
                    {
                        EditorGUILayout.LabelField("Relative Location Mappings", EditorStyles.boldLabel);
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Key", EditorStyles.boldLabel, GUILayout.Width(200));
                        EditorGUILayout.LabelField("Location", EditorStyles.boldLabel);
                        GUILayout.EndHorizontal();
                        foreach (RelativeLocation location in AssetInventory.UserRelativeLocations)
                        {
                            GUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(location.Key, GUILayout.Width(200));

                            string otherSystems = "Mappings on other systems:\n\n";
                            string otherLocs = string.Join("\n", location.otherLocations);
                            otherSystems += string.IsNullOrWhiteSpace(otherLocs) ? "-None-" : otherLocs;

                            if (string.IsNullOrWhiteSpace(location.Location))
                            {
                                EditorGUILayout.LabelField(UIStyles.Content("-Not yet connected-", otherSystems));

                                // TODO: add ability to force delete relative mapping in case it is not used in additional folders anymore
                            }
                            else
                            {
                                EditorGUILayout.LabelField(UIStyles.Content(location.Location, otherSystems));
                                if (string.IsNullOrWhiteSpace(otherLocs))
                                {
                                    EditorGUI.BeginDisabledGroup(true);
                                    GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash", "|Cannot delete only remaining mapping"), GUILayout.Width(30));
                                    EditorGUI.EndDisabledGroup();
                                }
                                else
                                {
                                    if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash", "|Delete mapping"), GUILayout.Width(30)))
                                    {
                                        DBAdapter.DB.Delete(location);
                                        AssetInventory.LoadRelativeLocations();
                                    }
                                }
                            }
                            if (GUILayout.Button(UIStyles.Content("...", "Select folder"), GUILayout.Width(30)))
                            {
                                SelectRelativeFolderMapping(location);
                            }
                            GUILayout.EndHorizontal();
                        }
                        EditorGUILayout.Space(20);
                    }
                    EndIndentBlock();
                }

                // Unity Asset Manager
                EditorGUILayout.Space();
                DrawAssetManager();

                EndIndentBlock();
            }

            // settings
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            AssetInventory.Config.showIndexingSettings = EditorGUILayout.Foldout(AssetInventory.Config.showIndexingSettings, "Indexing");
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            if (AssetInventory.Config.showIndexingSettings)
            {
                BeginIndentBlock();
                EditorGUI.BeginChangeCheck();

                EditorGUI.BeginChangeCheck();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Index Sub-Packages", "Will scan packages for other .unitypackage files and also index these."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.indexSubPackages = EditorGUILayout.Toggle(AssetInventory.Config.indexSubPackages, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Download Assets for Indexing", "Automatically download uncached items from the Asset Store for indexing. Will delete them again afterwards if not selected otherwise below."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.downloadAssets = EditorGUILayout.Toggle(AssetInventory.Config.downloadAssets, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                if (AssetInventory.Config.downloadAssets)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}Keep Downloaded Assets", "Will not delete automatically downloaded assets after indexing but keep them in the cache instead."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.keepAutoDownloads = EditorGUILayout.Toggle(AssetInventory.Config.keepAutoDownloads, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}Limit Package Size", "Will not automatically download packages larger than specified."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.limitAutoDownloads = EditorGUILayout.Toggle(AssetInventory.Config.limitAutoDownloads, GUILayout.Width(15));

                    if (AssetInventory.Config.limitAutoDownloads)
                    {
                        GUILayout.Label("to", GUILayout.ExpandWidth(false));
                        AssetInventory.Config.downloadLimit = EditorGUILayout.DelayedIntField(AssetInventory.Config.downloadLimit, GUILayout.Width(40));
                        GUILayout.Label("Mb", GUILayout.ExpandWidth(false));
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Extract Color Information", "Determines the hue of an image which will enable search by color. Increases indexing time. Can be turned on & off as needed."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.extractColors = EditorGUILayout.Toggle(AssetInventory.Config.extractColors, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                if (ShowAdvanced())
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Extract Full Metadata", "Will extract dimensions from images and length from audio files to make these searchable at the cost of a slower indexing process."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.gatherExtendedMetadata = EditorGUILayout.Toggle(AssetInventory.Config.gatherExtendedMetadata, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Index Asset Package Contents", "Will extract asset packages (.unitypackage) and make contents searchable. This is the foundation for the search. Deactivate only if you are solely interested in package metadata."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.indexAssetPackageContents = EditorGUILayout.Toggle(AssetInventory.Config.indexAssetPackageContents, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Exclude Hidden Packages", "Will activate the exclude flag for packages that have been hidden by the user on the Asset Store."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.excludeHidden = EditorGUILayout.Toggle(AssetInventory.Config.excludeHidden, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Exclude New Packages By Default", "Will not cause automatic indexing of newly downloaded assets. Instead this needs to be triggered manually per package."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.excludeByDefault = EditorGUILayout.Toggle(AssetInventory.Config.excludeByDefault, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Extract New Packages By Default", "Will set the Extract flag on newly downloaded assets. This will cause them to remain in the cache after indexing making the next access fast."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.extractByDefault = EditorGUILayout.Toggle(AssetInventory.Config.extractByDefault, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Pause indexing regularly", "Will pause all hard disk activity regularly to allow the disk to cool down."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.useCooldown = EditorGUILayout.Toggle(AssetInventory.Config.useCooldown, GUILayout.Width(15));

                if (AssetInventory.Config.useCooldown)
                {
                    GUILayout.Label("every", GUILayout.ExpandWidth(false));
                    AssetInventory.Config.cooldownInterval = EditorGUILayout.DelayedIntField(AssetInventory.Config.cooldownInterval, GUILayout.Width(30));
                    GUILayout.Label("minutes for", GUILayout.ExpandWidth(false));
                    AssetInventory.Config.cooldownDuration = EditorGUILayout.DelayedIntField(AssetInventory.Config.cooldownDuration, GUILayout.Width(30));
                    GUILayout.Label("seconds", GUILayout.ExpandWidth(false));
                }
                GUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

                if (EditorGUI.EndChangeCheck())
                {
                    AssetInventory.SaveConfig();
                    _requireLookupUpdate = ChangeImpact.Write;
                }
                EndIndentBlock();
            }

            // importing
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            AssetInventory.Config.showImportSettings = EditorGUILayout.Foldout(AssetInventory.Config.showImportSettings, "Import");
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            if (AssetInventory.Config.showImportSettings)
            {
                BeginIndentBlock();
                EditorGUI.BeginChangeCheck();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Adapt to Render Pipeline", "Will automatically adapt materials to the current render pipeline upon import."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                if (AssetInventory.Config.convertToPipeline)
                {
                    if (GUILayout.Button("Deactivate", GUILayout.ExpandWidth(false))) AssetInventory.SetPipelineConversion(false);
                }
                else
                {
                    if (GUILayout.Button("Activate", GUILayout.ExpandWidth(false)))
                    {
                        if (EditorUtility.DisplayDialog("Confirmation", "This will adapt materials to the current render pipeline if it is not the built-in one. This will affect newly imported as well as already existing project files. It is the same as running the Unity Render Pipeline Converter manually for all project materials. Are you sure?", "Yes", "Cancel"))
                        {
                            AssetInventory.SetPipelineConversion(true);
                        }
                    }
                }
#if USE_URP_CONVERTER
                GUILayout.Label("(URP only, supported in current project)", EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(false));
#else
                GUILayout.Label("(URP only, unsupported in current project, requires URP version 14 or higher)", EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(false));
#endif
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("You can always drag & drop assets from the search into a folder of your choice in the project view. What can be configured is the behavior when using the Import button or double-clicking an asset.", MessageType.Info);

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Structure", "Structure to materialize the imported files in"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.importStructure = EditorGUILayout.Popup(AssetInventory.Config.importStructure, _importStructureOptions, GUILayout.Width(300));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Destination", "Target folder for imported files"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.importDestination = EditorGUILayout.Popup(AssetInventory.Config.importDestination, _importDestinationOptions, GUILayout.Width(300));
                GUILayout.EndHorizontal();

                if (AssetInventory.Config.importDestination == 2)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Target Folder", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField(string.IsNullOrWhiteSpace(AssetInventory.Config.importFolder) ? "[Assets Root]" : AssetInventory.Config.importFolder, GUILayout.ExpandWidth(true));
                    EditorGUI.EndDisabledGroup();
                    if (GUILayout.Button("Select...", GUILayout.ExpandWidth(false))) SelectImportFolder();
                    if (!string.IsNullOrWhiteSpace(AssetInventory.Config.importFolder) && GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                    {
                        AssetInventory.Config.importFolder = null;
                        AssetInventory.SaveConfig();
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Calculate FBX Dependencies", "Will scan FBX files for embedded texture references. This is recommended for maximum compatibility but can reduce performance of dependency calculation and preview generation."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.scanFBXDependencies = EditorGUILayout.Toggle(AssetInventory.Config.scanFBXDependencies, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();
                EndIndentBlock();
            }

            // preview images
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            AssetInventory.Config.showPreviewSettings = EditorGUILayout.Foldout(AssetInventory.Config.showPreviewSettings, "Preview Images");
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            if (AssetInventory.Config.showPreviewSettings)
            {
                BeginIndentBlock();
                EditorGUI.BeginChangeCheck();

                if (ShowAdvanced())
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Extract Preview Images", "Keep a folder with preview images for each asset file. Will require a moderate amount of space if there are many files."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.extractPreviews = EditorGUILayout.Toggle(AssetInventory.Config.extractPreviews, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Use Fallback-Icons as Previews", "Will show generic icons in case a file preview is missing instead of an empty tile."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.showIconsForMissingPreviews = EditorGUILayout.Toggle(AssetInventory.Config.showIconsForMissingPreviews, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Upscale Preview Images", "Resize preview images to make them fill a bigger area of the tiles."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.upscalePreviews = EditorGUILayout.Toggle(AssetInventory.Config.upscalePreviews, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                if (AssetInventory.Config.upscalePreviews)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}Lossless" + (Application.platform == RuntimePlatform.WindowsEditor ? " (Windows only)" : ""), "Only create upscaled versions if base resolution is bigger. This will then mostly only affect images which can be previewed at a higher scale but leave prefab previews at the resolution they have through Unity, avoiding scaling artifacts."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.upscaleLossless = EditorGUILayout.Toggle(AssetInventory.Config.upscaleLossless, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content(AssetInventory.Config.upscaleLossless ? $"{UIStyles.INDENT}Target Size" : $"{UIStyles.INDENT}Minimum Size", "Minimum size the preview image should have. Bigger images are not changed."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.upscaleSize = EditorGUILayout.DelayedIntField(AssetInventory.Config.upscaleSize, GUILayout.Width(50));
                    EditorGUILayout.LabelField("px", EditorStyles.miniLabel);
                    GUILayout.EndHorizontal();
                }

                if (EditorGUI.EndChangeCheck())
                {
                    AssetInventory.SaveConfig();
                    _requireSearchUpdate = true;
                }
                EndIndentBlock();
            }

            // backup
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            AssetInventory.Config.showBackupSettings = EditorGUILayout.Foldout(AssetInventory.Config.showBackupSettings, "Backup");
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            if (AssetInventory.Config.showBackupSettings)
            {
                BeginIndentBlock();
                EditorGUILayout.LabelField("Automatically create backups of your asset purchases. Unity does not store old versions and assets get regularly deprecated. Backups will allow you to go back to previous versions easily. Backups will be done at the end of each update cycle.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space();
                EditorGUI.BeginChangeCheck();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Create Backups", "Store downloaded assets in a separate folder"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.createBackups = EditorGUILayout.Toggle(AssetInventory.Config.createBackups, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                if (AssetInventory.Config.createBackups)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Active for New Packages", "Will mark newly encountered packages to be backed up automatically. Otherwise you need to select packages manually which will save a lot of disk space potentially."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.backupByDefault = EditorGUILayout.Toggle(AssetInventory.Config.backupByDefault, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Override Patch Versions", "Will remove all but the latest patch version of an asset inside the same minor version (e.g. 5.4.3 instead of 5.4.2)"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.onlyLatestPatchVersion = EditorGUILayout.Toggle(AssetInventory.Config.onlyLatestPatchVersion, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Backups per Asset", "Number of versions to keep per asset"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.backupsPerAsset = EditorGUILayout.IntField(AssetInventory.Config.backupsPerAsset, GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Storage Folder", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField(string.IsNullOrWhiteSpace(AssetInventory.Config.backupFolder) ? "[Default] " + AssetInventory.GetBackupFolder(false) : AssetInventory.Config.backupFolder, GUILayout.ExpandWidth(true));
                    EditorGUI.EndDisabledGroup();
                    if (GUILayout.Button("Select...", GUILayout.ExpandWidth(false))) SelectBackupFolder();
                    if (!string.IsNullOrWhiteSpace(AssetInventory.Config.backupFolder))
                    {
                        if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                        {
                            AssetInventory.Config.backupFolder = null;
                            AssetInventory.SaveConfig();
                        }
                    }
                    if (GUILayout.Button("Open", GUILayout.ExpandWidth(false))) EditorUtility.RevealInFinder(AssetInventory.GetBackupFolder(false));
                    GUILayout.EndHorizontal();
                }
                if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();
                EndIndentBlock();
            }

            // AI
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            AssetInventory.Config.showAISettings = EditorGUILayout.Foldout(AssetInventory.Config.showAISettings, "Artificial Intelligence (Experimental)");
            if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

            if (AssetInventory.Config.showAISettings)
            {
                BeginIndentBlock();
                EditorGUI.BeginChangeCheck();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(UIStyles.Content("Create AI Captions", "Will use AI to create an automatic caption of what is visible in each individual asset file using the existing preview images. Once indexed this will yield potentially much better search results."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                AssetInventory.Config.createAICaptions = EditorGUILayout.Toggle(AssetInventory.Config.createAICaptions, GUILayout.MaxWidth(cbWidth));
                GUILayout.EndHorizontal();

                if (AssetInventory.Config.createAICaptions)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}for Prefabs", "Will create captions for prefabs."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.aiForPrefabs = EditorGUILayout.Toggle(AssetInventory.Config.aiForPrefabs, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}for Images", "Will create captions for image files."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.aiForImages = EditorGUILayout.Toggle(AssetInventory.Config.aiForImages, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}for Models", "Will create captions for model files."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.aiForModels = EditorGUILayout.Toggle(AssetInventory.Config.aiForModels, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    if (ShowAdvanced())
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Log Created Captions", "Will print finished captions to the console."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.logAICaptions = EditorGUILayout.Toggle(AssetInventory.Config.logAICaptions, GUILayout.MaxWidth(cbWidth));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Pause Between Calculations", "AI inference requires significant resources and will bring a system to full load. Running constantly can lead to system crashes. Feel free to experiment with lower pauses."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.aiPause = EditorGUILayout.DelayedIntField(AssetInventory.Config.aiPause, GUILayout.Width(30));
                        EditorGUILayout.LabelField("seconds", EditorStyles.miniLabel);
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Used Model", "The model to be used for captioning. Local models are free of charge, but require a potent computer and graphics card."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    GUILayout.BeginVertical();
                    if (GUILayout.Button("Salesforce Blip through Blip-Caption tool (local, free)", UIStyles.wrappedLinkLabel, GUILayout.ExpandWidth(true)))
                    {
                        Application.OpenURL("https://github.com/simonw/blip-caption");
                    }
                    EditorGUILayout.HelpBox("This model requires installing the Blip-Caption tool. It is free of charge and the guide can be found under the GitHub link above (Python, pipx, blip).", MessageType.Info);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Model Type", "The variant of the model that should be used."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.blipType = EditorGUILayout.Popup(AssetInventory.Config.blipType, _blipOptions, GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    if (ShowAdvanced())
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("GPU Usage", "Specify if and which GPU should be used. Otherwise only the CPU will be used. GPU support requires a patched blip version supporting GPU usage, see pull requests."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.gpuUsage = EditorGUILayout.Popup(AssetInventory.Config.gpuUsage, _gpuOptions, GUILayout.Width(100));
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Bulk Process Size", "Number of files that are captioned by the model at once."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.blipChunkSize = EditorGUILayout.IntField(AssetInventory.Config.blipChunkSize, GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Space();
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Test Image", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    GUILayout.BeginVertical(GUILayout.Width(120));
                    GUILayout.Box(Logo, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(100), GUILayout.MaxHeight(100));
                    if (GUILayout.Button("Create Caption", GUILayout.ExpandWidth(false)))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Texture2D asset-inventory-logo").FirstOrDefault());
                        string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
                        List<BlipResult> captionResult = CaptionCreator.CaptionImage(new List<string> {absolutePath});
                        _captionTest = captionResult?.FirstOrDefault()?.caption;
                        if (string.IsNullOrWhiteSpace(_captionTest))
                        {
                            _captionTest = "-Failed to create caption. Check tooling.-";
                        }
                        else
                        {
                            _captionTest = $"\"{_captionTest}\"";
                        }
                    }
                    GUILayout.EndVertical();
                    EditorGUILayout.LabelField(_captionTest);
                    GUILayout.EndHorizontal();
                }

                if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();
                EndIndentBlock();
            }

            // advanced
            if (AssetInventory.Config.showAdvancedSettings || ShowAdvanced())
            {
                EditorGUILayout.Space();
                EditorGUI.BeginChangeCheck();
                AssetInventory.Config.showAdvancedSettings = EditorGUILayout.Foldout(AssetInventory.Config.showAdvancedSettings, "Advanced");
                if (EditorGUI.EndChangeCheck()) AssetInventory.SaveConfig();

                if (AssetInventory.Config.showAdvancedSettings)
                {
                    BeginIndentBlock();
                    EditorGUI.BeginChangeCheck();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Hide Advanced behind CTRL", "Will show only the main features in the UI permanently and hide all the rest until CTRL is held down."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.hideAdvanced = EditorGUILayout.Toggle(AssetInventory.Config.hideAdvanced, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Preferred Currency", "Currency to show asset prices in"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.currency = EditorGUILayout.Popup(AssetInventory.Config.currency, _currencyOptions, GUILayout.Width(70));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Concurrent Requests to Unity API", "Max number of requests that should be send at the same time to the Unity backend."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.maxConcurrentUnityRequests = EditorGUILayout.DelayedIntField(AssetInventory.Config.maxConcurrentUnityRequests, GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Online Metadata Refresh Cycle", "Number of days after which all metadata from the Asset Store should be refreshed to gather update information, new descriptions etc."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.assetStoreRefreshCycle = EditorGUILayout.DelayedIntField(AssetInventory.Config.assetStoreRefreshCycle, GUILayout.Width(50));
                    EditorGUILayout.LabelField("days");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Package State Refresh Speed", "Number of packages to gather update information for in the background per cycle."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.observationSpeed = EditorGUILayout.DelayedIntField(AssetInventory.Config.observationSpeed, GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Reporting Batch Size", "Amount of GUIDs that will be processed in a single request. Balance between performance and UI responsiveness."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.reportingBatchSize = EditorGUILayout.DelayedIntField(AssetInventory.Config.reportingBatchSize, GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Hide Settings Automatically", "Will automatically hide the search settings again after interaction."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.autoHideSettings = EditorGUILayout.Toggle(AssetInventory.Config.autoHideSettings, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Extract Single Audio Files", "Will only extract single audio files for preview and not the full archive. Advantage is less space requirements for caching but each preview will potentially again need to go through the full archive to extract, leading to more waiting time."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.extractSingleFiles = EditorGUILayout.Toggle(AssetInventory.Config.extractSingleFiles, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Updates For Indirect Dependencies", "Will show updates for packages even if they are indirect dependencies."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.showIndirectPackageUpdates = EditorGUILayout.Toggle(AssetInventory.Config.showIndirectPackageUpdates, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Updates For Custom Packages", "Will show custom packages in the list of available updates even though they cannot be updated automatically."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.showCustomPackageUpdates = EditorGUILayout.Toggle(AssetInventory.Config.showCustomPackageUpdates, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Tile Size under Search Results", "Will show the slider for tile size directly under the search results next to the pagination."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.showTileSizeSlider = EditorGUILayout.Toggle(AssetInventory.Config.showTileSizeSlider, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Enlarge Grid Tiles", "Will make grid tiles use all the available space and only snap to a different size if the tile size allows it."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.enlargeTiles = EditorGUILayout.Toggle(AssetInventory.Config.enlargeTiles, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Auto-Refresh Metadata", "Will update the package metadata in the background when selecting a package to ensure the displayed information is up-to-date."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.autoRefreshMetadata = EditorGUILayout.Toggle(AssetInventory.Config.autoRefreshMetadata, GUILayout.MaxWidth(cbWidth));
                    GUILayout.EndHorizontal();

                    if (AssetInventory.Config.autoRefreshMetadata)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}Max Age", "Maximum age in hours after which the metadata is loaded again."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.metadataTimeout = EditorGUILayout.DelayedIntField(AssetInventory.Config.metadataTimeout, GUILayout.Width(50));
                        EditorGUILayout.LabelField("hours");
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Auto-Stop Cache Observer", "Will stop the cache observer after no new events came in for the specified time. This will save around 10% CPU background consumption. The only drawback will be that downloads started from the package manager will not be immediately be picked up by the tool anymore but only upon reselection."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.autoStopObservation = EditorGUILayout.Toggle(AssetInventory.Config.autoStopObservation, GUILayout.MaxWidth(cbWidth));
                    EditorGUILayout.LabelField(AssetInventory.IsObserverActive() ? "currently active" : "currently inactive");
                    GUILayout.EndHorizontal();

                    if (AssetInventory.Config.autoStopObservation)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content($"{UIStyles.INDENT}Timeout", "Time in seconds of no incoming file events after which the observer will be shut down."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                        AssetInventory.Config.observationTimeout = EditorGUILayout.DelayedIntField(AssetInventory.Config.observationTimeout, GUILayout.Width(50));
                        EditorGUILayout.LabelField("seconds");
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Tag Selection Window Height", "Height of the tag list window when selecting 'Add Tag...'"), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.tagListHeight = EditorGUILayout.DelayedIntField(AssetInventory.Config.tagListHeight, GUILayout.Width(50));
                    EditorGUILayout.LabelField("px");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("No Package Text Below", "Don't show text for packages in grid mode when the tile size is below the value."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.noPackageTileTextBelow = EditorGUILayout.DelayedIntField(AssetInventory.Config.noPackageTileTextBelow, GUILayout.Width(50));
                    EditorGUILayout.LabelField("tile size");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(UIStyles.Content("Exception Logging", "Will specify which errors should be logged to the console."), EditorStyles.boldLabel, GUILayout.Width(labelWidth));
                    AssetInventory.Config.logAreas = EditorGUILayout.MaskField(AssetInventory.Config.logAreas, _logOptions, GUILayout.MaxWidth(200));
                    GUILayout.EndHorizontal();

                    if (EditorGUI.EndChangeCheck())
                    {
                        AssetInventory.SaveConfig();
                        _requireAssetTreeRebuild = true;
                        if (!AssetInventory.Config.autoStopObservation) AssetInventory.StartCacheObserver();
                    }
                    EndIndentBlock();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.Space();

            GUILayout.BeginVertical();
            GUILayout.BeginVertical("Update", "window", GUILayout.Width(UIStyles.INSPECTOR_WIDTH), GUILayout.ExpandHeight(false));
            UIBlock("settings.updateintro", () =>
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Ensure to regularly update the index and to fetch the newest updates from the Asset Store.", EditorStyles.wordWrappedLabel);
            });
            EditorGUILayout.Space();

            bool easyMode = AssetInventory.Config.allowEasyMode && !ShowAdvanced();
            if (_usageCalculationInProgress)
            {
                EditorGUILayout.LabelField("Other activity in progress...", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(AssetProgress.CurrentMain);
            }
            else
            {
                if (easyMode)
                {
                    if (AssetInventory.IndexingInProgress || AssetInventory.CurrentMain != null)
                    {
                        EditorGUI.BeginDisabledGroup(AssetProgress.CancellationRequested && AssetStore.CancellationRequested);
                        if (GUILayout.Button("Stop Indexing"))
                        {
                            AssetProgress.CancellationRequested = true;
                            AssetStore.CancellationRequested = true;
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        if (GUILayout.Button(UIStyles.Content("Update Index", "Update everything in one go and perform all necessary actions."), GUILayout.Height(40))) PerformFullUpdate();
                    }
                }
                else
                {
                    // local
                    if (AssetInventory.IndexingInProgress)
                    {
                        EditorGUI.BeginDisabledGroup(AssetProgress.CancellationRequested);
                        if (GUILayout.Button("Stop Indexing")) AssetProgress.CancellationRequested = true;
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        if (GUILayout.Button(UIStyles.Content("Update Index (All-In-One)", "Update everything in one go and perform all necessary actions."))) PerformFullUpdate();
                        EditorGUILayout.Space();
                        if (GUILayout.Button(UIStyles.Content("Update Local Index", "Update all local folders and scan for cache and file changes."))) AssetInventory.RefreshIndex();
                        if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Force Update Local Index", "Will parse all package metadata again (not the contents if unchanged) and update the index."))) AssetInventory.RefreshIndex(true);
                    }
                }
            }

            // status
            if (AssetInventory.IndexingInProgress)
            {
                EditorGUILayout.Space();
                if (AssetProgress.MainCount > 0)
                {
                    EditorGUILayout.LabelField("Package Progress", EditorStyles.boldLabel);
                    UIStyles.DrawProgressBar(AssetProgress.MainProgress / (float)AssetProgress.MainCount, $"{AssetProgress.MainProgress:N0}/{AssetProgress.MainCount:N0}");
                    EditorGUILayout.LabelField("Package", EditorStyles.boldLabel);

                    string package = !string.IsNullOrEmpty(AssetProgress.CurrentMain) ? IOUtils.GetFileName(AssetProgress.CurrentMain) : "scanning...";
                    EditorGUILayout.LabelField(UIStyles.Content(package, package), EditorStyles.wordWrappedLabel);
                }

                if (AssetProgress.SubCount > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("File Progress", EditorStyles.boldLabel);
                    UIStyles.DrawProgressBar(AssetProgress.SubProgress / (float)AssetProgress.SubCount, $"{AssetProgress.SubProgress:N0}/{AssetProgress.SubCount:N0} - " + IOUtils.GetFileName(AssetProgress.CurrentSub));
                }
            }

            if (!easyMode)
            {
                // asset store
                EditorGUILayout.Space();
                EditorGUI.BeginDisabledGroup(AssetInventory.CurrentMain != null);
                if (GUILayout.Button(UIStyles.Content("Update Asset Store Data", "Refresh purchases and metadata from Unity Asset Store."))) FetchAssetPurchases(false);
                if (ShowAdvanced() && GUILayout.Button(UIStyles.Content("Force Update Asset Store Data", "Force updating all assets instead of only changed ones."))) FetchAssetPurchases(true);
                EditorGUI.EndDisabledGroup();
                if (AssetInventory.CurrentMain != null)
                {
                    if (GUILayout.Button("Cancel")) AssetStore.CancellationRequested = true;
                }
            }

            if (AssetInventory.CurrentMain != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"{AssetInventory.CurrentMain} {AssetInventory.MainProgress:N0}/{AssetInventory.MainCount:N0}", EditorStyles.centeredGreyMiniLabel);
            }
            else if (!AssetInventory.IndexingInProgress)
            {
                if (!ShowAdvanced())
                {
                    UIBlock("settings.whatwillhappen", () =>
                    {
                        if (GUILayout.Button(UIStyles.Content("What will happen?"), UIStyles.centerLinkLabel, GUILayout.ExpandWidth(true)))
                        {
                            List<string> updates = new List<string>();
                            if (AssetInventory.Config.indexAssetStore) updates.Add("Fetch purchases from Asset Store");
                            if (AssetInventory.Config.indexAssetStore) updates.Add("Fetch details for each asset from Asset Store");
                            if (AssetInventory.Config.indexAssetCache) updates.Add("Index asset cache");
                            if (AssetInventory.Config.indexPackageCache) updates.Add("Index package cache");
                            if (AssetInventory.Config.indexAdditionalFolders) updates.Add("Index additional folders");
                            if (AssetInventory.Config.downloadAssets) updates.Add("Download and index new assets");
                            if (AssetInventory.Config.indexAssetManager) updates.Add("Index asset manager organizations and projects");
                            if (AssetInventory.Config.extractColors) updates.Add("Analyze colors");
                            if (AssetInventory.Config.createAICaptions) updates.Add("Create AI captions");
                            if (AssetInventory.Config.createBackups) updates.Add("Perform backups");

                            for (int i = 0; i < updates.Count; i++)
                            {
                                updates[i] = $"{i + 1}. {updates[i]}";
                            }
                            string updateOrder = string.Join("\n", updates);

                            EditorUtility.DisplayDialog("Update Order With Current Settings", updateOrder, "OK");
                        }
                    });
                }
                if (AssetInventory.LastIndexUpdate != DateTime.MinValue)
                {
                    UIBlock("settings.lastupdate", () =>
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField($"Last updated {StringUtils.GetRelativeTimeDifference(AssetInventory.LastIndexUpdate)}", EditorStyles.centeredGreyMiniLabel);
                    });
                }
            }
            GUILayout.EndVertical();

            EditorGUILayout.Space();
            GUILayout.BeginVertical("Statistics", "window", GUILayout.Width(UIStyles.INSPECTOR_WIDTH));
            EditorGUILayout.Space();
            int labelWidth2 = 130;
            _statsScrollPos = GUILayout.BeginScrollView(_statsScrollPos, false, false);
            UIBlock("settings.statistics", () =>
            {
                DrawPackageStats(false);
                GUILabelWithText("Database Size", EditorUtility.FormatBytes(_dbSize), labelWidth2);
            });

            if (_indexedPackageCount < _packageCount - _abandonedAssetsCount - _registryPackageCount && !AssetInventory.IndexingInProgress && !AssetInventory.Config.downloadAssets)
            {
                UIBlock("settings.hints.indexremaining", () =>
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("To index the remaining assets, download them first. Tip: You can multi-select packages in the Packages view to start a bulk download.", MessageType.Info);
                });
            }

            UIBlock("settings.diskspace", () =>
            {
                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();
                _showDiskSpace = EditorGUILayout.Foldout(_showDiskSpace, "Used Disk Space");
                EditorGUI.BeginDisabledGroup(_calculatingFolderSizes);
                if (GUILayout.Button(_calculatingFolderSizes ? "Calculating..." : "Refresh", GUILayout.ExpandWidth(false)))
                {
                    _showDiskSpace = true;
                    CalcFolderSizes();
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();

                if (_showDiskSpace)
                {
                    if (_lastFolderSizeCalculation != DateTime.MinValue)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Previews", "Size of folder containing asset preview images."), EditorStyles.boldLabel, GUILayout.Width(120));
                        EditorGUILayout.LabelField(EditorUtility.FormatBytes(_previewSize), GUILayout.Width(80));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Cache", "Size of folder containing temporary cache. Can be deleted at any time."), EditorStyles.boldLabel, GUILayout.Width(120));
                        EditorGUILayout.LabelField(EditorUtility.FormatBytes(_cacheSize), GUILayout.Width(80));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Persistent Cache", "Size of extracted packages in cache that are marked 'extracted' and not automatically removed."), EditorStyles.boldLabel, GUILayout.Width(120));
                        EditorGUILayout.LabelField(EditorUtility.FormatBytes(_persistedCacheSize), GUILayout.Width(80));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(UIStyles.Content("Backups", "Size of folder containing asset preview images."), EditorStyles.boldLabel, GUILayout.Width(120));
                        EditorGUILayout.LabelField(EditorUtility.FormatBytes(_backupSize), GUILayout.Width(80));
                        GUILayout.EndHorizontal();

                        EditorGUILayout.LabelField("last updated " + _lastFolderSizeCalculation.ToShortTimeString(), EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Not calculated yet....", EditorStyles.centeredGreyMiniLabel);
                    }
                }
            });

            EditorGUILayout.Space();
            _showMaintenance = EditorGUILayout.Foldout(_showMaintenance, "Maintenance");
            if (_showMaintenance)
            {
                EditorGUI.BeginDisabledGroup(AssetInventory.CurrentMain != null || AssetInventory.IndexingInProgress);
                UIBlock("settings.actions.maintenance", () =>
                {
                    if (GUILayout.Button("Maintenance Wizard..."))
                    {
                        MaintenanceUI maintenanceUI = MaintenanceUI.ShowWindow();
                        maintenanceUI.Prepare();
                    }
                });
                UIBlock("settings.actions.recreatepreviews", () =>
                {
                    if (GUILayout.Button("Previews Wizard..."))
                    {
                        PreviewWizardUI previewsUI = PreviewWizardUI.ShowWindow();
                        previewsUI.Init(null, _assets);
                    }
                });

                UIBlock("settings.actions.clearcache", () =>
                {
                    EditorGUILayout.Space();
                    EditorGUI.BeginDisabledGroup(AssetInventory.ClearCacheInProgress);
                    if (GUILayout.Button(UIStyles.Content("Clear Cache", "Will delete the 'Extracted' folder used for speeding up asset access. It will be recreated automatically when needed."))) AssetInventory.ClearCache(() => UpdateStatistics(true));
                    EditorGUI.EndDisabledGroup();
                });
                UIBlock("settings.actions.cleardb", () =>
                {
                    if (GUILayout.Button(UIStyles.Content("Clear Database", "Will reset the database to its initial empty state. ALL data in the index will be lost.")))
                    {
                        if (DBAdapter.DeleteDB())
                        {
                            AssetUtils.ClearCache();
                            if (Directory.Exists(AssetInventory.GetPreviewFolder())) Directory.Delete(AssetInventory.GetPreviewFolder(), true);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Error", "Database seems to be in use by another program and could not be cleared.", "OK");
                        }
                        UpdateStatistics(true);
                        _assets = new List<AssetInfo>();
                        _requireAssetTreeRebuild = true;
                    }
                });
                UIBlock("settings.actions.resetconfig", () =>
                {
                    if (GUILayout.Button(UIStyles.Content("Reset Configuration", "Will reset the configuration to default values, also deleting all Additional Folder configurations."))) AssetInventory.ResetConfig();
                });
                UIBlock("settings.actions.resetuiconfig", () =>
                {
                    if (GUILayout.Button(UIStyles.Content("Reset UI Customization", "Will reset the visibility of UI elements to initial default values."))) AssetInventory.ResetUICustomization();
                    EditorGUILayout.Space();
                });

                EditorGUI.BeginDisabledGroup(_cleanupInProgress);
                UIBlock("settings.actions.optimizedb", () =>
                {
                    if (GUILayout.Button("Optimize Database")) OptimizeDatabase();
                });
                EditorGUI.EndDisabledGroup();
                if (DBAdapter.IsDBOpen())
                {
                    UIBlock("settings.actions.closedb", () =>
                    {
                        if (GUILayout.Button(UIStyles.Content("Close Database", "Will allow to safely copy the database in the file system. Database will be reopened automatically upon activity."))) DBAdapter.Close();
                    });
                }

                UIBlock("settings.actions.dblocation", () =>
                {
                    EditorGUI.BeginDisabledGroup(AssetInventory.CurrentMain != null || AssetInventory.IndexingInProgress);
                    if (GUILayout.Button("Change Database Location...")) SetDatabaseLocation();
                    EditorGUI.EndDisabledGroup();
                });

                EditorGUI.EndDisabledGroup();
            }

            UIBlock("settings.locations", () =>
            {
                EditorGUILayout.Space();
                _showLocations = EditorGUILayout.Foldout(_showLocations, "Locations");
                if (_showLocations)
                {
                    EditorGUILayout.LabelField(UIStyles.Content("Database", "To change, use the command in the Maintenance section"), EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(AssetInventory.GetStorageFolder(), EditorStyles.wordWrappedLabel);

                    EditorGUILayout.LabelField(UIStyles.Content("Access Cache", "To change, close Unity and adjust the 'cacheFolder' parameter directly in the configuration file."), EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(AssetInventory.GetMaterializeFolder(), EditorStyles.wordWrappedLabel);

                    EditorGUILayout.LabelField("Preview Cache", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(AssetInventory.GetPreviewFolder(), EditorStyles.wordWrappedLabel);

                    EditorGUILayout.LabelField(UIStyles.Content("Backup", "To change, go to the Backup settings"), EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(AssetInventory.GetBackupFolder(), EditorStyles.wordWrappedLabel);

                    EditorGUILayout.LabelField(UIStyles.Content("Configuration", "To change, either copy the json file into your project to use a project-specific configuration or use the 'ASSETINVENTORY_CONFIG_PATH' environment variable to define a new global location (see documentation)."), EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(AssetInventory.UsedConfigLocation, EditorStyles.wordWrappedLabel);
                }
            });

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void OptimizeDatabase(bool initOnly = false)
        {
            if (!initOnly)
            {
                long savings = DBAdapter.Compact();
                UpdateStatistics(true);
                EditorUtility.DisplayDialog("Success", $"Database was compacted. Size reduction: {EditorUtility.FormatBytes(savings)}", "OK");
            }

            AppProperty lastOpt = new AppProperty("LastOptimization", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            DBAdapter.DB.InsertOrReplace(lastOpt);
        }

        private void SelectRelativeFolderMapping(RelativeLocation location)
        {
            string folder = EditorUtility.OpenFolderPanel("Select folder to map to", location.Location, "");
            if (!string.IsNullOrEmpty(folder))
            {
                location.SetLocation(Path.GetFullPath(folder));
                if (location.Id > 0)
                {
                    DBAdapter.DB.Execute("UPDATE RelativeLocation SET Location = ? WHERE Id = ?", location.Location, location.Id);
                }
                else
                {
                    DBAdapter.DB.Insert(location);
                }
                AssetInventory.LoadRelativeLocations();
            }
        }

        private void SelectBackupFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Select storage folder for backups", AssetInventory.Config.backupFolder, "");
            if (!string.IsNullOrEmpty(folder))
            {
                AssetInventory.Config.backupFolder = Path.GetFullPath(folder);
                AssetInventory.SaveConfig();
            }
        }

        private void SelectAssetCacheFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Select asset cache folder of Unity (ending with 'Asset Store-5.x')", AssetInventory.Config.assetCacheLocation, "");
            if (!string.IsNullOrEmpty(folder))
            {
                if (Path.GetFileName(folder).ToLowerInvariant() != AssetInventory.ASSET_STORE_FOLDER_NAME.ToLowerInvariant())
                {
                    EditorUtility.DisplayDialog("Error", $"Not a valid Unity asset cache folder. It should point to a folder ending with '{AssetInventory.ASSET_STORE_FOLDER_NAME}'", "OK");
                    return;
                }
                AssetInventory.Config.assetCacheLocation = Path.GetFullPath(folder);
                AssetInventory.SaveConfig();

                AssetInventory.GetObserver().SetPath(AssetInventory.Config.assetCacheLocation);
            }
        }

        private void SelectPackageCacheFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Select package cache folder of Unity", AssetInventory.Config.packageCacheLocation, "");
            if (!string.IsNullOrEmpty(folder))
            {
                AssetInventory.Config.packageCacheLocation = Path.GetFullPath(folder);
                AssetInventory.SaveConfig();
            }
        }

        private void SelectImportFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Select folder for imports", AssetInventory.Config.importFolder, "");
            if (!string.IsNullOrEmpty(folder))
            {
                if (!folder.ToLowerInvariant().StartsWith(Application.dataPath.ToLowerInvariant()))
                {
                    EditorUtility.DisplayDialog("Error", "Folder must be inside current project", "OK");
                    return;
                }

                // store only part relative to /Assets
                AssetInventory.Config.importFolder = folder.Substring(Path.GetDirectoryName(Application.dataPath).Length + 1);
                AssetInventory.SaveConfig();
            }
        }

        private async void CalcFolderSizes()
        {
            if (_calculatingFolderSizes) return;
            _calculatingFolderSizes = true;
            _lastFolderSizeCalculation = DateTime.Now;

            _backupSize = await AssetInventory.GetBackupFolderSize();
            _cacheSize = await AssetInventory.GetCacheFolderSize();
            _persistedCacheSize = await AssetInventory.GetPersistedCacheSize();
            _previewSize = await AssetInventory.GetPreviewFolderSize();

            _calculatingFolderSizes = false;
        }

        private void PerformFullUpdate()
        {
            AssetInventory.RefreshIndex();

            if (AssetInventory.Config.indexAssetStore)
            {
                // start also asset download if not already done before manually
                if (string.IsNullOrEmpty(AssetInventory.CurrentMain)) FetchAssetPurchases(false);
            }
        }

        private void SetDatabaseLocation()
        {
            string targetFolder = EditorUtility.OpenFolderPanel("Select folder for database and cache", AssetInventory.GetStorageFolder(), "");
            if (string.IsNullOrEmpty(targetFolder)) return;

            // check if same folder selected
            if (IOUtils.IsSameDirectory(targetFolder, AssetInventory.GetStorageFolder())) return;

            // check for existing database
            if (File.Exists(Path.Combine(targetFolder, DBAdapter.DB_NAME)))
            {
                if (EditorUtility.DisplayDialog("Use Existing?", "The target folder contains a database. Switch to this one? Otherwise please select an empty directory.", "Switch", "Cancel"))
                {
                    AssetInventory.SwitchDatabase(targetFolder);
                    ReloadLookups();
                    PerformSearch();
                }

                return;
            }

            // target must be empty
            if (!IOUtils.IsDirectoryEmpty(targetFolder))
            {
                EditorUtility.DisplayDialog("Error", "The target folder needs to be empty or contain an existing database.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Keep Old Database", "Should a new database be created or the current one moved?", "New", "Move"))
            {
                AssetInventory.SwitchDatabase(targetFolder);
                ReloadLookups();
                PerformSearch();
                AssetStore.GatherAllMetadata();
                AssetStore.GatherProjectMetadata();
                return;
            }

            _previewInProgress = true;
            AssetInventory.MoveDatabase(targetFolder);
            _previewInProgress = false;
        }

        private IEnumerator UpdateStatisticsDelayed()
        {
            yield return null;
            UpdateStatistics(false);
        }

        private void UpdateStatistics(bool force)
        {
            if (!force && _assets != null && _tags != null && _dbSize > 0)
            {
                // check if assets were already correctly initialized since this method is also used for initial bootstrapping
                if (_assets.Any(a => a.PackageDownloader == null || (a.ParentId > 0 && a.ParentInfo == null)))
                {
                    AssetInventory.InitAssets(_assets);
                }
                return;
            }

            if (AssetInventory.DEBUG_MODE) Debug.LogWarning("Update Statistics");
            if (Application.isPlaying) return;

            _assets = AssetInventory.LoadAssets();
            _tags = AssetInventory.LoadTags();
            _packageCount = _assets.Count;
            _indexedPackageCount = _assets.Count(a => a.FileCount > 0);
            _subPackageCount = _assets.Count(a => a.ParentId > 0);
            _deprecatedAssetsCount = _assets.Count(a => a.IsDeprecated);
            _abandonedAssetsCount = _assets.Count(a => a.IsAbandoned);
            _excludedAssetsCount = _assets.Count(a => a.Exclude);
            _registryPackageCount = _assets.Count(a => a.AssetSource == Asset.Source.RegistryPackage);
            _customPackageCount = _assets.Count(a => a.AssetSource == Asset.Source.CustomPackage || a.SafeName == Asset.NONE);

            // registry packages are too unpredictable to be counted and cannot be force indexed
            _indexablePackageCount = _packageCount - _abandonedAssetsCount - _registryPackageCount - _excludedAssetsCount;
            if (_indexablePackageCount < _indexedPackageCount) _indexablePackageCount = _indexedPackageCount;

            _packageFileCount = DBAdapter.DB.Table<AssetFile>().Count();

            // only load slow statistics on Index tab when nothing else is running
            if (AssetInventory.Config.tab == 3)
            {
                _dbSize = DBAdapter.GetDBSize();
            }
        }
    }
}
