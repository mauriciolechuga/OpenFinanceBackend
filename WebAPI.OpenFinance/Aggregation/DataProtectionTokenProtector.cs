using Microsoft.AspNetCore.DataProtection;

namespace WebAPI.OpenFinance.Aggregation
{
    // Default ITokenProtector using ASP.NET Core Data Protection. Keys are managed by the framework;
    // in production, point Data Protection at a persisted, KMS-encrypted key ring.
    public class DataProtectionTokenProtector : ITokenProtector
    {
        private readonly IDataProtector _protector;

        public DataProtectionTokenProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("OpenFinance.ProviderAccessToken.v1");
        }

        public string Protect(string plaintext) => _protector.Protect(plaintext);

        public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
    }
}
