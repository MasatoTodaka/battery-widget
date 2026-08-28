namespace LogiBatteryWidget.App.Settings;

/// <summary>
/// User's choice of whether to show a device in the widget, and where in the list. Order in the
/// persisted list *is* the display order - there's no separate index field to keep in sync.
/// </summary>
public sealed record DevicePreference(string Key, string Name, bool Visible);
