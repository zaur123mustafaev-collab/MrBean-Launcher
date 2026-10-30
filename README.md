# 🟢 MrBean Launcher

**MrBean Launcher** is an independent desktop launcher project for **Minecraft: Java Edition**, developed for Windows.

The project aims to provide a convenient interface for selecting a Minecraft version, managing launch settings, and signing in with a Microsoft account to play Minecraft using an account that owns the game.

> **Development status:** MrBean Launcher is a work in progress. Some features, including Microsoft sign-in, are being configured and tested.

## ✨ Planned and existing features

- Windows desktop interface built with C# and Windows Forms.
- Minecraft: Java Edition launch and version configuration.
- Player name and launcher settings.
- Microsoft account sign-in integration (under development).
- Minecraft ownership and profile verification through official services (under development).

Feature availability may vary by build. See the latest release notes when a public build becomes available.

## 🔐 Microsoft account sign-in

MrBean Launcher is intended to use Microsoft's official OAuth authentication flow. The sign-in integration is being developed to authenticate users securely and check access to Minecraft: Java Edition through the appropriate Microsoft, Xbox, and Minecraft services.

- Users should sign in through Microsoft's official authentication pages.
- The launcher should not request or store Microsoft account passwords.
- Access to licensed online services requires a valid account and appropriate game entitlement.
- Authentication tokens, if stored, should be protected locally and never committed to this repository.

**Current status:** Microsoft authentication is not yet confirmed to be working in a released build.

## 💻 Platform

- **Operating system:** Windows
- **Application technology:** C# / .NET Framework 4.8 / Windows Forms
- **Game:** Minecraft: Java Edition

## 📦 Installation

A verified public installer or release is not yet available here. Once a tested build is published, installation instructions will be added to this section.

Do not download unofficial builds claiming to be MrBean Launcher from unknown sources.

## 🛠️ Development

This repository is the public information page for the MrBean Launcher project. Source files, screenshots, documentation, and tested releases may be added as development progresses.

## 📬 Contact and feedback

For questions, bug reports, or feedback, please open an [issue](https://github.com/zaur123mustafaev-collab/MrBean-Launcher/issues) in this repository.

## ⚖️ Disclaimer

MrBean Launcher is an **independent, unofficial** project. It is **not affiliated with, endorsed by, or approved by Microsoft, Mojang Studios, or Minecraft**.

Minecraft and related trademarks belong to their respective owners. This project does not grant access to Minecraft without a valid license.
