# Contributing to TypeIt4Me

Thanks for considering a contribution. Bug reports, docs fixes, and small
improvements are all welcome.

## Dev setup

```bash
git clone https://github.com/Avicennasis/TypeIt4Me.git
cd TypeIt4Me
# Requires Visual Studio 2022 (or `dotnet` SDK 8) on Windows.
dotnet restore TypeIt4Me.sln
```

## Running the tests

```bash
dotnet test TypeIt4Me.sln -c Release
```

CI runs the same `dotnet test` against `windows-latest`. Make sure the
build and test pass locally before opening a PR.

## PR checklist

- [ ] Tests added or updated; `dotnet test` is green locally.
- [ ] `dotnet build TypeIt4Me.sln -c Release` is clean.
- [ ] README and docs updated if public behavior changed.
- [ ] `CHANGELOG.md` updated under `[Unreleased]`.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).
Be respectful; assume good faith.
