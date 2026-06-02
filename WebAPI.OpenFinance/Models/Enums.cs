namespace WebAPI.OpenFinance.Models
{
    // Stored as strings in the database (see OpenFinanceContext) so values stay readable
    // and adding members does not renumber existing rows.

    public enum AccountType
    {
        Chequing,
        Savings,
        CreditCard,
        Brokerage,
        Tfsa,
        Rrsp,
        Other
    }

    public enum ConnectionStatus
    {
        Pending,
        Active,
        Error,
        Disconnected
    }

    public enum SecurityType
    {
        Equity,
        Etf,
        MutualFund,
        Cash,
        Other
    }

    public enum TransactionType
    {
        Debit,
        Credit
    }
}
