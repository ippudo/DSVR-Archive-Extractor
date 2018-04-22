using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DSVRExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            if ((args.Length == 0) || (args[0] == "") || (!File.Exists(args[0])))
            {
                Console.WriteLine("Usage:   KDMARCEXACTOR.exe [filename] [mode]");
                Console.WriteLine("mode:    -E exract arc files to testout folder");
                Console.WriteLine("CAUTION: Only for Dancing Stage VR Files (.varc)");
                Console.WriteLine();
                Console.WriteLine("Press Any Key to Continue");

                Console.Write("Select Your Mode(E):");
                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.E:
                        Console.WriteLine();
                        Console.WriteLine("Select the Filename that You Want to Extract From:");
                        ExtractArc(Console.ReadLine(), "test");
                        break;
                    default:
                        Console.WriteLine();
                        Console.WriteLine("Unsupported Method");
                        break;
                }
                return;
            }
            else
            {
                if ((args.Length > 1) && (args[1].ToUpper() == "-E")) ExtractArc(args[0], "test");
                else Console.WriteLine("Unsupported Method");
                Console.WriteLine("Done.");
            }

        }

        static void ExtractFolder(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);

            if (File.Exists("db.txt")) { File.Delete("db.txt"); }
            //if (Directory.Exists("test")) DeleteFolder("test");
            //if (Directory.Exists("testout")) DeleteFolder("testout");

            foreach (FileInfo fi in di.GetFiles())
            {
                String str = fi.FullName;
                ExtractArc(str, "test");
                if (File.Exists("db.txt")) { File.Delete("db.txt"); }
                //DeleteFolder("test");
            }

        }

        public class Filedb
        {
            public int length;
            public int reallength;
            public string path;
            public int datastart;
            public int pathlength;
            public Filedb(string pto, int ato, int bto, int cto)
            {
                path = pto;
                datastart = ato;
                reallength = bto;
                length = cto;
            }
        }

        static void DecryptArc(string pathin, string pathout, string filedb = "db.txt")
        {
            DirectoryInfo diin = new DirectoryInfo(pathin);
            DirectoryInfo diout = new DirectoryInfo(pathout);
            StreamReader srdb = new StreamReader(filedb);
            Dictionary<string, Filedb> db = new Dictionary<string, Filedb>()
                ;
            while (!srdb.EndOfStream)
            {
                string[] strdb = srdb.ReadLine().Split('|');
                db.Add(strdb[0], new Filedb(strdb[0], Int32.Parse(strdb[1]), Int32.Parse(strdb[2]), Int32.Parse(strdb[3])));
            }
            foreach (DirectoryInfo di in diin.GetDirectories())
            {
                Console.WriteLine(di.Name);
                Directory.CreateDirectory(pathout + "\\" + di.Name);
                DecryptArc(pathin + "\\" + di.Name, pathout + "\\" + di.Name);
            }
            foreach (FileInfo fi in diin.GetFiles())
            {
                Console.WriteLine(fi.DirectoryName);

                string str = (pathin + "\\" + fi.Name).Replace('\\', '/');
                DecryptLz77(pathin + "\\" + fi.Name, pathout + "\\" + fi.Name, db[(pathin + "\\" + fi.Name).Replace('\\', '/')]);
            }
            srdb.Close();
        }

        static void ExtractArc(string filein, string pathout, string filedb = "db.txt")
        {
            FileInfo fiin = new FileInfo(filein);
            FileStream fsin = fiin.OpenRead();
            StreamWriter swdb = new StreamWriter(filedb);
            long totallength = fiin.Length;
            byte[] datain = new byte[totallength];
            fsin.Read(datain, 0, (int)totallength);
            fsin.Close();


            int filenum = BitConverter.ToInt32(datain, 8);
            for (int i = 0; i < filenum; i++)
            {
                int baseaddress = (i + 1) * 16;
                long pathstart = BitConverter.ToUInt32(datain, baseaddress);
                long pathlength = pathstart;
                while (datain[pathlength] != 0) pathlength++;
                string path = Encoding.Default.GetString(datain, (int)pathstart, (int)(pathlength - pathstart));
                uint datastart = BitConverter.ToUInt32(datain, baseaddress + 4);
                uint datalength = BitConverter.ToUInt32(datain, baseaddress + 8);
                uint datareallength = BitConverter.ToUInt32(datain, baseaddress + 12);

                Directory.CreateDirectory(pathout + "\\" + Path.GetDirectoryName(path));
                FileStream fsfile = new FileStream(pathout + "\\" + path, FileMode.Create);
                fsfile.Write(datain, (int)datastart, (int)datareallength);
                fsfile.Close();

                swdb.WriteLine(string.Join("|", new string[4] { pathout + "/" + path, datastart.ToString(), datareallength.ToString(), datalength.ToString() }));

            }
            swdb.Close();
            DecryptArc(pathout, "testout", filedb);
        }

        static void DecryptLz77(string filein, string fileout, Filedb db)
        {

            int buffersize = 100000000;
            FileStream fsin = new FileStream(filein, FileMode.Open);
            FileStream fsout = new FileStream(fileout, FileMode.Create);

            if (db.reallength == db.length)
            {
                fsin.CopyTo(fsout);
                fsin.Close();
                fsout.Close();
                return;
            }

            int[] dest = new int[buffersize];
            int ptrres = 0, ptrlz77 = 0;


            while (fsin.Length > fsin.Position)
            {
                int lz77byte = fsin.ReadByte();
                for (int i = 1; i < 256; i <<= 1)
                {
                    if ((lz77byte & i) > 0)
                    {
                        int databyte = fsin.ReadByte();
                        dest[ptrres++] = databyte;

                    }
                    else
                    {
                        int databyte1 = fsin.ReadByte();
                        int databyte2 = fsin.ReadByte();
                        int lz77repeat = (databyte2 & 15) + 3;//LowBit - Bytes to Copy (An addtional 3 bytes needs)
                        int lz77offset = (databyte1 * 16) + ((databyte2 & 240) >> 4);//Offset for the start position                            
                        if ((databyte1 == 0) && (databyte2 == 0)) break;


                        ptrlz77 = ptrres - lz77offset;
                        int repeatrest = lz77repeat;
                        while ((repeatrest > 0) && (ptrlz77 < 0))
                        {
                            dest[ptrres++] = 0;
                            ptrlz77++; repeatrest--;
                        }
                        while (repeatrest > 0)
                        {
                            dest[ptrres++] = dest[ptrlz77++];
                            repeatrest--;
                        }
                    }
                }
            }
            fsin.Close();
            Console.WriteLine();
            //sw.WriteLine();
            Console.WriteLine("Done!");
            //sw.Close();
            for (int i = 0; i < ptrres; i++) fsout.WriteByte((byte)dest[i]);
            fsout.Close();
            //Console.ReadKey();
        }

        static string Get2Shin(int num)
        {
            string str = "";
            for (int i = 1; i < 256; i <<= 1) if ((num & i) > 0) str += "1"; else str += "0";
            return str;
        }
    }
}
