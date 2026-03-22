# Cross-Device Session Sync

## Overview

Enable real-time synchronization of focus sessions across all of a user's devices—desktop app, browser extensions, and web dashboard. When you start, pause, or end a session on one device, all your other devices instantly reflect that change.

## Problem Statement

Users work across multiple devices throughout their day:
- Desktop PC at home
- Laptop at the office  
- Personal laptop on the go
- Multiple browser instances

Currently, each device operates independently. If you start a focus session on your desktop and then open your laptop, the laptop has no awareness of the active session. This creates confusion and data fragmentation—you can't seamlessly transition between devices while maintaining focus continuity.

## User Experience

### Starting a Session

**Scenario**: You start a focus session on your desktop app for "Write quarterly report"

**What happens**:
- Your desktop app immediately begins tracking focus
- Within 1-2 seconds, your browser extension badge updates to show active session
- Your web dashboard (if open) refreshes to display the in-progress session
- If you have Foqus open on your laptop, it shows a notification: "Session started on Home Desktop"

All devices now show the same active session with synchronized elapsed time and focus score.

### Ending a Session

**Scenario**: You finish your work and end the session from the browser extension

**What happens**:
- The extension stops tracking and saves the final session data
- Your desktop app receives the update and transitions back to "Ready" state
- The web dashboard refreshes to show the completed session in your history
- All devices are back in sync, ready for the next session

### Switching Devices Mid-Session

**Scenario**: You're working at home (desktop session active), then leave for a coffee shop and open your laptop

**What happens**:
- Your laptop loads and shows the active session that started on your desktop
- Your laptop continues tracking the same session
- When you interact with your laptop, its focus data contributes to the shared session
- The desktop app (if still open at home) also reflects the updated session state

### Session Conflicts

**Scenario**: You accidentally try to start a new session on your laptop while one is already running on your desktop

**What happens**:
- The system automatically ends the desktop session and starts the new one on your laptop
- Your desktop receives notification: "Session ended remotely from Work Laptop"
- Focus data from the desktop session is saved before the new session begins
- No data loss—both sessions are preserved in your history

This prevents duplicate sessions and ensures data integrity.

## Key Benefits

### Seamless Device Transitions
Move between devices naturally without worrying about session state. Your focus session follows you.

### Single Source of Truth
No more wondering "Did I already start a session?" or "Which device has my active session?"—all devices show the same state.

### Unified History
All focus sessions, regardless of which device they started on, appear in one unified timeline in the web dashboard.

### Better Insights
Analytics can show patterns like "You're most focused on your desktop in the morning, but switch to laptop in the afternoon."

### Peace of Mind
Never lose a session due to device switching. The system handles conflicts automatically while preserving your data.

## Technical Notes (High-Level)

- Uses WebSocket connections for near-instant updates (typically under 2 seconds)
- Requires internet connection for cross-device sync
- Same-machine sync (desktop ↔ extension) works offline via local connection
- Session state is always saved to the cloud before broadcasting updates
- Automatic reconnection if connection drops temporarily

## User Requirements

- Must be signed in with a Foqus account (free or paid)
- Devices must be online to receive real-time updates
- Each device must run a compatible Foqus client (desktop app v1.2+, extension v1.1+, web app)

## Future Enhancements

### Manual Conflict Resolution
Instead of auto-ending remote sessions, present a choice:
- "Session active on Home Desktop. End that session and start here?"
- "Join the existing session on this device?"
- "Cancel"

### Offline Queue
Allow session start/end even when offline, queue the operation, and sync when connection restored.

### Sync Status Indicators
Visual feedback showing sync state:
- ✓ Synced across devices
- ↻ Syncing...
- ⚠ Offline (local only)
- ✗ Sync failed (retry)

### Device Management
Web dashboard showing all registered devices:
- "Home Desktop (online, last seen 2m ago)"
- "Work Laptop (offline, last seen 3h ago)"
- Option to remove/deactivate old devices

### Selective Sync
Choose which devices participate in sync:
- "Pause sync on this device" for temporary local-only sessions
- Useful when you want to work without interrupting an active shared session

## Success Metrics

- **Primary**: Time to reflect session state change across devices (target: <2 seconds)
- **User satisfaction**: Reduction in "session confusion" support tickets
- **Engagement**: Increase in multi-device usage (users active on 2+ devices per day)
- **Reliability**: 99.5%+ successful sync operations (no lost sessions)

## Launch Strategy

**Phase 1: Desktop ↔ Web** (Minimal viable sync)
- Real-time session updates between desktop app and web dashboard
- Auto-conflict resolution (end remote session)
- No offline support

**Phase 2: Extension Integration**
- Add browser extension to the sync ecosystem
- Full parity across all three client types

**Phase 3: Advanced Features**
- Manual conflict resolution UI
- Offline queue with retry
- Sync status indicators
- Device management dashboard

**Phase 4: Polish**
- Selective sync options
- Advanced device preferences
- Multi-user testing and edge case hardening
