﻿using TUnit.Assertions.AssertConditions;
using TUnit.Assertions.Assertions.Strings.Conditions;

namespace TUnit.Assertions.AssertionBuilders.Wrappers;

public class StringContainsAssertionBuilderWrapper : InvokableValueAssertionBuilder<string>
{
    internal StringContainsAssertionBuilderWrapper(InvokableAssertionBuilder<string> invokableAssertionBuilder) : base(invokableAssertionBuilder)
    {
    }

    public StringContainsAssertionBuilderWrapper WithTrimming()
    {
        var assertion = (ExpectedValueAssertCondition<string, string>) Assertions.Peek();

        assertion.WithTransform(s => s?.Trim(), s => s?.Trim());

        AppendCallerMethod([]);
        
        return this;
    }
    
    public StringContainsAssertionBuilderWrapper IgnoringWhitespace()
    {
        var assertion = (StringContainsExpectedValueAssertCondition) Assertions.Peek();

        assertion.WithTransform(StringUtils.StripWhitespace, StringUtils.StripWhitespace);
        assertion.IgnoreWhitespace = true;
        
        AppendCallerMethod([]);
        
        return this;
    }
}