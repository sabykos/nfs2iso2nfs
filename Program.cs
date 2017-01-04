using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static string keyFile = "..\\code\\htk.bin";
        public static string isoFile = "game.iso";
        public static string wiiKeyFile = "wii_common_key.bin";
        public static string nfsDir = "";

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
                unpackNFS("hif_dec.nfs","hif_unpack.nfs", header);
                if (!keepFiles)
                    File.Delete("hif_dec.nfs");
                manipulateISO("hif_unpack.nfs", "game.iso", true);
                if (!keepFiles)
                    File.Delete("hif_unpack.nfs");
            }
            else if (enc)
            {
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
                        dec=true;
                        break;
                    case "-enc":
                        enc = true;
                        break;
                    case "-keep":
                        keepFiles = true;
                        break;
                    case "-key":
                        if (i == args.Length)
                            return -1;
                        keyFile = args[i+1];
                        i++;
                        break;
                    case "-wiikey":
                        if (i == args.Length)
                            return -1;
                        wiiKeyFile = args[i+1];
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
                    case "-h":
                        Console.WriteLine("+++++ NFS2ISO2NFS +++++");
                        Console.WriteLine();
                        Console.WriteLine("-dec            Decrypt .nfs files to an .iso file.");
                        Console.WriteLine("-enc            Encrypt an .íso file to -nfs file.s");
                        Console.WriteLine("-key <file>     Location of AES key file. Default: code\\htk.bin.");
                        Console.WriteLine("-wiikey <file>  Location of Wii Common key file. Default: wii_common_key.bin.");
                        Console.WriteLine("-iso <file>     Location of .iso file. Default: game.iso.");
                        Console.WriteLine("-nfs <file>     Location of .nfs files. Default: current Directory.");
                        Console.WriteLine("-keep           Don't delete the files produced in intermediate steps.");
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


            if (dec || ((!dec && !enc) && File.Exists(nfsDir + "\\hif_000000.nfs")))
            {
                Console.WriteLine("+++++ NFS2ISO +++++");
                Console.WriteLine();
                if (dec && !enc && !File.Exists(nfsDir + "\\hif_000000.nfs"))
                {
                    Console.WriteLine(".nfs files not found! Exiting...");
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
                    Console.WriteLine(".iso file not found! Exiting...");
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
                Console.WriteLine("Could not find AES key file! Exiting...");
                return null;
            }
            byte[] key = getKey(keyFile);
            if (key == null)
            {
                Console.WriteLine("AES key file has wrong file size! Exiting...");
                return null;
            }
            Console.WriteLine("AES key file found!");

            if (WII_COMMON_KEY[0] != 0xeb)
            {
                Console.WriteLine("Wii common key not found in source code. Looking for file...");
                if (!File.Exists(wiiKeyFile))
                {
                    Console.WriteLine("Could not find Wii common key file! Exiting...");
                    return null;
                }
                WII_COMMON_KEY = getKey(wiiKeyFile);
                if (key == null)
                {
                    Console.WriteLine("Wii common key file has wrong file size! Exiting...");
                    return null;
                }
                Console.WriteLine("Wii Common Key file found!");
            }
            else Console.WriteLine("Wii common key found in source code!");
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
                int i=0;
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
                int[,] partitionInfo = new int[2,4];            //first coorfinate number of partitions, second offset of partition table
                for (byte i = 0; i < 4; i++)
                {
                    partitionInfo[0,i] = partitionTable[0x0 + 0x8*i] * 0x1000000 + partitionTable[0x1 + 0x8*i] * 0x10000 + partitionTable[0x2 + 0x8*i] * 0x100 + partitionTable[0x3 + 0x8*i];
                    Console.WriteLine("Number of " + (i+1) + ". partitions: " + partitionInfo[0,i]);
                    if (partitionInfo[0, i] == 0)
                        partitionInfo[1, i] = 0;
                    else partitionInfo[1,i] = (partitionTable[0x4 + 0x8*i] * 0x1000000 + partitionTable[0x5 + 0x8*i] * 0x10000 + partitionTable[0x6 + 0x8*i] * 0x100 + partitionTable[0x7 + 0x8*i]) * 0x4;
                    Console.WriteLine("Partition info table offset: 0x" + Convert.ToString(partitionInfo[1,i], 16));
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
                        ew.Write(er.ReadBytes(0x3D0));
                        IV = er.ReadBytes(0x10);
                        ew.Write(IV);
                        ew.Write(er.ReadBytes(0x20));
                        Sector = er.ReadBytes(SECTOR_SIZE - 0x400);
                        Sector = aes_128_cbc(titlekey, IV, Sector, enc);
                        ew.Write(Sector);
                        partitionSize -= SECTOR_SIZE;
                    }
                    sizeInfo[1] = curPos - sizeInfo[0];
                    if (partitionSize != 0)
                        Console.WriteLine("Last cluster was not complete. This may be a problem.");
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
                int start, length;
                int pos = 0x0;
                int j = 0;
                for (int i = 0; i < numberOfParts; i++)
                {
                    start = SECTOR_SIZE * (0x1000000 * header[0x14 + i * 0x8] + 0x10000 * header[0x15 + i * 0x8] + 0x100 * header[0x16 + i * 0x8] + header[0x17 + i * 0x8]);
                    length = SECTOR_SIZE * (0x1000000 * header[0x18 + i * 0x8] + 0x10000 * header[0x19 + i * 0x8] + 0x100 * header[0x1A + i * 0x8] + header[0x1B + i * 0x8]);
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

                header[0x0]=0x45;
                header[0x1]=0x47;
                header[0x2]=0x47;
                header[0x3]=0x53;

                header[0x4]=0x00;
                header[0x5]=0x01;
                header[0x6]=0x10;
                header[0x7]=0x11;

                header[0x8]=0x00;
                header[0x9]=0x00;
                header[0xA]=0x00;
                header[0xB]=0x00;

                header[0xC]=0x00;
                header[0xD]=0x00;
                header[0xE]=0x00;
                header[0xF]=0x00;

                header[0x10]=0x00;
                header[0x11]=0x00;
                header[0x12]=0x00;
                header[0x13]=0x03;

                header[0x14]=0x00;
                header[0x15]=0x00;
                header[0x16]=0x00;
                header[0x17]=0x00;

                header[0x18]=0x00;
                header[0x19]=0x00;
                header[0x1A]=0x00;
                header[0x1B]=0x01;

                header[0x1C]=0x00;
                header[0x1D]=0x00;
                header[0x1E]=0x00;
                header[0x1F]=0x08;

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
                int start, length;
                int pos = 0x0;
                int j = 0;
                for (int i = 0; i < numberOfParts; i++)
                {
                    start = SECTOR_SIZE * (0x1000000 * header[0x14 + i * 0x8] + 0x10000 * header[0x15 + i * 0x8] + 0x100 * header[0x16 + i * 0x8] + header[0x17 + i * 0x8]);
                    length = SECTOR_SIZE * (0x1000000 * header[0x18 + i * 0x8] + 0x10000 * header[0x19 + i * 0x8] + 0x100 * header[0x1A + i * 0x8] + header[0x1B + i * 0x8]);
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
                    if (enc)
                        Sector = aes_128_cbc(key, iv, Sector, true);
                    else
                        Sector = aes_128_cbc(key, iv, Sector, false);

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
                for (int i = 0; i < size-j; i++)
                    if (list[1,i] > max) 
                    {
                        max = list[1,i];
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
    }
}
