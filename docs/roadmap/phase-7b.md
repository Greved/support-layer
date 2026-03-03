[← Index](index.md) · [← Phase 7](phase-7.md) · [Phase 8 →](phase-8.md)

---

## Phase 7b — Visual Dialog Flow Editor
**Goal:** Add a drag-and-drop canvas view to the procedure editor. Non-technical users (support managers, business owners) can design procedures visually. Developers can stay in list view. Both views edit the same data.

### Why add it
The text-based step editor from Phase 7 is sufficient for technical users but creates friction for conversation designers who think in flowcharts. Voiceflow's entire market position rests on this insight. Adding a canvas:
- Lowers the skill floor for building complex branching procedures — no JSON, no code
- Makes procedures auditable for non-technical stakeholders ("show me what the bot does")
- Enables simulation replay visualization (see exactly which steps fired, highlighted on the canvas)
- Closes the UX gap vs Voiceflow without changing the execution model at all

The `procedure_steps` table already has `on_true_step_id` / `on_false_step_id` — it is already a directed acyclic graph. A canvas is just a different rendering of the same data.

### Data model addition
```sql
ALTER TABLE procedure_steps
  ADD COLUMN canvas_x FLOAT DEFAULT NULL,
  ADD COLUMN canvas_y FLOAT DEFAULT NULL;
-- NULL = position managed by auto-layout; set on first manual drag
```

### Node shapes by step type

| Step type | Shape | Color |
|-----------|-------|-------|
| `instruction` | Rectangle | Blue |
| `condition` | Diamond | Amber |
| `tool_call` | Hexagon | Purple |
| `sub_procedure` | Rounded rect + chain icon | Teal |
| `checkpoint` | Circle + pause icon | Yellow |
| `escalate` | Rect + arrow-right | Red |
| `end` | Filled circle | Gray |

Condition steps have two outgoing edges labelled **Yes** (green) and **No** (red). Non-linear jumps (steps that skip ahead) render as curved arrows.

### Implementation

- **React Flow** (`@xyflow/react`) — MIT license, de facto standard for React flow editors, used across production chatbot builders; free for commercial use
- Custom node components per step type using shadcn/ui + Tailwind
- **Dagre** layout algorithm for "Auto-arrange" button
- **Toggle view**: List view ↔ Canvas view button in procedure editor header; both edit the same `procedure_steps` rows
- **Step edit drawer**: clicking any node opens the same form components from Phase 7's list editor, in a side drawer
- **Simulation replay**: after a simulation run, executed steps are highlighted (green = passed, red = failed branch) using `conversation_json` from `simulation_runs`

### Tasks
- [ ] Add `canvas_x`, `canvas_y` float columns to `procedure_steps` migration
- [ ] Install `@xyflow/react` + `dagre` in portal SPA
- [ ] Custom React Flow node components for each step type (icon + label + typed connection handles)
- [ ] "List / Canvas" toggle button in procedure editor header
- [ ] Step edit side drawer: reuse all Phase 7 form components; triggered by clicking a node
- [ ] Drag from step type palette to add new steps; auto-connect to the currently selected node
- [ ] "Auto-arrange" button: compute Dagre layout client-side, persist new `canvas_x/y` values
- [ ] Simulation replay mode: color-code executed steps from `simulation_runs.conversation_json`
- [ ] Mini-map + zoom/fit controls for large procedures

### Tests (Phase 7b)
- **Component (React Testing Library):** Canvas renders correct node count for a 5-step procedure; condition node renders two labeled edges
- **E2E (Playwright):**
  - Build a 3-step procedure with one condition branch using only the canvas (no list view used) → save → switch to list view → steps match
  - Run simulation → canvas replay highlights correct execution path
  - Auto-arrange button repositions all nodes without overlap

### Quality Gate ✅
- A non-developer user can build a 4-step procedure with one condition branch using only the canvas in < 8 minutes (UX test)
- Canvas and list view always show the same data — editing either one is immediately reflected in the other
- Simulation replay highlights the correct executed path on the canvas
- React Flow MIT license confirmed; no commercial licensing cost incurred at the core library level

---
