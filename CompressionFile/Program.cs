using System;

namespace GZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            checkArgument(args);
        }

        private static void checkArgument(string[] args)
        {
            
            if (args.Length==3)
            {
                int value = 0 ;
                using (Compression compress = new Compression(args[0]))
                {
                    if (args[0] == "compress")
                       value= compress.StartStream(args[1],args[2]);
                    else if (args[0] == "decompress")
                        value=compress.StartStream(args[1], args[2]);
                    else
                        Console.WriteLine("Не правильно задана конфигурация");
                }
                Console.WriteLine(value) ;
            }
            else
            {
                Console.WriteLine("Должно быть три аргумента");
                Console.WriteLine("GZipTest.exe compress/decompress [имя исходного файла] [имя результирующего файла]");
            }
        }
    }
}
