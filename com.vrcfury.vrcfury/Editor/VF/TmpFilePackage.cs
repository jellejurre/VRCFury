using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;

namespace VF {
    [InitializeOnLoad]
    public class TmpFilePackage {
        public static string GetPath() {
            if (!Directory.Exists(TmpDirPath)) {
                Directory.CreateDirectory(TmpDirPath);
            }

            if (!File.Exists(TmpPackagePath) ||
                Encoding.UTF8.GetString(File.ReadAllBytes(TmpPackagePath)) != PackageJson) {
                File.WriteAllBytes(TmpPackagePath, Encoding.UTF8.GetBytes(PackageJson));

                EditorApplication.delayCall += ReresolvePackages;
            }

            EditorApplication.delayCall += () => {
                if (Directory.Exists("Assets/_VRCFury")) {
                    AssetDatabase.MoveAsset("Assets/_VRCFury", GetPath() + "/LegacyBackup");
                }
            };

            return TmpDirPath;
        }

        public static void ReresolvePackages() {
            MethodInfo method = typeof(Client).GetMethod("Resolve",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new Type[] {},
                null
            );
            method.Invoke(null, null);
        }

        static TmpFilePackage() {
            GetPath();
        }
         
        private const string TmpDirPath = "Packages/com.vrcfury.temp";
        private const string TmpPackagePath = TmpDirPath + "/" + "package.json";

        private static readonly string PackageJson =
            "{\n" +
            "\"name\": \"com.vrcfury.temp\",\n" +
            "\"displayName\": \"VRCFury Temp Files\",\n" +
            "\"version\": \"0.0.0\",\n" +
            "\"hideInEditor\": false\n" +
            "}";
    }
}
