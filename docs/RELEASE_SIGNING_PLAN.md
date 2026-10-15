# Release signing plan

Prepared: 2026-10-16 (Asia/Karachi). Status: proposal only (AR-040).

## Goal and limits

Add a verifiable publisher identity and tamper detection to distributed TimeTracker releases using Windows Authenticode. Signing does not establish that software is harmless, remove URL detections, or override Apex One policies. No certificates, accounts, signing scripts, or release changes are created by this plan.

## Current release

TimeTracker is a .NET 8 WPF app. `TimeTracker.App/Properties/PublishProfiles/FolderProfile.pubxml` publishes a self-contained Windows x64 folder with single-file publishing disabled. The local Release executable inspected during the investigation was unsigned. The release folder contains app binaries alongside third-party/.NET runtime files; signing only an intermediate build output would miss the final published executable.

## 1. Select the signing identity

| Route | When to consider it | Decision needed |
|---|---|---|
| Public code-signing certificate from a trusted certificate authority | Direct distribution as a ZIP/folder or future installer | Confirm acceptance of your country and individual/organization status, required identity evidence, total renewal costs, and token/cloud signing support before buying. |
| Microsoft Azure Artifact Signing, formerly Trusted Signing | Managed signing when eligible | Public Trust individual applicants currently must be in the US or Canada. Pakistan is also absent from the current organization eligibility list. Use the service's quickstart for current eligibility. |
| Organization-managed signing | Distribution only to managed company computers | Ask IT whether it can sign the app and deploy the necessary certificate trust and Apex One policy. Internal trust does not automatically extend to public users. |
| Self-signed certificate | Controlled testing | Requires deliberate trust setup on target devices; unsuitable as a general public-distribution solution. |

Recommendation: for direct distribution, investigate a trusted CA that accepts your actual identity and location. If this app is only for work-managed devices, first ask IT about its existing signing process. No provider or purchase is selected yet. Microsoft Store MSIX distribution is a separate packaging option with Store-managed signing; it would require a separate implementation plan.

Sources: [Microsoft signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options), [Artifact Signing eligibility](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart).

## 2. Prepare the tools and credentials

Install the Windows SDK signing tools/Visual Studio tooling to obtain `signtool.exe`. Follow the chosen provider's setup for its hardware token or cloud signing service. The example below assumes the certificate and private-key provider are accessible through the current user's Windows certificate store. Cloud services may instead require their own SignTool integration and authentication.

Keep signing credentials outside the repository. Do not put a PFX password in a script or command history. Do not assume a provider offers an exportable PFX. Choose the intended publisher name during identity verification; an EXE metadata field cannot replace that verified identity.

Source: [Microsoft SignTool reference](https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe).

## 3. Publish, then sign the final files

Proposed release sequence:

1. Build/test the selected version and publish into a fresh staging directory. Preserve existing releases and user databases.
2. Use the existing README publish command, with the chosen staging output path:

   ```powershell
   dotnet publish .\TimeTracker.App\TimeTracker.App.csproj -c Release -r win-x64 --self-contained -o .\publish\release\
   ```

3. Inventory the output. Sign first-party files: `TimeTracker.exe`, `TimeTracker.dll`, `TimeTracker.Core.dll`, and `TimeTracker.Data.dll`. Confirm their names against the actual output. Preserve third-party publisher signatures; do not blanket re-sign the whole runtime folder.
4. Sign using SHA-256 and an RFC 3161 timestamp from the provider. This template is for manual adaptation, not a configured command:

   ```powershell
   $signingThumbprint = 'REPLACE_WITH_CERTIFICATE_THUMBPRINT'
   $signingTimestampUrl = 'REPLACE_WITH_PROVIDER_RFC3161_URL'
   $signingFiles = @(
       '.\publish\release\TimeTracker.exe',
       '.\publish\release\TimeTracker.dll',
       '.\publish\release\TimeTracker.Core.dll',
       '.\publish\release\TimeTracker.Data.dll'
   )
   foreach ($signingFile in $signingFiles) {
       signtool sign /sha1 $signingThumbprint /s My /fd SHA256 /tr $signingTimestampUrl /td SHA256 $signingFile
       if ($LASTEXITCODE -ne 0) { throw "Signing failed: $signingFile" }
   }
   ```

   `/sha1` selects the certificate by thumbprint; `/fd SHA256` sets the file digest. Timestamping supports validation after certificate expiry, subject to trust/revocation rules. Treat a missing timestamp as a failed release check even if signing returns only a warning.

5. Verify before packaging. If an installer is introduced later, build it from signed payloads and sign the final installer too. A ZIP is a container for the signed files, not an Authenticode-signed executable. Do not rebuild or modify binaries after signing; republish and sign again for each release.

Sources: [SignTool commands](https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe), [Authenticode timestamping](https://learn.microsoft.com/en-us/windows/win32/seccrypto/time-stamping-authenticode-signatures).

## 4. Verify the distributed artifact

- Run `signtool verify /pa /all /v /tw <file>` for every first-party signed binary. Require successful verification, the intended publisher, a trusted chain, and a timestamp. `/tw` warns if a timestamp is missing.
- Inspect the EXE's Properties → Digital Signatures and check the publisher/countersignature.
- Extract the final archive on a representative target PC and repeat signature verification; launch the app and check normal tray/timer operation.
- Record the version, commit plus any uncommitted-source provenance, certificate identity, timestamp, and SHA-256 hashes of the final distribution. Hash after signing because signing changes file bytes.
- Check Apex One logs for the exact distributed files. If still blocked, submit the signed sample and detection logs to Trend Micro/IT; signing alone is not the acceptance criterion.

Sources: [SignTool verification](https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe), [Trend Micro false-positive handling](https://success.trendmicro.com/en-US/solution/KA-0006822).

## 5. Later implementation deliverables

After a signing provider is chosen: add a separate release-signing script with explicit first-party file selection, verification, timestamp checks, and failure handling; document credential setup without secrets; then produce one signed release for target-device validation. Keep ordinary development builds independent of paid signing access. Authenticode should be added to the release process; enabling .NET assembly strong-name signing alone would not provide this Windows publisher signature.

Acceptance: every intended first-party release binary verifies with the chosen publisher and timestamp; the final archive retains those signatures; no private signing material is tracked; Apex One validation is recorded separately.
