using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace AssetInventory
{
    public sealed class CaptionCreator : AssetImporter
    {
        public async Task Index()
        {
            ResetState(false);
            int progressId = MetaProgress.Start("Creating AI captions");

            string previewFolder = AssetInventory.GetPreviewFolder();

            List<string> types = new List<string>();
            if (AssetInventory.Config.aiForPrefabs) types.AddRange(AssetInventory.TypeGroups["Prefabs"]);
            if (AssetInventory.Config.aiForImages) types.AddRange(AssetInventory.TypeGroups["Images"]);
            if (AssetInventory.Config.aiForModels) types.AddRange(AssetInventory.TypeGroups["Models"]);

            string typeStr = string.Join("\",\"", types);
            string query = "select *, AssetFile.Id as Id from AssetFile inner join Asset on Asset.Id = AssetFile.AssetId where Asset.Exclude = false and Asset.UseAI = true and AssetFile.Type in (\"" + typeStr + "\") and AssetFile.AICaption is null and (AssetFile.PreviewState = ? or AssetFile.PreviewState = ?) order by Asset.Id desc";
            List<AssetFile> files = DBAdapter.DB.Query<AssetFile>(query, AssetFile.PreviewOptions.Custom, AssetFile.PreviewOptions.Provided).ToList();

            int chunkSize = AssetInventory.Config.blipChunkSize;
            bool toolChainWorking = true;

            for (int i = 0; i < files.Count; i += chunkSize)
            {
                if (CancellationRequested) break;
                await Cooldown.Do();
                await Task.Delay(AssetInventory.Config.aiPause * 1000); // crashes system otherwise after a while

                List<AssetFile> fileChunk = files.Skip(i).Take(chunkSize).ToList();
                List<string> previewFiles = new List<string>();

                foreach (AssetFile file in fileChunk)
                {
                    MetaProgress.Report(progressId, i + 1, files.Count, file.FileName);
                    SubCount = files.Count;
                    CurrentSub = $"Captioning {file.FileName}";
                    SubProgress = i + 1;

                    string previewFile = ValidatePreviewFile(file, previewFolder);
                    if (!string.IsNullOrEmpty(previewFile))
                    {
                        previewFiles.Add(previewFile);
                    }
                }
                if (previewFiles.Count == 0) continue;

                await Task.Run(() =>
                {
                    List<BlipResult> captions = CaptionImage(previewFiles);
                    if (captions != null && captions.Count > 0)
                    {
                        for (int j = 0; j < captions.Count; j++)
                        {
                            if (!string.IsNullOrWhiteSpace(captions[j].caption))
                            {
                                fileChunk[j].AICaption = captions[j].caption;
                                Persist(fileChunk[j]);

                                if (AssetInventory.Config.logAICaptions)
                                {
                                    Debug.Log($"Caption: {captions[j].caption} ({fileChunk[j].FileName})");
                                }
                            }
                            else if (i == 0)
                            {
                                toolChainWorking = false;
                            }
                        }
                    }
                    else if (i == 0)
                    {
                        toolChainWorking = false;
                    }
                });
                if (!toolChainWorking) break;
            }
            MetaProgress.Remove(progressId);
            ResetState(true);
        }

        public static List<BlipResult> CaptionImage(List<string> filenames)
        {
            string blipType = AssetInventory.Config.blipType == 1 ? "--large" : "";
            string gpuUsage = AssetInventory.Config.aiUseGPU ? "--gpu" : "";
            string nameList = "\"" + string.Join("\" \"", filenames) + "\"";
            string result = IOUtils.ExecuteCommand("blip-caption", $"{blipType} {gpuUsage} --json {nameList}");
            if (string.IsNullOrWhiteSpace(result)) return null;

            return JsonConvert.DeserializeObject<List<BlipResult>>(result);
        }
    }

    public class BlipResult
    {
        public string path;
        public string caption;
    }
}