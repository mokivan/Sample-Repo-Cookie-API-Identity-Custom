using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace TestIdentity.Configuration
{
    public sealed class SecurityOptions
    {
        public const string SectionName = "Security";

        public bool AllowSelfAssignedRoles { get; init; }
        public bool ExposeMachineDebugHeaders { get; init; }
        public bool RequireHttpsForAuthCookie { get; init; } = true;
        public string[] TrustedProxies { get; init; } = [];
        public string[] TrustedNetworks { get; init; } = [];

        public static bool IsValid(SecurityOptions options)
        {
            return options.TrustedProxies.All(IsValidIpAddress)
                && options.TrustedNetworks.All(IsValidCidrNotation);
        }

        public static void ApplyForwardingConfiguration(ForwardedHeadersOptions options, SecurityOptions securityOptions)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(securityOptions);

            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedHost
                | ForwardedHeaders.XForwardedProto;

            if (securityOptions.TrustedProxies.Length == 0 && securityOptions.TrustedNetworks.Length == 0)
            {
                return;
            }

            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var trustedProxy in securityOptions.TrustedProxies)
            {
                if (IPAddress.TryParse(trustedProxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var trustedNetwork in securityOptions.TrustedNetworks)
            {
                if (TryParseNetwork(trustedNetwork, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
            }
        }

        private static bool IsValidIpAddress(string value)
        {
            return string.IsNullOrWhiteSpace(value) || IPAddress.TryParse(value, out _);
        }

        private static bool IsValidCidrNotation(string value)
        {
            return string.IsNullOrWhiteSpace(value) || TryParseNetwork(value, out _);
        }

        private static bool TryParseNetwork(string value, out System.Net.IPNetwork network)
        {
            network = default!;

            var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) || !int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (prefixLength is < 0 or > 32)
                {
                    return false;
                }
            }
            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (prefixLength is < 0 or > 128)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            network = new System.Net.IPNetwork(address, prefixLength);
            return true;
        }
    }
}
