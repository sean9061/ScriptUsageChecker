using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

public class ScriptUsageChecker : EditorWindow
{
    private string targetDirectory = "Assets/Scripts";
    private string csvOutputDirectory = "Assets";
    private bool exportCsv = false;

    [MenuItem("Tools/スクリプト使用状況チェック")]
    public static void ShowWindow()
    {
        GetWindow<ScriptUsageChecker>("Script Usage Checker");
    }

    private void OnGUI()
    {
        GUILayout.Label("スクリプト検索ディレクトリ", EditorStyles.boldLabel);
        targetDirectory = EditorGUILayout.TextField("検索ディレクトリ", targetDirectory);

        if (GUILayout.Button("検索ディレクトリ選択"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("スクリプトフォルダを選択", targetDirectory, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                targetDirectory = "Assets" + selectedPath.Replace(Application.dataPath, "");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("CSV出力設定", EditorStyles.boldLabel);
        exportCsv = EditorGUILayout.Toggle("CSVを出力する", exportCsv);

        if (exportCsv)
        {
            csvOutputDirectory = EditorGUILayout.TextField("CSV出力ディレクトリ", csvOutputDirectory);
            if (GUILayout.Button("出力先ディレクトリ選択"))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("CSV出力フォルダを選択", csvOutputDirectory, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    csvOutputDirectory = "Assets" + selectedPath.Replace(Application.dataPath, "");
                }
            }
        }

        GUILayout.Space(10);
        if (GUILayout.Button("チェック開始"))
        {
            CheckScriptUsage();
        }
    }

    private void CheckScriptUsage()
    {
        Debug.Log($"=== スクリプト使用状況チェック ({targetDirectory}) ===");

        MonoBehaviour[] allScriptsInScene = GameObject.FindObjectsOfType<MonoBehaviour>();
        Dictionary<string, List<string>> scriptToGameObjects = new Dictionary<string, List<string>>();

        foreach (var script in allScriptsInScene)
        {
            if (script == null) continue;

            string scriptName = script.GetType().Name;
            string objectName = script.gameObject.name;

            if (!scriptToGameObjects.ContainsKey(scriptName))
            {
                scriptToGameObjects[scriptName] = new List<string>();
            }
            scriptToGameObjects[scriptName].Add(objectName);
        }

        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { targetDirectory });
        List<string> csvLines = new List<string>();
        csvLines.Add("スクリプト名,クラスの種類,アタッチ先,使用ステータス,参照元");

        foreach (string guid in scriptGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (monoScript == null) continue;

            Type scriptType = monoScript.GetClass();
            if (scriptType == null) continue;

            string scriptName = scriptType.Name;

            // クラスの種類を判定
            string classType = "Unknown";
            if (typeof(MonoBehaviour).IsAssignableFrom(scriptType))
                classType = "MonoBehaviour";
            else if (typeof(ScriptableObject).IsAssignableFrom(scriptType))
                classType = "ScriptableObject";
            else if (scriptType.IsClass)
                classType = "Static / PlainClass";

            // アタッチチェック
            bool isAttached = scriptToGameObjects.ContainsKey(scriptName);
            string attachedTo = isAttached ? string.Join(";", scriptToGameObjects[scriptName]) : "なし";

            // MonoBehaviour の Start / Update 実装チェック
            bool hasStartOrUpdate = false;
            if (classType == "MonoBehaviour")
            {
                hasStartOrUpdate =
                    scriptType.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null ||
                    scriptType.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null;
            }

            // 参照チェック（他スクリプトから使われているか）
            bool isReferencedElsewhere = false;
            List<string> referenceDetails = new List<string>();

            string[] allScriptPaths = AssetDatabase.FindAssets("t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".cs") && !p.Equals(path))
                .ToArray();

            foreach (string otherScriptPath in allScriptPaths)
            {
                string[] lines = File.ReadAllLines(otherScriptPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (
                        line.Contains($"{scriptName}.") ||
                        line.Contains($"new {scriptName}(") ||
                        line.Contains($"{scriptName} ")
                    )
                    {
                        isReferencedElsewhere = true;
                        string fileName = Path.GetFileName(otherScriptPath);
                        string cleanedLine = line.Trim().Replace("\"", "\"\"");
                        referenceDetails.Add($"{fileName}:{i + 1}「{cleanedLine}」");
                    }
                }
            }

            string status = (isAttached || hasStartOrUpdate || isReferencedElsewhere) ? "使用中" : "未使用";

            Debug.Log($"スクリプト: {scriptName}\n- クラスの種類: {classType}\n- アタッチ先: {attachedTo}\n- 使用ステータス: {status}");

            if (exportCsv)
            {
                string references = referenceDetails.Count > 0 ? string.Join(" | ", referenceDetails) : "";
                csvLines.Add($"{scriptName},{classType},{attachedTo},{status},\"{references}\"");
            }
        }

        if (exportCsv)
        {
            string fileName = $"ScriptUsageReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(csvOutputDirectory, fileName);
            File.WriteAllLines(fullPath, csvLines);
            AssetDatabase.Refresh();
            Debug.Log($"✅ CSV出力完了: {fullPath}");
        }
    }
}
