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

```powershell
dotnet restore TypeIt4Me.sln
dotnet build TypeIt4Me.sln -c Release --no-restore
dotnet test TypeIt4Me.sln -c Release --no-build --logger "trx;LogFileName=results.trx" --logger "console;verbosity=normal"
dotnet run --project tools/UiReview/UiReview.csproj -c Release -- ui-review
```

CI runs this Release gate and UI resource review on GitHub-hosted
`windows-latest`, then publishes a self-contained executable. Inspect the UI
review artifact when changing XAML. See [the design system](docs/UI-DESIGN.md)
and [verification guide](docs/VALIDATION.md) for theme, screenshot, and native
smoke-test conventions. Use a fresh isolated directory for production smoke;
never use a personal AppData profile as a test fixture.

## PR checklist

- [ ] Tests added or updated; `dotnet test` is green locally.
- [ ] Release build succeeds; new warnings are resolved and existing warnings are reported.
- [ ] README and docs updated if public behavior changed.
- [ ] `CHANGELOG.md` updated under `[Unreleased]`.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).
Be respectful; assume good faith.
