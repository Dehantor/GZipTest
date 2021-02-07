using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Linq;

namespace GZipTest
{
    /// <summary>
    /// Класс для архивации файлов
    /// </summary>
    class Compression:IDisposable
    {
        //Размер блока - 1 МБ
        private int blockSize = 1024 * 1024; 
        //объект синхранизации
        object _locker = new object();
        Semaphore _semaphore;
        //мой пул потоков
        PrimitiveThreadPool _threadPool;
        //для очереди
        int queueAmount = Environment.ProcessorCount * 4;
        //сжатие или распаковка
        string _variant;
        public Compression(string variant)
        {
            _variant = variant;
            _semaphore = new Semaphore(queueAmount, queueAmount);
            _threadPool = new PrimitiveThreadPool();

        }
        public int StartStream(string source, string target)
        {
            try
            {
                string path = Path.Combine(Environment.CurrentDirectory, source);
                // поток для чтения исходного файла
                using (FileStream sourceStream = new FileStream(path, FileMode.OpenOrCreate))
                {
                    // поток для записи сжатого файла
                    using (FileStream targetStream = File.Create(target))
                    {
                        if (_variant == "compress")
                        {
                            compress(sourceStream, targetStream);
                        }
                        else
                        {
                            decompress(sourceStream, targetStream);
                        }
                    }
                }

                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine( $"При обработке возникла одна или несколько ошибок: \n {ex.Message} ");
                Dispose();
                return 0;
            }
        }
        /// <summary>
        /// сжатие
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        void compress(Stream source, Stream target)
        {
            //Вычислим кол-во блоков
            int blockAmount = (int)(source.Length / blockSize + (source.Length % blockSize > 0 ? 1 : 0));
            if(blockAmount==0)
                throw new  Exception("Файл весит 0 КБ");
            //текущий блок
            int blockCurrent = 0;
            //информация о блоках
            var dump = new Dump(blockAmount);
            //с учетом дапма в конечном файле
            target.Seek(dump.Size, SeekOrigin.Begin);
            source.Seek(0, SeekOrigin.Begin);
            var resetEvent = new AutoResetEvent(false);
            while (source.Position < source.Length)
            {
                //Читаем один блок
                byte[] data = new byte[blockSize];
                int dataLength = source.Read(data, 0, blockSize);
                //добавляем в очередь на упаковку
                addQueueCompress(() =>
                {
                    lock (_locker)
                    {

                            using (var result = new MemoryStream())
                            {
                                using (var compressionStream = new GZipStream(result, CompressionMode.Compress))
                                {
                                    compressionStream.Write(data, 0, dataLength);
                                }
                                target.Write(result.ToArray(), 0, result.ToArray().Length);
                                //записываем данные текущего блока
                                dump.Blocks[blockCurrent].Index = blockCurrent;
                                dump.Blocks[blockCurrent].Length = result.ToArray().Length;
                                dump.Blocks[blockCurrent].OriginalLength = dataLength;
                            }
                            //если последний завершил работу
                            if (++blockCurrent == blockAmount)
                                resetEvent.Set();

                    }
                });
            }
            //когда отработали все потоки
            resetEvent.WaitOne();
            lock (_locker)
            {
                //Когда все закончено, встанем в начала файла дамп 
                target.Seek(0, SeekOrigin.Begin);
                dump.WriteTo(target);
            }
        }
        /// <summary>
        /// распаковка
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        private void decompress(Stream source, Stream target)
        {
            //читаем дамп
            var dump = Dump.ReadFrom(source); 
            target.Seek(0, SeekOrigin.Begin);
            var blockCurrent = 0;
            var writedBlockCount = 0;
            var resentEvent = new AutoResetEvent(false);
            //Начнем читать исходный файл
            while (source.Position < source.Length) 
            {
                var sourceSize = dump.Blocks[blockCurrent].Length;
                var sourceIndex = dump.Blocks[blockCurrent].Index;
                var data = new byte[sourceSize];
                var dataLength = source.Read(data, 0, sourceSize);
                if (dataLength != sourceSize)
                    throw new FileLoadException("Исходный файл имеет неверный формат");

                addQueueCompress( () =>
                {
                    //Рассчитаем оригинальное смещение блока
                    var blockOffset = dump.GetOffset(sourceIndex);

                    lock (_locker)
                    {
                        target.Seek(blockOffset, SeekOrigin.Begin);
                        using (var sourceMemory = new MemoryStream(data, 0, dataLength))
                        {
                            using (var targetMemory = new MemoryStream())
                            {
                                using (var compressionStream = new GZipStream(sourceMemory, CompressionMode.Decompress))
                                {
                                    compressionStream.CopyTo(targetMemory);
                                    targetMemory.ToArray();
                                }
                                target.Write(targetMemory.ToArray(), 0, targetMemory.ToArray().Length);
                            }
                        }
                        if (++writedBlockCount == dump.Blocks.Length)
                            resentEvent.Set();
                    }
                });
                blockCurrent++;
            }

            resentEvent.WaitOne();
        }
        /// <summary>
        /// Постановка задачь в очередь
        /// </summary>
        /// <param name="action"></param>
        /// 
        void addQueueCompress(Action action)
        {
            _semaphore.WaitOne();
            _threadPool.AddTask(()=> {
                try
                {
                    action();
                    _semaphore.Release();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        public void Dispose()
        {
            _threadPool.Dispose();
        }
    }
}
