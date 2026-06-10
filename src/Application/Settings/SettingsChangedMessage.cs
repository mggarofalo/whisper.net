// The typed message broadcast on the instant-apply channel (WHISPER-78) when settings are committed.
// Generalizes the ad-hoc SettingsChangeBroadcaster: instead of a bespoke event, a committed, valid change
// is sent through CommunityToolkit's IMessenger as this strongly-typed message, so any running service can
// register weakly for it and reconfigure live (e.g. the hotkey matcher rebinds) without an app restart.
// Carries the new settings as the message value via ValueChangedMessage<AppSettings>.

using CommunityToolkit.Mvvm.Messaging.Messages;
using Domain.Settings;

namespace Application.Settings;

public sealed class SettingsChangedMessage(AppSettings settings) : ValueChangedMessage<AppSettings>(settings);
