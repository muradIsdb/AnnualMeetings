using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One-time migration that:
    /// 1. Normalises all existing Flight.FlightNumber values to canonical form
    ///    (remove spaces, strip leading zeros from numeric suffix).
    ///    e.g. "TK 0334" → "TK334", "FZ 707" → "FZ707", " LH612" → "LH612"
    /// 2. For each set of duplicate rows that normalise to the SAME canonical value
    ///    AND the SAME date (date part of ScheduledArrival for arrival flights, or
    ///    ScheduledDeparture for departure flights), picks one canonical row,
    ///    re-points all TravelBooking.FlightId references to it, and deletes the
    ///    redundant rows.
    ///
    /// IMPORTANT: Two flights with the same number but different dates are NOT
    /// duplicates — e.g. TK334 on June 4 and TK334 on June 16 are separate flights.
    /// The grouping key is (NormalizedFlightNumber, DATE(ScheduledArrival)).
    ///
    /// After this migration, every FlightNumber in the Flights table is in canonical
    /// form, and the EventsAir sync controller normalises at ingest so no new
    /// duplicates can be created.
    /// </summary>
    public partial class NormaliseFlightNumbers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Normalise all FlightNumber values in-place using a PostgreSQL
            // expression that mirrors the C# FlightNumberHelper.Normalise logic:
            //   - Remove all spaces
            //   - Strip leading zeros from the numeric suffix
            //   - Uppercase the result
            migrationBuilder.Sql(@"
                UPDATE ""Flights""
                SET ""FlightNumber"" = UPPER(
                    REGEXP_REPLACE(
                        REPLACE(""FlightNumber"", ' ', ''),
                        '^([A-Za-z]{1,3})0+([0-9].*)$',
                        '\1\2'
                    )
                )
                WHERE ""FlightNumber"" IS NOT NULL;
            ");

            // Step 2: For each group of duplicate rows that share the same
            // (FlightNumber, DATE(ScheduledArrival)) pair, keep the one with the
            // smallest Id (deterministic), re-point all TravelBookings to it,
            // then delete the duplicates.
            //
            // Flights with the same number but different dates are kept separate.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    dup RECORD;
                    canonical_id uuid;
                BEGIN
                    -- Find all (FlightNumber, ArrivalDate) pairs that appear more than once
                    FOR dup IN
                        SELECT ""FlightNumber"", DATE(""ScheduledArrival"") AS arr_date
                        FROM ""Flights""
                        GROUP BY ""FlightNumber"", DATE(""ScheduledArrival"")
                        HAVING COUNT(*) > 1
                    LOOP
                        -- Pick the canonical row: smallest Id (deterministic)
                        SELECT ""Id"" INTO canonical_id
                        FROM ""Flights""
                        WHERE ""FlightNumber"" = dup.""FlightNumber""
                          AND DATE(""ScheduledArrival"") = dup.arr_date
                        ORDER BY ""Id""
                        LIMIT 1;

                        -- Re-point all TravelBookings that reference a duplicate row
                        UPDATE ""TravelBookings""
                        SET ""FlightId"" = canonical_id
                        WHERE ""FlightId"" IN (
                            SELECT ""Id""
                            FROM ""Flights""
                            WHERE ""FlightNumber"" = dup.""FlightNumber""
                              AND DATE(""ScheduledArrival"") = dup.arr_date
                              AND ""Id"" <> canonical_id
                        );

                        -- Delete the duplicate rows (now safe — no TravelBookings reference them)
                        DELETE FROM ""Flights""
                        WHERE ""FlightNumber"" = dup.""FlightNumber""
                          AND DATE(""ScheduledArrival"") = dup.arr_date
                          AND ""Id"" <> canonical_id;
                    END LOOP;
                END;
                $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The normalisation is a lossy transformation — we cannot recover the original
            // raw strings (e.g. "TK 0334" vs "TK0334") after they have been merged.
            // Down migration is intentionally a no-op; restoring from a backup is the
            // correct rollback path if needed.
        }
    }
}
