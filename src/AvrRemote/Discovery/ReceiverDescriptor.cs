// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

namespace AvrRemote.Discovery;

/// <summary>Compact identity for a discovered receiver, shown in the UI.</summary>
public sealed record ReceiverDescriptor(
    string Host,
    string Manufacturer,
    string Model,
    string FriendlyName,
    string Uuid)
{
    public override string ToString() =>
        string.IsNullOrWhiteSpace(FriendlyName) ? $"{Model} ({Host})" : $"{FriendlyName} · {Host}";
}

