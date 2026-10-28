using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace EmiliosSoundboard.Tests
{
    public class AppLoggerTests
    {
        private static readonly Regex LineShape = new Regex(
            @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} (TRACE|DEBUG|INFO |WARN |ERROR|FATAL) \[[a-z]{0,8} *] .*$",
            RegexOptions.Compiled);

        private static string ReadAll(AppLogger logger)
        {
            return File.ReadAllText(logger.CurrentFile);
        }

        [Fact]
        public void Write_AppendsOneWellFormedLine()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root);
                logger.Info("ui", "hello world");

                string text = ReadAll(logger).TrimEnd();
                Assert.Single(text.Split('\n'));
                Assert.Matches(LineShape, text);
                Assert.Contains("hello world", text);
                Assert.Contains("INFO", text);
                Assert.Equal(1, logger.WrittenCount);
            }
        }

        [Fact]
        public void Write_CreatesTheFolderAndUsesTheDateInTheName()
        {
            using (var tmp = new TempLibrary())
            {
                string folder = Path.Combine(tmp.Root, "logs", "nested");
                var logger = new AppLogger(folder);
                logger.Info("app", "x");

                Assert.True(Directory.Exists(folder));
                Assert.EndsWith(DateTime.Now.ToString("yyyy-MM-dd") + ".log", logger.CurrentFile);
                Assert.True(File.Exists(logger.CurrentFile));
            }
        }

        [Fact]
        public void MinimumLevel_DropsQuieterEntries()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root) { MinimumLevel = LogLevel.Info };
                logger.Debug("app", "invisible");
                logger.Info("app", "visible");

                string text = ReadAll(logger);
                Assert.DoesNotContain("invisible", text);
                Assert.Contains("visible", text);

                logger.MinimumLevel = LogLevel.Debug;
                logger.Debug("app", "now visible");
                Assert.Contains("now visible", ReadAll(logger));
            }
        }

        [Fact]
        public void Write_KeepsMultiLineMessagesOnOneLine()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root);
                logger.Warn("storage", "line one\nline two\r\nline three");

                string[] lines = ReadAll(logger).TrimEnd().Split('\n');
                Assert.Single(lines);
                Assert.Contains("line one / line two / line three", lines[0]);
            }
        }

        [Fact]
        public void Write_RecordsTheExceptionTypeMessageAndInnerException()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root);
                var error = new InvalidOperationException("outer failed",
                    new IOException("inner failed"));

                logger.Error("audio", "playback failed", error);

                string text = ReadAll(logger);
                Assert.Contains("playback failed", text);
                Assert.Contains("InvalidOperationException: outer failed", text);
                Assert.Contains("---> IOException: inner failed", text);
                Assert.Contains("    ", text);   // the exception block is indented
            }
        }

        [Fact]
        public void Recent_ReturnsNewestFirstAndHonoursTheRingCapacity()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root, ringCapacity: 10);
                for (int i = 1; i <= 25; i++) logger.Info("app", "entry " + i);

                var recent = logger.Recent(5);
                Assert.Equal(5, recent.Count);
                Assert.Equal("entry 25", recent[0].Message);
                Assert.Equal("entry 21", recent[4].Message);

                Assert.Equal(10, logger.Recent(500).Count);
            }
        }

        [Fact]
        public void Tail_ReturnsLinesOldestToNewest()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root);
                logger.Info("app", "first");
                logger.Info("app", "second");
                logger.Info("app", "third");

                string[] lines = logger.Tail(2).TrimEnd().Split('\n');
                Assert.Equal(2, lines.Length);
                Assert.Contains("second", lines[0]);
                Assert.Contains("third", lines[1]);
            }
        }

        [Fact]
        public void Rollover_StartsANewFileWhenTheSizeCapIsReached()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root, maxFileBytes: 16 * 1024);
                string filler = new string('x', 400);
                for (int i = 0; i < 80; i++) logger.Info("app", filler);

                var files = Directory.GetFiles(tmp.Root, "app-*.log").OrderBy(f => f).ToArray();
                Assert.True(files.Length >= 2, "expected a rolled file, got " + files.Length);
                Assert.Contains(files, f => f.EndsWith(".1.log"));
                Assert.True(new FileInfo(files[0]).Length <= 16 * 1024 + 1024);
            }
        }

        [Fact]
        public void Purge_DeletesFilesOlderThanTheRetentionWindow()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root, retentionDays: 3);
                string stale = Path.Combine(tmp.Root, "app-2020-01-01.log");
                File.WriteAllText(stale, "old");
                File.SetLastWriteTime(stale, DateTime.Now.AddDays(-10));

                string fresh = Path.Combine(tmp.Root, "app-2026-01-01.log");
                File.WriteAllText(fresh, "new");

                logger.Purge();

                Assert.False(File.Exists(stale));
                Assert.True(File.Exists(fresh));
            }
        }

        [Fact]
        public void Write_DoesNotThrowWhenTheFolderCannotBeUsed()
        {
            using (var tmp = new TempLibrary())
            {
                // A file where the logger wants a directory: creation fails, the app must survive.
                string blocker = Path.Combine(tmp.Root, "blocked");
                File.WriteAllText(blocker, "not a folder");

                AppLogger logger = null;
                Exception thrown = Record.Exception(() => logger = new AppLogger(blocker));
                Assert.Null(thrown);

                thrown = Record.Exception(() => logger.Info("app", "still fine"));
                Assert.Null(thrown);
                Assert.NotNull(logger.LastWriteError);
                Assert.Equal(0, logger.WrittenCount);
            }
        }

        [Fact]
        public void Describe_IsOneLineAndNeverThrows()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root);
                logger.Info("app", "x");

                string text = logger.Describe();
                Assert.Contains("app-", text);
                Assert.Contains("Info", text);
                Assert.DoesNotContain("\n", text);
            }
        }

        [Fact]
        public void Write_IsThreadSafe()
        {
            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root, ringCapacity: 1000);
                Parallel.For(0, 200, i => logger.Info("app", "parallel " + i));

                string[] lines = File.ReadAllLines(logger.CurrentFile);
                Assert.Equal(200, lines.Length);
                Assert.All(lines, l => Assert.Matches(LineShape, l));

                var ids = lines.Select(l => l.Substring(l.IndexOf("parallel", StringComparison.Ordinal))).Distinct().Count();
                Assert.Equal(200, ids);
            }
        }

        [Fact]
        public void StaticFacade_IsSilentWithoutAnInstalledLogger()
        {
            Log.Uninstall();
            Assert.False(Log.IsActive);

            Exception thrown = Record.Exception(() =>
            {
                Log.Info("app", "ignored");
                Log.Error("app", "ignored", new Exception("boom"));
            });
            Assert.Null(thrown);

            using (var tmp = new TempLibrary())
            {
                var logger = new AppLogger(tmp.Root);
                Log.Install(logger);
                Log.Info("app", "through the facade");
                Log.Uninstall();

                Assert.Contains("through the facade", ReadAll(logger));
            }
        }
    }
}
