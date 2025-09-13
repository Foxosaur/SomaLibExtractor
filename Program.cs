using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

class LibFileExtractor
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BITMAPFILEHEADER
    {
        public ushort bfType;
        public uint bfSize;
        public ushort bfReserved1;
        public ushort bfReserved2;
        public uint bfOffBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    static void Main(string[] args)
    {
        Console.WriteLine("SOMA Lib File Extractor");
        Console.WriteLine("------------------------");
        Console.WriteLine("1. Process a single .lib file");
        Console.WriteLine("2. Process all .lib files in a folder");
        Console.Write("\nSelect an option (1-2): ");
        
        string option = Console.ReadLine()?.Trim();
        
        switch (option)
        {
            case "1":
                ProcessSingleFile(args);
                break;
            case "2":
                ProcessFolder();
                break;
            default:
                Console.WriteLine("Invalid option selected.");
                break;
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    static void ProcessFolder()
    {
        Console.Write("Enter the folder path containing .lib files: ");
        string folderPath = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(folderPath))
        {
            Console.WriteLine("No folder path provided. Exiting...");
            return;
        }
        
        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"Folder not found: {folderPath}");
            return;
        }
        
        string[] libFiles = Directory.GetFiles(folderPath, "*.lib");
        
        if (libFiles.Length == 0)
        {
            Console.WriteLine("No .lib files found in the specified folder.");
            return;
        }
        
        Console.WriteLine($"Found {libFiles.Length} .lib files. Processing...\n");
        
        int processedCount = 0;
        foreach (string libFile in libFiles)
        {
            Console.WriteLine($"Processing file {++processedCount} of {libFiles.Length}: {Path.GetFileName(libFile)}");
            ExtractBitmapsFromFile(libFile);
            Console.WriteLine();
        }
        
        Console.WriteLine($"Finished processing {processedCount} .lib files.");
    }
    
    static void ProcessSingleFile(string[] args)
    {
        string filePath;
        
        if (args.Length != 1)
        {
            Console.Write("Please enter the path to the .lib file: ");
            filePath = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine("No file path provided. Exiting...");
                return;
            }
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }
        }
        else
        {
            filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }
        }
        
        ExtractBitmapsFromFile(filePath);
    }
    
    static void ExtractBitmapsFromFile(string filePath)
    {
        string outputDir = Path.Combine(
            Path.GetDirectoryName(filePath),
            Path.GetFileNameWithoutExtension(filePath) + "_extracted"
        );

        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"Extracting to: {outputDir}");

        try
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                long fileSize = fs.Length;
                Console.WriteLine($"File size: {fileSize} bytes");

                int bitmapCount = 0;

                // Scan file for bitmap headers ('BM')
                long pos = 0;
                while (pos < fileSize - 2)
                {
                    fs.Position = pos;
                    byte b1 = br.ReadByte();
                    byte b2 = br.ReadByte();

                    if (b1 == 'B' && b2 == 'M')
                    {
                        // Found potential bitmap header
                        fs.Position = pos;

                        try
                        {
                            // Read bitmap header
                            BITMAPFILEHEADER fileHeader = new BITMAPFILEHEADER();
                            fileHeader.bfType = br.ReadUInt16();
                            fileHeader.bfSize = br.ReadUInt32();
                            fileHeader.bfReserved1 = br.ReadUInt16();
                            fileHeader.bfReserved2 = br.ReadUInt16();
                            fileHeader.bfOffBits = br.ReadUInt32();

                            // Read info header to get dimensions
                            BITMAPINFOHEADER infoHeader = new BITMAPINFOHEADER();
                            infoHeader.biSize = br.ReadUInt32();

                            // Validate the info header size
                            if (infoHeader.biSize != 40)
                            {
                                pos++;
                                continue; // Not a valid BMP format we support
                            }

                            infoHeader.biWidth = br.ReadInt32();
                            infoHeader.biHeight = br.ReadInt32();
                            infoHeader.biPlanes = br.ReadUInt16();
                            infoHeader.biBitCount = br.ReadUInt16();
                            infoHeader.biCompression = br.ReadUInt32();
                            infoHeader.biSizeImage = br.ReadUInt32();
                            infoHeader.biXPelsPerMeter = br.ReadInt32();
                            infoHeader.biYPelsPerMeter = br.ReadInt32();
                            infoHeader.biClrUsed = br.ReadUInt32();
                            infoHeader.biClrImportant = br.ReadUInt32();

                            // Validate dimensions and size
                            if (infoHeader.biWidth <= 0 || infoHeader.biWidth > 4000 ||
                                Math.Abs(infoHeader.biHeight) <= 0 || Math.Abs(infoHeader.biHeight) > 4000 ||
                                fileHeader.bfSize > fileSize - pos)
                            {
                                pos++;
                                continue; // Probably not a valid bitmap
                            }

                            Console.WriteLine($"Found bitmap at offset: {pos}");
                            Console.WriteLine($"  Size: {fileHeader.bfSize} bytes");
                            Console.WriteLine($"  Dimensions: {infoHeader.biWidth}x{Math.Abs(infoHeader.biHeight)}");
                            Console.WriteLine($"  Bit Depth: {infoHeader.biBitCount}");

                            // Extract the bitmap
                            fs.Position = pos;
                            byte[] bmpData = new byte[fileHeader.bfSize];
                            int bytesRead = fs.Read(bmpData, 0, (int)fileHeader.bfSize);

                            if (bytesRead == fileHeader.bfSize)
                            {
                                string bmpFilename = Path.Combine(outputDir, $"bitmap_{bitmapCount:D4}.bmp");
                                File.WriteAllBytes(bmpFilename, bmpData);
                                Console.WriteLine($"  Saved as: {bmpFilename}");
                                bitmapCount++;

                                // Try to load as image to verify it's valid
                                try
                                {
                                    using (MemoryStream ms = new MemoryStream(bmpData))
                                    using (Bitmap bmp = new Bitmap(ms))
                                    {
                                        // If we get here, it's a valid bitmap
                                        Console.WriteLine($"  Verified as valid bitmap: {bmp.Width}x{bmp.Height}");
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine($"  Warning: Extracted file may not be a valid bitmap");
                                }
                            }

                            // Skip past this bitmap
                            pos += fileHeader.bfSize;
                        }
                        catch (Exception ex)
                        {
                            // Not a valid bitmap or read error
                            Console.WriteLine($"  Error processing potential bitmap: {ex.Message}");
                            pos++;
                        }
                    }
                    else
                    {
                        pos++;
                    }
                }

                Console.WriteLine($"Extraction complete. Found {bitmapCount} bitmaps.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}