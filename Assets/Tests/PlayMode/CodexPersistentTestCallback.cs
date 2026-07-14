using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(CodexPersistentTestCallback))]

public sealed class CodexPersistentTestCallback : ITestRunCallback
{
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string SummaryPath => Path.Combine(ProjectRoot, "CodexPlayModeTestSummary.txt");
    static string ResultPath => Path.Combine(ProjectRoot, "PlayModeTestResults.xml");

    public void RunStarted(ITest testsToRun)
    {
        File.WriteAllText(
            SummaryPath,
            "RUNNING " + DateTime.Now.ToString("O") + Environment.NewLine +
            "tests=" + testsToRun.TestCaseCount);
    }

    public void RunFinished(ITestResult testResults)
    {
        File.WriteAllText(ResultPath, testResults.ToXml(true).OuterXml);

        StringBuilder summary = new StringBuilder();
        summary.AppendLine("status=" + testResults.ResultState.Status);
        summary.AppendLine("passed=" + testResults.PassCount);
        summary.AppendLine("failed=" + testResults.FailCount);
        summary.AppendLine("skipped=" + testResults.SkipCount);
        summary.AppendLine("inconclusive=" + testResults.InconclusiveCount);
        summary.AppendLine("durationSeconds=" + testResults.Duration.ToString("0.###"));

        List<ITestResult> failures = new List<ITestResult>();
        CollectFailures(testResults, failures);
        foreach (ITestResult failure in failures)
        {
            summary.AppendLine();
            summary.AppendLine("FAIL: " + failure.FullName);
            summary.AppendLine(failure.Message ?? string.Empty);
            summary.AppendLine(failure.StackTrace ?? string.Empty);
        }

        File.WriteAllText(SummaryPath, summary.ToString());
    }

    public void TestStarted(ITest test)
    {
    }

    public void TestFinished(ITestResult result)
    {
    }

    static void CollectFailures(ITestResult result, List<ITestResult> failures)
    {
        if (!result.HasChildren)
        {
            if (result.ResultState.Status == TestStatus.Failed)
            {
                failures.Add(result);
            }
            return;
        }

        foreach (ITestResult child in result.Children)
        {
            CollectFailures(child, failures);
        }
    }
}
