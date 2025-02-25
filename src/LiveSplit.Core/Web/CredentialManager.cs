
using System;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using System.IO;

namespace LiveSplit.Web;

public class CredentialManager
{
    public static Credential ReadCredential(string secret_identifier)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ReadCredential_Windows(secret_identifier);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ReadCredential_Linux(secret_identifier);
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported.");
        }
    }

    public static void WriteCredential(string secret_identifier, string user_name, string secret)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WriteCredential_Windows(secret_identifier, user_name, secret);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            WriteCredential_Linux(secret_identifier, user_name, secret);
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported.");
        }
    }

    public static bool CredentialExists(string secret_identifier)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CredentialExists_Windows(secret_identifier);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return CredentialExists_Linux(secret_identifier);
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported.");
        }
    }

    public static void DeleteCredential(string secret_identifier)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DeleteWindowsCredential(secret_identifier);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            DeleteCredential_Linux(secret_identifier);
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported.");
        }
    }

    #region Windows Implementation
    private static Credential ReadCredential_Windows(string secret_identifier)
    {
        // Windows implementation using CredentialManagerInterop
        using var credentialManager = new CredentialManagerInterop();
        ICredential credential = credentialManager.GetCredential(secret_identifier);
        return new Credential(
            CredentialType.Generic,
            secret_identifier,
            credential.UserName,
            credential.Password
        );
    }

    private static void WriteCredential_Windows(string secret_identifier, string user_name, string secret)
    {
        using var credentialManager = new CredentialManagerInterop();
        credentialManager.SaveCredential_Windows(new Credential
        (
            CredentialType.Generic,
            secret_identifier,
            user_name,
            secret
        ));
    }

    private static bool CredentialExists_Windows(string secret_identifier)
    {
        using var credentialManager = new CredentialManagerInterop();
        return credentialManager.GetCredential(secret_identifier) != null;
    }

    private static void DeleteWindowsCredential(string applicationName)
    {
        using var credentialManager = new CredentialManagerInterop();
        credentialManager.DeleteCredential_Windows(applicationName);
    }

    private sealed class CredentialManagerInterop : IDisposable
    {
        private readonly object _credentialManager;

        public CredentialManagerInterop()
        {
            _credentialManager = Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("00000100-0000-0000-C000-000000000046")),
                true
            );
        }

        public void Dispose()
        {
            Marshal.ReleaseComObject(_credentialManager);
        }

        public ICredential GetCredential(string applicationName)
        {
            return (ICredential)_credentialManager.GetType().InvokeMember(
                "GetCredential",
                BindingFlags.InvokeMethod,
                null,
                _credentialManager,
                new object[] { applicationName, false }
            );
        }

        public void SaveCredential_Windows(Credential credential)
        {
            var credentialInterface = (ICredential)_credentialManager.GetType().InvokeMember(
                "CreateCredential",
                BindingFlags.InvokeMethod,
                null,
                _credentialManager,
                new object[] { credential.ApplicationName, credential.UserName, credential.Password }
            );

            _credentialManager.GetType().InvokeMember(
                "SaveCredential",
                BindingFlags.InvokeMethod,
                null,
                _credentialManager,
                new object[] { credentialInterface, true }
            );
        }

        public void DeleteCredential_Windows(string applicationName)
        {
            _credentialManager.GetType().InvokeMember(
                "DeleteCredential",
                BindingFlags.InvokeMethod,
                null,
                _credentialManager,
                new object[] { applicationName }
            );
        }
    }

    [ComImport, Guid("00000101-0000-0000-C000-000000000046")]
    private interface ICredential
    {
        [DispId(1)] string TargetName { get; }
        [DispId(2)] string UserName { get; }
        [DispId(3)] string Password { get; }
    }
    #endregion

    #region Linux Implementation
    private static Credential ReadCredential_Linux(string applicationName)
    {
        // Linux implementation using SecretStorage
        SecretStorage.Secret secret = SecretStorage.GetSecret(applicationName);
        if (secret == null)
        {
            return null;
        }

        return new Credential(
            CredentialType.Generic,
            applicationName,
            secret.Username,
            secret.Password
        );
    }

    private static void WriteCredential_Linux(string applicationName, string userName, string secret)
    {
        SecretStorage.SetSecret(applicationName, userName, secret);
    }

    private static bool CredentialExists_Linux(string applicationName)
    {
        return SecretStorage.GetSecret(applicationName) != null;
    }

    private static void DeleteCredential_Linux(string applicationName)
    {
        SecretStorage.DeleteSecret(applicationName);
    }

    private static class SecretStorage
    {
        public static Secret GetSecret(string applicationName)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"search --username {applicationName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
            }
            catch (FileNotFoundException)
            {
                throw new NoLibsecretException();
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return null;
            }

            string[] lines = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var secret = new Secret();

            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { '\t' }, 2);
                if (parts.Length < 2)
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "username":
                        secret.Username = parts[1];
                        break;
                    case "password":
                        secret.Password = parts[1];
                        break;
                }
            }

            return secret;
        }

        public static void SetSecret(string applicationName, string username, string password)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"store --label {applicationName} --username {username} {password}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
            }
            catch (FileNotFoundException)
            {
                throw new NoLibsecretException();
            }

            process.WaitForExit();
        }

        public static void DeleteSecret(string applicationName)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"clear {applicationName}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
            }
            catch (FileNotFoundException)
            {
                throw new NoLibsecretException();
            }

            process.WaitForExit();
        }

        internal sealed class Secret
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class NoLibsecretException : Exception
        {
            public NoLibsecretException()
            {
                new Exception("LiveSplit on Linux needs libsecret - tools and libsecret - common installed from your package manager.");
            }
        }
    }
    #endregion
}

public enum CredentialType
{
    Generic = 1,
    DomainPassword,
    DomainCertificate,
    DomainVisiblePassword,
    GenericCertificate,
    DomainExtended,
    Maximum,
    MaximumEx = Maximum + 1000,
}

public class Credential
{
    public CredentialType CredentialType { get; }

    public string ApplicationName { get; }

    public string UserName { get; }

    public string Password { get; }

    public Credential(CredentialType credentialType, string applicationName, string userName, string password)
    {
        ApplicationName = applicationName;
        UserName = userName;
        Password = password;
        CredentialType = credentialType;
    }

    public override string ToString()
    {
        return string.Format("CredentialType: {0}, ApplicationName: {1}, UserName: {2}, Password: {3}", CredentialType, ApplicationName, UserName, Password);
    }
}