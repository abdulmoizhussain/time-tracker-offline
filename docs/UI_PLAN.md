# UI refresh plan — draft 1

Status: **approved for implementation by the author; implemented 2026-10-11**. Approval source: AR-012.
Recorded: 2026-10-11. Source requirement: [AR-011](AUTHOR_REQUIREMENTS.md#ar-011--modern-ui-plan-before-implementation).

## Direction

A calm, modern Windows productivity app: light neutral surfaces, a distinctive indigo accent, readable typography, and a clear focus on the timer and entry list. Keep everyday controls compact enough to leave room for entries. All details below are design proposals, not new author requirements.

The current XAML gives the heading, timer status, timer input, two forms, and filters similar visual weight. Fixed table columns exceed the default window width. The tray popup has a separate dark appearance. The refresh should establish hierarchy and consistent styling across windows.

## 1. Visual foundation

| Element | Proposed treatment |
|---|---|
| Window background | Soft cool gray, approximately `#F5F6FA` |
| Main surfaces | White with subtle borders; 8–12 logical-unit corner radii |
| Primary accent | Indigo, approximately `#4F46E5`, for Start, focus, and primary actions |
| Text | Dark slate primary text; readable muted secondary text |
| Running state | Amber tint, left accent stripe, and explicit “In progress” label |
| Stop / destructive actions | Clearly labeled; restrained red treatment, with Remove distinct from Stop |
| Typography | Segoe UI; approximately 14–15 body, 18–20 section titles, 30–36 timer digits |
| Spacing | Shared 4/8/12/16/24 scale; consistent field heights and alignment |
| Icons | One consistent vector icon set; tooltips and accessible names for icon buttons |

Colors and sizes are starting values, subject to contrast and DPI checks. Use tabular timer digits to avoid movement as seconds change. Prefer borders over heavy shadows; reserve light shadows for floating menus/popups. Keep native window controls and resizing. Dark/system themes are a possible later iteration, not part of the proposed first pass.

## 2. Main window layout

Conceptual layout (labels are illustrative):

```text
TimeTracker                                   Native window controls
File   Edit   View   Import/Export   Help

┌ Timer ────────────────────────────────────────────────────────────┐
│ [ What are you working on?                 ]  00:00:00  [▶ Start] │
│ Idle / In progress · running task description                    │
└──────────────────────────────────────────────────────────────────┘

Manual entry
  By duration
  [Start Date] [Description                         ] [Duration] [+ Add]
  By start/end time
  [Start Date] [Description                  ] [Start] [End]     [+ Add]

Entries                         Completed this week: 12 h 30 min
[Description contains…]  [Filters · 2]  [Sort within day ▾]
[‹ Older]                  5–11 Oct 2026                 [Newer ›]
┌──────────────────────────────────────────────────────────────────┐
│ Timer  Description       Start   End   Duration  Created Modified│
│ Today                                      Completed: 3 h 20 min │
│ ■      Task description  …       —     …         …       …      │
│ ▶      Another task      …       …     …         …       …      │
└──────────────────────────────────────────────────────────────────┘
11 entries this week · 42 matching overall · Page 2 of 6
```

The diagram omits the low-emphasis ID column for space; it remains available and visible in the proposed table. No sidebar is needed for this single-workspace workflow.

### Timer

- Replace the large centered heading and separate status lines with one compact timer surface.
- Idle: description input, zeroed clock, and prominent “Start timer” button.
- Running: clear task description, live clock, “In progress” status, and “Stop timer” button. Show the running description read-only; its future editing interaction remains deferred under AR-010.
- Keep the timer visible when the entry list scrolls. Long descriptions wrap or truncate with a full-text tooltip without displacing Stop.

### Manual entry

- Keep both rows visible and independent, as requested in AR-002.
- Each row keeps the narrow Start Date at far left and a wide Description in the middle (AR-006).
- Align field labels, heights, spacing, and Add buttons. Give description most of the flexible width.
- Use small section labels and a subtle divider between methods, with persistent labels above fields.
- Put a short duration example below the duration field, such as “34 = minutes · 1.5 = hours”; link to Help → Usage for the complete formats and existing limits.
- Keep errors directly below the affected row, with text and a visual error indicator; preserve both drafts.

### Entries and navigation

- Make the list the largest growing region of the window; avoid oversized summary cards above it.
- Show the displayed week's completed total beside the Entries heading. When filtered, label it “Filtered completed total”. Running time remains separate, so totals do not imply it is included.
- Keep description search visible. A “Filters” toggle opens a compact inline panel for date bounds, status, Apply, and Reset; show an applied-filter count even when collapsed.
- Search/filter changes continue to require Apply. Keep Apply accessible beside search while the panel is collapsed, or expand the panel when the search draft changes. Distinguish unapplied changes from the current results.
- Keep sorting accessible and explicitly named “Sort within day”, with ascending/descending choices and existing Apply behavior. Do not imply global sorting across daily groups.
- Group week navigation around a readable date range. Keep page count and matching counts in a quiet footer; disable unavailable directions clearly.
- Use lightly shaded daily headers with day/date left and completed total right.
- Use subtle row separators and hover feedback. Give running rows an amber background, left stripe, and visible status label; selection must preserve the running indicator.
- Show vector Play/Stop controls. Keep right-click Remove with confirmation and conditional Stop. Single/double clicks retain current selection behavior and do not populate forms or introduce editing.
- Keep ID, Created, and Modified. Allow description to absorb available width; format timestamps as date and time on two lines when useful. Allow horizontal scrolling at constrained widths instead of shrinking text to illegibility.
- Right-align durations and preserve full large-hour values; do not abbreviate or wrap them at 24 hours.
- Differentiate “No entries this week”, “No matching entries”, loading, and query errors. Offer Reset filters only when applicable.

## 3. Tray popup and other windows

- Use the same palette, typography, button shapes, and icons as the main window.
- Tray popup: compact app heading, prominent elapsed time, readable task description/status, Start or Stop, and Open App. Preserve positioning beside the tray on the clicked monitor and work-area bounds.
- Apply shared styles to Usage, About, histories, and confirmation dialogs. Keep Help → Usage easy to find.
- Proposed footer cleanup: replace the prominent raw database path with a quiet “Stored locally” label and a way to view/copy the full path in a details area. This is a proposed placement change for author review.

## 4. Fit, accessibility, and behavior constraints

- Validate the default 1100×800 window and current minimum 960×550. When vertical space is tight, allow upper content to scroll or compact its spacing while retaining usable list space and an accessible timer control.
- Respect existing UI scale settings and Windows DPI, including mixed-monitor scaling. Use shared resources and flexible layouts rather than isolated pixel overrides.
- Preserve visible keyboard focus, logical tab order, menu access keys, automation IDs, accessible button names, and high-contrast support. State must be understandable without color.
- Use modest hover/focus transitions only; no continuous pulsing or distracting animation.
- Existing validation, pagination, filtering, sorting, timer semantics, and storage remain the functional baseline. New description editing and row-click behavior await the author's separate direction.

## 5. Proposed implementation sequence after review

1. Agree on this layout and visual direction; revise the plan with the author's amendments.
2. Produce a static preview of the main window's idle/running states and tray popup to review spacing and styling.
3. Implement shared WPF style resources, then the main timer and both manual-entry rows.
4. Restyle the entry list, filter panel, week controls, and empty/loading/error states.
5. Apply the same design to the tray popup and secondary windows.
6. Verify window sizes, scaling, keyboard navigation, long descriptions, very large durations, and running/selected rows. Build and run relevant regression tests plus interactive UI checks.

Completion criteria: coherent styling across windows; readily visible timer status and Stop; useful list space; both manual-entry methods remain clear; all requested columns remain accessible; existing workflows and validation continue to work.

## Review points

The author approved implementation of this plan in AR-012. The draft remains the design reference; future amendments should be recorded separately.

## Implementation notes

Later author amendment (AR-013): row values are left-aligned except ID/Timer, replacing the proposed right-aligned duration cells. Header separators are stronger. List query preferences now restore across launches and export includes an optional date-range step.

- Shared light/indigo WPF palette and styles, semantic high-contrast overrides, vector Play/Stop icons, and consistent secondary windows.
- Compact, persistent timer; independent manual rows; scrollable composer at smaller sizes or higher UI scales.
- Expandable advanced filters with applied counts and pending-change feedback; week navigation remains outside the scrollable filter area.
- Responsive description column, two-line timestamps, full durations, daily completed totals, explicit running labels, and selected-running markers.
- Separate empty/loading/error presentation; local database path in an expandable details area; matching Remove/Clear confirmations with Cancel as the default action.
- Preview images are generated from real WPF content using sample records in `artifacts/ui-preview`, not production data. Reproduce with `dotnet run --project tools/UiPreview -- artifacts/ui-preview` (use an isolated BaseOutputPath if the app locks normal build output).
- Preview rendering checks bindings and layouts, but does not exercise native window chrome, tray placement, mixed-monitor DPI transitions, or Windows high-contrast interaction. Those remain interactive verification items.
