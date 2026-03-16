using System.IO;
using GETravelGames.PrizeManager;
using NUnit.Framework;

namespace GETravelGames.PrizeManager.Tests
{
    public sealed class PrizeManagerCsvTests
    {
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
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "PrizeHourEnd"), Is.True);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "HasToComeOutDuringHour"), Is.True);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "PrizeDays"), Is.True);
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
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "PrizeHourStart"), Is.True);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "PrizeHourEnd"), Is.True);
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
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "PrizeCategoryId"), Is.True);
        }

        [Test]
        public void PreviewSettingsImportContent_ParsesBaseAndThresholdRows()
        {
            var csv = string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent",
                "America/Buenos_Aires,15,20,10,,25,",
                ",,,15,50,45,60",
                ",,,20,75,,",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewSettingsImportContent(csv);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.Settings.Timezone, Is.EqualTo("America/Buenos_Aires"));
            Assert.That(preview.Settings.FalsePrizeThresholds.Count, Is.EqualTo(2));
            Assert.That(preview.Settings.ForcedHourThresholds.Count, Is.EqualTo(1));
        }

        [Test]
        public void PreviewSettingsImportContent_RejectsInvalidBaseRowAndThresholdPairs()
        {
            var csv = string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent",
                ",0,0,101,25,abc,10",
                ",,,20,,45,",
            });

            var service = new PrizeCsvService();
            var preview = service.PreviewSettingsImportContent(csv);

            Assert.That(preview.IsValid, Is.False);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "Timezone"), Is.True);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "PrizeReservationTimeoutMinutes"), Is.True);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "FalsePrizeChancePercent"), Is.True);
            Assert.That(preview.Issues.Exists(issue => issue.ColumnName == "ForcedHourChancePercent"), Is.True);
        }

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
                Assert.That(store.AvailablePrizeInstances[0].PrizeInstanceId, Is.EqualTo("10-0003"));
            }
            finally
            {
                File.Delete(filePath);
            }
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
                Assert.That(result.Issues.Exists(issue => issue.ColumnName == "PrizeCategoryId"), Is.True);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void PrizeAdminService_ApplySettingsImport_ReplacesActiveSettings()
        {
            var filePath = WriteTempFile(string.Join("\n", new[]
            {
                "Timezone,PrizeReservationTimeoutMinutes,MaxPrizesPerDay,FalsePrizeChancePercent,FalsePrizeThresholdPercent,ForcedHourChancePercent,ForcedHourThresholdPercent",
                "UTC,10,30,5,,15,",
                ",,,7,40,,",
            }));

            try
            {
                var store = new PrizeAdminStateStore();
                var service = new PrizeAdminService(new PrizeCsvService(), store);

                var result = service.ApplySettingsImport(filePath);

                Assert.That(result.Success, Is.True);
                Assert.That(store.ActiveSettings.Timezone, Is.EqualTo("UTC"));
                Assert.That(store.ActiveSettings.FalsePrizeThresholds.Count, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Test]
        public void ExportWonPrizesCsv_WritesExpectedColumns()
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
                },
            });

            Assert.That(csv, Does.StartWith("WonPrizeInstanceId,PrizeCategoryId,PrizeName,PrizeDescription,WinnerOffice,WinnerName,WinnerPhoneNumber"));
            Assert.That(csv, Does.Contain("\"Headphones, Large\""));
            Assert.That(csv, Does.Contain("\"Noise \"\"cancelling\"\"\""));
        }

        [Test]
        public void PrizeAdminService_DebugClaimCancelAndConfirm_MovePrizeAcrossStoreStates()
        {
            var store = new PrizeAdminStateStore();
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

            var claimResult = service.DebugClaimPrize();
            Assert.That(claimResult.Success, Is.True);
            Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(0));
            Assert.That(store.ActiveReservation, Is.Not.Null);
            Assert.That(store.ActiveReservation.ReservedPrize.PrizeInstanceId, Is.EqualTo("10-0001"));

            var cancelResult = service.DebugCancelClaim();
            Assert.That(cancelResult.Success, Is.True);
            Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(1));
            Assert.That(store.ActiveReservation, Is.Null);
            Assert.That(store.WonPrizeHistory.Count, Is.EqualTo(0));

            var secondClaimResult = service.DebugClaimPrize();
            Assert.That(secondClaimResult.Success, Is.True);

            var confirmResult = service.DebugConfirmClaim();
            Assert.That(confirmResult.Success, Is.True);
            Assert.That(store.ActiveReservation, Is.Null);
            Assert.That(store.AvailablePrizeInstances.Count, Is.EqualTo(0));
            Assert.That(store.WonPrizeHistory.Count, Is.EqualTo(1));
            Assert.That(store.WonPrizeHistory[0].WonPrizeInstanceId, Is.EqualTo("10-0001"));
        }

        private static string WriteTempFile(string content)
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, content);
            return path;
        }
    }
}
