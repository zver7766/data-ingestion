namespace DataIngestion.Contracts.Stats;

public sealed record StatsResponse(long TotalCustomers, long TotalEvents, long EventsLast24h);