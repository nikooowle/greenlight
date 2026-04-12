# Milestone 6: Notifications & Alerts

## Plan

**Delivers:** Automated email and in-app alerts so the right people know immediately when something goes wrong.

- **Notification triggers:**
  - SLA breach: subprocess misses deadline
  - SLA warning: subprocess approaching deadline (configurable threshold)
  - Subprocess failure: task fails or stops
  - DQ failure: data quality check fails (wired up in Milestone 7)
  - MCP completion: full run completes
- **Recipient management:** configure who gets which alerts (by email, by role: operator, risk manager, management)
- **In-app notification bell:** unread count badge, dropdown panel with recent alerts, click to navigate to related item
- **Notification history log:** all sent alerts with timestamp, type, recipient, content
- **Email sending** via SMTP (can point at ING's mail relay)

## Status: ⬚ Not Started
