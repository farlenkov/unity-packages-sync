using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPackagesSync
{
    public class PackagesSyncWindow : EditorWindow
    {
        Vector2 scroll;
        List<string> projectPaths;
        Dictionary<string, string> depsByProject;
        Dictionary<string, List<string>> depsVersions;

        string GetManifestPath(string projectPath) => Path.Combine(projectPath, "Packages", "manifest.json");
        string GetDepKey(string projectName, string depName) => $"{projectName} - {depName}";

        [MenuItem("Window/Packages Sync")]
        static void OpenWindow()
        {
            GetWindow<PackagesSyncWindow>("Packages Sync");
        }

        void OnGUI()
        {
            Reset<List<string>, string>(ref projectPaths);
            Reset<Dictionary<string, List<string>>, KeyValuePair<string, List<string>>>(ref depsVersions);
            Reset<Dictionary<string, string>, KeyValuePair<string, string>>(ref depsByProject);

            CollectProjectPaths();
            ReadManifestFiles();
            DrawDeps();
        }

        void CollectProjectPaths()
        {
            var rootFolder = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));
            var foldersInRoot = Directory.EnumerateDirectories(rootFolder);

            foreach(var folderInRoot in foldersInRoot)
            {
                var manifestPath = GetManifestPath(folderInRoot);

                if (File.Exists(manifestPath))
                    projectPaths.Add(folderInRoot);
            }
        }

        void ReadManifestFiles()
        {
            foreach (var projectPath in projectPaths)
            {
                var manifestJson = ReadManifest(projectPath);
                var depsJson = manifestJson.Value<JObject>("dependencies");
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                
                foreach(var dep in depsJson)
                {
                    var projectDepKey = GetDepKey(projectName, dep.Key);
                    var depValue = (string)dep.Value;
                    depsByProject.Add(projectDepKey, depValue);

                    if (!depsVersions.TryGetValue(dep.Key, out var depVersions))
                        depsVersions.Add(dep.Key, new List<string>(){depValue});
                    else
                        depVersions.Add(depValue);
                }
            }
        }

        void DrawDeps()
        {
            
            // GUILayout.BeginVertical();

            // PROJECT NAMES

            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(200));

            foreach(var projectPath in projectPaths)
            {
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                GUILayout.Label(projectName, GUILayout.Width(160));
            }

            GUILayout.EndHorizontal();

            // DEP NAMES
            scroll = GUILayout.BeginScrollView(scroll);
            foreach(var depName in depsVersions.Keys)
            {
                var hasPrevValue = false;
                var prevValue = (string)null;

                GUILayout.BeginHorizontal();
                GUILayout.Label(depName, GUILayout.Width(200));

                // DEP VALUES

                foreach(var projectPath in projectPaths)
                {
                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    var depKey = GetDepKey(projectName, depName);

                    depsByProject.TryGetValue(depKey, out var depValue);
                    DrawDepVersions(projectPath, depName, depValue, !hasPrevValue ? true : prevValue == depValue);

                    hasPrevValue = true;
                    prevValue = depValue;
                }

                GUILayout.EndHorizontal();
            }

            // GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        void DrawDepVersions(string projectPath, string depName, string currentValue, bool isEqual)
        {
            var originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = isEqual ? originalBackgroundColor : Color.red;

            depsVersions.TryGetValue(depName, out var depVersions);

            var currentIndex = depVersions.IndexOf(currentValue);
            var newIndex = EditorGUILayout.Popup(currentIndex, depVersions.ToArray(), GUILayout.Width(160));
            
            if (currentIndex != newIndex)
                ChangeDependency(projectPath, depName, depVersions[newIndex]);

            GUI.backgroundColor = originalBackgroundColor;
        }

        void ChangeDependency(string projectPath, string depName, string newValue)
        {
            Debug.Log($"New Dependency Value: {Path.GetFileNameWithoutExtension(projectPath)} / {depName} = {newValue}");

            var manifestJson = ReadManifest(projectPath);
            var depsJson = manifestJson.Value<JObject>("dependencies");
            depsJson[depName] = newValue;
            
            var manifestPath = GetManifestPath(projectPath);
            File.WriteAllText(manifestPath, manifestJson.ToString(Formatting.Indented));
        }

        JObject ReadManifest(string projectPath)
        {
            var manifestPath = GetManifestPath(projectPath);
            var manifestText = File.ReadAllText(manifestPath);
            var manifestJson = JObject.Parse(manifestText);
            return manifestJson;
        }

        void Reset<T, V>(ref T collection) where T : ICollection<V>, new()
        {
            if (collection == null)
                collection = new ();
            else
                collection.Clear();
        }
    }
}