// One choice in the General section's overlay-display picker: the stable device name to persist (null for
// the default "follow the primary display") and the friendly label the ComboBox shows. Kept a tiny
// view-model type in Logic so the picker is driven WPF-free in specs; the ComboBox binds Label for display
// and DeviceName as the selected value.

namespace Logic.AppManagement.Shell;

public sealed record OverlayMonitorOption(string? DeviceName, string Label);
