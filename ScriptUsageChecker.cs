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
    private Vector2 scrollPos;

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
        csvLines.Add("スクリプト名,アタッチ先,使用ステータス");

        foreach (string guid in scriptGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (monoScript == null) continue;

            Type scriptType = monoScript.GetClass();
            if (scriptType == null || !typeof(MonoBehaviour).IsAssignableFrom(scriptType)) continue;

            string scriptName = scriptType.Name;

            bool hasStartOrUpdate =
                scriptType.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null ||
                scriptType.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null;

            bool isAttached = scriptToGameObjects.ContainsKey(scriptName);
            string attachedTo = isAttached ? string.Join("; ", scriptToGameObjects[scriptName]) : "なし";
            string status = (isAttached || hasStartOrUpdate) ? "使用中" : "未使用";

            Debug.Log($"スクリプト: {scriptName}\n- アタッチ先: {attachedTo}\n- 使用ステータス: {status}");

            if (exportCsv)
            {
                csvLines.Add($"{scriptName},{attachedTo},{status}");
            }
        }

        if (exportCsv)
        {
            string fullPath = Path.Combine(csvOutputDirectory, "ScriptUsageReport.csv");
            File.WriteAllLines(fullPath, csvLines);
            AssetDatabase.Refresh();
            Debug.Log($"✅ CSV出力完了: {fullPath}");
        }
    }
}
