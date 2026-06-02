namespace WebAPI.OpenFinance.Aggregation
{
    // Encrypts/decrypts provider access tokens before they touch the database.
    // Backed by ASP.NET Core Data Protection now ($0); swap for AWS KMS in production.
    public interface ITokenProtector
    {
        string Protect(string plaintext);
        string Unprotect(string ciphertext);
    }
}
