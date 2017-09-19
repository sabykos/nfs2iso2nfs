using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace nfs2iso2nfs
{
    class Program
    {
        public const int SECTOR_SIZE = 0x8000;
        public const int HEADER_SIZE = 0x200;
        public static byte[] WII_COMMON_KEY = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public const int NFS_SIZE = 0xFA00000;
        public static bool dec = false;
        public static bool enc = false;
        public static bool keepFiles = false;
        public static bool keepLegit = false;
        public static bool horiz_wiimote = false;
        public static bool vert_wiimote = false;
        public static bool map_shoulder_to_trigger = false;
        public static bool homebrew = false;
        public static string keyFile = "..\\code\\htk.bin";
        public static string isoFile = "game.iso";
        public static string wiiKeyFile = "wii_common_key.bin";
        public static string nfsDir = "";
        public static string fw_file = "..\\code\\fw.img";


        static void Main(string[] args)
        {
            Console.WriteLine();
            if (checkArgs(args) == -1)
                return;
            byte[] key = checkKeyFiles();
            if (key == null)
                return;
            if (dec)
            {
                byte[] header = getHeader(nfsDir + "\\hif_000000.nfs");
                combineNFSFiles("hif.nfs");
                EnDecryptNFS("hif.nfs", "hif_dec.nfs", key, buildZero(key.Length), false, header);
                if (!keepFiles)
                    File.Delete("hif.nfs");
                unpackNFS("hif_dec.nfs", "hif_unpack.nfs", header);
                if (!keepFiles)
                    File.Delete("hif_dec.nfs");
                manipulateISO("hif_unpack.nfs", "game.iso", true);
                if (!keepFiles)
                    File.Delete("hif_unpack.nfs");
            }
            else if (enc)
            {
                if (!keepLegit || horiz_wiimote || vert_wiimote || map_shoulder_to_trigger)
                    DoThePatching(fw_file);
                long[] size = manipulateISO(isoFile, "hif_unpack.nfs", false);
                byte[] header = packNFS("hif_unpack.nfs", "hif_dec.nfs", size);
                if (!keepFiles)
                    File.Delete("hif_unpack.nfs");
                EnDecryptNFS("hif_dec.nfs", "hif.nfs", key, buildZero(key.Length), true, header);
                if (!keepFiles)
                    File.Delete("hif_dec.nfs");
                splitNFSFile("hif.nfs");
                if (!keepFiles)
                    File.Delete("hif.nfs");
            }
        }


        public static int checkArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
                switch (args[i])
                {
                    case "-dec":
                        dec = true;
                        break;
                    case "-enc":
                        enc = true;
                        break;
                    case "-keep":
                        keepFiles = true;
                        break;
                    case "-legit":
                        keepLegit = true;
                        break;
                    case "-key":
                        if (i == args.Length)
                            return -1;
                        keyFile = args[i + 1];
                        i++;
                        break;
                    case "-wiikey":
                        if (i == args.Length)
                            return -1;
                        wiiKeyFile = args[i + 1];
                        i++;
                        break;
                    case "-iso":
                        if (i == args.Length)
                            return -1;
                        isoFile = args[i + 1];
                        i++;
                        break;
                    case "-nfs":
                        if (i == args.Length)
                            return -1;
                        nfsDir = args[i + 1];
                        i++;
                        break;
                    case "-fwimg":
                        if (i == args.Length)
                            return -1;
                        fw_file = args[i + 1];
                        i++;
                        break;
                    case "-lrpatch":
                        map_shoulder_to_trigger = true;
                        break;
                    case "-wiimote":
                        horiz_wiimote = true;
                        break;
                    case "-vertical":
                        vert_wiimote = true;
                        break;
                    case "-homebrew":
                        homebrew = true;
                        break;

                    case "-help":
                        Console.WriteLine("+++++ NFS2ISO2NFS v0.5.3 +++++");
                        Console.WriteLine();
                        Console.WriteLine("-dec            Decrypt .nfs files to an .iso file.");
                        Console.WriteLine("-enc            Encrypt an .iso file to .nfs file(s)");
                        Console.WriteLine("-key <file>     Location of AES key file. DEFAULT: code\\htk.bin.");
                        Console.WriteLine("-wiikey <file>  Location of Wii Common key file. DEFAULT: wii_common_key.bin.");
                        Console.WriteLine("-iso <file>     Location of .iso file. DEFAULT: game.iso.");
                        Console.WriteLine("-nfs <file>     Location of .nfs files. DEFAULT: current Directory.");
                        Console.WriteLine("-fwimg <file>   Location of fw.img. DEFAULT: code\\fw.img.");
                        Console.WriteLine("-keep           Don't delete the files produced in intermediate steps.");
                        Console.WriteLine("-legit          Don't patch fw.img to allow fakesigned content");
                        Console.WriteLine("-lrpatch        Map emulated Classic Controller's L & R to Gamepad's ZL & ZR");
                        Console.WriteLine("-wiimote        Emulate a Wii Remote instead of the Classic Controller");
                        Console.WriteLine("-vertical       Remap Wii Remote d-pad for vertical usage (implies -wiimote)");
                        Console.WriteLine("-homebrew       Various patches to enable proper homebrew functionality");
                        Console.WriteLine("-help           Print this text.");
                        return -1;
                    default:
                        break;
                }

            string dir = Directory.GetCurrentDirectory();
            if (!Path.IsPathRooted(keyFile))
                keyFile = dir + "\\" + keyFile;
            if (!Path.IsPathRooted(isoFile))
                isoFile = dir + "\\" + isoFile;
            if (!Path.IsPathRooted(wiiKeyFile))
                wiiKeyFile = dir + "\\" + wiiKeyFile;
            if (!Path.IsPathRooted(nfsDir))
                nfsDir = dir + "\\" + nfsDir;
            if (!Path.IsPathRooted(fw_file))
                fw_file = dir + "\\" + fw_file;


            if (map_shoulder_to_trigger && horiz_wiimote || map_shoulder_to_trigger && vert_wiimote)
            {
                Console.WriteLine("ERROR: Please don't mix patches for Classic Controller and  Wii Remote.");
                return -1;
            }


            if (dec || ((!dec && !enc) && File.Exists(nfsDir + "\\hif_000000.nfs")))
            {
                Console.WriteLine("+++++ NFS2ISO +++++");
                Console.WriteLine();
                if (dec && !enc && !File.Exists(nfsDir + "\\hif_000000.nfs"))
                {
                    Console.WriteLine("ERROR: .nfs files not found! Exiting...");
                    return -1;
                }
                else if ((!dec && !enc) && File.Exists(nfsDir + "\\hif_000000.nfs"))
                {
                    Console.WriteLine("You haven't specified if you want to use nfs2iso or iso2nfs");
                    Console.WriteLine("Found .nfs files! Assuming you want to use nfs2iso...");
                    dec = true;
                    enc = false;
                }
            }

            else if (enc || (((!dec && !enc) || (!dec && !enc)) && File.Exists(isoFile)))
            {
                Console.WriteLine("+++++ ISO2NFS +++++");
                Console.WriteLine();
                if (!dec && enc && !File.Exists(isoFile))
                {
                    Console.WriteLine("ERROR: .iso file not found! Exiting...");
                    return -1;
                }
                if (!dec && enc && !File.Exists(fw_file))
                {
                    Console.WriteLine("ERROR: fw.img not found! Exiting...");
                    return -1;
                }
                else if (((dec && enc) || (!dec && !enc)) && File.Exists(isoFile))
                {
                    Console.WriteLine("You haven't specified if you want to use nfs2iso or iso2nfs");
                    Console.WriteLine("Found .iso file!  Assuming you want to use iso2nfs...");
                    dec = false;
                    enc = true;
                }
            }

            else
            {
                Console.WriteLine("You haven't specified if you want to use nfs2iso or iso2nfs");
                Console.WriteLine("Found neither .iso nor .nfs files! Check -help for usage of this program.");
                return -1;
            }
            return 0;
        }


        public static byte[] checkKeyFiles()
        {
            Console.WriteLine("Searching for AES key file...");
            if (!File.Exists(keyFile))
            {
                Console.WriteLine("ERROR: Could not find AES key file! Exiting...");
                return null;
            }
            byte[] key = getKey(keyFile);
            if (key == null)
            {
                Console.WriteLine("ERROR: AES key file has wrong file size! Exiting...");
                return null;
            }
            Console.WriteLine("AES key file found!");

            if (WII_COMMON_KEY[0] != 0xeb)
            {
                Console.WriteLine("Wii common key not found in source code. Looking for file...");
                if (!File.Exists(wiiKeyFile))
                {
                    Console.WriteLine("ERROR: Could not find Wii common key file! Exiting...");
                    return null;
                }
                WII_COMMON_KEY = getKey(wiiKeyFile);
                if (key == null)
                {
                    Console.WriteLine("ERROR: Wii common key file has wrong file size! Exiting...");
                    return null;
                }
                Console.WriteLine("Wii Common Key file found!");
            }
            else Console.WriteLine("Wii common key found in source code!");

            Console.WriteLine();
            return key;
        }


        public static byte[] getKey(string keyDir)
        {
            using (var keyFile = new BinaryReader(File.OpenRead(keyDir)))
            {
                long keySize = keyFile.BaseStream.Length;
                if (keySize != 16)
                    return null;
                return keyFile.ReadBytes(0x10);
            }
        }


        public static byte[] buildZero(int size)
        {
            byte[] iv = new byte[size];
            for (int i = 0; i < size; i++)
                iv[i] = 0;
            return iv;
        }


        public static void combineNFSFiles(string outFile)
        {
            using (var nfs = new BinaryWriter(File.OpenWrite(outFile)))
            {
                Console.WriteLine("Looking for .nfs files...");
                int nfsNo = -1;
                while (File.Exists(nfsDir + "\\hif_" + String.Format("{0:D6}", nfsNo + 1) + ".nfs"))
                    nfsNo++;
                Console.WriteLine((nfsNo + 1) + " .nfs files found!");
                Console.WriteLine("Joining .nfs files...");
                Console.WriteLine();
                for (int i = 0; i <= nfsNo; i++)
                {
                    Console.WriteLine("Processing hif_" + String.Format("{0:D6}", i) + ".nfs...");
                    var nfsTemp = new BinaryReader(File.OpenRead(nfsDir + "\\hif_" + String.Format("{0:D6}", i) + ".nfs"));
                    if (i == 0)
                    {
                        nfsTemp.ReadBytes(HEADER_SIZE);
                        nfs.Write(nfsTemp.ReadBytes((int)nfsTemp.BaseStream.Length - HEADER_SIZE));
                    }
                    else nfs.Write(nfsTemp.ReadBytes((int)nfsTemp.BaseStream.Length));
                }
            }
        }


        public static void splitNFSFile(string inFile)
        {
            using (var nfs = new BinaryReader(File.OpenRead(inFile)))
            {
                Console.WriteLine();
                long size = nfs.BaseStream.Length;
                int i = 0;
                do
                {
                    Console.WriteLine("Building hif_" + String.Format("{0:D6}", i) + ".nfs...");
                    var nfsTemp = new BinaryWriter(File.OpenWrite(Directory.GetCurrentDirectory() + "\\hif_" + String.Format("{0:D6}", i) + ".nfs"));
                    nfsTemp.Write(nfs.ReadBytes(size > NFS_SIZE ? NFS_SIZE : (int)size));
                    size -= NFS_SIZE;
                    i++;
                } while (size > 0);
            }
        }


        public static byte[] getHeader(string inFile)
        {
            using (var file = new BinaryReader(File.OpenRead(inFile)))
            {
                return file.ReadBytes(0x200);
            }
        }


        public static long[] manipulateISO(string InFile, string OutFile, bool enc)
        {
            using (var er = new BinaryReader(File.OpenRead(InFile)))
            using (var ew = new BinaryWriter(File.OpenWrite(OutFile)))
            {
                long[] sizeInfo = new long[2];

                Console.WriteLine();
                Console.WriteLine("Read partition table...");
                Console.WriteLine();
                ew.Write(er.ReadBytes(0x40000));

                byte[] partitionTable = er.ReadBytes(0x20);
                ew.Write(partitionTable);
                int[,] partitionInfo = new int[2, 4];            //first coorfinate number of partitions, second offset of partition table
                for (byte i = 0; i < 4; i++)
                {
                    partitionInfo[0, i] = partitionTable[0x0 + 0x8 * i] * 0x1000000 + partitionTable[0x1 + 0x8 * i] * 0x10000 + partitionTable[0x2 + 0x8 * i] * 0x100 + partitionTable[0x3 + 0x8 * i];
                    Console.WriteLine("Number of " + (i + 1) + ". partitions: " + partitionInfo[0, i]);
                    if (partitionInfo[0, i] == 0)
                        partitionInfo[1, i] = 0;
                    else partitionInfo[1, i] = (partitionTable[0x4 + 0x8 * i] * 0x1000000 + partitionTable[0x5 + 0x8 * i] * 0x10000 + partitionTable[0x6 + 0x8 * i] * 0x100 + partitionTable[0x7 + 0x8 * i]) * 0x4;
                    Console.WriteLine("Partition info table offset: 0x" + Convert.ToString(partitionInfo[1, i], 16));
                }
                Console.WriteLine();
                partitionInfo = sort(partitionInfo, 4);
                byte[][] partitionInfoTable = new byte[4][];
                List<int> partitionOffsetList = new List<int>();
                long curPos = 0x40020;
                int k = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (partitionInfo[0, i] != 0)
                    {
                        ew.Write(er.ReadBytes((int)(partitionInfo[1, i] - curPos)));
                        curPos += (partitionInfo[1, i] - curPos);
                        partitionInfoTable[i] = er.ReadBytes(0x8 * partitionInfo[0, i]);
                        curPos += (0x8 * partitionInfo[0, i]);
                        for (int j = 0; j < partitionInfo[0, i]; j++)
                            if (partitionInfoTable[i][0x7 + 0x8 * j] == 0) //check if game partition
                            {
                                partitionOffsetList.Add((partitionInfoTable[i][0x0 + 0x8 * j] * 0x1000000 + partitionInfoTable[i][0x1 + 0x8 * j] * 0x10000 + partitionInfoTable[i][0x2 + 0x8 * j] * 0x100 + partitionInfoTable[i][0x3 + 0x8 * j]) * 0x4);
                                Console.WriteLine("Data partition at offset: 0x" + Convert.ToString(partitionOffsetList[k], 16));
                                k++;
                            }
                        ew.Write(partitionInfoTable[i]);
                    }
                }
                Console.WriteLine();
                int[] partitionOffsets = partitionOffsetList.ToArray();
                partitionOffsets = sort(partitionOffsets, partitionOffsets.Length);
                sizeInfo[0] = partitionOffsets[0];
                byte[] IV = new byte[0x10];
                byte[] decHashTable = new byte[0x400];
                byte[] encHashTable = new byte[0x400];
                int timer = 0;
                int l = 0;
                for (int i = 0; i < partitionOffsets.Length; i++)
                {
                    ew.Write(er.ReadBytes((int)(partitionOffsets[i] - curPos)));
                    curPos += (partitionOffsets[i] - curPos);
                    ew.Write(er.ReadBytes(0x1BF));                              //Write start of partiton
                    byte[] enc_titlekey = er.ReadBytes(0x10);                   //read encrypted titlekey
                    ew.Write(enc_titlekey);                                     //Write encrypted titlekey
                    ew.Write(er.ReadBytes(0xD));                                //Write bytes till titleID
                    byte[] titleID = er.ReadBytes(0x8);                         //read titleID
                    ew.Write(titleID);
                    for (int j = 0; j < 0x10; j++)                              //build IV
                        if (j < 8)
                            IV[j] = titleID[j];
                        else IV[j] = 0x0;
                    ew.Write(er.ReadBytes(0xC0));                               //Write bytes till end of ticket
                    byte[] partitionHeader = er.ReadBytes(0x1FD5C);
                    long partitionSize = (long)0x4 * (partitionHeader[0x18] * 0x1000000 + partitionHeader[0x19] * 0x10000 + partitionHeader[0x1A] * 0x100 + partitionHeader[0x1B]);
                    Console.WriteLine("Partition size: 0x" + Convert.ToString(partitionSize, 16));
                    ew.Write(partitionHeader);                                  //Write bytes till start of partition data
                    curPos += 0x20000;
                    curPos += partitionSize;
                    byte[] titlekey = aes_128_cbc(WII_COMMON_KEY, IV, enc_titlekey, false);
                    Console.WriteLine("Write game partition " + i + "...");
                    byte[] Sector = new byte[SECTOR_SIZE];
                    while (partitionSize >= SECTOR_SIZE)
                    {
                        if (timer == 8000)
                        {
                            timer = 0;
                            l++;
                            Console.WriteLine((l * 256) + " MB processed...");
                        }
                        timer++;


                        // NFS to ISO
                        if (enc)
                        {
                            Array.Clear(IV, 0, 0x10);                                                // clear IV for encrypting hash table
                            decHashTable = er.ReadBytes(0x400);                                      // read raw hash table from nfs
                            encHashTable = aes_128_cbc(titlekey, IV, decHashTable, true);            // encrypt table
                            ew.Write(encHashTable);                                                  // write encrypted hash table to iso

                            //quit the loop if already at the end of input file or beyond (avoid the crash)
                            if (er.BaseStream.Position >= er.BaseStream.Length)
                            {
                                break;
                            }
                            Array.Copy(encHashTable, 0x3D0, IV, 0, 0x10);                            // get IV for encrypting the rest
                            Sector = er.ReadBytes(SECTOR_SIZE - 0x400);
                            Sector = aes_128_cbc(titlekey, IV, Sector, enc);                         // encrypt the remaining bytes
                        }

                        // ISO to NFS
                        else
                        {
                            Array.Clear(IV, 0, 0x10);                                                // clear IV for decrypting hash table
                            encHashTable = er.ReadBytes(0x400);                                      // read encrypted hash table from iso
                            decHashTable = aes_128_cbc(titlekey, IV, encHashTable, false);           // decrypt table
                            ew.Write(decHashTable);                                                  // write decrypted hash table to nfs


                            //quit the loop if already at the end of input file or beyond (avoid the crash)
                            if (er.BaseStream.Position >= er.BaseStream.Length)
                            {
                                break;
                            }
                            Array.Copy(encHashTable, 0x3D0, IV, 0, 0x10);                           // IV for decrypting the remaining data
                            Sector = er.ReadBytes(SECTOR_SIZE - 0x400);
                            Sector = aes_128_cbc(titlekey, IV, Sector, false);                      // decrypt the remaining bytes
                        }


                        ew.Write(Sector);
                        partitionSize -= SECTOR_SIZE;
                    }
                    sizeInfo[1] = curPos - sizeInfo[0];
                    if (partitionSize != 0)
                        Console.WriteLine("WARNING: Last cluster was not complete. This may be a problem.");
                }
                if (enc)
                {
                    Console.WriteLine();
                    Console.WriteLine("Writing zeros...");
                    long rest;
                    if (curPos > 0x118240000)
                        rest = 0x1FB4E0000 - curPos;
                    else rest = 0x118240000 - curPos;
                    l = 0;
                    timer = 0;
                    while (rest > 0)
                    {
                        if (timer == 8000)
                        {
                            timer = 0;
                            l++;
                            Console.WriteLine((l * 256) + " MB processed...");
                        }
                        timer++;
                        ew.Write(buildZero(rest > SECTOR_SIZE ? SECTOR_SIZE : (int)rest));
                        rest -= SECTOR_SIZE;
                    }
                    return null;
                }
                else return sizeInfo;
            }
        }


        public static void unpackNFS(string InFile, string OutFile, byte[] header)
        {
            using (var er = new BinaryReader(File.OpenRead(InFile)))
            using (var ew = new BinaryWriter(File.OpenWrite(OutFile)))
            {
                Console.WriteLine();
                Console.WriteLine("Unpacking nfs...");
                Console.WriteLine();
                int numberOfParts = 0x1000000 * header[0x10] + 0x10000 * header[0x11] + 0x100 * header[0x12] + header[0x13];
                Console.WriteLine(numberOfParts + " parts found...");
                long start, length;
                long pos = 0x0;
                long j = 0;
                for (int i = 0; i < numberOfParts; i++)
                {
                    start = (long)SECTOR_SIZE * ((long)0x1000000 * (long)header[0x14 + i * 0x8] + (long)0x10000 * (long)header[0x15 + i * 0x8] + (long)0x100 * (long)header[0x16 + i * 0x8] + (long)header[0x17 + i * 0x8]);
                    length = (long)SECTOR_SIZE * ((long)0x1000000 * (long)header[0x18 + i * 0x8] + (long)0x10000 * (long)header[0x19 + i * 0x8] + (long)0x100 * (long)header[0x1A + i * 0x8] + (long)header[0x1B + i * 0x8]);
                    j = start - pos;
                    Console.WriteLine("Writing zero segment " + i + " of size 0x" + Convert.ToString(j, 16));
                    while (j > 0)
                    {
                        ew.Write(buildZero(SECTOR_SIZE));
                        j -= SECTOR_SIZE;
                    }
                    Console.WriteLine("Writing data segment " + i + " of size 0x" + Convert.ToString(length, 16));
                    j = length;
                    while (j > 0)
                    {
                        ew.Write(er.ReadBytes(SECTOR_SIZE));
                        j -= SECTOR_SIZE;
                    }
                    pos = start + length;
                }
            }
        }


        public static byte[] packNFS(string InFile, string OutFile, long[] sizeInfo)
        {
            using (var er = new BinaryReader(File.OpenRead(InFile)))
            using (var ew = new BinaryWriter(File.OpenWrite(OutFile)))
            {
                Console.WriteLine();
                Console.WriteLine("Generating EGGS header...");
                byte[] header = new byte[0x200];
                for (int i = 0; i < 0x200; i++)
                    header[i] = 0xff;

                header[0x0] = 0x45;
                header[0x1] = 0x47;
                header[0x2] = 0x47;
                header[0x3] = 0x53;

                header[0x4] = 0x00;
                header[0x5] = 0x01;
                header[0x6] = 0x10;
                header[0x7] = 0x11;

                header[0x8] = 0x00;
                header[0x9] = 0x00;
                header[0xA] = 0x00;
                header[0xB] = 0x00;

                header[0xC] = 0x00;
                header[0xD] = 0x00;
                header[0xE] = 0x00;
                header[0xF] = 0x00;

                header[0x10] = 0x00;
                header[0x11] = 0x00;
                header[0x12] = 0x00;
                header[0x13] = 0x03;

                header[0x14] = 0x00;
                header[0x15] = 0x00;
                header[0x16] = 0x00;
                header[0x17] = 0x00;

                header[0x18] = 0x00;
                header[0x19] = 0x00;
                header[0x1A] = 0x00;
                header[0x1B] = 0x01;

                header[0x1C] = 0x00;
                header[0x1D] = 0x00;
                header[0x1E] = 0x00;
                header[0x1F] = 0x08;

                header[0x20] = 0x00;
                header[0x21] = 0x00;
                header[0x22] = 0x00;
                header[0x23] = 0x02;

                header[0x24] = (byte)((sizeInfo[0] / 0x8000) / 0x1000000);
                header[0x25] = (byte)(((sizeInfo[0] / 0x8000) / 0x10000) % 0x100);
                header[0x26] = (byte)(((sizeInfo[0] / 0x8000) / 0x100) % 0x10000);
                header[0x27] = (byte)((sizeInfo[0] / 0x8000) % 0x1000000);

                header[0x28] = (byte)((sizeInfo[1] / 0x8000) / 0x1000000);
                header[0x29] = (byte)(((sizeInfo[1] / 0x8000) / 0x10000) % 0x100);
                header[0x2A] = (byte)(((sizeInfo[1] / 0x8000) / 0x100) % 0x10000);
                header[0x2B] = (byte)((sizeInfo[1] / 0x8000) % 0x1000000);

                header[0x1FC] = 0x53;
                header[0x1FD] = 0x47;
                header[0x1FE] = 0x47;
                header[0x1FF] = 0x45;

                Console.WriteLine("Packing nfs...");

                int numberOfParts = 0x1000000 * header[0x10] + 0x10000 * header[0x11] + 0x100 * header[0x12] + header[0x13];
                Console.WriteLine("Packing " + numberOfParts + " parts...");
                long start, length;
                long pos = 0x0;
                long j = 0;
                for (int i = 0; i < numberOfParts; i++)
                {
                    start = (long)SECTOR_SIZE * ((long)0x1000000 * (long)header[0x14 + i * 0x8] + (long)0x10000 * (long)header[0x15 + i * 0x8] + (long)0x100 * (long)header[0x16 + i * 0x8] + (long)header[0x17 + i * 0x8]);
                    length = (long)SECTOR_SIZE * ((long)0x1000000 * (long)header[0x18 + i * 0x8] + (long)0x10000 * (long)header[0x19 + i * 0x8] + (long)0x100 * (long)header[0x1A + i * 0x8] + (long)header[0x1B + i * 0x8]);
                    j = start - pos;
                    Console.WriteLine("Delete zero segment " + i + " of size 0x" + Convert.ToString(j, 16));
                    while (j > 0)
                    {
                        er.ReadBytes(SECTOR_SIZE);
                        j -= SECTOR_SIZE;
                    }
                    Console.WriteLine("Writing data segment " + i + " of size 0x" + Convert.ToString(length, 16));
                    j = length;
                    while (j > 0)
                    {
                        ew.Write(er.ReadBytes(SECTOR_SIZE));
                        j -= SECTOR_SIZE;
                    }
                    pos = start + length;
                }
                return header;
            }
        }


        public static void EnDecryptNFS(string InFile, string OutFile, byte[] key, byte[] iv, bool enc, byte[] header)
        {
            using (var er = new BinaryReader(File.OpenRead(InFile)))
            using (var ew = new BinaryWriter(File.OpenWrite(OutFile)))
            {
                Console.WriteLine();
                if (enc)
                {
                    Console.WriteLine("Writing EGGS header...");
                    ew.Write(header);
                    Console.WriteLine("Encrypting hif.nfs...");
                }
                else
                    Console.WriteLine("Decrypting hif.nfs...");
                Console.WriteLine();
                byte[] block_iv = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, 0x00 };
                byte[] Sector = new byte[SECTOR_SIZE];
                int timer = 0;
                int i = 0;
                //init size
                long leftSize = er.BaseStream.Length;
                do
                {
                    if (timer == 8000)
                    {
                        timer = 0;
                        i++;
                        Console.WriteLine((i * 256) + " MB processed...");
                    }
                    timer++;
                    Sector = er.ReadBytes(leftSize > SECTOR_SIZE ? SECTOR_SIZE : (int)leftSize);


                    if (ew.BaseStream.Position >= 0x18000)                               //use the different IVs if writing game partition data
                    {
                        iv = block_iv;
                    }

                    // ENCRYPTION
                    if (enc && ew.BaseStream.Position < 0x18000)                        // if encrypting and not game partition
                    {
                        Sector = aes_128_cbc(key, iv, Sector, true);                    // use zero IV
                    }

                    if (enc && ew.BaseStream.Position >= 0x18000)                       // if encrypting game partition
                    {
                        Sector = aes_128_cbc(key, block_iv, Sector, true);              // use different IV for each block
                        block_iv[15]++;                                                 // increment the value after writing
                        if (block_iv[15] == 0)                                          // and go further if necessary
                        {
                            block_iv[14]++;
                            if (block_iv[14] == 0)
                            {
                                block_iv[13]++;
                                if (block_iv[13] == 0)
                                {
                                    block_iv[12]++;                                     // I suppose it's a 4 byte value...?
                                }                                                       // it won't ever happen anyway
                            }
                        }
                    }

                    // DECRYPTION
                    if (!enc && ew.BaseStream.Position < 0x18000)                       // if decrypting and not game partition
                    {
                        Sector = aes_128_cbc(key, iv, Sector, false);
                    }

                    if (!enc && ew.BaseStream.Position >= 0x18000)                      // if decrypting game partition
                    {
                        Sector = aes_128_cbc(key, iv, Sector, false);                   // use different IV for each block
                        block_iv[15]++;                                                 // increment the value after writing
                        if (block_iv[15] == 0)                                          // and go further if necessary
                        {
                            block_iv[14]++;
                            if (block_iv[14] == 0)
                            {
                                block_iv[13]++;
                                if (block_iv[13] == 0)
                                {
                                    block_iv[12]++;                                     // I suppose it's a 4 byte value...?
                                }                                                       // it won't ever happen anyway
                            }
                        }

                    }


                    //write it to outfile
                    ew.Write(Sector);

                    //decrease remaining size
                    leftSize -= SECTOR_SIZE;

                    //loop till end of file
                } while (leftSize > 0);
            }
        }


        public static byte[] aes_128_cbc(byte[] key, byte[] iv, byte[] data, bool enc)
        {
            byte[] result = new byte[data.Length];

            try
            {
                System.Security.Cryptography.RijndaelManaged rm = new System.Security.Cryptography.RijndaelManaged();
                rm.Mode = System.Security.Cryptography.CipherMode.CBC;
                rm.Padding = System.Security.Cryptography.PaddingMode.None;
                rm.KeySize = 128;
                rm.BlockSize = 128;
                rm.Key = key;
                rm.IV = iv;

                if (enc)
                    using (System.Security.Cryptography.ICryptoTransform itc = rm.CreateEncryptor())
                    {
                        result = itc.TransformFinalBlock(data, 0, data.Length);
                    }
                else
                    using (System.Security.Cryptography.ICryptoTransform itc = rm.CreateDecryptor())
                    {
                        result = itc.TransformFinalBlock(data, 0, data.Length);
                    }

                rm.Clear();

                return result;
            }
            catch (System.Security.Cryptography.CryptographicException e)
            {
                Console.WriteLine("A Cryptographic error occurred: {0}", e.Message);
                return null;
            }
        }


        public static int[,] sort(int[,] list, int size)
        {
            int max = 0;
            int maxIndex = 0;
            int temp;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size - j; i++)
                    if (list[1, i] > max)
                    {
                        max = list[1, i];
                        maxIndex = i;
                    }
                temp = list[0, size - j - 1];
                list[0, size - j - 1] = list[0, maxIndex];
                list[0, maxIndex] = temp;
                temp = list[1, size - j - 1];
                list[1, size - j - 1] = list[1, maxIndex];
                list[1, maxIndex] = temp;
            }
            return list;
        }


        public static int[] sort(int[] list, int size)
        {
            int max = 0;
            int maxIndex = 0;
            int temp;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size - j; i++)
                    if (list[i] > max)
                    {
                        max = list[i];
                        maxIndex = i;
                    }
                temp = list[size - j - 1];
                list[size - j - 1] = list[maxIndex];
                list[maxIndex] = temp;
            }
            return list;
        }


        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }


        public static void DoThePatching(string fw_file)
        {
            var input_ios = new MemoryStream(File.ReadAllBytes(fw_file));                     //copy fw.img into a memory stream

            Console.WriteLine("Checking fw.img's revision number...");

            byte[] buffer_rev = new byte[4];
            byte[] rev_pattern = { 0x73, 0x76, 0x6E, 0x2D };                                  // search for "svn-"
            string revision = "";

            for (int offset = 0; offset < input_ios.Length - 4; offset++)
            {
                input_ios.Position = offset;                                                  // set position to advance byte by byte
                input_ios.Read(buffer_rev, 0, 4);                                             // because we read 4 bytes at once

                if (ByteArrayCompare(buffer_rev, rev_pattern))                                // see if it matches
                {
                    input_ios.Read(buffer_rev, 0, 4);
                    revision = System.Text.Encoding.UTF8.GetString(buffer_rev, 0, buffer_rev.Length);
                    break;
                }
            }

            if (revision == "r590")
            {
                Console.WriteLine("OK, revision 590 detected.");
            }
            else
            {
                Console.WriteLine("Warning: {0} detected. These patches are designed for revision 590 only.", revision);
            }
            Console.WriteLine();


            byte[] buffer_4 = new byte[4];                                                    // buffer for 4-byte arrays
            byte[] buffer_8 = new byte[8];                                                    // buffer for 8-byte arrays

            Console.WriteLine("Patching fw.img.");
            if (!keepLegit)
            {
                Array.Clear(buffer_4, 0, 4);
                int patchCount = 0;
                byte[] oldHashCheck = { 0x20, 0x07, 0x23, 0xA2 };
                byte[] newHashCheck = { 0x20, 0x07, 0x4B, 0x0B };

                for (int offset = 0; offset < input_ios.Length - 4; offset++)
                {
                    input_ios.Position = offset;                                                               // set position to advance byte by byte
                    input_ios.Read(buffer_4, 0, 4);                                                            // because we read 4 bytes at once

                    if (ByteArrayCompare(buffer_4, oldHashCheck) || ByteArrayCompare(buffer_4, newHashCheck))  // see if it matches one of the patterns
                    {
                        input_ios.Seek(offset + 1, SeekOrigin.Begin);                                          // if it does, advance on byte further in
                        input_ios.WriteByte(0x00);                                                             // the output and write a zero

                        patchCount++;
                    }
                }

                if (patchCount == 0)
                    Console.WriteLine("Fakesign patching: Nothing to patch.");
                else
                    Console.WriteLine("Fakesigning patching finished... (Patches applied: {0})", patchCount);

                Console.WriteLine();
            }

            //map classic controller's L & R to the gamepad's ZL & ZR
            if (map_shoulder_to_trigger)
            {
                Array.Clear(buffer_4, 0, 4);
                int patchCount = 0;

                byte[] pattern1 = { 0x40, 0x05, 0x46, 0xA9 };
                byte[] patch1 = { 0x26, 0x80, 0x40, 0x06 };

                byte[] pattern2 = { 0x1C, 0x05, 0x40, 0x35 };
                byte[] patch2 = { 0x25, 0x40, 0x40, 0x05 };

                byte[] pattern3 = { 0x46, 0x53, 0x42, 0x18 };
                byte[] patch3 = { 0x23, 0x10, 0x40, 0x03 };

                byte[] pattern4 = { 0x1C, 0x05, 0x80, 0x22 };
                byte[] patch4 = { 0x25, 0x40, 0x80, 0x22, 0x40, 0x05 };

                for (int offset = 0; offset < input_ios.Length - 4; offset++)
                {
                    input_ios.Position = offset;                                             // set position to advance byte by byte
                    input_ios.Read(buffer_4, 0, 4);                                          // because we read 4 bytes at once

                    if (ByteArrayCompare(buffer_4, pattern1))                                // see if it matches
                    {
                        input_ios.Seek(offset, SeekOrigin.Begin);                            // seek
                        input_ios.Write(patch1, 0, 4);                                       // and then patch

                        patchCount++;
                    }

                    if (ByteArrayCompare(buffer_4, pattern2))                                // see if it matches
                    {
                        input_ios.Seek(offset, SeekOrigin.Begin);                            // seek
                        input_ios.Write(patch2, 0, 4);                                       // and then patch

                        patchCount++;
                    }

                    if (ByteArrayCompare(buffer_4, pattern3))                                // see if it matches
                    {
                        input_ios.Seek(offset, SeekOrigin.Begin);                            // seek
                        input_ios.Write(patch3, 0, 4);                                       // and then patch

                        patchCount++;
                    }

                    if (ByteArrayCompare(buffer_4, pattern4))                                // see if it matches
                    {
                        input_ios.Seek(offset, SeekOrigin.Begin);                            // seek
                        input_ios.Write(patch4, 0, 6);                                       // and then patch

                        patchCount++;
                    }

                }

                if (patchCount == 0)
                    Console.WriteLine("LR to ZLZR patching: Nothing to patch.");
                else
                    Console.WriteLine("LR to ZLZR patching finished. (Patches applied: {0})", patchCount);

                Console.WriteLine();
            }


            //enable wii remote emulation
            if (horiz_wiimote || vert_wiimote)
            {
                Array.Clear(buffer_8, 0, 8);
                int patchCount = 0;
                byte[] pattern = { 0x16, 0x13, 0x1C, 0x02, 0x40, 0x9A, 0x1C, 0x13 };
                byte[] patch = { 0x23, 0x00 };

                for (int offset = 0; offset < input_ios.Length - 6; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern))                                  // see if it matches
                    {
                        input_ios.Seek(offset, SeekOrigin.Begin);                             // seek
                        input_ios.Write(patch, 0, 2);                                         // and then patch

                        patchCount++;
                    }

                }

                if (patchCount == 0)
                    Console.WriteLine("Wii Remote emulation patching: Nothing to patch.");
                else
                    Console.WriteLine("Wii Remote emulation enabled... (Patches applied: {0})", patchCount);

                Console.WriteLine();

            }


            //enable horizontal wii remote emulation (remap dpad)
            if (vert_wiimote)
            {
                Array.Clear(buffer_8, 0, 8);
                int patchCount = 0;
                byte[] pattern = { 0x00, 0x2B, 0x00, 0xD0, 0x00, 0x80, 0x08, 0x9A };

                for (int offset = 0; offset < input_ios.Length - 8; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern))                                  // see if it matches
                    {
                        input_ios.Seek(offset, SeekOrigin.Begin);
                        input_ios.WriteByte(0x02);                                            // dpad left -> down
                        patchCount++;

                        input_ios.Seek(offset + 8, SeekOrigin.Begin);
                        input_ios.WriteByte(0x03);                                            // dpad right -> up
                        patchCount++;

                        input_ios.Seek(offset + 0x16, SeekOrigin.Begin);
                        input_ios.WriteByte(0x01);                                            // dpad down -> right
                        patchCount++;

                        input_ios.Seek(offset + 0x24, SeekOrigin.Begin);
                        input_ios.WriteByte(0x00);                                            // dpad up -> left

                        patchCount++;
                    }
                }

                if (patchCount == 0)
                    Console.WriteLine("Vertical Wii Remote patching: Nothing to patch.");
                else
                    Console.WriteLine("Vertical Wii Remote emulation enabled... (Patches applied: {0})", patchCount);

                Console.WriteLine();
            }


            // enable proper input support in homebrew
            if (homebrew)
            {
                Console.WriteLine("Homebrew-related patches:");
                Array.Clear(buffer_4, 0, 4);
                Array.Clear(buffer_8, 0, 8);
                int patchCount = 0;


                //wiimote passthrough
                byte[] pattern_passthrough = { 0x20, 0x4B, 0x01, 0x68, 0x18, 0x47, 0x70, 0x00 };
                byte[] patch_passthrough = { 0x20, 0x00 };

                for (int offset = 0; offset < input_ios.Length - 8; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern_passthrough))                      // if it matches
                    {
                        Console.WriteLine("* Enabling Wii Remote passthrough...");
                        input_ios.Seek(offset + 3, SeekOrigin.Begin);                         // seek to offset
                        input_ios.Write(patch_passthrough, 0, 2);                             // and then patch

                        patchCount++;
                    }

                }

                // the custom function
                byte[] pattern_custom_func = { 0x28, 0x00, 0xD0, 0x03, 0x49, 0x02, 0x22, 0x09 };
                byte[] patch_custom_func = { 0xF0, 0x04, 0xFF, 0x21, 0x48, 0x02, 0x21, 0x09, 0xF0, 0x04, 0xFE, 0xF9 };

                for (int offset = 0; offset < input_ios.Length - 8; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern_custom_func))                      // if it matches
                    {
                        Console.WriteLine("* Writing custom function...");
                        input_ios.Seek(offset, SeekOrigin.Begin);                             // seek to offset
                        input_ios.Write(patch_custom_func, 0, 12);                            // and then patch

                        patchCount++;
                    }

                }

                // call custom function
                byte[] pattern_custom_call = { 0xF0, 0x01, 0xFA, 0xB9 };
                byte[] patch_custom_call = { 0xF7, 0xFC, 0xFB, 0x95 };

                for (int offset = 0; offset < input_ios.Length - 4; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_4, 0, 4);                                           // because we read 4 bytes at once

                    if (ByteArrayCompare(buffer_4, pattern_custom_call))                      // if it matches
                    {
                        Console.WriteLine("* Writing call to the custom function...");
                        input_ios.Seek(offset, SeekOrigin.Begin);                             // seek to offset
                        input_ios.Write(patch_custom_call, 0, 4);                             // and then patch

                        patchCount++;
                    }

                }

                // disable AHBPROT
                byte[] pattern_ahbprot = { 0x20, 0x00, 0xF0, 0x04, 0xFB, 0x2B, 0x48, 0x29 };
                byte[] patch_ahbprot = { 0x20, 0x01 };

                for (int offset = 0; offset < input_ios.Length - 8; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern_ahbprot))                          // see if it matches
                    {
                        Console.WriteLine("* Disabling AHBPROT...");
                        input_ios.Seek(offset, SeekOrigin.Begin);                             // seek to offset
                        input_ios.Write(patch_ahbprot, 0, 2);                                 // and then patch

                        patchCount++;
                    }

                }

                //disable MEMPROT
                byte[] pattern_memprot = { 0x0D, 0x80, 0x01, 0x94, 0xB5, 0x00, 0x4B, 0x08 };
                byte[] patch_memprot = { 0x22, 0x00 };

                for (int offset = 0; offset < input_ios.Length - 8; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern_memprot))                          // see if it matches
                    {
                        Console.WriteLine("* Disabling MEMPROT...");
                        input_ios.Seek(offset + 8, SeekOrigin.Begin);                         // seek to offset
                        input_ios.Write(patch_memprot, 0, 2);                                 // and then patch

                        patchCount++;
                    }

                }

                // nintendont 1
                byte[] pattern_nintendont_1 = { 0xB0, 0xBA, 0x1C, 0x0F };
                byte[] patch_nintendont_1 = { 0xE5, 0x9F, 0x10, 0x04, 0xE5, 0x91, 0x00, 0x00, 0xE1, 0x2F, 0xFF, 0x10, 0x12, 0xFF, 0xFF, 0xE0 };
                for (int offset = 0; offset < input_ios.Length - 4; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_4, 0, 4);                                           // because we read 4 bytes at once

                    if (ByteArrayCompare(buffer_4, pattern_nintendont_1))                     // if it matches
                    {
                        Console.WriteLine("* Nintendont patch 1...");
                        input_ios.Seek(offset - 12, SeekOrigin.Begin);                        // seek to offset
                        input_ios.Write(patch_nintendont_1, 0, 16);                           // and then patch

                        patchCount++;
                    }

                }

                //nintendont 2
                byte[] pattern_nintendont_2 = { 0x68, 0x4B, 0x2B, 0x06 };
                byte[] patch_nintendont_2 = { 0x49, 0x01, 0x47, 0x88, 0x46, 0xC0, 0xE0, 0x01, 0x12, 0xFF, 0xFE, 0x00, 0x22, 0x00, 0x23, 0x01, 0x46, 0xC0, 0x46, 0xC0 };
                for (int offset = 0; offset < input_ios.Length - 4; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_4, 0, 4);                                           // because we read 4 bytes at once

                    if (ByteArrayCompare(buffer_4, pattern_nintendont_2))                     // if it matches
                    {
                        Console.WriteLine("* Nintendont patch 2...");
                        input_ios.Seek(offset, SeekOrigin.Begin);                             // seek to offset
                        input_ios.Write(patch_nintendont_2, 0, 20);                           // and then patch

                        patchCount++;
                    }

                }

                //nintendont 3
                byte[] pattern_nintendont_3 = { 0x0D, 0x80, 0x00, 0x00, 0x0D, 0x80, 0x00, 0x00 };
                byte[] patch_nintendont_3 = { 0x0D, 0x80, 0x00, 0x00, 0x0D, 0x80, 0x00, 0x00, 0x00, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00 };
                for (int offset = 0; offset < input_ios.Length - 8; offset++)
                {
                    input_ios.Position = offset;                                              // set position to advance byte by byte
                    input_ios.Read(buffer_8, 0, 8);                                           // because we read 8 bytes at once

                    if (ByteArrayCompare(buffer_8, pattern_nintendont_3))                     // if it matches
                    {
                        Console.WriteLine("* Nintendont patch 3...");
                        input_ios.Seek(offset, SeekOrigin.Begin);                             // seek to offset
                        input_ios.Write(patch_nintendont_3, 0, 24);                           // and then patch

                        patchCount++;
                    }

                }

                if (patchCount == 0)
                    Console.WriteLine("Homebrew patching: Nothing to patch.");
                else
                    Console.WriteLine("Homebrew patching finished... (Patches applied: {0})", patchCount);

                Console.WriteLine();
            }

            // write to disk
            //FileStream patched_file = File.OpenWrite("newfw.img");                     // for testing
            FileStream patched_file = File.OpenWrite(fw_file);                           // open file
            input_ios.WriteTo(patched_file);                                             // write
            patched_file.Close();
            input_ios.Close();

        }
    }
}
