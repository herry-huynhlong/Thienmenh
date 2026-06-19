using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class NpcPlayModeTestRunner
{
    static TestRunnerApi activeRunner;

    [MenuItem("Tools/Run NPC PlayMode Tests")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        if (activeRunner != null)
        {
            Debug.LogWarning("NPC playmode test run is already active.");
            return;
        }

        activeRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
        activeRunner.RegisterCallbacks(new Callbacks());

        activeRunner.Execute(
            new ExecutionSettings(
                new Filter
                {
                    testMode = TestMode.PlayMode,
                    assemblyNames = new[] { "NpcRuntimeAudit.PlayMode" },
                    testNames = new[]
                    {
                        "NpcRuntimeAuditTests.HybridBrainConflictIsResolvedBothWays",
                        "NpcRuntimeAuditTests.BusyTaskProviderFreezesActionTimerForBothNpcBrains",
                        "NpcRuntimeAuditTests.HungryNpcDeclinesTasksAndTaskVisitCooldownBlocksRepeat"
                    }
                }));
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
}
