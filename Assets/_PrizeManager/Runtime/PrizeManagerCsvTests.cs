using System.IO;
using GETravelGames.PrizeManager;
using NUnit.Framework;

namespace GETravelGames.PrizeManager.Tests
{
    public sealed class PrizeManagerCsvTests
    {
        // ── Prize CSV parsing ─────────────────────────────────────────────────

        [Test]
        public void PreviewPrizeImportContent_ParsesCommaDelimitedPrizeCsv()
        {
            var csv = string.Join("\n", new[]
            {
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription,PrizePriority,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays",
                "10,2,Headphones,Noise cancelling,5,19:00,23:34,false,1|3",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewPrizeImportContent(csv, PrizeImportMode.Initialize);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Delimiter, Is.EqualTo(','));
            Assert.That(preview.Templates.Count, Is.EqualTo(1));
            Assert.That(preview.Instances.Count, Is.EqualTo(2));
            Assert.That(preview.Templates[0].Schedule.PrizeStartMinutesOfDay, Is.EqualTo(19 * 60));
            Assert.That(preview.Templates[0].Schedule.PrizeEndMinutesOfDay, Is.EqualTo((23 * 60) + 34));
            Assert.That(preview.Templates[0].Schedule.PrizeDays, Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void PreviewPrizeImportContent_ParsesSemicolonDelimitedPrizeCsv()
        {
            var csv = string.Join("\n", new[]
            {
                "PrizeCategoryId;PrizeAmount;PrizeName;PrizeDescription;PrizePriority;PrizeHourStart;PrizeHourEnd;HasToComeOutDuringHour;PrizeDays",
                "12;1;Voucher;Airport food;4;;;;",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewPrizeImportContent(csv, PrizeImportMode.Initialize);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Delimiter, Is.EqualTo(';'));
            Assert.That(preview.Instances.Count, Is.EqualTo(1));
            Assert.That(preview.Templates[0].PrizeName, Is.EqualTo("Voucher"));
        }

        [Test]
        public void PreviewPrizeImportContent_RejectsInvalidScheduleValues()
        {
            var csv = string.Join("\n", new[]
            {
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription,PrizePriority,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays",
                "10,1,Headphones,Noise cancelling,5,10,9,true,1|3",
                "11,1,Gift Bag,Travel kit,3,,,true,9",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewPrizeImportContent(csv, PrizeImportMode.Initialize);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Issues.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "PrizeHourEnd"), Is.True);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "HasToComeOutDuringHour"), Is.True);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "PrizeDays"), Is.True);
        }

        [Test]
        public void PreviewPrizeImportContent_RejectsInvalidHourMinuteFormats()
        {
            var csv = string.Join("\n", new[]
            {
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription,PrizePriority,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays",
                "10,1,Headphones,Noise cancelling,5,24:00,23:00,false,1|3",
                "11,1,Gift Bag,Travel kit,3,21:15,23:60,false,1|3",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewPrizeImportContent(csv, PrizeImportMode.Initialize);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "PrizeHourStart"), Is.True);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "PrizeHourEnd"), Is.True);
        }

        [Test]
        public void PreviewPrizeImportContent_RejectsConflictingDuplicateCategoryRows()
        {
            var csv = string.Join("\n", new[]
            {
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription,PrizePriority,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays",
                "10,1,Headphones,Noise cancelling,5,9,11,false,1|3",
                "10,1,Headphones,Updated text,5,9,11,false,1|3",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewPrizeImportContent(csv, PrizeImportMode.Initialize);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "PrizeCategoryId"), Is.True);
        }

        // ── Settings CSV parsing ──────────────────────────────────────────────

        [Test]
        public void PreviewSettingsImportContent_ParsesBaseAndThresholdRows()
        {
            // Col 7 = KioskCount (new, required on base row).
            var csv = string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent,KioskCount",
                "America/Buenos_Aires,15,20,10,,25,,3",
                ",,,15,50,45,60,",
                ",,,20,75,,,",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewSettingsImportContent(csv);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Settings.Timezone, Is.EqualTo("America/Buenos_Aires"));
            Assert.That(preview.Settings.KioskCount, Is.EqualTo(3));
            Assert.That(preview.Settings.FalsePrizeThresholds.Count, Is.EqualTo(2));
            Assert.That(preview.Settings.ForcedHourThresholds.Count, Is.EqualTo(1));
        }

        [Test]
        public void PreviewSettingsImportContent_RejectsMissingKioskCount()
        {
            var csv = string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent,KioskCount",
                "UTC,10,30,5,,15,",  // KioskCount blank
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewSettingsImportContent(csv);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "KioskCount"), Is.True);
        }

        [Test]
        public void PreviewSettingsImportContent_RejectsInvalidBaseRowAndThresholdPairs()
        {
            var csv = string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent,KioskCount",
                ",0,0,101,25,abc,10,2",
                ",,,20,,45,,",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewSettingsImportContent(csv);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "Timezone"), Is.True);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "PrizeReservationTimeoutMinutes"), Is.True);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "FalsePrizeChancePercent"), Is.True);
            Assert.That(preview.Issues.Exists(i => i.ColumnName == "ForcedHourChancePercent"), Is.True);
        }

        // ── Prize import + kiosk distribution ────────────────────────────────

        [Test]
        public void PrizeAdminService_InitializeImport_PreservesWonHistoryAndResequencesInstances()
        {
            var filePath = WriteTempFile(string.Join("\n", new[]
            {
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription,PrizePriority,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays",
                "10,1,Headphones,Noise cancelling,5,9,11,false,1|3",
            }));

            try
            {
                var store = new PrizeAdminStateStore();
                store.AddWonPrizeRecord(new WonPrizeRecord
                {
                    WonPrizeInstanceId = "10-0002",
                    PrizeCategoryId = 10,
                    PrizeName = "Headphones",
                    PrizeDescription = "Noise cancelling",
                });

                var service = new PrizeAdminService(new PrizeCsvService(), store);
                var result = service.ApplyPrizeImport(filePath, PrizeImportMode.Initialize);

                Assert.That(result.Success, Is.True);
                Assert.That(store.WonPrizeHistory.Count, Is.EqualTo(1));
                Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(1));
                // Sequence continues past the won-prize history high watermark.
                Assert.That(store.AvailablePrizeInstances[0].PrizeInstanceId, Is.EqualTo("10-0003"));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void PrizeAdminStateStore_DistributesRoundRobinAcrossKiosks()
        {
            // 4 instances of category 10, 2 kiosks → each kiosk gets 2.
            var store = new PrizeAdminStateStore();
            store.ReplaceRuntimeSettings(new PrizeRuntimeSettings { Timezone = "UTC", KioskCount = 2, MaxPrizesPerDay = 10, PrizeReservationTimeoutMinutes = 15 });

            var templates = new[]
            {
                new PrizeTemplate { PrizeCategoryId = 10, PrizeName = "Prize A", PrizePriority = 1, Schedule = new PrizeSchedule() },
            };

            var instances = new[]
            {
                MakeInstance("10-0001", 10),
                MakeInstance("10-0002", 10),
                MakeInstance("10-0003", 10),
                MakeInstance("10-0004", 10),
            };

            store.ReplaceAvailablePrizes(templates, instances);

            var counts = store.KioskPrizeCounts;
            Assert.That(counts[1], Is.EqualTo(2));
            Assert.That(counts[2], Is.EqualTo(2));
        }

        [Test]
        public void PrizeAdminStateStore_RedistributesWhenKioskCountChanges()
        {
            var store = new PrizeAdminStateStore();
            // Start with 1 kiosk.
            store.ReplaceRuntimeSettings(new PrizeRuntimeSettings { Timezone = "UTC", KioskCount = 1, MaxPrizesPerDay = 10, PrizeReservationTimeoutMinutes = 15 });
            store.ReplaceAvailablePrizes(
                new[] { new PrizeTemplate { PrizeCategoryId = 10, PrizeName = "A", PrizePriority = 1, Schedule = new PrizeSchedule() } },
                new[] { MakeInstance("10-0001", 10), MakeInstance("10-0002", 10), MakeInstance("10-0003", 10) });

            Assert.That(store.KioskPrizeCounts[1], Is.EqualTo(3));

            // Switch to 3 kiosks – each should get 1.
            store.ReplaceRuntimeSettings(new PrizeRuntimeSettings { Timezone = "UTC", KioskCount = 3, MaxPrizesPerDay = 10, PrizeReservationTimeoutMinutes = 15 });

            var counts = store.KioskPrizeCounts;
            Assert.That(counts[1], Is.EqualTo(1));
            Assert.That(counts[2], Is.EqualTo(1));
            Assert.That(counts[3], Is.EqualTo(1));
        }

        [Test]
        public void PrizeAdminService_AddMode_RejectsTemplateConflictsAgainstExistingStore()
        {
            var existingStore = new PrizeAdminStateStore();
            existingStore.AddAvailablePrizes(
                new[]
                {
                    new PrizeTemplate
                    {
                        PrizeCategoryId = 10,
                        PrizeName = "Headphones",
                        PrizeDescription = "Noise cancelling",
                        PrizePriority = 5,
                        Schedule = new PrizeSchedule
                        {
                            PrizeStartMinutesOfDay = 9 * 60,
                            PrizeEndMinutesOfDay = 11 * 60,
                            PrizeDays = new System.Collections.Generic.List<int> { 1, 3 },
                        },
                    },
                },
                new[]
                {
                    new PrizeInstance
                    {
                        PrizeInstanceId = "10-0001",
                        PrizeCategoryId = 10,
                        PrizeName = "Headphones",
                        PrizeDescription = "Noise cancelling",
                        PrizePriority = 5,
                        Schedule = new PrizeSchedule
                        {
                            PrizeStartMinutesOfDay = 9 * 60,
                            PrizeEndMinutesOfDay = 11 * 60,
                            PrizeDays = new System.Collections.Generic.List<int> { 1, 3 },
                        },
                    },
                });

            var filePath = WriteTempFile(string.Join("\n", new[]
            {
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription,PrizePriority,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays",
                "10,1,Headphones,Updated text,5,9,11,false,1|3",
            }));

            try
            {
                var service = new PrizeAdminService(new PrizeCsvService(), existingStore);
                var result = service.ApplyPrizeImport(filePath, PrizeImportMode.Add);

                Assert.That(result.Success, Is.False);
                Assert.That(existingStore.AvailablePrizeInstances.Count, Is.EqualTo(1));
                Assert.That(result.Issues.Exists(i => i.ColumnName == "PrizeCategoryId"), Is.True);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void PrizeAdminService_ApplySettingsImport_ReplacesActiveSettingsWithKioskCount()
        {
            var filePath = WriteTempFile(string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent,KioskCount",
                "UTC,10,30,5,,15,,4",
                ",,,7,40,,,",
            }));

            try
            {
                var store = new PrizeAdminStateStore();
                var service = new PrizeAdminService(new PrizeCsvService(), store);

                var result = service.ApplySettingsImport(filePath);

                Assert.That(result.Success, Is.True);
                Assert.That(store.ActiveSettings.Timezone, Is.EqualTo("UTC"));
                Assert.That(store.ActiveSettings.KioskCount, Is.EqualTo(4));
                Assert.That(store.ActiveSettings.FalsePrizeThresholds.Count, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        // ── CSV exports ───────────────────────────────────────────────────────

        [Test]
        public void ExportWonPrizesCsv_WritesExpectedColumnsIncludingKioskId()
        {
            var service = new PrizeCsvService();
            var csv = service.ExportWonPrizesCsv(new[]
            {
                new WonPrizeRecord
                {
                    WonPrizeInstanceId = "10-0001",
                    PrizeCategoryId = 10,
                    PrizeName = "Headphones, Large",
                    PrizeDescription = "Noise \"cancelling\"",
                    WinnerOffice = "HQ",
                    WinnerName = "Jane Doe",
                    WinnerPhoneNumber = "555-0101",
                    KioskId = 2,
                },
            });

            Assert.That(csv, Does.StartWith(
                "WonPrizeInstanceId,PrizeCategoryId,PrizeName,PrizeDescription," +
                "WinnerOffice,WinnerName,WinnerPhoneNumber,KioskId"));
            Assert.That(csv, Does.Contain("\"Headphones, Large\""));
            Assert.That(csv, Does.Contain("\"Noise \"\"cancelling\"\"\""));
            Assert.That(csv, Does.Contain(",2"));
        }

        [Test]
        public void ExportPrizePoolSubtractionCsv_AggregatesWonPrizesByCategory()
        {
            var service = new PrizeCsvService();
            var records = new[]
            {
                new WonPrizeRecord { PrizeCategoryId = 10, PrizeName = "Headphones", PrizeDescription = "Noise cancelling", KioskId = 1 },
                new WonPrizeRecord { PrizeCategoryId = 10, PrizeName = "Headphones", PrizeDescription = "Noise cancelling", KioskId = 2 },
                new WonPrizeRecord { PrizeCategoryId = 20, PrizeName = "Voucher",    PrizeDescription = "Food",             KioskId = 1 },
            };

            var csv = service.ExportPrizePoolSubtractionCsv(records);

            Assert.That(csv, Does.StartWith("PrizeCategoryId,AmountToSubtract,PrizeName,PrizeDescription"));
            // Category 10: 2 wins; category 20: 1 win.
            Assert.That(csv, Does.Contain("10,2,Headphones,Noise cancelling"));
            Assert.That(csv, Does.Contain("20,1,Voucher,Food"));
        }

        // ── Debug claim / cancel / confirm lifecycle ──────────────────────────

        [Test]
        public void PrizeAdminService_DebugClaimCancelAndConfirm_MovePrizeAcrossStoreStates()
        {
            var store = new PrizeAdminStateStore();
            // 1 kiosk (default), 1 prize.
            store.AddAvailablePrizes(
                new[]
                {
                    new PrizeTemplate
                    {
                        PrizeCategoryId = 10,
                        PrizeName = "Headphones",
                        PrizeDescription = "Noise cancelling",
                        PrizePriority = 5,
                    },
                },
                new[]
                {
                    new PrizeInstance
                    {
                        PrizeInstanceId = "10-0001",
                        PrizeCategoryId = 10,
                        PrizeName = "Headphones",
                        PrizeDescription = "Noise cancelling",
                        PrizePriority = 5,
                    },
                });

            var service = new PrizeAdminService(new PrizeCsvService(), store);

            // Claim
            var claimResult = service.DebugClaimPrize();
            Assert.That(claimResult.Success, Is.True);
            Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(0));
            Assert.That(store.ActiveReservation, Is.Not.Null);
            Assert.That(store.ActiveReservation.ReservedPrize.PrizeInstanceId, Is.EqualTo("10-0001"));
            Assert.That(store.ActiveReservation.KioskId, Is.EqualTo(1));

            // Cancel – prize must return to pool
            var cancelResult = service.DebugCancelClaim();
            Assert.That(cancelResult.Success, Is.True);
            Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(1));
            Assert.That(store.ActiveReservation, Is.Null);
            Assert.That(store.WonPrizeHistory.Count, Is.EqualTo(0));

            // Claim again, then confirm
            var secondClaim = service.DebugClaimPrize();
            Assert.That(secondClaim.Success, Is.True);

            var confirmResult = service.DebugConfirmClaim();
            Assert.That(confirmResult.Success, Is.True);
            Assert.That(store.ActiveReservation, Is.Null);
            Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(0));
            Assert.That(store.WonPrizeHistory.Count, Is.EqualTo(1));
            Assert.That(store.WonPrizeHistory[0].WonPrizeInstanceId, Is.EqualTo("10-0001"));
            Assert.That(store.WonPrizeHistory[0].KioskId, Is.EqualTo(1));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static PrizeInstance MakeInstance(string id, ushort categoryId)
        {
            return new PrizeInstance
            {
                PrizeInstanceId = id,
                PrizeCategoryId = categoryId,
                PrizeName = "Prize",
                PrizeDescription = string.Empty,
                PrizePriority = 1,
                Schedule = new PrizeSchedule(),
            };
        }

        private static string WriteTempFile(string content)
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, content);
            return path;
        }
    }
}
