using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VF.Updater {
    public static class VRCFuryUpdater {
        
        private static readonly HttpClient httpClient = new HttpClient();

        [Serializable]
        private class Repository {
            public List<Package> packages;
        }

        [Serializable]
        private class Package {
            public string id;
            public string displayName;
            public string latestUpmTargz;
            public string latestVersion;
        }

        private static bool updating = false;
        public static async Task UpdateAll(bool failIfUpdaterNeedsUpdate = false) {
            if (updating) {
                Debug.Log("(VRCFury already has an update in progress)");
                return;
            }
            updating = true;
            await AsyncUtils.ErrorDialogBoundary(() => AsyncUtils.PreventReload(() => UpdateAllUnsafe(failIfUpdaterNeedsUpdate)));
            await AsyncUtils.InMainThread(EditorUtility.ClearProgressBar);
            updating = false;
        }

        private static async Task UpdateAllUnsafe(bool failIfUpdaterNeedsUpdate) {
            if (await AsyncUtils.InMainThread(() => EditorApplication.isPlaying)) {
                throw new Exception("VRCFury cannot update in play mode");
            }

            Debug.Log("Downloading update manifest...");
            await AsyncUtils.Progress("Checking for updates ...");
            string json = await DownloadString("https://updates.vrcfury.com/updates.json?_=" + DateTime.Now);

            var repo = JsonUtility.FromJson<Repository>(json);
            if (repo.packages == null) {
                throw new Exception("Failed to fetch packages from update server");
            }
            Debug.Log($"Update manifest includes {repo.packages.Count} packages");
            
            await AsyncUtils.Progress("Downloading updated packages ...");

            var deps = await AsyncUtils.ListInstalledPacakges();

            var localUpdaterPackage = deps.FirstOrDefault(d => d.name == "com.vrcfury.updater");
            var remoteUpdaterPackage = repo.packages.FirstOrDefault(p => p.id == "com.vrcfury.updater");

            if (remoteUpdaterPackage != null
                && remoteUpdaterPackage.latestUpmTargz != null
                && (localUpdaterPackage == null || localUpdaterPackage.version != remoteUpdaterPackage.latestVersion)
            ) {
                // An update to the package manager is available
                Debug.Log($"Upgrading updater from {localUpdaterPackage?.version} to {remoteUpdaterPackage.latestVersion}");
                if (failIfUpdaterNeedsUpdate) {
                    throw new Exception("Updater failed to update to new version");
                }

                var remoteName = remoteUpdaterPackage.id;
                var tgzPath = await DownloadTgz(remoteUpdaterPackage.latestUpmTargz);
                Directory.CreateDirectory(await VRCFuryUpdaterStartup.GetUpdateAllMarker());
                await AsyncUtils.AddAndRemovePackages(add: new[]{ (remoteName, tgzPath) });
                return;
            }

            var urlsToAdd = repo.packages
                .Where(remote => remote.latestUpmTargz != null)
                .Select(remote => (deps.FirstOrDefault(d => d.name == remote.id), remote))
                .Where(pair => {
                    var (local, remote) = pair;
                    if (local == null && remote.id == "com.vrcfury.vrcfury") return true;
                    if (local == null && remote.id == "com.vrcfury.legacyprefabs") return true;
                    if (local != null && local.version != remote.latestVersion) return true;
                    return false;
                });

            var packageFilesToAdd = new List<(string,string)>();
            foreach (var (local,remote) in urlsToAdd) {
                Debug.Log($"Upgrading {remote.id} from {local?.version} to {remote.latestVersion}");
                var remoteName = remote.id;
                var tgzPath = await DownloadTgz(remote.latestUpmTargz);
                packageFilesToAdd.Add((remoteName, tgzPath));
            }

            if (packageFilesToAdd.Count == 0) {
                await AsyncUtils.EnsureVrcfuryEmbedded();
                await AsyncUtils.DisplayDialog("No new updates are available.");
                return;
            }

            Directory.CreateDirectory(await VRCFuryUpdaterStartup.GetUpdatedMarkerPath());
            await SceneCloser.CloseScenes();
            await AsyncUtils.AddAndRemovePackages(add: packageFilesToAdd);
        }

        private static async Task<string> DownloadString(string url) {
            try {
                using (var response = await httpClient.GetAsync(url)) {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            } catch (Exception e) {
                throw new Exception($"Failed to download {url}\n\n{e.Message}", e);
            }
        }

        private static async Task<string> DownloadTgz(string url) {
            try {
                var tempFile = await AsyncUtils.InMainThread(FileUtil.GetUniqueTempPathInProject) + ".tgz";
                using (var response = await httpClient.GetAsync(url)) {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(tempFile, FileMode.CreateNew)) {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                return tempFile;
            } catch (Exception e) {
                throw new Exception($"Failed to download {url}\n\n{e.Message}", e);
            }
        }
    }
}
