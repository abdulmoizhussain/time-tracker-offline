# Repository privacy plan

Created: 2026-10-15 (Asia/Karachi). Requirement: AR-036.
Status: proposed; no files moved, repository visibility changed, or Git history rewritten.

## GitHub capability

GitHub repository visibility applies to the repository as a whole. A public repository cannot give individual tracked files or folders private read permissions. A separate branch does not provide that separation either. GitHub documents repository-level public/private visibility in [About repositories](https://docs.github.com/en/repositories/creating-and-managing-repositories/about-repositories).

`.gitignore` prevents untracked files from being added normally; it does not restrict access to files already committed. Stopping tracking does not remove earlier versions from history. See [Ignoring files](https://docs.github.com/en/get-started/git-basics/ignoring-files).

## Recommended arrangement

Keep the buildable TimeTracker application in this public repository. Put selected private documents or assets in a separate private repository, checked out alongside it rather than inside the public checkout. The proposed private repository name is a placeholder until the owner chooses one.

```text
workspace/
  TimeTracker-2.0/          public application repository
  TimeTracker-private/     separate private repository
```

Keep local runtime data in AppData and recovery copies in a separate backup location. A private source repository is not the application's database backup system.

| Content | Proposed location |
|---|---|
| App source, tests, public usage/build documentation | Public repository |
| Safe sample configuration and synthetic test data | Public repository |
| Chosen project license and required dependency notices | Public repository and relevant release packages |
| Owner-selected internal plans or conversation records | Private repository, with sanitized public summaries where useful |
| Real databases, exports, recovered logs, personal screenshots | Outside the public repository; backed up separately |
| Credentials and signing material, if introduced | Appropriate secret storage; no plaintext tracked files |

These are proposed categories, not an approved list of files to move. In particular, review `docs/AUTHOR_REQUIREMENTS.md` and planning documents with the owner before deciding which belong privately. Public contributors must still have sufficient requirements and instructions to work on the app.

## Implementation steps

1. **Choose the exact file list.** Produce a path-by-path manifest with public/private/local-only decisions. Review links and build dependencies. Do not copy private contents into a public issue or this plan.
2. **Check exposure.** Inspect the selected files in the working tree, tracked files, local branches/tags, and relevant Git history. Check what has actually been published; this plan does not claim a remote exposure audit has occurred.
3. **Prepare private storage.** Create the separate repository with private visibility and grant access only to selected collaborators. Preserve and verify the selected documents before removing their public copies. Keep a private mapping of moved paths.
4. **Make the public tree self-contained.** Replace private configuration with safe examples; move only approved paths; update links and contributor instructions. Public source and tests must build without credentials or access to the private repository. If selected private source is essential to the app, reconsider the split before proceeding.
5. **Prevent accidental reintroduction.** Add narrowly scoped ignore rules for any approved local-only paths. Verify their behavior and inspect staged files and release packaging. Ignore rules alone cannot protect already published history.
6. **Handle historical content separately.** If selected files were committed, decide whether removing them from the current tree is sufficient or whether history cleanup is required. Prepare a backup, affected-reference inventory, collaborator instructions, and a concrete cleanup plan before any rewrite/force-push. Obtain explicit authorization for those disruptive actions. For exposed credentials, revoke/rotate them first. GitHub explains cleanup limits and effects in [Removing sensitive data](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository); existing clones and forks cannot be assumed to lose their copies.
7. **Validate the split.** Build the public checkout using `dotnet build TimeTracker.sln`, check relevant tests, inspect links and packaged outputs, and verify private-repository access separately. Confirm no approved private files remain in public tracked content or any history covered by an authorized cleanup.

## Completion criteria

- The owner has approved the exact classification manifest.
- The public application builds independently and retains required license documents.
- Approved private files are preserved and accessible only through their selected private storage.
- Any existing exposure is documented honestly, with authorized cleanup completed where applicable.
- Documentation explains where contributors find public guidance and how authorized collaborators access private material.

The current task delivers this plan only. Repository creation, file relocation, permission changes, and history cleanup remain future work.
