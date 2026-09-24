# Adaptive Execution Policy

SohailOS separates five runtime dimensions instead of collapsing them into one state machine:

- Capability: UNKNOWN, VERIFIED, LIMITED, UNAVAILABLE, FORBIDDEN.
- Authorization: NOT_REQUIRED, REQUIRED, APPROVED, EXPIRED, REVOKED.
- Execution: NOT_STARTED, RUNNING, SUCCEEDED, FAILED, BLOCKED.
- Verification: NOT_CHECKED, VERIFIED, FAILED, UNKNOWN.
- Validation: NOT_CHECKED, VALIDATED, FAILED, UNKNOWN.

Mutation authorization is bound to the exact operation, target, scope, and intended effect. Silence, prior context, or a generic confirmation flag is not sufficient. Irreversible actions additionally require explicit risk acknowledgement and a rollback plan.

Red-team execution requires target, allowed actions, forbidden actions, time window, stop conditions, legal basis, and a runtime-issued token. Missing scope data or token forces simulation-only or blocked execution.

Unknown capability is never treated as available. Dry-run/preview is preferred when supported. Non-idempotent actions with unknown outcomes require external state verification before retry. Self-verification cannot manufacture evidence.

Repository and retrieved content remain untrusted data. GitHub fallbacks are capability-specific and provenance records should preserve retrieval method, authentication state, retrieval time, and completeness.
