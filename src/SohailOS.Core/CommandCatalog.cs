using System.Collections.ObjectModel;

namespace SohailOS.Core;

public sealed record SemanticCommand(
    string Trigger,
    string Layer,
    string Purpose,
    bool RequiresGovernance,
    bool IsApprovalIntent = false,
    bool GrantsAuthorization = false);

public sealed record CommandResolution(
    string Trigger,
    bool Found,
    SemanticCommand? Command = null);

public static class CommandCatalog
{
    private static readonly IReadOnlyList<SemanticCommand> Commands =
        new ReadOnlyCollection<SemanticCommand>(
        new[]
        {
            new SemanticCommand("/scope-first","lifecycle","Establish scope and boundaries.",false),
            new SemanticCommand("/goal","lifecycle","Set the current project goal.",false),
            new SemanticCommand("/plan","lifecycle","Create an implementation plan.",false),
            new SemanticCommand("/architecture","analysis","Inspect or evolve architecture.",false),
            new SemanticCommand("/deepdive","analysis","Deep repository investigation.",false),
            new SemanticCommand("/first-principles","analysis","Reason from first principles.",false),
            new SemanticCommand("/assumptions","analysis","Expose assumptions.",false),
            new SemanticCommand("/tradeoffs","analysis","Compare architectural trade-offs.",false),
            new SemanticCommand("/rootcause","analysis","Find root causes.",false),
            new SemanticCommand("/research","research","Research relevant evidence.",false),
            new SemanticCommand("/deep-research","research","Perform deeper evidence gathering.",false),
            new SemanticCommand("/academic-research","research","Use scholarly evidence.",false),
            new SemanticCommand("/evidence","evidence","Collect evidence.",false),
            new SemanticCommand("/sources","evidence","Enumerate sources.",false),
            new SemanticCommand("/fact-check","evidence","Check factual claims.",false),
            new SemanticCommand("/citation-check","evidence","Check citations.",false),
            new SemanticCommand("/gaps","analysis","Identify evidence or implementation gaps.",false),
            new SemanticCommand("/audit","analysis","Audit repository state.",false),
            new SemanticCommand("/critique","analysis","Critique a proposed solution.",false),
            new SemanticCommand("/compare","analysis","Compare alternatives.",false),
            new SemanticCommand("/benchmark","quality","Benchmark implementations.",false),
            new SemanticCommand("/redteam","security","Run a bounded red-team workflow.",true),
            new SemanticCommand("/threat-model","security","Build a threat model.",false),
            new SemanticCommand("/security-review","security","Review security boundaries.",false),
            new SemanticCommand("/secrets-scan","security","Scan for secrets.",false),
            new SemanticCommand("/failuremodes","security","Enumerate failure modes.",false),
            new SemanticCommand("/stressTest","quality","Stress test a governed component.",false),
            new SemanticCommand("/premortem","analysis","Perform a pre-mortem.",false),
            new SemanticCommand("/inversion","analysis","Use inversion to find failure paths.",false),
            new SemanticCommand("/code","implementation","Implement approved code changes.",true),
            new SemanticCommand("/debug","implementation","Diagnose implementation failures.",false),
            new SemanticCommand("/fix","implementation","Apply a bounded fix.",true),
            new SemanticCommand("/refactor","implementation","Refactor without changing intended behavior.",true),
            new SemanticCommand("/optimize","implementation","Optimize after evidence.",true),
            new SemanticCommand("/trace","analysis","Trace execution and side effects.",false),
            new SemanticCommand("/smell","quality","Detect code smells.",false),
            new SemanticCommand("/hotspot","quality","Find complexity hotspots.",false),
            new SemanticCommand("/depscan","security","Inspect dependencies.",false),
            new SemanticCommand("/test","quality","Run or add unit tests.",false),
            new SemanticCommand("/test-integration","quality","Run integration tests.",false),
            new SemanticCommand("/test-e2e","quality","Run end-to-end tests.",false),
            new SemanticCommand("/coverage","quality","Inspect coverage gaps.",false),
            new SemanticCommand("/mutation-test","quality","Assess test strength.",false),
            new SemanticCommand("/code-review","quality","Review code.",false),
            new SemanticCommand("/review","quality","Review a change set.",false),
            new SemanticCommand("/diff","repository","Inspect changes.",false),
            new SemanticCommand("/commit","repository","Create a commit.",true),
            new SemanticCommand("/verify","evidence","Independently verify an outcome.",false),
            new SemanticCommand("/validate","evidence","Validate verified output.",false),
            new SemanticCommand("/status","meta","Report repository and execution state.",false),
            new SemanticCommand("/roadmap","planning","Maintain the roadmap.",false),
            new SemanticCommand("/prioritize","planning","Prioritize work.",false),
            new SemanticCommand("/bottleneck","planning","Find bottlenecks.",false),
            new SemanticCommand("/risks","planning","Enumerate risks.",false),
            new SemanticCommand("/milestones","planning","Track milestones.",false),
            new SemanticCommand("/actionplan","planning","Turn findings into actions.",false),
            new SemanticCommand("/nextstep","planning","Select the next coherent step.",false),
            new SemanticCommand("/estimate","planning","Estimate work.",false),
            new SemanticCommand("/mvp-first","planning","Prefer the smallest useful increment.",false),
            new SemanticCommand("/permissions","security","Inspect effective permissions.",false),
            new SemanticCommand("/approve","governance","Express approval intent for a specific operation.",false,true,false),
            new SemanticCommand("/guardrail","governance","Inspect or strengthen guardrails.",false),
            new SemanticCommand("/no-autopilot","governance","Disable autonomous mutation.",false),
            new SemanticCommand("/temporary","governance","Limit an operation to a temporary scope.",false),
            new SemanticCommand("/reconcile","evidence","Reconcile expected and actual state.",false),
            new SemanticCommand("/provenance","evidence","Inspect provenance.",false),
            new SemanticCommand("/fingerprint","capability","Inspect capability fingerprints.",false),
            new SemanticCommand("/capability","capability","Inspect capability state.",false),
            new SemanticCommand("/capability-attest","capability","Create or refresh capability attestation.",false),
            new SemanticCommand("/provider-route","model","Route to a provider.",false),
            new SemanticCommand("/taskgraph","orchestration","Build or inspect a task graph.",false),
            new SemanticCommand("/knowledge-fabric","knowledge","Operate knowledge-fabric workflows.",false),
            new SemanticCommand("/memory-fabric","memory","Operate memory-fabric workflows.",true),
            new SemanticCommand("/mutation-gate","governance","Evaluate a mutation boundary.",false),
            new SemanticCommand("/independent-verify","evidence","Run an independent verifier.",false),
            new SemanticCommand("/toctou-check","security","Revalidate state before mutation.",false),
            new SemanticCommand("/replay-check","security","Check replay protection.",false),
            new SemanticCommand("/least-privilege","security","Compare required and granted capability scope.",false),
            new SemanticCommand("/rollback","execution","Rollback a reversible operation.",true),
            new SemanticCommand("/preview","execution","Preview a mutation.",false),
            new SemanticCommand("/dry-run","execution","Execute a non-mutating simulation.",false),
            new SemanticCommand("/run","execution","Run a bounded operation.",true),
            new SemanticCommand("/retry","execution","Retry only under retry policy.",true),
            new SemanticCommand("/grant","governance","Inspect configured collaboration grant.",false),
            new SemanticCommand("/merge-main","repository","Merge a reviewed change into main.",true),
            new SemanticCommand("/deploy","execution","Deploy after deployment gates pass.",true),
            new SemanticCommand("/access-audit","security","Audit access usage.",false),
            new SemanticCommand("/fork-audit","repository","Audit a fork.",false),
            new SemanticCommand("/capability-import","repository","Import a reviewed capability from a fork.",true),
            new SemanticCommand("/provider-health","model","Probe provider health.",false),
            new SemanticCommand("/command-audit","governance","Audit command catalog integrity.",false)
        });

    public static IReadOnlyList<SemanticCommand> CreateDefault() => Commands;

    public static CommandResolution Resolve(string trigger)
    {
        var normalized = trigger?.Trim() ?? string.Empty;
        var command = Commands.FirstOrDefault(
            x => string.Equals(x.Trigger, normalized, StringComparison.OrdinalIgnoreCase));
        return command is null
            ? new CommandResolution(normalized, false)
            : new CommandResolution(normalized, true, command);
    }
}
