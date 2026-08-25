# TRLM — *The Road Leading to the Mountain*

> **Active development.** This repository is the **open-source code & configuration** side of an
> in-development commercial game. It is **not** a finished product and **not** a playable Steam build.

TRLM is a single-player narrative survival-exploration game built in **Unity 6** (URP), being
developed for commercial release on **Steam**. Development happens in the open: this repository is
updated regularly as the game evolves, so the engineering work behind TRLM can be followed, read,
and learned from.

---

## ⚠️ What this repository is (and is not)

**This repository contains ONLY the source code and text-based project configuration authored by the
TRLM team.** It intentionally **excludes all third-party, purchased, and non-open content.**

* ✅ **Included:** our C# gameplay systems, ScriptableObject/config assets, scenes & prefabs we
  authored, project settings, package manifests, and design/engineering documentation.
* ❌ **Not included:** third-party & Unity Asset Store packages, purchased 3D models / textures /
  materials / animations, audio clips whose license is not fully cleared, voice-over, music,
  captures, screenshots, build outputs, and any generated Unity cache.

Because the third-party art and audio assets are **not** part of this repository, **cloning it will
not produce the full audio-visual game.** Scenes and prefabs reference those assets by GUID, so the
project **compiles**, but many objects will render as missing/placeholder until the proprietary
assets are supplied separately. This is expected: TRLM is a commercial game, and only its
open-sourceable engineering layer lives here.

See **[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)** for details on the excluded content.

---

## Development status

**Pre-release / in active development.** Systems are being built and iterated sprint by sprint.
Expect breaking changes, work-in-progress features, and incomplete content. The game is **not
finished**.

## Implemented systems (engineering layer)

A snapshot of the TRLM-authored systems present in this repository:

* **Core game loop** — objective chain, checkpoints, scene flow, save/continue.
* **Save / load** — orchestrated save game data & progression persistence.
* **Player & interaction** — first-person controller integration, interaction system, loot/items.
* **Survival** — health / stamina / injury systems.
* **Combat** — weapon & impact systems.
* **World & AI** — wildlife ecology, companion squad behavior, cinematic camera direction.
* **Narrative** — dialogue system, prophecy notebook, story flags, cinematic triggers.
* **Tooling** — QA/debug HUD and development/editor utilities.

Engineering and design notes live in **[`Documents/`](Documents/)**.

## Tech

* **Engine:** Unity `6000.4.8f1` (Universal Render Pipeline)
* **Language:** C#
* Package dependencies are declared in [`Packages/manifest.json`](Packages/manifest.json).

## Building / opening

1. Install **Unity 6000.4.8f1**.
2. Open this folder as a Unity project.
3. The project will compile. Objects that depend on excluded third-party assets will appear missing
   until those assets are provided separately (they are not distributed here).

## Contributing

Contributions, discussion, and bug reports are welcome.

* **Issues / bug reports:** please use the GitHub **Issues** tab. Include Unity version, steps to
  reproduce, and logs/screenshots where relevant.
* **Pull requests:** keep changes focused; match the surrounding code style. By contributing you
  agree your contributions are licensed under the repository license (below).
* Please **do not** add any third-party, purchased, or unlicensed art/audio assets to this
  repository.

## License

Source code and configuration in this repository are licensed under the
**Mozilla Public License 2.0 (MPL-2.0)** — see **[LICENSE](LICENSE)**.

MPL-2.0 is file-level copyleft: modifications to MPL-licensed files must be shared under the same
license, while the files may still be combined with proprietary code and assets (as in the
commercial TRLM build). The TRLM team retains copyright.

## Third-party content

Third-party and commercial assets used in the full game are **not** part of this open-source release
and are **not** covered by the MPL-2.0 license above. See
**[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)**.
