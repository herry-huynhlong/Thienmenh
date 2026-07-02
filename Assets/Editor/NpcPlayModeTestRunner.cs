using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class NpcPlayModeTestRunner
{
    const string BatchRunQueuedKey = "NpcPlayModeTestRunner.BatchRunQueued";

    static readonly string[] DefaultTestNames =
    {
        "NpcRuntimeAuditTests.HybridBrainConflictIsResolvedBothWays",
        "NpcRuntimeAuditTests.BusyTaskProviderFreezesActionTimerForBothNpcBrains",
        "NpcRuntimeAuditTests.HungryNpcDeclinesTasksAndTaskVisitCooldownBlocksRepeat",
        "NpcRuntimeAuditTests.MortalSmartNpcStillHandlesHungerAndFatigueWhileCultivationIsEnabled",
        "NpcRuntimeAuditTests.AuditThreeDaysForSelectedActors"
    };

    static TestRunnerApi activeRunner;
    static BatchCallbacks activeBatchCallbacks;

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        if (!Application.isBatchMode || !HasCommandLineArgument("-runTests"))
        {
            return;
        }

        if (SessionState.GetBool(BatchRunQueuedKey, false))
        {
            return;
        }

        SessionState.SetBool(BatchRunQueuedKey, true);
        Debug.Log("NpcPlayModeTestRunner: detected batchmode -runTests.");
        Debug.Log("NpcPlayModeTestRunner: redirecting batch run to NpcThreeDayAuditBatchRunner.");
        NpcThreeDayAuditBatchRunner.Run();
    }

    [MenuItem("Tools/Run NPC PlayMode Tests")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        Run(DefaultTestNames, false);
    }

    static void Run(string[] testNames, bool batchMode)
    {
        if (activeRunner != null)
        {
            Debug.LogWarning("NPC playmode test run is already active.");
            return;
        }

        Debug.Log(
            "NpcPlayModeTestRunner: Run invoked. BatchMode=" +
            batchMode +
            " Tests=" +
            testNames.Length);

        activeRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
        if (batchMode)
        {
            activeBatchCallbacks = new BatchCallbacks();
            activeRunner.RegisterCallbacks(activeBatchCallbacks);
        }
        else
        {
            activeRunner.RegisterCallbacks(new Callbacks());
        }

        activeRunner.Execute(
            new ExecutionSettings(
                new Filter
                {
                    testMode = TestMode.PlayMode,
                    assemblyNames = new[] { "NpcRuntimeAudit.PlayMode" },
                    testNames = testNames
                }));
    }

    static bool HasCommandLineArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static string GetCommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    static void ResetBatchRunState()
    {
        activeRunner = null;
        activeBatchCallbacks = null;
        SessionState.EraseBool(BatchRunQueuedKey);
    }

    sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log("Running NPC playmode tests...");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Debug.Log(
                "NPC playmode tests finished. Status=" +
                result.TestStatus +
                " Passed=" + result.PassCount +
                " Failed=" + result.FailCount +
                " Skipped=" + result.SkipCount);

            activeRunner = null;
            EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                Debug.LogError(
                    "Failed " + result.Name + ": " + result.Message);
            }
        }
    }

    sealed class BatchCallbacks : ICallbacks
    {
        readonly List<string> failedTests = new List<string>();

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log("NpcPlayModeTestRunner: batch PlayMode tests started.");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string resultsPath = GetCommandLineValue("-testResults");
            if (!string.IsNullOrEmpty(resultsPath))
            {
                WriteResultsFile(resultsPath, result, failedTests);
            }

            Debug.Log(
                "NpcPlayModeTestRunner: batch PlayMode tests finished. Status=" +
                result.TestStatus +
                " Passed=" + result.PassCount +
                " Failed=" + result.FailCount +
                " Skipped=" + result.SkipCount);

            ResetBatchRunState();
            EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                string message = string.IsNullOrWhiteSpace(result.Message)
                    ? "No failure message."
                    : result.Message;
                failedTests.Add(result.Name + ": " + message);
                Debug.LogError("Failed " + result.Name + ": " + message);
            }
        }

        static void WriteResultsFile(
            string resultsPath,
            ITestResultAdaptor result,
            List<string> failures)
        {
            string fullPath = Path.GetFullPath(resultsPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            builder.Append("<test-run");
            builder.Append(" result=\"").Append(XmlEscape(result.TestStatus.ToString())).Append("\"");
            builder.Append(" total=\"").Append(result.PassCount + result.FailCount + result.SkipCount).Append("\"");
            builder.Append(" passed=\"").Append(result.PassCount).Append("\"");
            builder.Append(" failed=\"").Append(result.FailCount).Append("\"");
            builder.Append(" skipped=\"").Append(result.SkipCount).Append("\"");
            builder.Append(" duration=\"").Append(result.Duration.ToString("0.###", CultureInfo.InvariantCulture)).Append("\">");
            builder.AppendLine();
            builder.AppendLine("  <failures>");
            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("    <failure message=\"")
                    .Append(XmlEscape(failures[i]))
                    .AppendLine("\" />");
            }

            builder.AppendLine("  </failures>");
            builder.AppendLine("</test-run>");
            File.WriteAllText(fullPath, builder.ToString(), Encoding.UTF8);
            Debug.Log("NpcPlayModeTestRunner: wrote results to " + fullPath);
        }

        static string XmlEscape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value
                    .Replace("&", "&amp;")
                    .Replace("\"", "&quot;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
        }
    }
}
