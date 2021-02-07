using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest
{
    /// <summary>
    /// Класс для создания дампа
    /// </summary>
    class Dump
    {
        //точка дапма
        private const uint Point = 0xAEDFBBFE;
        /// <summary>
        /// Описание блоков
        /// </summary>
        public Block[] Blocks { get; }
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="count"></param>
        public Dump(int count)
        {
            Blocks = new Block[count];
            for (int i = 0; i < Blocks.Length; i++)
            {
                Blocks[i] = new Block();
            }
        }
        /// <summary>
        /// Записываем дамп в архив
        /// </summary>
        /// <param name="target">Поток</param>
        public void WriteTo(Stream target)
        {
            var binWrit = new BinaryWriter(target);
            binWrit.Write(Point);
            binWrit.Write(Blocks.Length);
            foreach (var blockSize in Blocks)
                blockSize.WriteTo(binWrit);
        }
        /// <summary>
        /// чтение информации о сжатом файле с дампа
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Dump ReadFrom(Stream source)
        {
            try
            {
                var breader = new BinaryReader(source);
                var header = breader.ReadUInt32();
                if (header != Point)
                {
                    throw new Exception("Исходный файл имеет неверный формат");
                }

                var count = breader.ReadInt32();
                var result = new Dump(count);
                for (var idx = 0; idx < count; idx++)
                    result.Blocks[idx].ReadFrom(breader);
                return result;
            }
            catch
            {
                throw new Exception("Ошибка чтения файла");
            }
        }
        /// <summary>
        /// рассчет смещения блоков для исходного файлаы
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public long GetOffset(int id)
        {
            long value = 0; ;
            for (int i = 0; i < id; i++)
            {
                value += Blocks[i].OriginalLength;
            }
            return value;
        }
        public int Size { get { return sizeof(uint) + sizeof(int) + Block.Size * Blocks.Length; } }
        /// <summary>
        /// блоки на которые бьются архив
        /// </summary>
        public class Block
        {
            /// <summary>
            /// Собственный размер структуры
            /// </summary>
            public static int Size => sizeof(int) + sizeof(int) + sizeof(int);
            /// <summary>
            /// индекс
            /// </summary>
            public int Index { get; set; }
            /// <summary>
            /// Длина блока
            /// </summary>
            public int Length { get; set; }
            /// <summary>
            /// исходный размер
            /// </summary>
            public int OriginalLength { get; set; }
            /// <summary>
            /// Записываем блоки в архив
            /// </summary>
            /// <param name="target"></param>
            public void WriteTo(BinaryWriter target)
            {
                target.Write(Index);
                target.Write(Length);
                target.Write(OriginalLength);
            }
            /// <summary>
            /// читаем блоки с архива
            /// </summary>
            /// <param name="breader"></param>
            public void ReadFrom(BinaryReader breader)
            {
                Index = breader.ReadInt32();
                Length = breader.ReadInt32();
                OriginalLength = breader.ReadInt32();

            }
        }
    }

}
