﻿using System.Collections.Concurrent;
using System.Reflection;
using TUnit.Core;
using TUnit.Core.Attributes;

namespace TUnit.Engine;

public class SingleTestExecutor
{
    private readonly MethodInvoker _methodInvoker;
    private readonly TestClassCreator _testClassCreator;
    private readonly Disposer _disposer;

    public SingleTestExecutor(MethodInvoker methodInvoker, 
        TestClassCreator testClassCreator,
        Disposer disposer)
    {
        _methodInvoker = methodInvoker;
        _testClassCreator = testClassCreator;
        _disposer = disposer;
    }
    
    private readonly ConcurrentDictionary<string, Task> _oneTimeSetUpRegistry = new();

    public async Task<TUnitTestResult> ExecuteTest(TestDetails testDetails, Type[] allClasses)
    {
        var start = DateTimeOffset.Now;

        if (testDetails.IsSkipped)
        {
            return testDetails.SetResult(new TUnitTestResult
            {
                TestDetails = testDetails,
                Duration = TimeSpan.Zero,
                Start = start,
                End = start,
                ComputerName = Environment.MachineName,
                Exception = null,
                Status = Status.Skipped
            });
        }
        
        try
        {
            await ExecuteCore(testDetails, allClasses);
            
            var end = DateTimeOffset.Now;
            
            return testDetails.SetResult(new TUnitTestResult
            {
                TestDetails = testDetails,
                Duration = end - start,
                Start = start,
                End = end,
                ComputerName = Environment.MachineName,
                Exception = null,
                Status = Status.Passed
            });
        }
        catch (Exception e)
        {
            var end = DateTimeOffset.Now;
            
            return testDetails.SetResult(new TUnitTestResult
            {
                TestDetails = testDetails,
                Duration = end - start,
                Start = start,
                End = end,
                ComputerName = Environment.MachineName,
                Exception = e,
                Status = Status.Failed
            });
        }
    }

    private async Task ExecuteCore(TestDetails testDetails, Type[] allClasses)
    {
        var @class = _testClassCreator.CreateTestClass(testDetails, allClasses);

        try
        {
            var isRetry = testDetails.RetryCount > 0;
            var executionCount = isRetry ? testDetails.RetryCount : testDetails.RepeatCount;

            for (var i = 0; i < executionCount + 1; i++)
            {
                try
                {
                    await ExecuteSetUps(@class);

                    await _methodInvoker.InvokeMethod(@class, testDetails.MethodInfo, BindingFlags.Default,
                        testDetails.ArgumentValues?.ToArray());

                    await ExecuteTearDowns(@class);

                    if (isRetry)
                    {
                        break;
                    }
                }
                catch
                {
                    if (!isRetry || i == executionCount)
                    {
                        throw;
                    }
                }
            }
        }
        finally
        {
            await _disposer.DisposeAsync(@class);
        }
    }

    private async Task ExecuteSetUps(object @class)
    {
        await _oneTimeSetUpRegistry.GetOrAdd(@class.GetType().FullName!, _ => ExecuteOneTimeSetUps(@class));

        var setUpMethods = @class.GetType()
            .GetMethods()
            .Where(x => !x.IsStatic)
            .Where(x => x.CustomAttributes.Any(attributeData => attributeData.AttributeType == typeof(SetUpAttribute)));

        foreach (var setUpMethod in setUpMethods)
        {
            await _methodInvoker.InvokeMethod(@class, setUpMethod, BindingFlags.Default, null);
        }
    }
    
    private async Task ExecuteTearDowns(object @class)
    {
        var tearDownMethods = @class.GetType()
            .GetMethods()
            .Where(x => !x.IsStatic)
            .Where(x => x.CustomAttributes.Any(attributeData => attributeData.AttributeType == typeof(TearDownAttribute)));

        var exceptions = new List<Exception>();
        
        foreach (var tearDownMethod in tearDownMethods)
        {
            try
            {
                await _methodInvoker.InvokeMethod(@class, tearDownMethod, BindingFlags.Default, null);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        }
        
        if (exceptions.Any())
        {
            throw new AggregateException(exceptions);
        }
    }

    private async Task ExecuteOneTimeSetUps(object @class)
    {
        var oneTimeSetUpMethods = @class.GetType()
            .GetMethods()
            .Where(x => x.IsStatic)
            .Where(x => x.CustomAttributes.Any(attributeData => attributeData.AttributeType == typeof(OneTimeSetUpAttribute)));

        foreach (var oneTimeSetUpMethod in oneTimeSetUpMethods)
        {
            await _methodInvoker.InvokeMethod(@class, oneTimeSetUpMethod, BindingFlags.Static | BindingFlags.Public, null);
        }
    }
}