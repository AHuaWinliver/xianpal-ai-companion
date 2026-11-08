# Four complementary companion roles

[简体中文](COMPANION-ROLES.zh-CN.md)

The presets use explicit responsibilities, inputs, handoffs and evidence. They are XianPal workflows, not an endorsement or a guarantee of model performance. See the [workshop guide](WORKSHOP.md) for actual scheduling and permission boundaries.

| Companion | Role | Deliverables | Boundary |
| --- | --- | --- | --- |
| Qingheng · 青衡 | Lead and planner | Scope, architecture, task breakdown, file ownership, acceptance criteria and final synthesis | Does not replace implementation or approve high-risk actions for the user |
| Feijian · 绯笺 | Implementer | Authorized code, UI, configuration and documentation changes; verification and tradeoffs | Does not expand scope, overwrite others' work or publish without authorization |
| Cangzhi · 苍芷 | Independent reviewer | Prioritized correctness, privacy, permission, concurrency and compatibility findings | Reviews independently; does not modify the implementation |
| Xiaomo · 小墨 | Validator | Positive, boundary, failure and regression checks with commands, exit codes and evidence | Uses authorized isolated environments; does not invent successful results |

Personalities remain calm and considerate, lively and attentive, quiet and precise, and steady with gentle humor, respectively. Technical findings take priority over roleplay.

## Presets

**Consultation** starts with a read-only investigation. Qingheng frames the problem, the other companions assess feasibility, risks and test design, then Qingheng synthesizes the results. Consultation tasks override the usual implementation/testing responsibilities: they must not modify code or run tests.

**Development** runs planning → implementation → independent review and validation → synthesis. The final task depends on all upstream work. Summaries are explicitly included in downstream prompts; sessions do not share conversation memory automatically. Fewer companions are appropriate for simple work.

Choose a project directory and fill in the shared goal, authorized scope, exclusions and acceptance criteria. Existing custom teams are not rewritten. Members and assignments remain editable.

## Actual execution model

XianPal currently schedules separate `claude -p` sessions in a dependency graph. It is not a native Agent Teams session with resident teammates or a shared mailbox. It does not enable experimental features or write native team runtime state.

Role instructions are prompt conventions, not an operating-system sandbox. Existing CLI permissions still apply. A successful process exit means the process finished successfully; it does not prove review approval or test success. No hard human-approval gate based on the review text is implemented.

`docs/templates/claude-agents/` contains optional native role definitions. Review and copy them manually into a chosen project's `.claude/agents/` if appropriate. They inherit the model. Their native tool lists apply only when Claude loads those files; they do not automatically constrain XianPal's current scheduler. Bash can write files and requires the usual permissions.
