using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class CodexPlayModeTestBridge
{
    const string RequestFileName = "CodexRunPlayModeTests.request";
    const string ResultFileName = "PlayModeTestResults.xml";
    const string SummaryFileName = "CodexPlayModeTestSummary.txt";

    static TestRunnerApi runner;
    static ResultCallbacks callbacks;

    static CodexPlayModeTestBridge()
    {
        EditorApplication.update -= TryStartRequestedRun;
        EditorApplication.update += TryStartRequestedRun;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            runner = null;
            callbacks = null;
        }
    }

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string RequestPath => Path.Combine(ProjectRoot, "Temp", RequestFileName);

    static void TryStartRequestedRun()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            runner != null)
        {
            EditorApplication.delayCall += TryStartRequestedRun;
            return;
        }

        string[] requestedTests =
            File.ReadAllLines(RequestPath);
        File.Delete(RequestPath);
        callbacks = new ResultCallbacks(ProjectRoot);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(callbacks);

        File.WriteAllText(
            Path.Combine(ProjectRoot, SummaryFileName),
            "RUNNING " + DateTime.Now.ToString("O"));

        Filter filter = new Filter
        {
            testMode = TestMode.PlayMode,
            assemblyNames = new[] { "NpcRuntimeAudit.PlayMode" }
        };
        if (requestedTests.Length > 0 &&
            !(requestedTests.Length == 1 &&
              string.Equals(
                  requestedTests[0].Trim(),
                  "run",
                  StringComparison.OrdinalIgnoreCase)))
        {
            filter.testNames = requestedTests;
        }

        runner.Execute(new ExecutionSettings(filter));
    }

    [Serializable]
    sealed class ResultCallbacks : ICallbacks
    {
        readonly string projectRoot;

        public ResultCallbacks(string projectRoot)
        {
            this.projectRoot = projectRoot;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log("CODEX_PLAYMODE_TESTS_STARTED: " + testsToRun.TestCaseCount);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string resultPath = Path.Combine(projectRoot, ResultFileName);
            TestRunnerApi.SaveResultToFile(result, resultPath);

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("status=" + result.TestStatus);
            summary.AppendLine("passed=" + result.PassCount);
            summary.AppendLine("failed=" + result.FailCount);
            summary.AppendLine("skipped=" + result.SkipCount);
            summary.AppendLine("inconclusive=" + result.InconclusiveCount);
            summary.AppendLine("durationSeconds=" + result.Duration.ToString("0.###"));

            List<ITestResultAdaptor> failures = new List<ITestResultAdaptor>();
            CollectLeafFailures(result, failures);
            foreach (ITestResultAdaptor failure in failures)
            {
                summary.AppendLine();
                summary.AppendLine("FAIL: " + failure.FullName);
                summary.AppendLine(failure.Message ?? string.Empty);
                summary.AppendLine(failure.StackTrace ?? string.Empty);
            }

            File.WriteAllText(
                Path.Combine(projectRoot, SummaryFileName),
                summary.ToString());
            Debug.Log("CODEX_PLAYMODE_TESTS_FINISHED: " + resultPath);
            runner = null;
            callbacks = null;
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                Debug.Log(
                    "CODEX_TEST_RESULT: " + result.TestStatus + " " +
                    result.FullName + " (" + result.Duration.ToString("0.###") + "s)");
            }
        }

        static void CollectLeafFailures(
            ITestResultAdaptor result,
            List<ITestResultAdaptor> failures)
        {
            if (!result.HasChildren)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    failures.Add(result);
                }
                return;
            }

            foreach (ITestResultAdaptor child in result.Children)
            {
                CollectLeafFailures(child, failures);
            }
        }
    }
}
