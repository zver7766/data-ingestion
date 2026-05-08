namespace DataIngestion.Infrastructure;

public static class IngestionConstants
{
    public const int CurrencyCodeLength = 3;

    public const int SourceChannelMinLength = 2;
    public const int SourceChannelMaxLength = 32;

    public const decimal MinTransactionAmount = 0m;
    public const decimal MaxTransactionAmount = 1_000_000_000m;

    public const int TransactionDateFutureSkewMinutes = 5;

    public const int MaxAmountDecimalPlaces = 6;

    public const int TransactionDuplicateWindowSeconds = 2;

    public const int BatchChunkSize = 2000;

    public const int TransactionsDefaultPageSize = 50;
    public const int TransactionsMaxPageSize = 500;

    public static readonly HashSet<string> AllowedSourceChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "web",
        "mobile",
        "pos",
        "api",
        "batch"
    };
}