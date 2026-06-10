# Security Policy

## Supported Versions

Security fixes are applied to the latest release only.

| Version | Supported |
| ------- | :-------: |
| Latest stable | ✅ |
| Older versions | ❌ |

---

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

ClickyKeys monitors keyboard and mouse input by design. Any vulnerability that could expose this data to unintended parties — locally or over a network — is taken very seriously.

### How to Report

Use **[GitHub Security Advisories](https://github.com/Reksaku/ClickyKeys/security/advisories/new)** to submit a private report. You will need a GitHub account.

Alternatively, you can reach the maintainer directly through the contact form at [clickykeys.fun](https://clickykeys.fun).

### What to Include

A useful report contains:
- A description of the vulnerability and its potential impact
- Steps to reproduce or a proof-of-concept
- The affected version(s)
- Any suggested mitigation, if you have one

### What to Expect

- **Acknowledgement** within 5 business days
- **Status update** within 14 days (confirmed, not reproducible, or in progress)
- **Credit** in the release notes if you wish, once the fix is published

---

## Scope

Issues considered in scope:

- Unintended transmission of keystroke or mouse data over a network
- Local privilege escalation via the application
- Unauthorised access to stored statistics
- Vulnerabilities in the upcoming cloud sync or account features

Issues considered out of scope:

- Social engineering attacks
- Vulnerabilities in third-party dependencies not directly exploitable through ClickyKeys
- Issues affecting unsupported Windows versions

---

## A Note on Privacy

ClickyKeys is a keyboard and mouse monitoring application. The source code is publicly available so that anyone can verify exactly what data is collected and where it goes. If you have concerns about data handling rather than a security vulnerability, please open a regular issue or visit [clickykeys.fun](https://clickykeys.fun).
