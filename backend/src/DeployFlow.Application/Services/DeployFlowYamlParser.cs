using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeployFlow.Application.Services;

/// <summary>
/// Parses a <c>deployflow.yml</c> (or <c>.deployflow.yml</c>) file found in a repository
/// and converts it into a <see cref="DeployFlowConfig"/> that overrides or supplements
/// auto-generated pipeline stages.
///
/// Supported YAML subset (simple key-value + indent-based lists, no external dependency):
///
/// <code>
/// version: "1"
/// name: "My App Pipeline"
/// timeout: 30  # minutes
///
/// env:
///   NODE_ENV: production
///   PORT: 3000
///
/// cache:
///   - node_modules
///   - .next/cache
///
/// stages:
///   - name: Setup
///     parallel: false
///     depends_on: []
///     continue_on_failure: false
///     steps:
///       - name: Install
///         type: command
///         command: npm ci
///         timeout: 300
///         retry: 2
///
///   - name: Test
///     depends_on: [Setup]
///     steps:
///       - name: Unit Tests
///         type: test
///         command: npm test -- --coverage
///
///   - name: Deploy
///     depends_on: [Test]
///     steps:
///       - name: Deploy to production
///         type: deploy
///         command: docker compose up -d
///
/// on_failure: rollback   # rollback | notify | ignore
/// on_success: notify
/// notifications:
///   slack: "#deployments"
///   email: devops@example.com
/// </code>
/// </summary>
public static class DeployFlowYamlParser
{
    private static readonly string[] ConfigFileNames =
        ["deployflow.yml", ".deployflow.yml", "deployflow.yaml", ".deployflow.yaml"];

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>Finds the config filename from the list of repo files, or null.</summary>
    public static string? FindConfigFile(IEnumerable<string> repoFiles)
    {
        var lower = repoFiles.Select(f => f.ToLowerInvariant()).ToHashSet();
        return ConfigFileNames.FirstOrDefault(n => lower.Contains(n));
    }

    /// <summary>
    /// Parse a raw YAML string (the content of deployflow.yml) into a config.
    /// Uses a hand-rolled parser to avoid NuGet dependencies.
    /// </summary>
    public static DeployFlowConfig Parse(string yaml)
    {
        var config = new DeployFlowConfig();
        if (string.IsNullOrWhiteSpace(yaml)) return config;

        var lines  = yaml.ReplaceLineEndings("\n").Split('\n');
        var ctx    = new ParseContext(lines);

        while (ctx.HasMore)
        {
            var line = ctx.Current;
            string stripped = StripComment(line).Trim();

            if (stripped.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                config.Version = ExtractValue(stripped);

            else if (stripped.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                config.Name = ExtractValue(stripped);

            else if (stripped.StartsWith("timeout:", StringComparison.OrdinalIgnoreCase))
                config.TimeoutMinutes = int.TryParse(ExtractValue(stripped), out var t) ? t : 60;

            else if (stripped.StartsWith("on_failure:", StringComparison.OrdinalIgnoreCase))
                config.OnFailure = ExtractValue(stripped);

            else if (stripped.StartsWith("on_success:", StringComparison.OrdinalIgnoreCase))
                config.OnSuccess = ExtractValue(stripped);

            else if (stripped == "env:")
                config.Env = ParseStringMap(ctx, Indent(line));

            else if (stripped == "cache:")
                config.Cache = ParseStringList(ctx, Indent(line));

            else if (stripped == "notifications:")
                config.Notifications = ParseStringMap(ctx, Indent(line));

            else if (stripped == "stages:")
            {
                ctx.Advance();
                config.Stages = ParseStages(ctx);
                continue;
            }

            ctx.Advance();
        }

        return config;
    }

    // ── YAML section parsers ──────────────────────────────────────────────────

    private static List<DeployFlowStage> ParseStages(ParseContext ctx)
    {
        var stages  = new List<DeployFlowStage>();
        int baseInd = -1;

        while (ctx.HasMore)
        {
            string raw      = ctx.Current;
            string trimmed  = StripComment(raw).TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) { ctx.Advance(); continue; }

            int    ind         = Indent(raw);
            string trimmedAll  = trimmed.Trim();   // fully trimmed (no leading spaces)

            if (baseInd < 0 && trimmedAll.StartsWith("- ")) baseInd = ind;
            if (baseInd >= 0 && ind < baseInd && !trimmedAll.StartsWith("-")) break;

            if (trimmedAll.StartsWith("- ") || trimmedAll == "-")
            {
                var stage = new DeployFlowStage();
                // inline after dash
                string rest = trimmedAll.TrimStart('-').Trim();
                if (rest.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    stage.Name = ExtractValue(rest);

                int stageInd = ind + 2;  // properties indented further than "-"
                ctx.Advance();

                while (ctx.HasMore)
                {
                    string sRaw      = ctx.Current;
                    string sTrimmedE = StripComment(sRaw).TrimEnd();
                    if (string.IsNullOrWhiteSpace(sTrimmedE)) { ctx.Advance(); continue; }
                    int sInd = Indent(sRaw);

                    string sTrimmedAll = sTrimmedE.Trim();
                    if (sInd < stageInd && !sTrimmedAll.StartsWith("-")) break;
                    if (baseInd >= 0 && sInd == baseInd && sTrimmedAll.StartsWith("-")) break;

                    string sTrimmed = sTrimmedAll;

                    if (sTrimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                        stage.Name = ExtractValue(sTrimmed);
                    else if (sTrimmed.StartsWith("parallel:", StringComparison.OrdinalIgnoreCase))
                        stage.Parallel = ExtractValue(sTrimmed).Equals("true", StringComparison.OrdinalIgnoreCase);
                    else if (sTrimmed.StartsWith("continue_on_failure:", StringComparison.OrdinalIgnoreCase))
                        stage.ContinueOnFailure = ExtractValue(sTrimmed).Equals("true", StringComparison.OrdinalIgnoreCase);
                    else if (sTrimmed.StartsWith("depends_on:", StringComparison.OrdinalIgnoreCase))
                    {
                        string deps = ExtractValue(sTrimmed).Trim('[', ']');
                        stage.DependsOn = deps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        // Might be multi-line list
                        if (deps.Length == 0 || deps == "[]")
                        {
                            ctx.Advance();
                            while (ctx.HasMore)
                            {
                                string dRaw = StripComment(ctx.Current).Trim();
                                if (!dRaw.StartsWith("-")) break;
                                stage.DependsOn.Add(dRaw.TrimStart('-').Trim());
                                ctx.Advance();
                            }
                            continue;
                        }
                    }
                    else if (sTrimmed == "steps:")
                    {
                        ctx.Advance();
                        stage.Steps = ParseSteps(ctx, sInd + 2);
                        continue;
                    }

                    ctx.Advance();
                }

                stages.Add(stage);
            }
            else
            {
                ctx.Advance();
            }
        }

        return stages;
    }

    private static List<DeployFlowStep> ParseSteps(ParseContext ctx, int minIndent)
    {
        var steps   = new List<DeployFlowStep>();
        int baseInd = -1;

        while (ctx.HasMore)
        {
            string raw     = ctx.Current;
            string trimmed = StripComment(raw).TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) { ctx.Advance(); continue; }

            int ind = Indent(raw);
            if (baseInd < 0 && trimmed.Trim().StartsWith("- ")) baseInd = ind;
            if (ind < minIndent - 2) break;
            if (ind < baseInd) break;

            if (trimmed.Trim().StartsWith("- ") || trimmed.Trim() == "-")
            {
                var step = new DeployFlowStep();
                string rest = trimmed.Trim().TrimStart('-').Trim();
                if (rest.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    step.Name = ExtractValue(rest);

                ctx.Advance();

                while (ctx.HasMore)
                {
                    string sRaw     = ctx.Current;
                    string sTrimmed = StripComment(sRaw).TrimEnd();
                    if (string.IsNullOrWhiteSpace(sTrimmed)) { ctx.Advance(); continue; }
                    int sInd = Indent(sRaw);
                    if (sInd <= baseInd) break;

                    sTrimmed = sTrimmed.Trim();
                    if      (sTrimmed.StartsWith("name:",    StringComparison.OrdinalIgnoreCase)) step.Name    = ExtractValue(sTrimmed);
                    else if (sTrimmed.StartsWith("type:",    StringComparison.OrdinalIgnoreCase)) step.Type    = ExtractValue(sTrimmed);
                    else if (sTrimmed.StartsWith("command:", StringComparison.OrdinalIgnoreCase)) step.Command = ExtractValue(sTrimmed);
                    else if (sTrimmed.StartsWith("timeout:", StringComparison.OrdinalIgnoreCase)) step.Timeout = int.TryParse(ExtractValue(sTrimmed), out var t) ? t : 300;
                    else if (sTrimmed.StartsWith("retry:",   StringComparison.OrdinalIgnoreCase)) step.Retry   = int.TryParse(ExtractValue(sTrimmed), out var r) ? r : 0;
                    else if (sTrimmed.StartsWith("env:",     StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Advance();
                        step.Env = ParseStringMap(ctx, sInd + 2);
                        continue;
                    }

                    ctx.Advance();
                }

                steps.Add(step);
            }
            else
            {
                ctx.Advance();
            }
        }

        return steps;
    }

    private static Dictionary<string, string> ParseStringMap(ParseContext ctx, int minIndent)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ctx.Advance();

        while (ctx.HasMore)
        {
            string raw     = ctx.Current;
            string trimmed = StripComment(raw).TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) { ctx.Advance(); continue; }
            if (Indent(raw) < minIndent) break;

            trimmed = trimmed.Trim();
            int colon = trimmed.IndexOf(':');
            if (colon > 0)
            {
                string key = trimmed[..colon].Trim();
                string val = trimmed[(colon + 1)..].Trim().Trim('"').Trim('\'');
                map[key]   = val;
            }

            ctx.Advance();
        }

        return map;
    }

    private static List<string> ParseStringList(ParseContext ctx, int minIndent)
    {
        var list = new List<string>();
        ctx.Advance();

        while (ctx.HasMore)
        {
            string raw     = ctx.Current;
            string trimmed = StripComment(raw).TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) { ctx.Advance(); continue; }
            if (Indent(raw) < minIndent) break;

            trimmed = trimmed.Trim();
            if (trimmed.StartsWith("- "))
                list.Add(trimmed[2..].Trim());

            ctx.Advance();
        }

        return list;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string StripComment(string line) =>
        Regex.Replace(line, @"\s*#.*$", "");

    private static string ExtractValue(string kv)
    {
        int colon = kv.IndexOf(':');
        if (colon < 0) return kv.Trim();
        return kv[(colon + 1)..].Trim().Trim('"').Trim('\'');
    }

    private static int Indent(string line) =>
        line.Length - line.TrimStart().Length;

    // ── Context ───────────────────────────────────────────────────────────────

    private sealed class ParseContext(string[] lines)
    {
        private int _pos;
        public bool   HasMore  => _pos < lines.Length;
        public string Current  => lines[_pos];
        public void   Advance() => _pos++;
    }
}

// ── Config models ─────────────────────────────────────────────────────────────

public sealed class DeployFlowConfig
{
    public string                       Version        { get; set; } = "1";
    public string?                      Name           { get; set; }
    public int                          TimeoutMinutes { get; set; } = 60;
    public string?                      OnFailure      { get; set; }   // rollback | notify | ignore
    public string?                      OnSuccess      { get; set; }   // notify | ignore
    public List<DeployFlowStage>        Stages         { get; set; } = [];
    public Dictionary<string, string>   Env            { get; set; } = [];
    public List<string>                 Cache          { get; set; } = [];
    public Dictionary<string, string>   Notifications  { get; set; } = [];

    public bool HasStages => Stages.Count > 0;
}

public sealed class DeployFlowStage
{
    public string              Name              { get; set; } = "";
    public bool                Parallel          { get; set; }
    public bool                ContinueOnFailure { get; set; }
    public List<string>        DependsOn         { get; set; } = [];
    public List<DeployFlowStep> Steps            { get; set; } = [];
}

public sealed class DeployFlowStep
{
    public string                      Name    { get; set; } = "";
    public string                      Type    { get; set; } = "command";
    public string                      Command { get; set; } = "";
    public int                         Timeout { get; set; } = 300;
    public int                         Retry   { get; set; }
    public Dictionary<string, string>  Env     { get; set; } = [];
}
