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

### Frontend Design
> Reference: `docs/references/design/stitch_stark_fintech_prd/procedure_editor/`

**Canvas view appearance:**
- Full-width canvas area with a subtle dot-grid background; node shapes per step type (see node table above)
- Toolbar strip on the left edge: icons for each node type — drag from toolbar onto canvas to add a new step
- Top bar: procedure name + STATUS badge (ACTIVE / DRAFT) left; "Simulate" secondary button + "Save Procedure" primary button right; "List / Canvas" toggle in header
- Connecting edges: straight arrows between sequential steps; condition nodes (diamond) emit two labeled edges — "Yes" (green) / "No" (red); non-linear jumps rendered as curved arrows
- Node selected state: highlighted border; opens step-edit side drawer (reuses Phase 7 form components) without leaving canvas
- Right panel (step-edit drawer): ATTRIBUTES section listing context attributes available to the step; CONNECTED TOOLS section showing linked tools with connection status chip (Connected = green dot, Inactive = gray) and endpoint info; "+ Connect Tool" button

**Simulation replay overlay:**
- After a simulation run, executed nodes highlighted: green fill = step passed / reached, red fill = step where failure/wrong branch occurred
- Arrows along the executed path highlighted in the corresponding color
- Non-executed branches remain at default opacity

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

#### E2E tests (Playwright .NET — portal)
- [ ] **Canvas renders:** Open an existing procedure with 5+ steps → click "Canvas" toggle → verify React Flow canvas appears with dot-grid background → verify correct node count matches step count → verify node shapes: instruction=rectangle (blue), condition=diamond (amber), tool_call=hexagon (purple), end=filled circle (gray)
- [ ] **List ↔ Canvas sync:** Build a 3-step procedure (instruction → condition → end) in list view → switch to canvas → verify 3 nodes present with correct shapes → drag condition node to new position → switch back to list view → verify steps unchanged → switch back to canvas → verify node in moved position
- [ ] **Canvas-only build:** Start new procedure → switch to canvas → drag Instruction node from left toolbar onto canvas → enter step instructions in side drawer → drag Condition node → connect Instruction → Condition edge → set Yes branch to End node → save → switch to list view → verify 3 steps with correct types and order
- [ ] **Condition edges:** Add a condition step on canvas → verify diamond shape has two outgoing edges labeled "Yes" (green) and "No" (red) → connect Yes branch to one step → connect No branch to another step → verify arrows render with correct colors
- [ ] **Step edit drawer:** Click any node on canvas → verify side drawer opens without leaving canvas → edit step instructions text → click Save → verify node label updates in canvas → verify drawer closes → switch to list view → verify updated text persists
- [ ] **Add node from toolbar:** Drag "Tool Call" node type from left toolbar onto canvas → verify hexagon appears → verify it can be connected to existing nodes by dragging edge between handles → verify added step appears in list view
- [ ] **Auto-arrange:** Create a procedure with 6+ steps in list view → switch to canvas → verify nodes may overlap (random positions) → click "Auto-arrange" button → verify nodes rearrange without overlap → verify all edges still connect correctly → save → reload → verify layout persists
- [ ] **Simulation replay on canvas:** Run a simulation → click "View on Canvas" (or switch to canvas) → verify executed path highlighted: passed steps green fill, failed/wrong-branch step red fill → verify edges along executed path highlighted in matching color → verify non-executed branches at reduced opacity
- [ ] **Mini-map and zoom:** Open procedure with 8+ steps on canvas → verify mini-map renders in corner → click mini-map region → verify main canvas scrolls to that region → use zoom controls (+/−) → verify canvas zooms → click "Fit" or "Reset" → verify all nodes visible in viewport
- [ ] **Node selection highlight:** Click a node → verify selection border appears → open drawer → press Escape → verify selection cleared → click canvas background (empty area) → verify no node selected

### Quality Gate ✅
- A non-developer user can build a 4-step procedure with one condition branch using only the canvas in < 8 minutes (UX test)
- Canvas and list view always show the same data — editing either one is immediately reflected in the other
- Simulation replay highlights the correct executed path on the canvas
- React Flow MIT license confirmed; no commercial licensing cost incurred at the core library level

---
