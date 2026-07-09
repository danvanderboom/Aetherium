# Contributing to Aetherium

Thanks for your interest in contributing! Aetherium is licensed under the
[Apache License 2.0](LICENSE), and contributions are accepted under the same
license.

## Developer Certificate of Origin (DCO)

This project uses the [Developer Certificate of Origin](https://developercertificate.org/)
(DCO) instead of a Contributor License Agreement. The DCO is a lightweight
attestation that you have the right to submit the code you're contributing and
that you agree it may be distributed under the project's license.

To sign off, add a `Signed-off-by` line to each commit message:

```
Signed-off-by: Your Name <your.email@example.com>
```

Git does this for you with the `-s` flag:

```
git commit -s -m "Add feature X"
```

By signing off, you certify the statements in the
[Developer Certificate of Origin v1.1](https://developercertificate.org/):
that you wrote the contribution, or have the right to submit it under the
project's open source license.

Pull requests with unsigned commits cannot be merged. If you forgot to sign
off, amend with `git commit --amend -s` (or `git rebase --signoff` for a
series of commits) and force-push your branch.

## How to contribute

1. **Open an issue first** for anything non-trivial — bug reports, feature
   proposals, or design questions. This avoids wasted work on changes that
   conflict with the engine's direction.
2. **Fork and branch.** Create a feature branch from `develop`.
3. **Build and test.**
   ```powershell
   dotnet build Aetherium.sln
   dotnet test
   ```
   All tests must pass. New behavior should come with tests.
4. **Match the codebase.** Follow the existing code style, naming, and
   architecture patterns (ECS components, Orleans grains, server-authoritative
   state). See [docs/architecture/](docs/architecture/) for how the pieces fit
   together.
5. **Sign off every commit** (`git commit -s`, see above) and open a pull
   request against `develop`.

## What makes a good contribution

- **Render-agnostic:** the server and protocol must never assume a specific
  renderer — perception payloads are semantic, not visual.
- **Data-driven:** configurable gameplay behavior belongs in per-world data,
  not hardcoded engine logic.
- **Server-authoritative:** clients render and send intents; the server owns
  all game state.

## License

By contributing, you agree that your contributions will be licensed under the
[Apache License 2.0](LICENSE).
