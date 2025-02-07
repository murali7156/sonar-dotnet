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

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SonarAnalyzer.SourceGenerators;

[Generator]
[ExcludeFromCodeCoverage]
public class RuleCatalogGenerator : ISourceGenerator
{
    private const string SonarWayFileName = "Sonar_way_profile.json";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public void Initialize(GeneratorInitializationContext context)
    {
        // Not needed
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir))
        {
            throw new NotSupportedException("Cannot find ProjectDir");
        }
        var project = Path.GetFileName(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        var directorySuffix = project switch
        {
            "SonarAnalyzer.CSharp.Core" => "cs",
            "SonarAnalyzer.VisualBasic.Core" => "vbnet",
            _ => throw new ArgumentException($"Unexpected projectDir: {projectDir}")
        };
        var rspecDirectory = Path.Combine(projectDir, "..", "..", "rspec", directorySuffix);
        var sonarWay = ParseSonarWay(File.ReadAllText(Path.Combine(rspecDirectory, SonarWayFileName)));
        context.AddSource($"RuleCatalog.{directorySuffix}.g.cs", GenerateSource(project, RuleDescriptorArguments(rspecDirectory, sonarWay)));
    }

    private static IEnumerable<string[]> RuleDescriptorArguments(string rspecDirectory, ISet<string> sonarWay)
    {
        foreach (var jsonPath in Directory.GetFiles(rspecDirectory, "*.json").Where(x => Path.GetFileName(x) != SonarWayFileName))
        {
            var json = JObject.Parse(File.ReadAllText(jsonPath));
            var id = Path.GetFileNameWithoutExtension(jsonPath);
            var html = File.ReadAllText(Path.ChangeExtension(jsonPath, ".html"));
            yield return new[]
            {
                Encode(id),
                Encode(json.Value<string>("title")),
                Encode(json.Value<string>("type")),
                Encode(json.Value<string>("defaultSeverity")),
                Encode(json.Value<string>("status")),
                $"SourceScope.{json.Value<string>("scope")}",
                sonarWay.Contains(id).ToString().ToLower(),
                Encode(FirstParagraphText(id, html))
            };
        }
    }

    private static string GenerateSource(string namespacePrefix, IEnumerable<string[]> rulesArguments)
    {
        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>

            namespace {{namespacePrefix}}.Rspec;

            public static class RuleCatalog
            {
                public static Dictionary<string, RuleDescriptor> Rules { get; } = new()
                {
            """);
        foreach (var arguments in rulesArguments)
        {
            sb.AppendLine($@"        {{ {arguments[0]}, new({string.Join(", ", arguments)}) }},");
        }
        sb.AppendLine("""
                };
            }
            """);
        return sb.ToString();
    }

    private static string Encode(string value) =>
        $@"@""{value.Replace(@"""", @"""""")}""";

    private static string FirstParagraphText(string id, string html)
    {
        var match = Regex.Match(html, "<p>(?<Text>.*?)</p>", RegexOptions.Singleline, RegexTimeout);
        if (match.Success)
        {
            var text = match.Groups["Text"].Value;
            text = Regex.Replace(text, "<[^>]*>", string.Empty, RegexOptions.None, RegexTimeout);
            text = text.Replace("\n", " ").Replace("\r", " ");
            text = Regex.Replace(text, @"\s{2,}", " ", RegexOptions.None, RegexTimeout);
            return WebUtility.HtmlDecode(text);
        }
        else
        {
            throw new NotSupportedException($"Description of rule {id} does not contain any HTML <p>paragraphs</p>.");
        }
    }

    private static HashSet<string> ParseSonarWay(string json) =>
        new(JObject.Parse(json)["ruleKeys"].Values<string>());
}
