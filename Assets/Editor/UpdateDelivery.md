# Building and publishing Unite updates

Use **Build → Updates → Release tools** in Unity. The tool creates files on your computer; uploading them to R2 is a separate step.

## Your hosting settings

| Setting | Value |
|---|---|
| Game's Base URL (public downloads) | `https://unite-content.justmarcy.com` |
| R2 bucket name (uploads) | `mapsandclothes` |
| Cloudflare account ID | `57edf3816e58dd36363ff2bdedd95827` |
| AWS CLI endpoint (uploads) | `https://57edf3816e58dd36363ff2bdedd95827.r2.cloudflarestorage.com` |

The public domain goes in Unity's **Base URL**. The S3 endpoint is only for AWS CLI. Do not add the bucket name to the public URL. Your API credentials are already configured in AWS CLI; there is no need to enter them in Unity or configure them again.

Commands below assume your credentials are in AWS CLI's default profile. If you used a named profile, add `--profile YOUR_PROFILE` to each command. Run commands from the Unity project directory. You can check access without uploading anything:

```bash
aws s3 ls s3://mapsandclothes/ \
  --endpoint-url https://57edf3816e58dd36363ff2bdedd95827.r2.cloudflarestorage.com \
  --region auto
```

An empty bucket can produce no listed objects; an authentication error indicates a credentials, profile or token-permissions problem. In Cloudflare, ensure this bucket uses Standard storage and its custom domain is active with HTTPS.

## What each version means

- **Player version:** Unity Player Settings → Version. Change it when distributing a new Windows player/APK. Android also needs an increased Bundle Version Code and the same signing identity/application ID.
- **Addressables Version:** identifies a built content release. Change it when rebuilding maps, clothing or other Addressables. Leave it unchanged for balance-only and compatible code-only releases.
- **Patch Version:** identifies the balance JSON. Change it whenever exported balance data changes.
- **Compatibility Line:** a family of compatible content schemas. Keep `1` until a major code/engine/schema change makes old content unreadable.

The game uses all three release versions for matchmaking compatibility. Published content and patch versions are immutable: never reuse a version for different files.

## First release: do these steps for Windows, then Android

1. Open **Build → Updates → Release tools**. Base URL should be `https://unite-content.justmarcy.com`. Keep **Use Remote In Editor** off while authoring locally; it is only for testing a published remote release in Play mode.
2. Choose your player Version in Unity Player Settings. Initial Compatibility Line, Addressables Version and Patch Version can all be `1` if you have no completed build for those versions yet.
3. Select the target in Unity Build Profiles. Windows output uses the folder name `StandaloneWindows64`; Android uses `Android`.
4. Optionally click **Prepare Addressables configuration only**. This exports balance data and configures the remote groups without building bundles. It is useful for checking preparation separately after the earlier freeze.
5. Click **Build Addressables baseline**. Wait for the entire operation to finish. This builds both the remote bundles and the smaller local content needed by the player. Watch progress and the Console; see the next section if the editor appears busy.
6. After the Console reports **Completed**, click **Build player using existing Addressables**. It reuses the content archive instead of rebuilding maps/clothing. Player files appear under `Build/Players/<player-version>/<platform>/`.
7. Repeat steps 3–6 for the other platform. Each platform needs its own content build; Windows bundles cannot be used by Android.
8. Upload the files and publish the manifests using the commands below. Test a build before distributing it to players.

**Build All Platforms** queues player builds for Windows, Android, WebGL and Linux server. It requires their content archives and installed build modules already to exist. For only Windows/Android, use their individual build actions. The queue resumes after platform-switch domain reloads; cancel with **Build → Updates → Cancel queued player builds**. CI should use separate Unity invocations per `-buildTarget`.

## Progress, cancellation and the earlier freeze

The old preparation code sent a separate Addressables modification event for thousands of entries before Unity's usual bundle-build progress appeared. That notification work is now batched. Build actions clicked in the release window start after the current GUI event finishes.

You should now see:

1. Preparation progress: validating, exporting balance, configuring groups/labels, saving settings.
2. Unity's own bundle/shader build progress.
3. Archiving progress, followed by manifest export and a completion message.

Unity's content build is synchronous. The editor can remain unresponsive while native shader/asset tasks run; these tasks do not necessarily update progress continuously. Previous saved reports for this project include a build taking about 48 minutes. Treat that as context, not an estimated completion time for your machine.

Each release-tool operation writes a timestamped stage log to **`Build/Updates/Logs/`**, flushing it as stages change. Console lines begin with `[Update build]`. During the bundle-build stage, Unity's **Editor.log** has the detailed engine output. On Linux, it is `~/.config/unity3d/Editor.log`.

Preparation/archiving offer cancellation between operations; Unity's bundle pipeline supplies its own cancellation UI. Native work and an individual large file copy may not stop immediately. Progress is cleared after success, cancellation or an exception. No upload occurs during a build.

If an attempt was interrupted:

- First inspect its stage log and Console for the last completed step.
- If `Build/Updates/Artifacts/1/<platform>/<content-version>/release.json` exists, the content archive was produced. Try **Export manifest using existing Addressables**, then build the player; do not rebuild that content version blindly.
- If catalog files were written but no completed archive exists, the version may be partial. Use a new Addressables Version, or remove only the known unpublished partial output after inspecting it. Keep older published versions and the bundle directory.
- Do not clear the Unity Library/build cache as the first troubleshooting step; that can make the next build much longer.

## Upload files to your bucket

### 1. Upload content first

After successful builds, run:

```bash
aws s3 sync Build/Updates/Upload/ s3://mapsandclothes/ \
  --endpoint-url https://57edf3816e58dd36363ff2bdedd95827.r2.cloudflarestorage.com \
  --region auto \
  --exclude '*/latest.json' \
  --cache-control 'public,max-age=31536000,immutable'
```

This uploads the generated folders at the bucket root, preserving their paths, and deliberately leaves out the live release pointers. Do not add `--delete`: older supported clients may still reference older files.

Only upload **`Build/Updates/Upload/`** as content. Keep `Artifacts`, `WorkingState`, `Staging` and logs locally/backed up. Player ZIPs/APKs remain separate downloads on your usual distribution site.

### 2. Publish latest.json last

Each player version/platform has its own manifest. These commands use the current initial settings: compatibility line `1`, player version `0.7.2`. If you changed either setting, update both the local and bucket paths accordingly.

Windows:

```bash
aws s3 cp Build/Updates/Upload/1/players/0.7.2/StandaloneWindows64/latest.json \
  s3://mapsandclothes/1/players/0.7.2/StandaloneWindows64/latest.json \
  --endpoint-url https://57edf3816e58dd36363ff2bdedd95827.r2.cloudflarestorage.com \
  --region auto \
  --content-type application/json \
  --cache-control 'no-cache,max-age=0,must-revalidate'
```

Android:

```bash
aws s3 cp Build/Updates/Upload/1/players/0.7.2/Android/latest.json \
  s3://mapsandclothes/1/players/0.7.2/Android/latest.json \
  --endpoint-url https://57edf3816e58dd36363ff2bdedd95827.r2.cloudflarestorage.com \
  --region auto \
  --content-type application/json \
  --cache-control 'no-cache,max-age=0,must-revalidate'
```

Publishing latest.json makes that release available at the next boot. Do it only after its referenced patch, catalog and bundles are uploaded. Do not upload a manifest for a platform you have not built.

### 3. Check the public URL

For the example Windows release, the game requests:

`https://unite-content.justmarcy.com/1/players/0.7.2/StandaloneWindows64/latest.json`

It must return the JSON document, not an HTML download/error page. Also check the document's `catalogUrl` and `patchUrl`. Cloudflare cache rules must bypass caching for `latest.json` or always revalidate it; immutable files can be cached for a long time. Direct R2 egress is already free, but storage and requests have metered allowances.

## Later balance-only updates

1. Edit Pokémon stats in their PokemonBase assets. Edit supported item/XP coefficients in `Assets/Resources/BalanceTuning.asset`.
2. For move cooldown changes, add a `moveOverrides` entry to BalanceTuning using the stable asset ID in the exported JSON. Specify base cooldown, upgraded cooldown and Unite energy cost. The exporter enables cooldown overrides for these entries.
3. Increment **Patch Version**. Keep player and Addressables versions unchanged.
4. Select Windows and click **Export manifest using existing Addressables**. Repeat for Android and any other supported player/platform release.
5. Run the content sync, then publish those platforms' latest.json files as above. No player or Addressables rebuild is required.

**Export balance JSON** alone writes the data/defaults but does not update the release manifest. The complete snapshot currently contains 49 Pokémon, 123 moves and 22 numerical settings (about 268 KiB). Players need only the latest snapshot, not a chain of earlier patches.

Balance support currently covers stat arrays, Unite costs, optional cooldown overrides, XP thresholds, and selected Muscle Band/Focus Band/Shell Bell/Score Shield coefficients. Other damage formulas, durations and ranges that remain in C# still require a player rebuild. Default cooldown behavior is preserved until an override is explicitly enabled.

Stable IDs come from Unity asset GUIDs: preserve `.meta` files. Exported data is range-checked, checked against the player's baseline, and verified with SHA-256 at boot. Author balance changes through assets/tuning rather than manually modifying published JSON and invalidating its checksum.

## Later content-only updates

1. Change remote content and increment **Addressables Version**.
2. Select the platform and click **Build Addressables content update**.
3. Choose that platform's previous archived `addressables_content_state.bin`, under `Build/Updates/Artifacts/<line>/<platform>/<previous-content-version>/`.
4. Repeat for the other platform, then upload content and publish latest.json last.

The tools reject changes to static local Addressables during content-only builds; those changes require a new baseline and player. Content-state files must come from the same Unity version. Unchanged hash-named bundles stay cached. A changed map normally requires downloading its entire changed bundle.

## Later player/code updates

Change the Unity player Version and Android Bundle Version Code as applicable. Keep the existing Addressables Version if the content remains compatible, then **Build player using existing Addressables** for each platform. Publish manifests for the new player version, and distribute the new player/APK normally.

Keep Android's application ID/signing identity and Windows's company/product storage identity consistent so normal application upgrades retain cached content. Uninstalling/clearing data can remove it. New compiled components, RPC/schema changes or engine upgrades may require a fresh baseline/compatibility line.

## Runtime behavior and first release checks

At boot, the game checks its player/platform manifest, validates/downloads balance, loads the pinned catalog and downloads missing required content before enabling Start. The current implementation downloads the complete remote set on first boot. Later boots reuse unchanged bundles. There is no automatic deletion of old remote bundles.

Remote delivery includes maps, clothing models/icons/shared assets, trainer shaders and trainer card art. Essential boot/UI and other content remain local. An update failure blocks play and exposes **Retry update**, which preserves downloaded content. Remote delivery requires a successful online check; an empty Base URL/local editor mode uses packaged balance defaults and editor Addressables.

Before rollout, test first boot, second boot with no content redownload, a balance patch, a changed bundle, an interrupted download, a player/APK upgrade and incompatible peers. Versions are pinned until the next boot; there is no mid-match hot reload. The announcement JSON is informational.

Windows/Android do not require browser CORS. For WebGL, configure bucket CORS for your game site's origin, GET/HEAD, request headers such as Cache-Control, and exposed Content-Length/ETag. Browser storage can evict cached content. Interrupted partial downloads may restart even though complete cached bundles are retained.

## Generated files and backups

```
Build/Updates/Upload/<line>/<platform>/bundles/...
Build/Updates/Upload/<line>/<platform>/catalogs/<content-version>/...
Build/Updates/Upload/<line>/patches/<patch-version>-<checksum>.json
Build/Updates/Upload/<line>/players/<player-version>/<platform>/latest.json
Build/Updates/Upload/<line>/players/<player-version>/<platform>/releases/<content>-<patch>.json
Build/Updates/Artifacts/<line>/<platform>/<content-version>/PlayerContent/...
Build/Updates/Artifacts/<line>/<platform>/<content-version>/addressables_content_state.bin
Build/Updates/Artifacts/<line>/<platform>/<content-version>/release.json
Build/Updates/Artifacts/Patches/<line>/<patch-version>.sha256
Build/Updates/Logs/<timestamp>.log
Build/Players/<player-version>/<platform>/...
```

Back up Artifacts and Upload outside disposable Unity Library storage. Player rebuilds restore local content from Artifacts; future content updates use its state files. The internal Addressables working catalog URL stays fixed for Unity's incremental builder, while runtime manifests point to immutable catalog copies. Never publish the working catalog path.

To roll back a compatible release, restore its archived `releases/<content>-<patch>.json` as latest.json, keeping its patch/catalog/bundles available. Never overwrite an immutable catalog or delete bundles still referenced by supported clients.
