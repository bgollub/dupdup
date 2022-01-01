
using System.Collections.Generic;
using System.IO;
using System;
using System.Security.Cryptography;
using System.Linq;
using System.Diagnostics;

public class Dupdup
{
    // Direct byte comparison buffer (BitConverter.ToInt64 returns a 64-bit signed integer converted from eight bytes)
    public const int BUFFER_SIZE = 8;
    private DupdupRunSettings InstanceSettings { get; set; }
    public Dictionary<string, List<string>> HashedFiles { get; set; }
    private Stopwatch CurrentStopwatch { get; set; }

    /// <summary>
    /// Class that defines the operation of the current run
    /// </summary>
    public class DupdupRunSettings
    {
        // The directory to search for duplicate files
        public string TargetDirectory { get; set; }
        // searchPattern used by Directory.EnumerateFiles()
        public string SearchPattern { get; set; }
        // Delete all but one of the files found
        public bool DeleteFiles { get; set; } = false;
    }

    /// <summary>
    /// Parses command line arguments and generates DupdupRunSettings object
    /// </summary>
    /// <param name="args">Command line arguments provided to Main()</param>
    /// <returns>A DupdupRunSettings object corresponding to parsed arguments</returns>
    private static DupdupRunSettings ParseArguments(string[] args)
    {
        DupdupRunSettings currentRunSettings = new DupdupRunSettings();
        for (int argNumIndex = 0; argNumIndex < args.Length; argNumIndex++)
        {
            switch (args[argNumIndex].ToLower())
            {
                case "-target":
                    // If -target is followed by a valid path, store it
                    if (argNumIndex + 1 < args.Length)
                    {
                        if (Directory.Exists(args[argNumIndex + 1]))
                        {
                            currentRunSettings.TargetDirectory = args[argNumIndex + 1];
                            // Advance the argument counter to skip since that argument has been consumed
                            argNumIndex++;
                        }
                    }
                    break;
                case "-searchpattern":
                    // If -searchpattern is followed by a string, store it
                    if (argNumIndex + 1 < args.Length)
                    {
                        currentRunSettings.SearchPattern = args[argNumIndex + 1];
                        // Advance the argument counter to skip since that argument has been consumed
                        argNumIndex++;
                    }
                    break;
                case "-delete":
                    currentRunSettings.DeleteFiles = true;
                    break;
            }
        }
        return currentRunSettings;
    }

    public Dupdup(DupdupRunSettings instanceSettings)
    {
        InstanceSettings = instanceSettings;
        HashedFiles = new();
    }

    /// <summary>
    /// Gets the SHA256 hash of the specified file
    /// </summary>
    /// <param name="filePath">The path of the file to hash</param>
    /// <returns>SHA256 hash of the provided file</returns>
    public string GetSHA256Hash(string filePath)
    {
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            using FileStream fileStream = fileInfo.Open(FileMode.Open);
            fileStream.Position = 0;
            using SHA256 hash = SHA256.Create();
            // Convert hash bytes to hexadecimal strings
            var hashValue = hash.ComputeHash(fileStream).Select(currentByte => currentByte.ToString("x2"));
            return string.Concat(hashValue);
        }
        catch
        {
            Console.Error.WriteLine($"WARNING: Could not obtain hash for {filePath}");
            return null;
        }
    }
            
    /// <summary>
    /// Using the parameters stored in InstanceSettings, enumerates all files specified and stores
    /// all SHA256 hashes associated with more than one file in HashedFiles
    /// </summary>
    private void LoadHashes()
    {
        // Recurse into all subdirectories
        var files = Directory.EnumerateFiles(InstanceSettings.TargetDirectory, InstanceSettings.SearchPattern, SearchOption.AllDirectories);
        foreach (var currentFile in files)
        {
            string currentHash = GetSHA256Hash(currentFile);
            // If a hash couldn't be obtained, just skip the file
            if (null != currentHash)
            {
                // Create an entry for the hash if it doesn't yet exist
                if (!HashedFiles.ContainsKey(currentHash))
                {
                    HashedFiles.Add(currentHash, new List<string>());
                }
                // Add the current file to the hash's entry
                HashedFiles[currentHash].Add(currentFile);
            }
        }
        // Drop any hashes that don't have more than one file associated with them
        var uniqueHashes = HashedFiles.Where(hash => hash.Value.Count == 1);
        foreach (var currentHash in uniqueHashes)
        {
            HashedFiles.Remove(currentHash.Key);
        }
    }

    /// <summary>
    /// Iterates over all entries in HashedFiles and does a byte comparison between all files
    /// associated with a given hash. Drops both members of a pair comparison if they differ.
    /// </summary>
    public void RemoveHashCollisions()
    {
        foreach (var currentHash in HashedFiles)
        {
            List<string> knownCollisions = new();
            for (int i = 0; i < currentHash.Value.Count; i++)
            {
                for (int j = i+1; j < currentHash.Value.Count; j++)
                {
                    if (FileBytesDifferent(currentHash.Value[i], currentHash.Value[j]))
                    {
                        knownCollisions.Add(currentHash.Value[i]);
                        knownCollisions.Add(currentHash.Value[j]);
                    }
                }
            }

            if (knownCollisions.Count > 0)
            {
                // Remove duplicates in case the same filename was added more than once
                knownCollisions = knownCollisions.Distinct().ToList();

                foreach (var collision in knownCollisions)
                {
                    Console.Error.WriteLine($"WARNING: Skipping file because a hash collision was detected: {collision}");
                }

                // If files were added to the known collision list, remove them from the list
                List<string> filesWithoutCollisions = currentHash.Value.Where(fileName => !knownCollisions.Contains(fileName)).ToList();
                if (filesWithoutCollisions.Count > 1)
                {
                    // If there are still duplicate files after removing collisions, replace the list
                    HashedFiles[currentHash.Key] = filesWithoutCollisions;
                } else
                {
                    // Otherwise remove the hash entry entirely
                    HashedFiles.Remove(currentHash.Key);
                }
            }
        }
    }

    /// <summary>
    /// Parses HashedFiles entries, prints them to stdout, and deletes all files but one from each entry if InstanceSettings specify.
    /// </summary>
    private void ProcessDuplicates()
    {
        Console.WriteLine($"Found {HashedFiles.Count} distinct file{(HashedFiles.Count == 1 ? "" : "s")} with duplicates.");
        Console.WriteLine();

        foreach (var hash in HashedFiles)
        {
            // Print found duplicates
            Console.WriteLine($"Duplicates detected (SHA256 {hash.Key}):");
            hash.Value.ForEach(fileName => Console.WriteLine($"File: {fileName}"));
            
            // Delete all but one copy, if specified
            if (InstanceSettings.DeleteFiles)
            {
                while (hash.Value.Count > 1)
                {
                    string file = hash.Value[hash.Value.Count - 1];
                    hash.Value.RemoveAt(hash.Value.Count - 1);
                    try
                    {
                        File.Delete(file);
                        Console.WriteLine($"Deleted: {file}");
                    } catch (Exception ex){
                        Console.Error.WriteLine($"Failed to delete {file}: {ex}");
                    }
                    
                }
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Determine whether any bytes in two files are different.
    /// This is intended to be used only on files that have matching hashes to detect hash collisions.
    /// </summary>
    /// <param name="fileOne">The first file to compare</param>
    /// <param name="fileTwo">The second file to compare</param>
    /// <returns>True if any bytes in both files are different, false otherwise (including errors)</returns>
    public bool FileBytesDifferent(string fileOne, string fileTwo)
    {
        try
        {
            FileInfo fileOneInfo = new(fileOne);
            FileInfo fileTwoInfo = new(fileTwo);
            if (fileOneInfo.Length != fileTwoInfo.Length)
            {
                return true;
            }

            // Read both files, comparing BUFFER_SIZE bytes at a time
            long bytesToRead = fileOneInfo.Length;
            byte[] bytesOne = new byte[BUFFER_SIZE];
            byte[] bytesTwo = new byte[BUFFER_SIZE];
            using FileStream streamOne = fileOneInfo.OpenRead();
            using FileStream streamTwo = fileTwoInfo.OpenRead();
            for (long bytesRead = 0; bytesRead < bytesToRead; bytesRead += BUFFER_SIZE)
            {
                streamOne.Read(bytesOne, 0, BUFFER_SIZE);
                streamTwo.Read(bytesTwo, 0, BUFFER_SIZE);
                // If the number of bytes read is less than BUFFER_SIZE, this will also compare bytes previously in the array
                if (BitConverter.ToInt64(bytesOne, 0) != BitConverter.ToInt64(bytesTwo, 0))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        return false;
    }

    /// <summary>
    /// Replaces the instance's stopwatch with a new one, and starts timing from zero.
    /// </summary>
    private void StartTimer()
    {
        CurrentStopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Stop the instance's stopwatch, and print elapsed milliseconds to stdout.
    /// </summary>
    private void EndTimer()
    {
        CurrentStopwatch.Stop();
        Console.WriteLine($"Done in {CurrentStopwatch.ElapsedMilliseconds} ms.");
    }

    static int Main(string[] args)
    {
        try
        {
            DupdupRunSettings currentRunSettings = ParseArguments(args);

            // Check if required arguments are not present
            List<string> errorMessages = new List<string>();
            if (null == currentRunSettings.TargetDirectory)
            {
                errorMessages.Add("ERROR: A valid target directory must be specified with -target");
            }
            if (null == currentRunSettings.SearchPattern)
            {
                errorMessages.Add("ERROR: A search pattern must be specified with -searchpattern.");
            }
            // If there are errors, print them and exit
            if (errorMessages.Count > 0)
            {
                errorMessages.Add("\r\nUSAGE (to search and preserve duplicates): dupdup -target \"C:\\DIRECTORY\" -searchpattern \"*.jpg\"");
                errorMessages.Add("USAGE (to delete duplicates): dupdup -target \"C:\\DIRECTORY\" -searchpattern \"*.jpg\" -delete\r\n");
                errorMessages.Add("All argument values should be enclosed in quotes as shown.\r\n");

                errorMessages.ForEach(err => Console.Error.WriteLine(err));

                return 1;
            }

            Dupdup currentInstance = new(currentRunSettings);

            Console.WriteLine("Hashing files...");
            currentInstance.StartTimer();
            currentInstance.LoadHashes();
            currentInstance.EndTimer();

            Console.WriteLine("Detecting hash collisions...");
            currentInstance.StartTimer();
            currentInstance.RemoveHashCollisions();
            currentInstance.EndTimer();

            // Process contents of HashedFiles based on behavior specified in InstanceSettings
            currentInstance.ProcessDuplicates();

            return 0;
        } catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}
