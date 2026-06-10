# Contributing to ClickyKeys

Thank you for taking the time to contribute! This document explains how to get involved and what to expect.

---

## Before You Start

### Contributor License Agreement

All contributors must sign the [Contributor License Agreement](CLA.md) before their pull request can be merged. This is handled automatically — when you open a PR, a bot will ask you to confirm your agreement. It takes about 30 seconds.

### Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

---

## How to Contribute

### Reporting Bugs

Use the [Bug Report](.github/ISSUE_TEMPLATE/bug_report.md) template. Please include:
- Your Windows version and ClickyKeys version
- Clear steps to reproduce
- What you expected vs. what actually happened

### Suggesting Features

Use the [Feature Request](.github/ISSUE_TEMPLATE/feature_request.md) template. Consider whether the feature belongs in the free tier or the upcoming Pro tier — this helps prioritize.

### Reporting Security Vulnerabilities

**Do not open a public issue for security vulnerabilities.** See [SECURITY.md](SECURITY.md) for responsible disclosure instructions.

### Submitting a Pull Request

1. Fork the repository and create a branch from `main`.
2. Make your changes. Keep commits focused and descriptive.
3. Test your changes on Windows with at least one OBS scenario if applicable.
4. Open a pull request against `main` and fill in the PR template.
5. Sign the CLA when prompted by the bot.
6. Wait for review — feedback will arrive within a few days.

---

## What Gets Accepted

Contributions most likely to be merged:
- Bug fixes with a clear reproduction case
- Performance improvements with measurable impact
- Accessibility improvements
- Documentation fixes

Contributions that require prior discussion:
- New features — open an issue first to align on scope
- Changes to the UI layout or color scheme
- Anything touching the statistics or data storage layer

Contributions that will not be accepted:
- Features intended for the Pro tier (developed privately)
- Changes that introduce external dependencies without prior agreement
- Code that does not run on Windows 10 and later

---

## Development Setup

Requirements:
- Windows 10 or later
- Visual Studio 2022 with .NET Desktop Development workload
- .NET 8.0 SDK

```
git clone https://github.com/Reksaku/ClickyKeys.git
cd ClickyKeys
start ClickyKeys.sln
```

Build and run from Visual Studio or via `dotnet run` in the `ClickyKeys/` directory.

---

## License

Contributions become part of the Project and are distributed under the Project's license (currently Elastic License 2.0), subject to the Contributor License Agreement.