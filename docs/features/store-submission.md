# Store submission changes

## Overview

Changes made to the app package manifest (`src/FocusBot.App/Package.appxmanifest`) so the app can be submitted to the Microsoft Store and is correctly configured as a Windows desktop-only app.

## 1. Display name

**Problem:** Partner Center reported that the package's manifest used a display name that had not been reserved: `FocusBot.App`.

**Change:** Updated the manifest to use the reserved name **Focus Bot** in:

- **Package/Properties/DisplayName** — value used by the Store for validation and listing.
- **uap:VisualElements** — `DisplayName` and `Description` (name shown in Start menu, taskbar, and Store).

**Requirement:** Ensure **Focus Bot** is reserved for your app in Partner Center (App identity / Manage app names). Rebuild and create a new .msix after this change; the generated `AppxManifest.xml` will contain the updated display name.

## 2. Device family (desktop only)

**Problem:** The package targeted both **Windows.Universal** and **Windows.Desktop**, so the app could be offered to Mobile, Team, and Mixed Reality in Partner Center. The app is intended to be desktop-only.

**Change:** Removed the **Windows.Universal** target from `<Dependencies>`. The package now declares only:

- **Windows.Desktop** — MinVersion 10.0.17763.0, MaxVersionTested 10.0.19041.0.

**Partner Center:** On the Packages page, under "Device family availability", leave only **Windows 10/11 Desktop** checked. Uncheck Windows 10 Mobile, Windows 10 Team, and Windows 10 Mixed Reality so the app is only offered on desktop.

## Summary

| Item            | Before              | After        |
|-----------------|---------------------|--------------|
| Display name    | FocusBot.App        | Focus Bot    |
| Target families | Universal + Desktop | Desktop only |

Rebuild the app, create a new .msix, and upload it to Partner Center. The **runFullTrust** capability warning is expected for a full-trust desktop app and is handled separately (approval/declaration in the submission flow).
