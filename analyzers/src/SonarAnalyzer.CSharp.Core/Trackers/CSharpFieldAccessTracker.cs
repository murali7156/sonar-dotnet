﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2014-2025 SonarSource SA
 * mailto:info AT sonarsource DOT com
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the Sonar Source-Available License Version 1, as published by SonarSource SA.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the Sonar Source-Available License for more details.
 *
 * You should have received a copy of the Sonar Source-Available License
 * along with this program; if not, see https://sonarsource.com/license/ssal/
 */

using SonarAnalyzer.Core.Trackers;

namespace SonarAnalyzer.CSharp.Core.Trackers;

public class CSharpFieldAccessTracker : FieldAccessTracker<SyntaxKind>
{
    protected override ILanguageFacade<SyntaxKind> Language => CSharpFacade.Instance;
    protected override SyntaxKind[] TrackedSyntaxKinds { get; } =
        new[]
        {
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxKind.MemberBindingExpression,
            SyntaxKind.IdentifierName
        };

    public override Condition WhenRead() =>
        context => !((ExpressionSyntax)context.Node).IsLeftSideOfAssignment();

    public override Condition MatchSet() =>
        context => ((ExpressionSyntax)context.Node).IsLeftSideOfAssignment();

    public override Condition AssignedValueIsConstant() =>
        context =>
        {
            var assignment = (AssignmentExpressionSyntax)context.Node.Ancestors().FirstOrDefault(ancestor => ancestor.IsKind(SyntaxKind.SimpleAssignmentExpression));
            return assignment != null && assignment.Right.HasConstantValue(context.Model);
        };

    protected override bool IsIdentifierWithinMemberAccess(SyntaxNode expression) =>
        expression.IsKind(SyntaxKind.IdentifierName)
        && expression.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression);
}
