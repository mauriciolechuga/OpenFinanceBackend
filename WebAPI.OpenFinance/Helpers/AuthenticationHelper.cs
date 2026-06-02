using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Helpers
{
    // Authentication: lookup, password verification, registration, and login-attempt blocking.
    public static class AuthenticationHelper
    {
        // Hashes and verifies passwords using ASP.NET Core's PKBDF2-based hasher.
        // Passwords are never stored or compared in plaintext.
        private static readonly PasswordHasher<ClientCredentialModel> PasswordHasher = new();

        public static async Task<ClientsModel> GetClientByEmail(OpenFinanceContext context, string email)
        {
            return await context.Clients
                .Where(c => c.clientEmail == email)
                .FirstOrDefaultAsync();
        }

        public static async Task<bool> CheckEmailExists(OpenFinanceContext context, string email)
        {
            return await context.Clients.AnyAsync(c => c.clientEmail == email);
        }

        // Verifies the password against the stored hash. On failure, decrements the
        // client's remaining login attempts (which may block the client).
        public static async Task<bool> CheckPassword(OpenFinanceContext context, int clientID, string password)
        {
            var credential = await GetClientCredentialByClientID(context, clientID);

            if (credential == null)
            {
                return false;
            }

            var result = PasswordHasher.VerifyHashedPassword(credential, credential.clientPassword, password);

            if (result == PasswordVerificationResult.Failed)
            {
                await DecreaseRemainingLoginAttempts(context, clientID);
                return false;
            }

            // Transparently upgrade legacy/weaker hashes to the current format on successful login.
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                credential.clientPassword = PasswordHasher.HashPassword(credential, password);
                await context.SaveChangesAsync();
            }

            return true;
        }

        // Registers a new client and returns its generated ID.
        public static async Task<int> RegisterClient(OpenFinanceContext context, string clientName, string clientEmail, string clientAddress)
        {
            var newClient = new ClientsModel
            {
                clientName = clientName,
                clientEmail = clientEmail,
                clientAddress = clientAddress
            };

            context.Clients.Add(newClient);
            await context.SaveChangesAsync();

            return newClient.clientID;
        }

        // Stores the client's credential with the password hashed.
        public static async Task RegisterClientCredential(OpenFinanceContext context, int clientID, string clientPassword)
        {
            var newCredential = new ClientCredentialModel
            {
                clientID = clientID,
                // Set explicitly so the value is correct regardless of provider (the DB default is
                // not applied by every EF provider, e.g. the in-memory one used in tests).
                remainingLoginAttempts = 3,
                isBlocked = false
            };
            newCredential.clientPassword = PasswordHasher.HashPassword(newCredential, clientPassword);

            context.ClientCredentials.Add(newCredential);
            await context.SaveChangesAsync();
        }

        public static async Task<ClientCredentialModel> GetClientCredentialByClientID(OpenFinanceContext context, int clientID)
        {
            return await context.ClientCredentials
                .Where(c => c.clientID == clientID)
                .FirstOrDefaultAsync();
        }

        // Records a successful login and resets the attempt counter.
        public static async Task UpdateLastLogin(OpenFinanceContext context, int clientID)
        {
            var clientCredential = await GetClientCredentialByClientID(context, clientID);

            clientCredential.lastLogin = DateTime.UtcNow;
            clientCredential.remainingLoginAttempts = 3;

            await context.SaveChangesAsync();
        }

        // Decrements remaining attempts; blocks the client once they hit zero.
        public static async Task DecreaseRemainingLoginAttempts(OpenFinanceContext context, int clientID)
        {
            var clientCredential = await GetClientCredentialByClientID(context, clientID);

            clientCredential.remainingLoginAttempts--;

            if (clientCredential.remainingLoginAttempts <= 0)
            {
                await BlockClient(context, clientID);
            }

            await context.SaveChangesAsync();
        }

        // True if the client is blocked and the block has not yet expired;
        // otherwise lifts an expired block and returns false.
        public static async Task<bool> CheckIfClientIsBlocked(OpenFinanceContext context, int clientID)
        {
            var clientCredential = await GetClientCredentialByClientID(context, clientID);

            if (clientCredential.isBlocked && clientCredential.blockedUntil > DateTime.UtcNow)
            {
                return true;
            }

            await UnblockClient(context, clientID);
            return false;
        }

        // Blocks the client for 5 minutes.
        public static async Task BlockClient(OpenFinanceContext context, int clientID)
        {
            var clientCredential = await GetClientCredentialByClientID(context, clientID);

            clientCredential.isBlocked = true;
            clientCredential.blockedUntil = DateTime.UtcNow.AddMinutes(5);

            await context.SaveChangesAsync();
        }

        public static async Task UnblockClient(OpenFinanceContext context, int clientID)
        {
            var clientCredential = await GetClientCredentialByClientID(context, clientID);

            clientCredential.isBlocked = false;
            clientCredential.blockedUntil = null;

            await context.SaveChangesAsync();
        }
    }
}
