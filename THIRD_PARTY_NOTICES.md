# Third-Party Notices

**The Road Leading to the Mountain (TRLM)** is a commercial game. The full game uses third-party and
purchased assets that are **NOT** included in this open-source repository and are **NOT** covered by
the repository's MPL-2.0 license.

This file records the third-party content the TRLM team uses in the **full commercial build**. These
assets remain under their own respective licenses, held by their respective owners. Their exclusion
from this repository is deliberate: only TRLM-authored, open-sourceable code and configuration are
published here.

> **Nothing in this file grants any license to the third-party assets named below.** If you want to
> reproduce the full game you must obtain each asset yourself, under its own license, from its
> original source.

---

## Assets excluded from this repository

The following categories of content are **not** distributed here (see `.gitignore`):

* Third-party 3D models, characters, textures, materials, and animations.
* Unity Asset Store packages and other purchased assets.
* Audio clips whose license is not fully verified, plus any voice-over and music.
* Dev captures, screenshots, build outputs, and generated Unity cache.

## Third-party assets used in the full game (not redistributed here)

The following are used in the commercial build and are excluded from this repository. Names are
recorded only where confirmed by the TRLM asset registry (`Documents/AssetRegistry.md`); anything
unverified is left intentionally generic.

| Asset / Pack | Category | Author / Source | Notes |
|---|---|---|---|
| Low Poly Forest Tree Pack | Environment / vegetation | **99 Mil** | Commercial use permitted; attribution required (end credits). |
| Free Pack — Rocks Stylized | Environment / rocks | **PolyOne Studio** | Commercial use permitted; attribution required (end credits). |
| Grass Patches (Circle) | Environment / ground cover | **brandon_grey** | Commercial use permitted; attribution required (end credits). |
| Rowboat asset | Prop / vehicle | **Marpa Studio** | Third-party model + textures. |
| Uber Stylized Water | Environment / water shader | Third-party (Uber Stylized Water) | Custom URP water shader & demo content. |
| Character Creator base meshes (`CC_Base` / `CC3_Base_Plus`) | Characters | Reallusion Character Creator (base) | Third-party base character meshes/textures. |
| `npc_casual_set_00` character pack | Characters | Third-party pack | Character meshes, textures, source archive. |
| Unity Starter Assets (Third Person Controller) | Template / controller | Unity Technologies | Unity-provided starter package. |
| Wildlife SFX (bear / deer / rustle) | Audio (SFX) | Freesound.org contributors — **CC0 1.0** | CC0 public domain. Excluded here for a clean code-only release; see `Assets/_TRLM/Audio/SFX/Wildlife/CREDITS_Wildlife_Audio.txt` for per-file attribution. |
| Other SFX (combat / environment / interaction / movement / weather) | Audio (SFX) | Origin not fully verified | Excluded pending license verification. |
| Gobkit Free Animal Pack (Boar, Goat low-poly rigged) | Wildlife / models | **Gobkit** — gobkit.com — **CC0 1.0** | CC0 public domain. `PF_Boar` / `PF_MountainGoat` reference these meshes (excluded binaries). |
| Poly Haven — cave/cliff PBR textures (`cliff_side`, `brown_mud_rocks_01`) & rock models (`boulder_01`, `coastal_cliff_01`) | Environment / textures + models | **Poly Haven** — polyhaven.com — **CC0 1.0** | CC0 public domain. Cave `Sprint11` materials reference these maps (excluded binaries). |
| Cave ambience / drips / impacts / creature & shotgun SFX | Audio (SFX) | **Kenney** (kenney.nl) + OpenGameArt contributors (JaggedStone, qubodup, rubberduck, zer0_sol) — **CC0 1.0** | CC0 public domain; excluded here per the audio-binary exclusion policy. |
| Voice-over (VO) & music | Audio | Managed separately | Not generated/committed; excluded from OSS release. |

## Open-source package dependencies (fetched via UPM, not redistributed here)

* **glTF Fast (`com.unity.cloud.gltfast`)** — Unity Technologies / Andreas Atteneder — **Apache-2.0**. Declared in `Packages/manifest.json`; the Unity Package Manager fetches it automatically on project open. Required to import the CC0 `.glb`/`.gltf` models referenced above. Its source is not copied into this repository.

For fuller technical detail on the third-party assets and how they were integrated, see
`Documents/AssetRegistry.md` and `Documents/P0_Asset_Audit.md`, which are included in this
repository as engineering documentation.

## Attribution obligations carried into the commercial build

Several packs above require creator attribution in the shipped game's end credits (e.g. **99 Mil**
for trees, **PolyOne Studio** for rocks, **brandon_grey** for grass). These obligations apply to the
commercial build, independent of this repository.

---

*If you believe any asset is misattributed or should not be referenced here, please open an issue.*
