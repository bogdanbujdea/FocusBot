# Phase 6: Store Submission

**User Feature**: Install FocusBot from the Windows Store.

**End State**: MSIX package validated and ready for Store submission.

## What to Build

### Package Configuration (`Package.appxmanifest`)

- Display name: FocusBot
- Capabilities: `runFullTrust` (required for Win32 window monitoring)
- Visual assets (all required icon sizes)
- App execution alias

### Store Assets

- App icons (44x44, 50x50, 150x150, 300x300, 400x400)
- Store screenshots (1366x768 or 2560x1440)
- Store listing description and keywords
- Privacy policy URL (required for API key handling)

### Store Compliance

- Handle app suspension/resume properly
- Data stored in `ApplicationData.Current.LocalFolder`
- No background tasks without user consent
- Proper error handling and user feedback

### Validation

- Run Windows App Certification Kit (WACK)
- Test on clean Windows 10 and Windows 11
- Fix any certification failures

## Checklist

- [ ] Configure `Package.appxmanifest` with correct display name
- [ ] Add `runFullTrust` capability
- [ ] Create app icons in all required sizes
- [ ] Create store screenshots
- [ ] Write store listing description
- [ ] Create privacy policy and host at public URL
- [ ] Handle app suspension properly
- [ ] Handle app resume properly
- [ ] Verify data storage location
- [ ] Add proper error handling throughout app
- [ ] Run Windows App Certification Kit
- [ ] Fix any WACK failures
- [ ] Test on clean Windows 10
- [ ] Test on clean Windows 11
- [ ] Create MSIX package for submission
