using System.Collections.Generic;
#if UNITY_2021_2_OR_NEWER && UNITY_EDITOR_WIN
using System.Drawing.Imaging;
#endif
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AssetInventory
{
    public sealed class PreviewImporter : AssetImporter
    {
        private const int MAX_REQUESTS = 50;
        private const int OPEN_REQUESTS = 5;

        public async Task<bool> RecreatePreview(AssetInfo info)
        {
            return await RecreatePreviews(new List<AssetInfo> {info}) > 0;
        }

        public async Task<int> RecreateScheduledPreviews(List<AssetInfo> assets, List<AssetInfo> allAssets)
        {
            string assetFilter = GetAssetFilter(assets);
            string query = $"select *, AssetFile.Id as Id from AssetFile inner join Asset on Asset.Id = AssetFile.AssetId where Asset.Exclude = false and AssetFile.PreviewState=? {assetFilter} order by Asset.Id";
            List<AssetInfo> files = DBAdapter.DB.Query<AssetInfo>(query, AssetFile.PreviewOptions.Redo).ToList();
            AssetInventory.ResolveParents(files, allAssets);

            return await RecreatePreviews(files);
        }

        public static string GetAssetFilter(List<AssetInfo> assets)
        {
            string assetFilter = "";
            if (assets != null && assets.Count > 0)
            {
                assetFilter = "and Asset.Id in (";
                foreach (AssetInfo asset in assets)
                {
                    assetFilter += asset.AssetId + ",";
                }

                assetFilter = assetFilter.Substring(0, assetFilter.Length - 1) + ")";
            }
            return assetFilter;
        }

        public async Task<int> RecreatePreviews(List<AssetInfo> files)
        {
            int created = 0;

            ResetState(false);
            int progressId = MetaProgress.Start("Recreating previews");

            PreviewGenerator.Init(files.Count);
            string previewPath = AssetInventory.GetPreviewFolder();
            
            Asset curAsset = null;
            bool wasCurCached = false;
            string curTempPath = null;
            foreach (AssetInfo info in files.OrderBy(info => info.AssetId))
            {
                SubProgress++;
                SubCount = files.Count;
                CurrentSub = $"Creating preview for {info.FileName}";
                MetaProgress.Report(progressId, SubProgress, SubCount, string.Empty);
                if (CancellationRequested) break;
                await Cooldown.Do();
                if (SubProgress % 5000 == 0) await Task.Yield(); // let editor breath in case there are many non-previewable files 

                if (!info.Downloaded)
                {
                    Debug.Log($"Could not recreate preview for '{info}' since the package is not downloaded.");
                    continue;
                }

                // check if previewable at all
                if (!PreviewGenerator.IsPreviewable(info.FileName, true))
                {
                    if (info.PreviewState != AssetFile.PreviewOptions.Provided)
                    {
                        DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", AssetFile.PreviewOptions.NotApplicable, info.Id);
                    }
                    continue;
                }

                // check if handling next package already
                if (curAsset != null && info.AssetId != curAsset.Id)
                {
                    if (!wasCurCached) RemoveWorkFolder(curAsset, curTempPath);
                    curAsset = null;
                }
                
                // persist extraction state
                if (curAsset == null)
                {
                    curAsset = info.ToAsset();
                    curTempPath = AssetInventory.GetMaterializedAssetPath(curAsset);
                    wasCurCached = Directory.Exists(curTempPath);
                }
                string sourcePath = await AssetInventory.EnsureMaterializedAsset(info);
                if (sourcePath == null)
                {
                    if (info.PreviewState != AssetFile.PreviewOptions.Provided)
                    {
                        DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", AssetFile.PreviewOptions.Error, info.Id);
                    }
                    continue;
                }

                if (SubProgress % 10 == 0) await Task.Yield(); // let editor breath
                string previewFile = info.GetPreviewFile(previewPath);
                
                // from Unity 2021.2+ we can take a shortcut for images since the drawing library is supported in C#
                #if UNITY_2021_2_OR_NEWER && UNITY_EDITOR_WIN
                if (ImageUtils.SYSTEM_IMAGE_TYPES.Contains(info.Type))
                {
                    // take shortcut for images and skip Unity importer
                    if (ImageUtils.ResizeImage(sourcePath, previewFile, AssetInventory.Config.upscaleSize, !AssetInventory.Config.upscaleLossless, ImageFormat.Png))
                    {
                        StorePreviewResult(new PreviewRequest {DestinationFile = previewFile, Id = info.Id, Icon = Texture2D.grayTexture, SourceFile = sourcePath});
                        created++;
                    }
                    else
                    {
                        // try to use original preview
                        string originalPreviewFile = DerivePreviewFile(sourcePath);
                        if (File.Exists(originalPreviewFile))
                        {
                            File.Copy(originalPreviewFile, previewFile, true);
                            StorePreviewResult(new PreviewRequest {DestinationFile = previewFile, Id = info.Id, Icon = Texture2D.grayTexture, SourceFile = originalPreviewFile});
                            info.PreviewState = AssetFile.PreviewOptions.Provided;
                            DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", AssetFile.PreviewOptions.Provided, info.Id);
                            created++;
                        }
                        else if (info.PreviewState != AssetFile.PreviewOptions.Provided)
                        {
                            info.PreviewState = AssetFile.PreviewOptions.Error;
                            DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", AssetFile.PreviewOptions.Error, info.Id);
                        }
                    }
                }
                else
                {
                #endif
                    // import through Unity
                    if (AssetInventory.NeedsDependencyScan(info.Type))
                    {
                        if (info.DependencyState == AssetInfo.DependencyStateOptions.Unknown) await AssetInventory.CalculateDependencies(info);
                        if (info.Dependencies.Count > 0) sourcePath = await AssetInventory.CopyTo(info, PreviewGenerator.GetPreviewWorkFolder(), true);
                        if (sourcePath == null) // can happen when file system issues occur
                        {
                            if (info.PreviewState != AssetFile.PreviewOptions.Provided)
                            {
                                info.PreviewState = AssetFile.PreviewOptions.Error;
                                DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", AssetFile.PreviewOptions.Error, info.Id);
                            }
                            continue;
                        }
                    }

                    PreviewGenerator.RegisterPreviewRequest(info.Id, sourcePath, previewFile, req =>
                    {
                        StorePreviewResult(req);
                        if (req.Icon != null) created++;
                    }, info.Dependencies?.Count > 0);

                    PreviewGenerator.EnsureProgress();
                    if (PreviewGenerator.ActiveRequestCount() > MAX_REQUESTS) await PreviewGenerator.ExportPreviews(OPEN_REQUESTS);
                #if UNITY_2021_2_OR_NEWER && UNITY_EDITOR_WIN
                }
                #endif
            }
            await PreviewGenerator.ExportPreviews();
            PreviewGenerator.Clear();

            if (!wasCurCached) RemoveWorkFolder(curAsset, curTempPath);
            
            MetaProgress.Remove(progressId);
            ResetState(true);

            return created;
        }

        private static string DerivePreviewFile(string sourcePath)
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(sourcePath)), "preview.png");
        }

        public async Task<int> RestorePreviews(List<AssetInfo> assets)
        {
            int restored = 0;

            string previewPath = AssetInventory.GetPreviewFolder();
            string assetFilter = GetAssetFilter(assets);
            string query = $"select *, AssetFile.Id as Id from AssetFile inner join Asset on Asset.Id = AssetFile.AssetId where Asset.Exclude = false and (Asset.AssetSource = ? or Asset.AssetSource = ?) and AssetFile.PreviewState != ? {assetFilter} order by Asset.Id";
            List<AssetInfo> files = DBAdapter.DB.Query<AssetInfo>(query, Asset.Source.AssetStorePackage, Asset.Source.CustomPackage, AssetFile.PreviewOptions.Provided).ToList();

            ResetState(false);
            int progressId = MetaProgress.Start("Restoring previews");
            SubCount = files.Count;

            foreach (AssetInfo info in files)
            {
                SubProgress++;
                CurrentSub = $"Restoring preview for {info.FileName}";
                MetaProgress.Report(progressId, SubProgress, SubCount, string.Empty);
                if (CancellationRequested) break;
                await Cooldown.Do();
                if (SubProgress % 50 == 0) await Task.Yield(); // let editor breath 

                if (!info.Downloaded) continue;

                string previewFile = info.GetPreviewFile(previewPath);
                string sourcePath = await AssetInventory.EnsureMaterializedAsset(info);
                if (sourcePath == null)
                {
                    if (info.PreviewState != AssetFile.PreviewOptions.NotApplicable)
                    {
                        info.PreviewState = AssetFile.PreviewOptions.None;
                        DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", info.PreviewState, info.Id);
                    }
                    continue;
                }

                string originalPreviewFile = DerivePreviewFile(sourcePath);
                if (!File.Exists(originalPreviewFile))
                {
                    if (info.PreviewState != AssetFile.PreviewOptions.NotApplicable)
                    {
                        info.PreviewState = AssetFile.PreviewOptions.None;
                        DBAdapter.DB.Execute("update AssetFile set PreviewState=? where Id=?", info.PreviewState, info.Id);
                    }
                    continue;
                }

                File.Copy(originalPreviewFile, previewFile, true);
                info.PreviewState = AssetFile.PreviewOptions.Provided;
                info.Hue = -1f;
                DBAdapter.DB.Execute("update AssetFile set PreviewState=?, Hue=? where Id=?", info.PreviewState, info.Hue, info.Id);

                restored++;
            }

            MetaProgress.Remove(progressId);
            ResetState(true);

            return restored;
        }

        public static void StorePreviewResult(PreviewRequest req)
        {
            AssetFile af = DBAdapter.DB.Find<AssetFile>(req.Id);
            if (af == null) return;

            if (!File.Exists(req.DestinationFile))
            {
                if (af.PreviewState != AssetFile.PreviewOptions.Provided)
                {
                    af.PreviewState = AssetFile.PreviewOptions.Error;
                    DBAdapter.DB.Update(af);
                }
                return;
            }

            if (req.Obj != null)
            {
                if (req.Obj is Texture2D tex)
                {
                    af.Width = tex.width;
                    af.Height = tex.height;
                }
                if (req.Obj is AudioClip clip)
                {
                    af.Length = clip.length;
                }
            }

            // do not remove originally supplied previews even in case of error
            af.PreviewState = req.Icon != null ? AssetFile.PreviewOptions.Custom : (af.PreviewState != AssetFile.PreviewOptions.Provided ? AssetFile.PreviewOptions.Error : AssetFile.PreviewOptions.Provided);
            af.Hue = -1f;

            DBAdapter.DB.Update(af);
        }
    }
}