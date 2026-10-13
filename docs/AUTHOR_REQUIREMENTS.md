# Author Requirements

This document preserves the author's original requirements for TimeTracker so the author, contributors, and AI agents can check what was actually requested. Quoted text is the author's wording, including spelling, punctuation, and informal phrasing. Context and implementation notes are explicitly separate from those quotes.

## Source and scope

Initial record created on **2026-10-11 (Asia/Karachi)** from the messages available in the current development conversation, in their original order. Individual message timestamps are unavailable; this is the recording date, not a claimed timestamp for every request.

This record covers the feature requests and relevant diagnostic/clarification context available in that conversation. It does **not** claim to recover all requirements from the project's original creation. Earlier backlog/specification material remains in [TODO.md](TODO.md) and [SPEC.md](SPEC.md); do not present that material as verbatim author statements without its original source.

The quotes record intent, not proof that a feature works. [README.md](../README.md) describes current behavior, [TODO.md](TODO.md) tracks work, and the code/tests establish implementation. Later explicit author corrections take precedence; preserve earlier quotes and link the correction rather than rewriting history.

## Requirements index

| ID | Subject |
|---|---|
| [AR-001](#ar-001--entry-timestamps-tray-popup-and-running-description) | Entry timestamps, tray placement/Open App, running description |
| [AR-002](#ar-002--separate-manual-entry-rows) | Separate duration and start/end manual-entry rows |
| [AR-003](#ar-003--pagination-filtering-and-sorting) | Pagination with custom filters and sorting |
| [AR-004](#ar-004--help--usage-window) | Usage window under Help, including duration formats |
| [AR-005](#ar-005--highlight-the-running-entry) | Highlight the in-progress row |
| [AR-006](#ar-006--manual-entry-field-order-and-width) | Narrow Start Date at left; Description in the middle |
| [AR-007](#ar-007--investigate-the-extreme-week-total) | Investigate the 367233-hour total |
| [AR-008](#ar-008--strict-input-validation-with-unrestricted-large-duration-display) | Strict UI validation; preserve large-duration calculations/display |
| [AR-009](#ar-009--maintain-the-authors-original-requirements) | Maintain this requirements record |
| [AR-010](#ar-010--entry-context-actions-and-revert-clickediting-behavior) | Entry context actions, stop icon, and revert description/click editors |
| [AR-011](#ar-011--modern-ui-plan-before-implementation) | Modern UI plan for author review before implementation |
| [AR-012](#ar-012--implement-the-ui-plan) | Approval to implement the UI plan |
| [AR-013](#ar-013--table-alignment-persistent-queries-export-range-and-startup-option) | Table alignment, persistent queries, export range, and startup-option explanation |
| [AR-014](#ar-014--compact-input-fields) | Reduce input-field vertical padding |
| [AR-020](#ar-020--selectable-descriptions-suggestions-compact-rows-and-same-day-duration-placement) | Selectable descriptions, suggestions, compact rows, same-day duration placement |
| [AR-021](#ar-021--created-records-the-actual-addition-moment) | Created records the actual addition moment |
| [AR-022](#ar-022--unfinished-task-popup-initial-timer-duration-and-keyboard-suggestions) | Unfinished tasks, initial duration, keyboard suggestions |

## AR-001 — Entry timestamps, tray popup, and running description

Later correction: AR-010 withdraws the implemented running-description editors; the replacement design is deferred. Other parts of this request remain in scope.

Author's original request:

```text
ok lets start with these tasks,
we need to do these:
- need to show `Created` & `Modified` columns in the list view.
- the mini window that opens from the tray icon is getting opened in the mid of the screen. it should open right on the tray icon are (near bottom right)
- the `Open App` of the mini window is not opening the main application.
- I should be able to modify an ongoing task's description intuitively.
```

Implementation notes (agent decisions, not quoted requirements): local-time timestamp display; monitor/DPI-aware popup placement; explicit Save/Enter description editors in the main window and tray popup. The author did not prescribe the editor's exact controls or colors.

## AR-002 — Separate manual-entry rows

Later correction: AR-010 removes clicking-to-populate forms. The two manual-entry rows remain.

Author's original request:

```text
we now need to split the manual entry options into 2 rows. because adding by duration && adding by start time and end time are two different things i believe. right? both the rows should have the Description && Start Date options.
```

Implementation notes (agent decisions): independent drafts and save actions for the two rows; the duration row's selected date is persisted. AR-006 subsequently refines the field order and widths.

## AR-003 — Pagination, filtering, and sorting

Author's preceding question (context):

```text
the list view has pagination right?
```

Conversation context (not an author quote): the agent explained that the list loaded only the latest 500 entries and that Monday–Sunday week pagination was an open backlog item.

Author's original request:

```text
lets build this pagination thing.
also with the filter and sorting options, so that we can repopulate the listview with custom filter and sorting options in  the same paginated way.
```

Implementation notes (agent decisions): Monday–Sunday pages; description/date/status filters; sorting by Start, Description, Duration, Created, or Modified; Apply/Reset controls; filters/sorting retained during week navigation. Sorting within daily groups, inclusive date bounds, and keeping empty calendar weeks navigable were agent choices, not explicitly specified in the quoted request. The weekly calendar convention came from the existing backlog and preceding conversation context.

## AR-004 — Help → Usage window

Author's preceding question (context):

```text
pls remind me , what are the accepted formats of the Duration field?
```

Conversation context (not an author quote): the response listed `34`, `33:33`, `33:`, `:33`, `1:02:30`, `1.5`, and `5.3`, explaining minutes, minutes:seconds, hours:minutes:seconds, and decimal hours.

Author's original request:

```text
can we add this in some kind of Usage window?
inside Help button of the File bar.
```

Implementation notes (agent decisions): Help → Usage... opens a scrollable, resizable guide. Instructions for other app workflows were added alongside the duration examples. AR-008 subsequently adds validation guidance.

## AR-005 — Highlight the running entry

Author's original request:

```text
we now need to highlight the row that is currently in progress. if present in the listview.
```

Implementation notes (agent decisions): highlight uses `IsRunning`, an amber background/border, semibold text, and an “In progress” tooltip/accessibility status. Selected rows retain the running border. The author did not specify a color or ask for filters/pages to be changed to reveal the running row.

## AR-006 — Manual-entry field order and width

Author's original request:

```text
shift the Start Date box on the left most side, and make its width a little shorter. and put the description box on its right i.e. in the middle. 
for both the manual entry rows
```

Relationship: refines AR-002. Implementation uses a shared narrower date column and wider description column; the author did not prescribe an exact pixel width.

## AR-007 — Investigate the extreme week total

Author's original diagnostic request:

```text
how is this possible?
367233 hours?
you can access the .db file in the bin folder and validate the code as well how are we calculating the total hours?
```

Attachment context: the screenshot displayed “11 entries this week · 11 matching entries overall · Week total: 367233 h 11 min (completed entries)”. The screenshot is diagnostic evidence, not an additional instruction. Its original temporary attachment path is not a permanent project artifact.

Investigation findings (agent observations, not author requirements): the Debug database's 11 completed entries summed to 1,322,039,478 seconds; entries 8, 7, and 5 contained 333,333 hours, 33,333 hours, and 555 hours 33 minutes respectively. The total matched stored data. The row formatter used `TimeSpan.Hours`, hiding whole days by wrapping the displayed hours every 24 hours. The database does not retain the original duration input, so what was originally typed could not be established.

These are observations at investigation time, not a claim about the database's future contents. This diagnostic request did not authorize changing or deleting records.

## AR-008 — Strict input validation with unrestricted large-duration display

Author's original request:

```text
ok so we have to apply very restrictive validations on the UI level. but we dont have to block these kind of scenarios while the calculation. let the UI show any kind of large durations. handle the scenarios as well
```

Relationship: follows AR-007 and clarifies the distinction between input validation and calculation/display behavior.

Implementation notes (agent decisions, not explicit author limits):

- The **1 second–24 hours** manual-duration limit was chosen and announced by the agent. The author requested restrictive validation but did **not** specify that numeric limit.
- Strict time components, required dates, inline errors, overflow handling, and protection against changing a running timer's dates/times were implementation choices.
- Existing large durations are retained; rows, running elapsed time, totals, and exports show full hours without 24-hour wrapping. Totals are not capped to the manual-input limit.
- Imported durations can exceed the manual-input limit within the existing signed 32-bit per-entry seconds storage range. That range is an implementation constraint, not a user-requested business limit. Aggregate formatting supports signed 64-bit seconds without relying on `TimeSpan`'s range.
- No production database values were changed to make the total smaller.

Do not mistake the chosen 24-hour limit, storage type, or specific validation messages for verbatim author requirements. Revisit those choices if the author changes or clarifies the requirement.

## AR-009 — Maintain the author's original requirements

Author's original request:

```text
lets start maintaining a `author requirements` documents, where we need to keep the requirements maintained. we should keep my near-exact statements in there, so that I can and any other AI agent can go back check the actual/exact requirements that were originally provided while the tool creation.
```

Implementation notes (agent decisions): this document, stable AR identifiers, verbatim quotes from the available conversation, explicit separation of context/assumptions from author statements, and links from README/AGENTS.

## AR-010 — Entry context actions and revert click/editing behavior

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
the right click on a listview entry should show & perform these actions:

- remove an entry.
- stop a running, if it is currently in running/in-progess.



need to do some other things as well.

- the feature you have provided for modifying the descripton of an ongoing task is not very much intuitive. lets revert that. i will explain myself later in what way to do this.
- currently when i click on a task in the list view it sends that description to the manual entry boxes. lets not do that as well. may be the previous AI developed it that way. I will explain later what to do on single clicks on a list view item.
- the play button icon does not show stop icon on an ongoing task in the listview.
```

Relationship: supersedes the current running-description editor chosen for AR-001 and the click-to-edit implementation associated with AR-002. It does not remove the two manual-entry rows. The author explicitly defers the replacement description-editing and single-click interaction designs.

Implementation notes (agent choices): context actions target the right-clicked row independently of selection; removal retains confirmation; Stop is shown only for running rows. A running row's button shows ■ and stops that entry. Both description editors and single/double-click form population are removed. Ordinary selection remains, without changing manual drafts. Existing internal edit/repository methods are retained for future design work but are not exposed through row clicks.

## AR-011 — Modern UI plan before implementation

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
can you please create an improved UI plan for this tool? that looks good and catchy.
because this app has features but the looks are not really modern.
a little modern look make it feel good to use as well.
so just create a plan for first, I will tell you where to make amendents in the plan
```

Context (agent notes): [UI_PLAN.md](UI_PLAN.md) is a draft proposal for review. Colors, layout, filter-panel placement, and implementation phases are agent suggestions, not approved author requirements. This request authorizes planning only; the application's UI is not changed. AR-002/006's two manual rows and field order, and AR-010's deferred editing interactions, remain relevant constraints.

## AR-012 — Implement the UI plan

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
pls now implement the UI_PLAN.md
```

Relationship: approves implementation of the draft created for AR-011. The original AR-011 planning-only request remains unchanged as history. Implementation follows [UI_PLAN.md](UI_PLAN.md), preserving the two independent manual rows, narrow leftmost dates, and the deferred editing interactions of AR-010.

Implementation notes (agent choices): shared WPF theme resources; scrollable composer/filter controls at constrained sizes; persistent timer and week navigation; applied-filter counts and draft feedback; compact timestamps; matching destructive confirmation windows; isolated WPF preview helper. No new task-description editing interaction is introduced.

## AR-013 — Table alignment, persistent queries, export range, and startup option

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
- the column separator of the header row should have more bold separator color.
- the values of every row should be left aligned, currently they are center aligned. except only for the ID & Timer columns.
- the selected filter & sorting values should be persisting. on get remapped on next app start.
- Export option should give the date range option as well (as an optional thing) before asking to select the export directory.



After implementing all the above points, let me know, how can we provide an option to register this app as an startup application?
```

Relationship: refines AR-012; supersedes the UI plan's right-aligned duration cells. Startup registration is requested as an explanation, not an instruction to register or implement it now.

Implementation notes (agent decisions): darker two-unit header separators retain resizing; ID/Timer remain centered. Query preferences live beside each database in a `.view.json` file, preserving applied results and pending drafts separately; Apply/Reset and normal close/exit save them. Export's optional inclusive local-date range uses Start, then End, then Created, independent of list filters, excludes running entries, and has no former 10,000-record cap. The save-file dialog still chooses the directory and filename after range selection.

## AR-014 — Compact input fields

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
reduce the vertical paddings of all the input fields because the fields are taking quite much screen area of the app.
```

Implementation notes (agent choices): shared text fields and dropdowns use two-unit vertical padding and a 28-unit minimum height instead of 34. Date inputs also use compact internal text padding. Adjacent manual/filter action controls match the shorter fields. Font sizes and existing UI scaling are retained. Applies across main and secondary windows.

## AR-015 — Windows startup registration in File and tray menus

Recorded: 2026-10-11 (Asia/Karachi). Source: author messages in the current development conversation.

Author's preceding questions and implementation constraint (verbatim):

```text
ok about the start minimized to tray thing. when the app starts on the windows startup, will it be able to detect the windows lock/unlock events as well by being in the tray background?
also: adding & removing the current user's windows run registry key would require the admin access for the app or not?

DO NOT IMPLEMENT IT YET.
```

Author's subsequent authorization (verbatim):

```text
perfect. lets implement the changes now.

The launch at startup option should be in both the places:

- The File option.
- and the Tray icon right click menu as well.
```

Relationship: authorizes the startup implementation discussed in AR-013 and supersedes the preceding instruction to defer implementation.

Implementation notes (agent decisions): synchronized checkable menus read/write only the current user's `TimeTracker` Run value. Registration is opt-in and points to the built/published executable with `--startup`. Such launches initialize the tray without showing the main window, retaining the existing session-event subscription and configured recording. Normal launches still show/activate the app; duplicate startup launches do not bring it forward. Errors are surfaced and registry state is re-read. Moving the executable requires re-registration. Windows-level disabling/policy remains outside the app's registration toggle.

## AR-016 — Manual action alignment and left-side daily totals

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
- rename the button label `Add Range` to `+ Add with Range`
- and also correct the vertical alignment of this button and aldo the `+ Add` button.
- move the calculated hours info `Completed: 567 h 25 min` on left most side, exactly beside TOday/Yesterday text.
```

Implementation notes (agent decisions): keep Save/Save Range labels in edit mode. Both action buttons use the same label-space layout as adjacent fields and match the text input height. Daily totals sit immediately after the date label at the left; calculation and formatting are unchanged.

## AR-017 — Clarify that File Exit completely closes the app

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
The File>Exit button should clearly mention that this Exit button is going to completely close the app, it will kill the app, even close it from the tray icon.
```

Implementation notes (agent decisions): label File's action “Exit completely (including tray)” and add tooltip/accessibility text explaining full shutdown versus the window's X hiding to the tray. Preserve the existing graceful shutdown action; no forced process termination or new confirmation dialog.

## AR-018 — Inline duration hints in the section heading

Recorded: 2026-10-11 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
move the line `34 = minutes . 1.5 = hours . 1 second-24 hours. duration help` into the brackets of this section's heading i.e. By duration (move here)
```

Implementation notes (agent decisions): place the existing hints and clickable Duration help link in parentheses immediately after By duration. Remove the separate hint line, retain muted styling for the hints, and allow the heading to wrap when space is limited.

## AR-019 — Edit entry description popup

Recorded: 2026-10-12 (Asia/Karachi). Source: author messages in the current development conversation.

Author's original request (verbatim):

```text
hey, we need to provide option to modify a task's description.

we need to do this via a popup window.

the popup window should open upon,

- by double clicking on a task in the listview.
- also in the right click menu of a task in the listview



pls tackle the cases for ongoing tasks and completed tasks.

if you have any assumptions, ask them first.
```

Author's clarification (verbatim):

```text
- Empty or whitespace-only descriptions are rejected. -- it will disable the Save button.
- the right-click option will say "Edit entry" for both type of entries -- we will later add capability to modify duration or start date.

correct for all the others.
```

Confirmed scope (summary, not an author quote): prefilled description-only popup with Save/Cancel; ongoing timers continue and display the saved description everywhere including the tray; completed timing/Created remain unchanged while Modified updates. Blank drafts disable Save. Duration/start-date editing is deferred.

Relationship: replaces the deferred list/running-description interaction from AR-010 without restoring the removed inline editors or manual-form population.

Implementation notes (agent decisions): save only Description/UpdatedUtc by stable entry ID regardless of running state, trim outer whitespace, detect deleted entries, keep errors/drafts in the popup for retry, and disable dismissal while saving. Refresh the current query without clearing filters or manual drafts. A renamed row may no longer match its filter. Timer-control double-clicks, headers, and empty space do not open the popup.

## AR-020 — Selectable descriptions, suggestions, compact rows, and same-day duration placement

Recorded: 2026-10-12 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
- every listview entry's description text should be selectable, for it to be copyable.
- the type and search drop down is not working upon clikcing on the search result dropdown item. means when i click on the dropdown items it does not add it in the description box.
- reduce vertical paddings/margins of these: "Start Date" input fields, "TimeTracker" card box specially "TimeTracker" top margin, "Manual entry" top margin, listview's header row's vertical padding, make the timestamps single line of the all the listview items (Start, End, Created, Modified)
- no need to show `In progress` text in the listview for the ongoing task.
- The `add by duration` entries are getting added by day start time. and not by current date time. An edge case might happen where if someone lets say gives a duration input to be 2 hours, but the current time is 23:00, in that case consider start time to be: {CurrentDate:23:59:59} - {InputDuration} i.e. StartTime = DayEndMoment - InputDurationTime.
-- pls remember that we have check other areas as well for such kind of issues, because we our goal is to put each days task in their specific days. we are not considering the complications of logging the times of going between more than one days. hightlight to me such areas.
```

Relationship: refines AR-002/008/012/014; removes the visible running status chosen for AR-005 while retaining highlighting. Refines AR-019: double-click on description text selects a word; elsewhere on the row still opens Edit entry.

Implementation notes (agent decisions): preserve the selected date, using the current local clock time on that date; save Start and End in UTC. Shift Start back when necessary to cap End at 23:59:59. Reject durations beyond 23:59:59 because they cannot fit with that exact boundary, and reject ambiguous/nonexistent clock-change times or UTC elapsed-time mismatches. Historical large durations remain unchanged. Description text supports selection/Ctrl+C and row context-menu copying.

Audit findings (agent observations): import parses but discards supplied dates/times and saves undated entries, which are grouped by Created; live/restored timers can cross midnight and their entire completed duration is grouped under Start. Existing undated/cross-day records and query/export grouping also need a policy. No historical records or timer/import behavior are changed by this UI/manual-duration fix; follow-ups are recorded in TODO.

## AR-021 — Created records the actual addition moment

Recorded: 2026-10-12 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
`Duration entries now use the current clock time on the selected date. At 23:00, a 2-hour entry becomes 21:59:59–23:59:59. The maximum is now 23:59:59 to keep both timestamps on that day.` pls ensure that the Created date should be the exact moment the entry is being added at.
```

Relationship: clarifies AR-020. Calculating or shifting Start/End must not shift Created.

Implementation notes (agent observations): the repository already stamps CreatedUtc with DateTime.UtcNow when adding the entry, independently of its selected date and Start/End. The UI converts it to local time. Regression coverage checks past, current, and future selected dates with shifted late-night ranges, asserting Created is between the actual before/after add moments and initial Modified equals Created.

## AR-022 — Unfinished-task popup, initial timer duration, and keyboard suggestions

Recorded: 2026-10-12 (Asia/Karachi). Source: author messages and question replies in the current development conversation.

Author's original request (verbatim):

```text
- we need to add a pop up msg for the user to let them know: You left this task open when you last locked your PC (turned off your PC or closed this TimeTracker App) at {this time} -- its a draft for an idea. rephrase the msg in better form.
- start timer should also have an option to start a new entry with a defined duration, and the timer should continue from that duration. like for the Start Timer, beside the description box the duration box should be an input field where the user can enter duration just like the manual-entry-duration box, and then upon clikcing the Start timer button the time continue to tick from that duration.
- when the description input box is empty - it should listen for the down-arrow-key key-up event, and give last 5 entries, just like the search results.
- and then, the search results and the down arrow key event results should all be selectable by enter key. like from the up down arrow key i should be able to select the shown results and when i press the enter button the selected result should then be populated in the relevant description box/field.

let me know if you have any confusion in these?
```

Author's answers (verbatim; questions summarized separately for context):

Popup timing (context: unlock, startup, or both):

```text
On unlock and app startup
```

Popup actions:

```text
Continue, or stop at the recorded lock/exit time, or stop at the current time. all these 3.
```

Initial duration exceeding time since midnight (context: option 2 was allowing Start on the previous day):

```text
selecting number 2 for this for now, but flag this behaviour I will review this again later.
```

Relationship: AR-021's Created timestamp rule remains. The previous-day timer-start exception is explicitly temporary; AR-020's same-day manual-duration placement is unchanged.

Implementation notes (agent decisions): optional initial duration accepts zero/blank and manual formats up to 23:59:59, backdates Start in UTC, clears after start, and does not backdate Created. Three description inputs show the newest five records by Created, retaining duplicate descriptions, on empty-field Down-arrow key-up; searches and recents share Up/Down, Enter, Escape, click selection, and stale-query protection. Enter fills only the field.

Recovery uses per-entry SQLite checkpoints (schema v3), independent of optional session-history recording. Exact recorded lock/exit times are distinguished from best-effort Windows shutdown/sign-out notifications and periodic last-activity fallback. Unknown timestamps on legacy running tasks disable the recorded-time action. Continue preserves the original Start (including elapsed interruption time); Escape/X also means Continue. Full Exit is recorded; hiding the main window to the tray is not an exit. Normal startup and hidden Windows startup both show recovery. Checkpoints do not change entry Created/Modified and are removed on Stop or entry deletion. Native OS-event delivery/focus behavior remains an interactive verification item.

## AR-023 — Explain every unfinished-task popup action

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

> on the unlock screen popup message, also add hint-detail-explanation for every button.

Relationship: refines AR-022's unfinished-task popup.

Implementation notes (agent decisions): show an explanation beside each action, also exposed as its tooltip and accessibility help. Continue keeps counting time while away; Stop at recorded time excludes time after the displayed timestamp; Stop now includes time while away and ends at the current moment. Explain the last-activity fallback and why the recorded-time action is disabled when no timestamp exists. Continue's hint also explains Enter/Escape/X behavior. Applies to the shared unlock/startup popup.

## AR-024 — Collapsible manual entry and tighter Entries heading spacing

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
manual entry section should be collapsible to make some more space for the listview.
top margin of the listview heading (Entries) can be reduced a bit more.
```

Implementation notes (agent decisions): use an initially expanded, keyboard-accessible Expander for both manual rows. Collapsing releases space to the list and dismisses manual suggestion popups without clearing drafts. The section heading remains outside the forms' scroll area. Reduce the Entries card's top padding from 12 to 6 layout units, retaining side/bottom padding and existing UI scaling. Expanded/collapsed state lasts for the window session.

## AR-025 — Investigate typing search appearing not to work

Later correction: AR-026 confirms that longer text also fails in all three fields; the cutoff improvement is not a confirmed resolution of that report.

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original report (verbatim):

> i beleive the search upon typing is no longer working.

Relationship: follows AR-024's collapsible composer and AR-022's suggestion keyboard interactions.

Investigation notes (agent observations): automatic typing with two or more characters returned visible results in all three fields in an isolated WPF focus test. The existing two-character cutoff prevented any results for one character; the author's exact failing query/field was not yet provided. Earlier checks explicitly invoked the search method and did not establish the automatic typing path.

Implementation notes (agent decisions): allow typed matching from the first nonblank character and trim outer query spaces without changing the description draft. Keep version checks, recent-entry keyboard behavior, and selection handling. Validate automatic property-binding updates, actual popup visibility, and input focus after collapsing/reopening Manual entry, without explicitly invoking the search method.

## AR-026 — Longer typing searches fail in all three description fields

Recorded: 2026-10-13 (Asia/Karachi). Source: author's question replies in the current development conversation.

Author's answers (verbatim; question context summarized separately):

Which description fields fail:

> All three fields

Whether the failure also occurs with two or more characters:

> It also fails with longer text

Relationship: corrects the diagnosis associated with AR-025. Removing the two-character cutoff does not establish a fix for this report.

Investigation notes (agent observations): the running app is the separately installed copy in TimeTrackerOffline, with binaries differing from the workspace build. Its own assemblies and a read-only snapshot of its database returned visible suggestions for a 12-character query through WPF TextComposition events in all three fields, with input focus retained. The reported failure is not yet reproduced. The Computer Use runtime could not initialize (Node startup status 0xc0000142), including after reset/retry, so live popup inspection was unavailable. The exact failing query and expected description were requested. Keep this issue open until reproduced and resolved; do not claim the cutoff improvement fixes it.

## AR-027 — Directory-based instance policy

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
can we amend this to be like:

- we must not be able to launch the multiple instances from the same directory -- lets ensure this without any configuration.
- but if an instance is being launched whose exe location is different than the one currently running instance, then we should be checking a configuration from the appsettings that will be named like AllowMultipleInstances:true, only then we let the other instance launch.
```

Implementation notes (agent decisions): read the launching copy's root AllowMultipleInstances setting, default false. Keep the existing Windows user/session scope. Normalize the executable directory, independently of working directory or database path. Same-directory rejection occurs before reading configuration and activates that copy; denied other-directory launches activate an existing copy. Startup launches remain quiet. Serialize admission with a named mutex and retain directory/shared named presence handles in every admitted copy.

## AR-028 — Clear stopped timer description and align timer buttons

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
next items,

- upon stopping a running entry, the description box of the TimeTracker window should become empty.
- vertically align the Start Timer / Stop Timer button.
```

Implementation notes (agent decisions): clear the description and dismiss its suggestions after a successful stop, including list and recovery stops. Center Start/Stop in the same grid row as the timer description/duration fields rather than across heading and input rows.

## AR-029 — Initial duration placeholder

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

> TimeTracker box > Initial duration field should have a placeholder/hint > `00:00:00`

Implementation notes (agent decisions): show a muted, noninteractive hint inside the empty field, including while focused. Hide it when text is entered; leave the duration draft empty so existing blank-to-zero parsing is preserved.

## AR-030 — App icon visibility in dark Windows themes

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

```text
on dark theme of windows, the icon of this app is not so much visible.
i think we should add a static relevant background color to its icon.
or suggest me any better idea.
```

Implementation notes (agent decisions): use the app's indigo accent as a fixed rounded badge, with a white stopwatch and transparent outer corners. Retain all nine ICO resolutions (16–256 pixels). This updates the executable/window icon; the independent green/red tray status icons are unchanged.

## AR-031 — Maximize taskbar icon artwork

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original request (verbatim):

> the taskbar icon is very small, please maximize it. as much possible

Relationship: refines AR-030's icon design.

Implementation notes (agent decisions): remove the badge's outer padding so it fills the icon canvas, retaining rounded corners. Increase the stopwatch radius from 28% to 36% of the canvas and reposition its top button to fit safely. Windows determines the taskbar's icon slot size; these changes maximize the artwork within it. Preserve all nine ICO resolutions.

## AR-032 — Remove remaining EXE icon transparency

Recorded: 2026-10-13 (Asia/Karachi). Source: author message in the current development conversation.

Author's original report (verbatim):

> the exe icon is still transparent.

Relationship: supersedes the transparent rounded corners in AR-030/AR-031's implementation decisions.

Implementation notes (agent decisions): fill the entire canvas with opaque indigo, including corners, while retaining the enlarged white stopwatch. Regenerate all ICO frames, rebuild the application, and verify the actual embedded EXE icon matches the new opaque frames. The separate installed copy is not replaced by a workspace build.

## AR-033 — Store writable data outside the installation directory

Recorded: 2026-10-14 (Asia/Karachi). Source: author messages in the current development conversation.

Author's original question (verbatim):

> hey, in these kind of apps, we usually use `%userprofile%\AppData\Local` this kind of directory for storing the app's data right? instead of saving it inside the installation directory.

Author's authorization (verbatim):

```text
Ok lets do this work then ..
because i just accidentally shift deleted my current installation directory's files of TimeTracker where i was maintaing the time logging for the last 4-5 days :/
```

Context (not an author quote): the preceding proposal was to switch the default to LocalAppData, keep a portable option, and migrate existing data on first launch.

Implementation notes (agent decisions): default root `%LOCALAPPDATA%\TimeTracker`, database under `Data`, settings and error log under the root. PortableMode defaults false; true selects the executable directory as writable root. Absolute custom paths and --db-path are preserved; relative configured database folders now resolve under the writable root. Installation appsettings remains read-only configuration. Copy legacy SQLite data through the backup API, preserve its adjacent list preferences, serialize concurrent migrations, leave the source intact, and never overwrite a destination. Stop startup on migration errors. Different installation copies share the per-user database unless explicitly configured otherwise. No general backup/recovery feature is implied.

Recovery authorization (verbatim answer to whether the discovered 22-entry snapshot should be restored to the new AppData location without replacing existing data):

> Restore the recovered entries

Recovery notes (agent observations): a retained database snapshot from the installed-app search investigation contains 22 real entries from October 12–13. A separate integrity-checked recovery copy was preserved, and its 22 entries were restored to the new default AppData database after author approval. All restored tables matched the recovery copy. This does not establish recovery of all lost days. Keep private log contents out of tracked documentation.

## Maintenance rules

1. Append new author feature requirements and meaningful corrections as new entries with stable IDs (`AR-010`, `AR-011`, ...). Include source context and a recording date; claim an original message date only when known.
2. Copy the author's actual message verbatim whenever available. Preserve typos and informal language. If text is shortened, paraphrased, or reconstructed, label it **near-exact**, **excerpt**, or **summary** and state the reason. Never manufacture an exact quote from code, an agent reply, or a backlog item.
3. Keep original quotes immutable. For later corrections, add a new entry and cross-link it to the earlier entry, marking the affected part superseded where appropriate. Do not silently replace the original request.
4. Put inferred scope, numeric limits, UI choices, and implementation constraints under clearly labeled context/implementation notes. Do not turn agent assumptions into author statements.
5. Keep work status in TODO/CHANGELOG and current behavior in README/SPEC. Reference AR IDs from related documentation when useful; the quote itself must not change with implementation status.
6. Do not copy secrets or personal data into a tracked document. If a requirement includes sensitive content, redact that part explicitly rather than calling the redacted quote verbatim.
7. Treat this as a historical requirements reference. Current explicit author instructions and applicable repository rules govern the present task; old quotes do not independently authorize unrelated operations or data changes.

### Entry template

```markdown
## AR-034 — Short subject

Recorded: YYYY-MM-DD. Source: author message in the current development conversation.

Author's original request (verbatim / near-exact / excerpt; state which):

> Copy the author's wording here.

Context (not an author quote): ...

Relationship: refines/supersedes AR-... (if applicable).

Implementation notes (agent decisions, not original requirements): ...
```
