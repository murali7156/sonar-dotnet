﻿using System.Text;

namespace SonarAnalyzer.SymbolicExecution.Roslyn.RuleChecks.CSharp;

public abstract class HardcodedBytesRuleBase : SymbolicRuleCheck
{
    protected abstract SymbolicConstraint Hardcoded { get; }
    protected abstract SymbolicConstraint NotHardcoded { get; }

    // new byte/char[] { ... }
    // new byte/char[42]
    protected ProgramState ProcessArrayCreation(ProgramState state, IArrayCreationOperationWrapper arrayCreation)
    {
        if (arrayCreation.Type.IsAny(KnownType.System_Byte_Array, KnownType.System_Char_Array))
        {
            var isConstant = arrayCreation.Initializer.WrappedOperation is null || arrayCreation.Initializer.ElementValues.All(x => x.ConstantValue.HasValue);
            return state.SetOperationConstraint(arrayCreation, isConstant ? Hardcoded : NotHardcoded);
        }
        return state;
    }

    // array[42] = ...
    protected ProgramState ProcessArrayElementReference(ProgramState state, IArrayElementReferenceOperationWrapper arrayElementReference) =>
        (arrayElementReference.IsAssignmentTarget() || arrayElementReference.IsCompoundAssignmentTarget())
        && arrayElementReference.ArrayReference.TrackedSymbol(state) is { } array
            ? state.SetSymbolConstraint(array, NotHardcoded)
            : state;

    // array.SetValue(value, index)
    protected ProgramState ProcessArraySetValue(ProgramState state, IInvocationOperationWrapper invocation)
    {
        if (invocation.TargetMethod.Name == nameof(Array.SetValue)
            && invocation.TargetMethod.ContainingType.Is(KnownType.System_Array)
            && invocation.Instance.TrackedSymbol(state) is { } array)
        {
            return invocation.ArgumentValue("value") is { ConstantValue.HasValue: true }
                       ? state
                       : state.SetSymbolConstraint(array, NotHardcoded);
        }
        return null;
    }

    // array.Initialize()
    protected ProgramState ProcessArrayInitialize(ProgramState state, IInvocationOperationWrapper invocation) =>
        invocation.TargetMethod.Name == nameof(Array.Initialize)
        && invocation.TargetMethod.ContainingType.Is(KnownType.System_Array)
        && invocation.Instance.TrackedSymbol(state) is { } array
            ? state.SetSymbolConstraint(array, Hardcoded)
            : null;

    // Encoding.UTF8.GetBytes(s)
    // Convert.FromBase64CharArray(chars, ...)
    // Convert.FromBase64String(s)
    protected ProgramState ProcessStringToBytes(ProgramState state, IInvocationOperationWrapper invocation)
    {
        return (IsEncodingGetBytes() || IsConvertFromBase64String() || IsConvertFromBase64CharArray())
                   ? state.SetOperationConstraint(invocation, Hardcoded)
                   : null;

        bool IsEncodingGetBytes() =>
            invocation.TargetMethod.Name == nameof(Encoding.UTF8.GetBytes)
            && invocation.TargetMethod.ContainingType.DerivesFrom(KnownType.System_Text_Encoding)
            && (invocation.ArgumentValue("s") is { ConstantValue.HasValue: true } || ArgumentIsPredictable("chars"));

        bool IsConvertFromBase64CharArray() =>
            invocation.TargetMethod.Name == nameof(Convert.FromBase64CharArray)
            && invocation.TargetMethod.ContainingType.Is(KnownType.System_Convert)
            && ArgumentIsPredictable("inArray");

        bool IsConvertFromBase64String() =>
            invocation.TargetMethod.Name == nameof(Convert.FromBase64String)
            && invocation.TargetMethod.ContainingType.Is(KnownType.System_Convert)
            && invocation.ArgumentValue("s") is { ConstantValue.HasValue: true };

        bool ArgumentIsPredictable(string parameterName) =>
            invocation.ArgumentValue(parameterName) is { } value
            && state[value]?.HasConstraint(Hardcoded) is true;
    }
}
