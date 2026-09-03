#  Kerkenez Calendar

> **Lightweight, High-Performance Windows Desktop Calendar made to create a suit with KerkenezMail**  
> *Target Framework:* **.NET 10** (`net10.0-windows10.0.19041.0`)  
> *Author:* Kerkenez Development & DeepMind / Antigravity Engineering  

---

## 🌟 Overview

**Kerkenez Calendar** is a fast, modern, and lightweight native Windows calendar client built with C# and Windows Forms on .NET 10. Designed to create a suit with the github.com/KerkenezDev/KerkenezMail with minimal overhead.

### 📐 UI Layout Mapping (from Kerkenez Mail)
* **Mail Body Box $\rightarrow$ Month View Grid**: Interactive 7x6 month calendar grid displaying days, event counts, current date highlights, and category badges.
* **Summary Box $\rightarrow$ Chosen Event Inspector / Quick Form**: Full inspector view for the selected event (time, reminder, category, location, notes) with edit, delete, and alert test actions, as well as an inline quick-add form.
* **Side Inbox $\rightarrow$ That Day's Scheduled Events**: Chronological side-panel listing all scheduled events for the active date, with time badges, reminder tags, and quick `+` add button.
* **Left Collapsible Sidebar**: Fast micro-animated sidebar (`Calendar`, `Agenda`, `Accounts`, `Settings`) with tray daemon health indicator.

---

## 🔔 Event Times & "Time to Remember" (Reminders)

When scheduling events in Kerkenez Calendar, users can configure:
* **Timing**: Specific start & end times or full-day (`IsAllDay`) events.
* **Time to Remember**:
  * None
  * At time of event (0 min)
  * 5 minutes before
  * 10 minutes before
  * 15 minutes before
  * 30 minutes before
  * 1 hour before
  * 2 hours before
  * 1 day before
  * 2 days before
  * 1 week before
* **Categorization & Color Tags**: Work, Personal, Important, Meeting, Birthday, General.
* **Account Association**: Link events to configured email accounts from shared storage.

---

## 🔒 Shared Storage & Ecosystem Architecture

* **Shared Accounts Directory**: `%APPDATA%\Kerkenez\`
* **Shared Accounts Database**: `%APPDATA%\Kerkenez\accounts.dat`  
  * Encrypted using Windows DPAPI (`ProtectedData.Protect` / `Unprotect`).
  * Features multi-entropy fallback support (`Kerkenez.SecureAccounts.v1`, `EmailSummarizer.SecureAccounts.v1`, `KerkenezMail.SecureAccounts.v1`).
  * Seamlessly auto-migrates existing accounts from `EmailSummarizer` upon first launch.
* **Calendar Configuration & Events**: `%APPDATA%\Kerkenez\calendar\`
  * `config.json`: Calendar preferences, week start day, default reminder, and notification flags.
  * `events.json`: Events store with atomic temporary file swapping (`.tmp` $\rightarrow$ swap) for corruption immunity.

---

## ⚡ Sub-Megabyte Background System Tray Daemon

The application includes an independent, ultra-lightweight background system tray daemon:

```bash
KerkenezCalendar.exe --daemon
# or
KerkenezCalendar.exe --tray
```

### Daemon Capabilities
* **Headless Win32 Message-Only Loop**: Pure Win32 window (`RegisterClassEx`, `CreateWindowEx`, `DefWindowProc`) with **zero WinForms control allocations**.
* **Sub-Megabyte Memory Trimming**: Aggressive GC compaction and `EmptyWorkingSet()` memory flushing keeping active RAM footprint at minimal levels ($< 1\text{ MB}$ to $3.8\text{ MB}$ working set).
* **System Tray Notifications**: Delivers native Windows balloon/toast notifications when event reminders trigger.
* **Tray Context Menu**:
  * `📅 Open Kerkenez Calendar` (Bold default, focuses main window)
  * `🔔 Next Reminder Status`
  * `🔕 Toggle Notifications`
  * `➕ Quick Add Event...`
  * `❌ Exit Daemon`

---

## 🛠️ Building & Running

### Prerequisites
* Windows 10 (Build 19041+) or Windows 11
* .NET 10 SDK (`10.0.400+`)

### Commands
```powershell
# Build in Release mode
dotnet build -c Release

# Run Main GUI application
./bin/Release/net10.0-windows10.0.19041.0/KerkenezCalendar.exe

# Run Background Daemon
./bin/Release/net10.0-windows10.0.19041.0/KerkenezCalendar.exe --daemon
```
