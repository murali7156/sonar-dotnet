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

namespace SonarAnalyzer.VisualBasic.Rules;

[DiagnosticAnalyzer(LanguageNames.VisualBasic)]
public sealed class ConditionalStructureSameImplementation : ConditionalStructureSameImplementationBase
{
    private static readonly DiagnosticDescriptor Rule =
        DescriptorFactory.Create(DiagnosticId, MessageFormat);

    private static readonly ISet<SyntaxKind> IgnoredStatementsInSwitch = new HashSet<SyntaxKind> { SyntaxKind.ReturnStatement, SyntaxKind.ThrowStatement };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    protected override void Initialize(SonarAnalysisContext context)
    {
        context.RegisterNodeAction(
            c =>
            {
                var ifStatement = (SingleLineIfStatementSyntax)c.Node;

                if (ifStatement.ElseClause is not null &&
                    VisualBasicEquivalenceChecker.AreEquivalent(ifStatement.ElseClause.Statements, ifStatement.Statements))
                {
                    ReportIssue(c, ifStatement.ElseClause.Statements, ifStatement.Statements, "branch");
                }
            },
            SyntaxKind.SingleLineIfStatement);

        context.RegisterNodeAction(
            c =>
            {
                var ifBlock = (MultiLineIfBlockSyntax)c.Node;

                var statements = new[] { ifBlock.Statements }
                    .Concat(ifBlock.ElseIfBlocks.Select(elseIf => elseIf.Statements))
                    .Concat(new[] { ifBlock.ElseBlock?.Statements ?? new SyntaxList<StatementSyntax>() })
                    .Where(l => l.Any())
                    .ToList();

                for (var i = 1; i < statements.Count; i++)
                {
                    CheckStatementsAt(c, statements, i, ifBlock.ElseBlock is not null, "branch");
                }
            },
            SyntaxKind.MultiLineIfBlock);

        context.RegisterNodeAction(
            c =>
            {
                var select = (SelectBlockSyntax)c.Node;
                var statements = select.CaseBlocks.Select(b => b.Statements).ToList();
                var hasCaseElse = select.CaseBlocks.Any(b => b.IsKind(SyntaxKind.CaseElseBlock));
                for (var i = 1; i < statements.Count; i++)
                {
                    CheckStatementsAt(c, statements, i, hasCaseElse, "case");
                }
            },
            SyntaxKind.SelectBlock);
    }

    private static void CheckStatementsAt(SonarSyntaxNodeReportingContext context, List<SyntaxList<StatementSyntax>> statements, int currentIndex, bool hasElse, string constructType)
    {
        var currentBlockStatements = statements[currentIndex];
        var numberOfStatements = currentBlockStatements.Count(IsApprovedStatement);
        if (!hasElse && numberOfStatements == 1)
        {
            if (statements.Count > 1 && statements.TrueForAll(x => VisualBasicEquivalenceChecker.AreEquivalent(currentBlockStatements, x)))
            {
                ReportIssue(context, currentBlockStatements, statements[0], constructType);
            }
        }
        else if (numberOfStatements > 1)
        {
            for (var j = 0; j < currentIndex; j++)
            {
                if (VisualBasicEquivalenceChecker.AreEquivalent(currentBlockStatements, statements[j]))
                {
                    ReportIssue(context, currentBlockStatements, statements[j], constructType);
                    return;
                }
            }
        }
    }

    private static void ReportIssue(SonarSyntaxNodeReportingContext context, SyntaxList<StatementSyntax> statementsToReport, SyntaxList<StatementSyntax> locationProvider, string constructType)
    {
        var firstStatement = statementsToReport.FirstOrDefault();
        if (firstStatement is null)
        {
            return;
        }

        var lastStatement = statementsToReport.Last();

        context.ReportIssue(Rule, firstStatement.CreateLocation(lastStatement),
            locationProvider.First().LineNumberToReport().ToString(), constructType);
    }

    private static bool IsApprovedStatement(StatementSyntax statement) =>
        !statement.IsAnyKind(IgnoredStatementsInSwitch);
}
