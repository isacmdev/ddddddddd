## CodeGraph

For structural or codebase questions, CodeGraph MUST be used before broad
filesystem exploration.

Use CodeGraph for:

- architecture and repository maps;
- call flows and dependencies;
- symbol references and impact analysis;
- understanding how a feature works;
- deciding what code to edit;
- reviewing changes that affect existing behavior.

Before using broad file search:

1. Resolve the Git project root.
2. Check whether `.codegraph/` exists.
3. If it does not exist and CodeGraph is available, initialize it once.
4. Use CodeGraph to explore the relevant symbols or files.
5. Fall back to regular filesystem tools only if CodeGraph fails.

After editing code, use CodeGraph again when the index has synchronized to
inspect affected symbols and dependencies.
