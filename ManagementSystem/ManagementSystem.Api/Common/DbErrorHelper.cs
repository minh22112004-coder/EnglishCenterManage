using System;
using System.Collections.Generic;
using Npgsql;

namespace ManagementSystem.Api.Common
{
    public static class DbErrorHelper
    {
        public const string UniqueViolation = "23505";
        public const string SerializationFailure = "40001";
        public const string DeadlockDetected = "40P01";

        public static PostgresException? FindPostgresException(Exception? ex)
        {
            if (ex == null) return null;

            var queue = new Queue<Exception>();
            queue.Enqueue(ex);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is PostgresException pgEx)
                {
                    return pgEx;
                }

                if (current is AggregateException agg)
                {
                    foreach (var inner in agg.InnerExceptions)
                    {
                        if (inner != null) queue.Enqueue(inner);
                    }
                }
                else if (current.InnerException != null)
                {
                    queue.Enqueue(current.InnerException);
                }
            }

            return null;
        }

        public static bool IsUniqueViolation(Exception? ex)
        {
            return FindPostgresException(ex)?.SqlState == UniqueViolation;
        }

        public static bool IsTransientConcurrencyError(Exception? ex)
        {
            var state = FindPostgresException(ex)?.SqlState;
            return state == SerializationFailure || state == DeadlockDetected;
        }

        public static string? GetSqlState(Exception? ex)
        {
            return FindPostgresException(ex)?.SqlState;
        }
    }
}
