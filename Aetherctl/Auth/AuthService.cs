using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Aetherctl.Auth
{
    /// <summary>
    /// Service for authenticating with Azure AD B2C using MSAL.
    /// </summary>
    public class AuthService : IAsyncDisposable
    {
        private IPublicClientApplication? _pca;
        private MsalCacheHelper? _cacheHelper;
        private readonly string _tenant;
        private readonly string _policy;
        private readonly string _clientId;
        private readonly string[] _scopes;

        public AuthService(string tenant, string policy, string clientId, string scopes)
        {
            _tenant = tenant;
            _policy = policy;
            _clientId = clientId;
            _scopes = string.IsNullOrEmpty(scopes) 
                ? new[] { $"{clientId}/.default" } 
                : scopes.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.StartsWith("api://") ? s : $"api://{s}")
                        .ToArray();
        }

        private async Task<IPublicClientApplication> GetPcaAsync()
        {
            if (_pca != null)
                return _pca;

            var authority = $"https://{_tenant}/tfp/{_tenant}/{_policy}/";
            
            _pca = PublicClientApplicationBuilder
                .Create(_clientId)
                .WithB2CAuthority(authority)
                .WithRedirectUri("http://localhost")
                .Build();

            // Configure MSAL cache persistence
            var storageProperties = new StorageCreationPropertiesBuilder(
                "aetherctl_msal_cache",
                Path.Combine(GetCacheDirectory(), "msal_cache"))
                .WithMacKeyChain(serviceName: "Aetherctl", accountName: "MSALCache")
                .WithLinuxKeyring(
                    schemaName: "aetherctl.msal.tokencache",
                    collection: "default",
                    secretLabel: "MSAL Token Cache",
                    attribute1: new KeyValuePair<string, string>("Aetherctl", "TokenCache"),
                    attribute2: new KeyValuePair<string, string>("Version", "1"))
                .Build();

            _cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
            _cacheHelper.RegisterCache(_pca.UserTokenCache);

            return _pca;
        }

        private string GetCacheDirectory()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(appData, "Aetherctl");
            }
            else
            {
                var xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                if (string.IsNullOrEmpty(xdgCache))
                {
                    xdgCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
                }
                return Path.Combine(xdgCache, "aetherctl");
            }
        }

        /// <summary>
        /// Acquires a token using device code flow (default for interactive use).
        /// </summary>
        public async Task<string> AcquireTokenDeviceCodeAsync()
        {
            var pca = await GetPcaAsync();
            
            AuthenticationResult result;
            try
            {
                // Try to get token silently first
                var accounts = await pca.GetAccountsAsync();
                if (accounts.Any())
                {
                    try
                    {
                        result = await pca.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();
                        return result.AccessToken;
                    }
                    catch (MsalUiRequiredException)
                    {
                        // Silent auth failed, fall through to device code
                    }
                }

                // Use device code flow
                result = await pca.AcquireTokenWithDeviceCode(
                    _scopes,
                    deviceCodeResult =>
                    {
                        Console.WriteLine(deviceCodeResult.Message);
                        return Task.FromResult(0);
                    })
                    .ExecuteAsync();

                return result.AccessToken;
            }
            catch (MsalException ex)
            {
                throw new InvalidOperationException($"Authentication failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Acquires a token using ROPC flow (for CI/testing only).
        /// Requires B2C_ALLOW_ROPC=1 environment variable.
        /// </summary>
        public async Task<string> AcquireTokenROPCAsync(string username, string password)
        {
            if (Environment.GetEnvironmentVariable("B2C_ALLOW_ROPC") != "1")
            {
                throw new InvalidOperationException("ROPC flow is disabled. Set B2C_ALLOW_ROPC=1 to enable (for CI/testing only).");
            }

            var pca = await GetPcaAsync();
            
            try
            {
                var result = await pca.AcquireTokenByUsernamePassword(
                    _scopes,
                    username,
                    password.ToSecureString())
                    .ExecuteAsync();

                return result.AccessToken;
            }
            catch (MsalException ex)
            {
                throw new InvalidOperationException($"ROPC authentication failed: {ex.Message}", ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cacheHelper?.UnregisterCache(_pca?.UserTokenCache);
            _pca = null;
            _cacheHelper = null;
        }
    }

    internal static class StringExtensions
    {
        public static System.Security.SecureString ToSecureString(this string str)
        {
            var secure = new System.Security.SecureString();
            foreach (var c in str)
            {
                secure.AppendChar(c);
            }
            return secure;
        }
    }
}

