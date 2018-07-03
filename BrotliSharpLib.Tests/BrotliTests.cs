using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace BrotliSharpLib.Tests
{
    [TestFixture]
    public class BrotliTests
    {
        private static string TestdataDir = "testdata";

        private static readonly Dictionary<string, string> DecompressTestFiles = new Dictionary<string, string>()
        {
            { "10x10y.compressed", "10x10y" },
            { "64x.compressed", "64x" },
            { "alice29.txt.compressed", "alice29.txt" },
            { "asyoulik.txt.compressed", "asyoulik.txt" },
            { "backward65536.compressed", "backward65536" },
            { "compressed_file.compressed", "compressed_file" },
            { "compressed_repeated.compressed", "compressed_repeated" },
            { "empty.compressed", "empty" },
            { "empty.compressed.00", "empty" },
            { "empty.compressed.01", "empty" },
            { "empty.compressed.02", "empty" },
            { "empty.compressed.03", "empty" },
            { "empty.compressed.04", "empty" },
            { "empty.compressed.05", "empty" },
            { "empty.compressed.06", "empty" },
            { "empty.compressed.07", "empty" },
            { "empty.compressed.08", "empty" },
            { "empty.compressed.09", "empty" },
            { "empty.compressed.10", "empty" },
            { "empty.compressed.11", "empty" },
            { "empty.compressed.12", "empty" },
            { "empty.compressed.13", "empty" },
            { "empty.compressed.14", "empty" },
            { "empty.compressed.15", "empty" },
            { "empty.compressed.16", "empty" },
            { "empty.compressed.17", "empty" },
            { "empty.compressed.18", "empty" },
            { "lcet10.txt.compressed", "lcet10.txt" },
            { "mapsdatazrh.compressed", "mapsdatazrh" },
            { "monkey.compressed", "monkey" },
            { "plrabn12.txt.compressed", "plrabn12.txt" },
            { "quickfox.compressed", "quickfox" },
            { "quickfox_repeated.compressed", "quickfox_repeated" },
            { "random_org_10k.bin.compressed", "random_org_10k.bin" },
            { "ukkonooa.compressed", "ukkonooa" },
            { "x.compressed", "x" },
            { "x.compressed.00", "x" },
            { "x.compressed.01", "x" },
            { "x.compressed.02", "x" },
            { "x.compressed.03", "x" },
            { "xyzzy.compressed", "xyzzy" },
            { "zeros.compressed", "zeros" },
            { "pokemon_lvl_3.proto.br", "pokemon_lvl_3.proto" },
            { "pokemon_lvl_11.proto.br", "pokemon_lvl_11.proto" },
        };

        private static readonly List<string> CompressTestFiles = new List<string>
        {
            "alice29.txt",
            "asyoulik.txt",
            "lcet10.txt",
            "plrabn12.txt"
        };

        [SetUp]
        public void Setup()
        {
            // Look for testdata directory in project
            string directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, TestdataDir)))
                directory = Path.GetDirectoryName(directory);

            Assert.NotNull(directory, "testdata directory does not exist");
            TestdataDir = Path.Combine(directory, TestdataDir);
        }

        private void CompareBuffers(byte[] original, byte[] decompressed, string fileName)
        {
            // Compare with the original
            Assert.AreEqual(original.Length, decompressed.Length, "Decompressed length does not match original (" + fileName + ")");

            for (int i = 0; i < original.Length; i++)
                Assert.AreEqual(original[i], decompressed[i], "Decompressed byte-mismatch detected (" + fileName + ")");
        }

        [Test, Order(1)]
        public void Decompress()
        {
            // Run tests on data
            foreach (var kvp in DecompressTestFiles)
            {
                var compressedFilePath = Path.Combine(TestdataDir, kvp.Key);
                var originalFilePath = Path.Combine(TestdataDir, kvp.Value);

                Assert.IsTrue(File.Exists(compressedFilePath), "Unable to find the compressed test file: " + kvp.Key);
                Assert.IsTrue(File.Exists(originalFilePath), "Unable to find the test file: " + kvp.Value);

                // Decompress the compressed data
                var compressed = File.ReadAllBytes(compressedFilePath);
                var decompressed = Brotli.DecompressBuffer(compressed, 0, compressed.Length);

                // Compare the decompressed version with the original
                var original = File.ReadAllBytes(originalFilePath);
                CompareBuffers(original, decompressed, kvp.Key + " --> " + kvp.Value);
            }
        }

        [Test, Order(2)]
        public void DecompressViaStream()
        {
            // Run tests on data
            foreach (var kvp in DecompressTestFiles)
            {
                var compressedFilePath = Path.Combine(TestdataDir, kvp.Key);
                var originalFilePath = Path.Combine(TestdataDir, kvp.Value);

                Assert.IsTrue(File.Exists(compressedFilePath), "Unable to find the compressed test file: " + kvp.Key);
                Assert.IsTrue(File.Exists(originalFilePath), "Unable to find the test file: " + kvp.Value);

                // Decompress the compressed data
                using (var fs = File.OpenRead(compressedFilePath))
                using (var ms = new MemoryStream())
                using (var bs = new BrotliStream(fs, CompressionMode.Decompress))
                {
                    try
                    {
                        bs.CopyTo(ms);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Decompress failed with for " + kvp.Key, e);
                    }

                    // Compare the decompressed version with the original
                    var original = File.ReadAllBytes(originalFilePath);
                    CompareBuffers(original, ms.ToArray(), kvp.Key + " --> " + kvp.Value);
                }
            }
        }

        private static readonly int[] CompressQualities = { 1, 6, 9, 11 };

        [Test, Order(3)]
        public void Compress()
        {
            // Run tests on data
            foreach (var file in CompressTestFiles)
            {
                var filePath = Path.Combine(TestdataDir, file);
                Assert.IsTrue(File.Exists(filePath), "Unable to find the test file: " + file);

                foreach (var quality in CompressQualities)
                {
                    // Compress using the current quality
                    var original = File.ReadAllBytes(filePath);
                    var compressed = Brotli.CompressBuffer(original, 0, original.Length, quality);

                    // Decompress and verify with original
                    try
                    {
                        var decompressed = Brotli.DecompressBuffer(compressed, 0, compressed.Length);
                        CompareBuffers(original, decompressed, file);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Decompress failed with compressed buffer quality " + quality + " for " + file, e);
                    }
                }
            }
        }

        [Test, Order(4)]
        public void CompressViaStream()
        {
            // Run tests on data
            foreach (var file in CompressTestFiles)
            {
                var filePath = Path.Combine(TestdataDir, file);
                Assert.IsTrue(File.Exists(filePath), "Unable to find the test file: " + file);

                foreach (var quality in CompressQualities)
                {
                    // Compress using the current quality
                    using (var fs = File.OpenRead(filePath))
                    using (var ms = new MemoryStream())
                    {
                        using (var bs = new BrotliStream(ms, CompressionMode.Compress))
                        {
                            bs.SetQuality(quality);
                            fs.CopyTo(bs);
                            bs.Dispose();

                            var compressed = ms.ToArray();
                            // Decompress and verify with original
                            try
                            {
                                var decompressed = Brotli.DecompressBuffer(compressed, 0, compressed.Length);
                                CompareBuffers(File.ReadAllBytes(filePath), decompressed, file);
                            }
                            catch (Exception e)
                            {
                                throw new Exception("Decompress failed with compressed buffer quality " + quality + " for " + file, e);
                            }
                        }
                    }
                }
            }
        }
    }
}