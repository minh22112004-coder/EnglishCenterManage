using Microsoft.EntityFrameworkCore;
using Npgsql;
using ManagementSystem.Api.Common;

namespace ManagementSystem.Api.Tests
{
    public class DbErrorHelperTests
    {
        [Fact]
        public void DbErrorHelper_ReturnsNull_ForNullOrStandardException()
        {
            Assert.Null(DbErrorHelper.FindPostgresException(null));
            Assert.Null(DbErrorHelper.FindPostgresException(new InvalidOperationException("Generic error")));
            Assert.False(DbErrorHelper.IsUniqueViolation(new Exception()));
            Assert.False(DbErrorHelper.IsTransientConcurrencyError(new Exception()));
            Assert.Null(DbErrorHelper.GetSqlState(new Exception()));
        }

        [Fact]
        public void DbErrorHelper_DetectsUniqueViolation_DirectAndNested()
        {
            var pgEx = new PostgresException("duplicate key value", "ERROR", "ERROR", DbErrorHelper.UniqueViolation);
            var dbUpdateEx = new DbUpdateException("Database error occurred", pgEx);

            Assert.True(DbErrorHelper.IsUniqueViolation(pgEx));
            Assert.True(DbErrorHelper.IsUniqueViolation(dbUpdateEx));
            Assert.False(DbErrorHelper.IsTransientConcurrencyError(dbUpdateEx));
            Assert.Equal("23505", DbErrorHelper.GetSqlState(dbUpdateEx));
        }

        [Theory]
        [InlineData("40001", true)]
        [InlineData("40P01", true)]
        [InlineData("23505", false)]
        [InlineData("42P01", false)]
        public void DbErrorHelper_DetectsTransientConcurrencyError(string sqlState, bool expectedTransient)
        {
            var pgEx = new PostgresException("concurrency error", "ERROR", "ERROR", sqlState);
            var aggEx = new AggregateException("Batch failed", new Exception("Outer wrapper", pgEx));

            Assert.Equal(expectedTransient, DbErrorHelper.IsTransientConcurrencyError(aggEx));
            Assert.Equal(sqlState, DbErrorHelper.GetSqlState(aggEx));
        }
    }
}
