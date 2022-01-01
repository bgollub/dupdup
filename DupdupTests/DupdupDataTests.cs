using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[TestClass]
public class DupdupDataTests
{
    private const int BUFFER_MULTIPLE = Dupdup.BUFFER_SIZE * Dupdup.BUFFER_SIZE;
    private const int NOT_BUFFER_MULTIPLE = BUFFER_MULTIPLE - (Dupdup.BUFFER_SIZE / 2);

    /// <summary>
    /// Generates a specified number of random bytes, then writes those bytes to the specified number of temp files
    /// </summary>
    /// <param name="numberOfBytes">The number of bytes to write to a temp file</param>
    /// <param name="numberOfFiles">The number of files to write the random buffer to</param>
    /// <returns>The full path of the temporary files</returns>
    private List<string> WriteRandomBytesToTempFiles(int numberOfBytes, int numberOfFiles)
    {
        // Get random bytes
        byte[] bytes = new byte[numberOfBytes];
        Random random = new Random();
        random.NextBytes(bytes);

        // Generate temp files up to the specified limit
        List<string> tempFiles = Enumerable.Repeat("", numberOfFiles)
            .Select(element => Path.GetTempFileName())
            .ToList();

        foreach (string tempFile in tempFiles)
        {
            File.WriteAllBytes(tempFile, bytes);
        }
        
        return tempFiles;
    }

    [TestMethod]
    public void HashCollisionTest()
    {
        Dupdup.DupdupRunSettings settings = new();
        Dupdup instance = new(settings);
        instance.HashedFiles = new();

        List<string> duplicateFiles1 = WriteRandomBytesToTempFiles(BUFFER_MULTIPLE, 2);
        List<string> duplicateFiles2 = WriteRandomBytesToTempFiles(BUFFER_MULTIPLE, 2);
        List<string> uniqueFile1 = WriteRandomBytesToTempFiles(BUFFER_MULTIPLE, 1);
        List<string> uniqueFile2 = WriteRandomBytesToTempFiles(BUFFER_MULTIPLE, 1);

        // Case 1: Two unique files with a hash collision
        instance.HashedFiles.Add("case1", uniqueFile1.Concat(uniqueFile2).ToList());

        // Case 2: Two pairs of files, all with the same hash. Each pair consists of
        // two duplicate files, but the pairs are distinct from each other
        instance.HashedFiles.Add("case2", duplicateFiles1.Concat(duplicateFiles2).ToList());

        // Case 3: Two files are duplicates, but a third is distinct
        instance.HashedFiles.Add("case3", duplicateFiles1.Concat(uniqueFile1).ToList());

        // Case 4: Two duplicate files
        instance.HashedFiles.Add("case4", duplicateFiles1);

        instance.RemoveHashCollisions();

        Assert.IsFalse(instance.HashedFiles.ContainsKey("case1"));
        Assert.IsFalse(instance.HashedFiles.ContainsKey("case2"));
        Assert.IsFalse(instance.HashedFiles.ContainsKey("case3"));
        Assert.IsTrue(instance.HashedFiles.ContainsKey("case4"));
    }

    [TestMethod]
    public void FileBytesDifferentTest()
    {
        Dupdup.DupdupRunSettings settings = new();
        Dupdup instance = new(settings);

        // Setup identical files
        List<string> sameNotBufferFiles = WriteRandomBytesToTempFiles(NOT_BUFFER_MULTIPLE, 2);
        List<string> sameBufferFiles = WriteRandomBytesToTempFiles(BUFFER_MULTIPLE, 2);

        // Setup different files
        List<string> differentNotBufferFiles = WriteRandomBytesToTempFiles(NOT_BUFFER_MULTIPLE, 1);
        differentNotBufferFiles.Add(sameNotBufferFiles.First());

        List<string> differentBufferFiles = WriteRandomBytesToTempFiles(BUFFER_MULTIPLE, 1);
        differentBufferFiles.Add(sameBufferFiles.First());

        bool sameNotBufferFilesAreDifferent = instance.FileBytesDifferent(sameNotBufferFiles[0], sameNotBufferFiles[1]);
        bool sameBufferFilesAreDifferent = instance.FileBytesDifferent(sameBufferFiles[0], sameBufferFiles[1]);
        bool differentNotBufferFilesAreDifferent = instance.FileBytesDifferent(differentNotBufferFiles[0], differentNotBufferFiles[1]);
        bool differentBufferFilesAreDifferent = instance.FileBytesDifferent(differentBufferFiles[0], differentBufferFiles[1]);

        Assert.IsFalse(sameNotBufferFilesAreDifferent);
        Assert.IsFalse(sameBufferFilesAreDifferent);
        Assert.IsTrue(differentNotBufferFilesAreDifferent);
        Assert.IsTrue(differentBufferFilesAreDifferent);
    }
}
