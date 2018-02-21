﻿/*
 * Copyright (C) 2017, Kadirov Yurij.
 * All rights are reserved.
 * Licensed under Apache License 2.0 with additional restrictions.
 * 
 * @Author: Kadirov Yurij
 * @Website: https://sirkadirov.com/
 * @Email: admin@sirkadirov.com
 * @Repo: https://github.com/SirkadirovTeam/SimplePM_Server
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using IniParser;
using IniParser.Model;
using System.Web;
using CompilerBase;
using NLog;
using NLog.Config;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using SubmissionInfo;

namespace SimplePM_Server
{

    /*
     * Базовый класс сервера контролирует
     * работу сервера  проверки решений в
     * целом,      содержит     множество
     * инициализирующих что-либо функций,
     * множество переменных и т.д.
     */

    public class SimplePM_Worker
    {
        
        /*
         * Объявляем переменную указателя
         * на менеджер  журнала собылий и
         * присваиваем  ей  указатель  на
         * журнал событий текущего класса
         */
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private dynamic _serverConfiguration; // переменная хранит основную конфигурацию сервера

        private ulong _aliveTestersCount;    // Количество текущих обрабатываемых запросов
        
        private string EnabledLangs; // Список поддерживаемых ЯП для SQL запросов
        public List<ICompilerPlugin> _compilerPlugins; // Список, содержащий ссылки на модули компиляторов
        
        /*
         * Функция загружает в память компиляционные
         * модули,  которые  собирает  из специально
         * заготовленной директории.
         */
        private void LoadCompilerPlugins()
        {

            /*
             * Записываем в лог-файл информацию о том,
             * что  собираемся   подгружать  сторонние
             * модули компиляции.
             */
            logger.Debug("ICompilerPlugin modules are being loaded...");

            /*
             * Проводим инициализацию необходимых
             * для продолжения работы переменных.
             */
            _compilerPlugins = new List<ICompilerPlugin>();

            string[] pluginFilesList = Directory.GetFiles(
                _serverConfiguration.path.ICompilerPlugin,
                "ICompilerPlugin.*.dll"
            );

            foreach (var pluginFilePath in pluginFilesList)
            {
                
                /*
                 * Указываем в логе, что начинаем
                 * загружать  определённый модуль
                 * компиляции.
                 */
                logger.Debug("Start loading plugin [" + pluginFilePath + "]...");

                try
                {

                    /*
                     * Загружаем сборку из файла по указанному пути
                     */
                    var assembly = Assembly.LoadFrom(pluginFilePath);

                    /*
                     * Ищем необходимую для нас реализацию интерфейса
                     */
                    foreach (var type in assembly.GetTypes())
                    {

                        /*
                         * Если мы не нашли то, что искали - переходим
                         * к следующей итерации цикла foreach,  в ином
                         * случае  продолжаем  выполнение  необходимых
                         * действий по добавлению плагина в список.
                         */
                        if (type.FullName != "CompilerPlugin.Compiler") continue;
                        
                        // Добавляем плагин в список
                        _compilerPlugins.Add(
                            (ICompilerPlugin)Activator.CreateInstance(type)
                        );

                        logger.Debug("Plugin successfully loaded [" + pluginFilePath + "]");

                        // Выходим из цикла foreach
                        break;

                    }

                }
                catch (Exception ex)
                {

                    /*
                     * В случае возникновения ошибок
                     * записываем информацию о них в
                     * лог-файле.
                     */
                    logger.Debug("Plugin loading failed [" + pluginFilePath + "]:");
                    logger.Debug(ex);

                }

            }

            /*
             * Записываем в лог-файл информацию о том,
             * что мы завершили процесс загрузки всех
             * модулей компиляции (ну или не всех)
             **/
            logger.Debug("ICompilerPlugin modules were loaded...");

        }
        
        /*
         * Функция генерирует  строку из допустимых для
         * проверки языков программирования, на которых
         * написаны пользовательские программы.
         */
        public void GenerateEnabledLangsList()
        {

            /*
             * Инициализируем список строк и собираем
             * поддерживаемые языки программирования в массив
             */
            var EnabledLangsList = new List<string>();

            /*
             * В цикле перебираем все поддерживаемые языки
             * программирования подключаемыми модулями и
             * приводим список поддерживаемых системой
             * языков к требуемому виду.
             */
            foreach (var compilerPlugin in _compilerPlugins)
            {

                // Добавляем язык программирования в список
                //EnabledLangsList.Add("'" + compilerPlugin.CompilerPluginLanguageName + "'");

            }

            /*
             * Формируем список доступных языков
             */
            EnabledLangs = string.Join(", ", EnabledLangsList);
            
        }
        
        /*
         * Функция устанавливает "улавливатель"
         * непредвиденных исключений.
         */
        private void SetExceptionHandler()
        {

            /*
             * На всякий случай создаём директорию
             * для хранения лог-файлов, так как
             * некоторые версии NLog не создают
             * её автоматически.
             */
            
            try
            {

                Directory.CreateDirectory("./log/");

            }
            catch
            {
                /* Deal with it */
            }

            /* Устанавливаем обработчик необработанных исключений */
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {

                /*
                 * Записываем сообщение об
                 * ошибке в журнал событий
                 */
                logger.Fatal(e.ExceptionObject);

            };

        }
        
        /*
         * Функция,    очищающая    директорию   хранения
         * временных  файлов  сервера  проверки  решений.
         * В оснвном используется лишь при запуске нового
         * экземпляра сервера  для  избежания конфликтов.
         */
        public bool CleanTempDirectory()
        {

            /*
             * Объявляем переменную, которая будет
             * хранить информацию о том, успешна
             * ли очистка временных файлов или нет.
             */
            var f = true;
            
            try
            {

                /* Удаляем все файлы в папке */
                foreach (var file in Directory.GetFiles(_serverConfiguration.temp_path))
                    File.Delete(file);

                /* Удаляем все директории в папке */
                foreach (var dir in Directory.GetDirectories(_serverConfiguration.temp_path))
                    Directory.Delete(dir, true);

            }
            catch
            {

                /* Указываем, что очистка произведена с ошибкой */
                f = false;

            }

            // Возвращаем результат выполнения операции
            return f;

        }
        
        /*
         * Функция, инициализирующая все необходимые
         * переменные и прочий  хлам для возможности
         * работы сервера проверки решений
         */
        private void LoadResources(string[] args)
        {
            
            /*
             * Производим инициализацию
             * всех подсистем и модулей
             * сервера проверки решений
             */

            // Устанавливаем "улавливатель исключений"
            SetExceptionHandler();

            // Очищаем директорию временных файлов
            CleanTempDirectory();
            
            _serverConfiguration = JsonConvert.DeserializeObject(
                File.ReadAllText("./config/server.json")
            );
            
            // Конфигурируем журнал событий (библиотека NLog)
            try
            {

                LogManager.Configuration = new XmlLoggingConfiguration(
                    "./NLog.config"
                );

            }
            catch
            {
                /* Deal with it */
            }

            if (_serverConfiguration.submission.rechecks_without_timeout == "auto")
                _serverConfiguration.submission.rechecks_without_timeout = Environment.ProcessorCount.ToString();

            if (_serverConfiguration.submission.max_threads == "auto")
                _serverConfiguration.submission.max_threads = Environment.ProcessorCount.ToString();

            // Модули компиляторов
            LoadCompilerPlugins();

            // Модули сервера
            //TODO:LoadServerPlugins();

            /*
             * Вызываем функцию получения строчного списка
             * поддерживаемых языков программирования.
             */
            GenerateEnabledLangsList();

            ///////////////////////////////////////////////////
            // Вызов метода обработки аргументов запуска
            // консольного приложения
            ///////////////////////////////////////////////////

            new SimplePM_Commander().SplitArguments(args);

        }
        
        /*
         * Функция,  которая  запускает  соновной  цикл
         * сервера проверки решений. Работает постоянно
         * в высшем родительском потоке.
         */
        private void ServerLoop()
        {
            

            uint rechecksCount = 0; // количество перепроверок без ожидания

#if DEBUG
            Console.WriteLine(EnabledLangs);
#endif

            /*
             * В бесконечном цикле опрашиваем базу данных
             * на наличие новых не обработанных  запросов
             * на тестирование решений задач.
             */
            while (true)
            {

#if DEBUG
                Console.WriteLine(_customersCount + "/" + _maxCustomersCount);
#endif

                if (_aliveTestersCount < _serverConfiguration.submission.max_threads)
                {

                    /*
                     * Действия  необходимо   выполнять  в  блоке
                     * обработки    непредвиденных    исключений,
                     * так   как   при   выполнении   операций  с
                     * удалённой  базой  данных  могут  возникать
                     * непредвиденные ошибки,   которые не должны
                     * повлиять   на    общую    стабильность   и
                     * работоспособность сервер проверки решений.
                     */
                    try
                    {

                        /*
                         * Инициализируем   новое   уникальное
                         * соединение с базой данных для того,
                         * чтобы не мешать остальным потокам.
                         */
                        var conn = StartMysqlConnection();

                        //Вызов чекера (если всё "хорошо")
                        if (conn != null)
                            GetSubIdAndRunCompile(conn);
                        
                    }
                    /*
                     * В случае  обнаружения  каких-либо
                     * ошибок, записываем их в лог-файл.
                     */
                    catch (Exception ex)
                    {

                        // Записываем все исключения как ошибки
                        logger.Error(ex);

                    }

                }

                /*
                 * Проверяем, необходимо ли установить
                 * таймаут для ослабления  нагрузки на
                 * процессор, или нет.
                 */
                var tmpCheck = rechecksCount >= uint.Parse(
                    _serverConfiguration.submission.rechecks_without_timeout
                );

                if (_aliveTestersCount < _serverConfiguration.submission.max_threads && tmpCheck)
                {

                    // Ожидание для уменьшения нагрузки на сервер
                    Thread.Sleep(_serverConfiguration.submission.check_timeout);

                    // Обнуляем итератор
                    rechecksCount = 0;

                }
                else
                    rechecksCount++;

            }

            ///////////////////////////////////////////////////
            
        }
        
        /*
         * Точка входа с автозапуском бесконечного цикла
         */
        public void Run(string[] args)
        {
            
            // Загружаем все необходимые ресурсы
            LoadResources(args);

            // Запускаем основной цикл
            ServerLoop();
            
        }
        
        /*
         * Функция обработки запросов на проверку решений
         */
        public void GetSubIdAndRunCompile(MySqlConnection conn)
        {
            
            // Создаём новую задачу, без неё - никак!
            new Task(() =>
            {
                
                // Формируем запрос на выборку
                var querySelect = $@"
                    SELECT 
                        `spm_problems`.`difficulty`, 
                        `spm_problems`.`adaptProgramOutput`, 
                        `spm_submissions`.submissionId, 
                        `spm_submissions`.classworkId, 
                        `spm_submissions`.olympId, 
                        `spm_submissions`.time, 
                        `spm_submissions`.codeLang, 
                        `spm_submissions`.userId, 
                        `spm_submissions`.problemId, 
                        `spm_submissions`.testType, 
                        `spm_submissions`.problemCode, 
                        `spm_submissions`.customTest 
                    FROM 
                        `spm_submissions` 
                    INNER JOIN
                        `spm_problems` 
                    ON
                        spm_submissions.problemId = spm_problems.id 
                    WHERE 
                        `status` = 'waiting' 
                    AND 
                        `codeLang` IN ({EnabledLangs}) 
                    ORDER BY 
                        `submissionId` ASC 
                    LIMIT 
                        1
                    ;
                ";
                
                // Создаём запрос на выборку из базы данных
                var cmdSelect = new MySqlCommand(querySelect, conn);

                // Производим выборку полученных результатов из временной таблицы
                var dataReader = cmdSelect.ExecuteReader();

                // Объявляем временную переменную, так называемый "флаг"
                bool f;

                // Делаем различные проверки в безопасном контексте
                lock (new object())
                {

                    f = _aliveTestersCount >= _serverConfiguration.submission.max_threads | !dataReader.Read();

                }

                // Проверка на пустоту полученного результата
                if (f)
                {

                    // Закрываем чтение пустой временной таблицы
                    dataReader.Close();

                    // Закрываем соединение с БД
                    conn.Close();

                }
                else
                {

                    /* 
                     * Запускаем   секундомер  для  того,
                     * чтобы определить время, за которое
                     * запрос на проверку  обрабатывается
                     * сервером проверки решений задач.
                     */
                    var sw = Stopwatch.StartNew();

                    // Увеличиваем количество текущих соединений
                    lock (new object())
                    {

                        _aliveTestersCount++;

                    }

                    /*
                     * Объявляем объект, который будет хранить
                     * всю информацию об отправке и записываем
                     * в него только что полученные данные.
                     */
                    var submissionInfo = new SubmissionInfo.SubmissionInfo
                    {

                        /*
                         * Основная информация о запросе
                         */
                        SubmissionId = int.Parse(dataReader["submissionId"].ToString()),
                        UserId = int.Parse(dataReader["userId"].ToString()),

                        /*
                         * Привязка к уроку и соревнованию
                         */
                        ClassworkId = int.Parse(dataReader["classworkId"].ToString()),
                        OlympId = int.Parse(dataReader["olympId"].ToString()),
                        
                        /*
                         * Тип тестирования и доплнительные поля
                         */
                        TestType = dataReader["testType"].ToString(),
                        CustomTest = HttpUtility.HtmlDecode(dataReader["customTest"].ToString()),

                        /*
                         * Исходный код решения задачи
                         * и дополнительная информация
                         * о нём.
                         */
                        ProblemCode = (byte[]) dataReader["problemCode"],
                        CodeLang = dataReader["codeLang"].ToString(),

                        /*
                         * Информация о задаче
                         */
                        ProblemInformation = new ProblemInfo
                        {

                            ProblemId = int.Parse(dataReader["problemId"].ToString()),
                            ProblemDifficulty = int.Parse(dataReader["difficulty"].ToString()),
                            AdaptProgramOutput = bool.Parse(dataReader["adaptProgramOutput"].ToString())

                        }

                    };
                    
                    // Закрываем чтение временной таблицы
                    dataReader.Close();

                    // Устанавливаем статус запроса на "в обработке"
                    var queryUpdate = $@"
                        UPDATE 
                            `spm_submissions` 
                        SET 
                            `status` = 'processing' 
                        WHERE 
                            `submissionId` = '{submissionInfo.SubmissionId}'
                        LIMIT 
                            1
                        ;
                    ";

                    // Выполняем запрос к базе данных
                    new MySqlCommand(queryUpdate, conn).ExecuteNonQuery();
                    
                    /*
                     * Зовём официанта-шляпочника
                     * уж он знает, что делать в таких
                     * вот неожиданных ситуациях
                     */
                    new SimplePM_Officiant(
                        conn,
                        ref _serverConfiguration,
                        ref _compilerPlugins,
                        submissionInfo
                    ).ServeSubmission();

                    /*
                     * Уменьшаем количество текущих соединений
                     * чтобы другие соединения были возможны.
                     */
                    lock (new object())
                    {
                        _aliveTestersCount--;
                    }

                    /*
                     * Останавливаем секундомер и записываем
                     * полученное значение в Debug log поток
                     */
                    sw.Stop();

#if DEBUG
                    // Выводим затраченное время на экран
                    Console.WriteLine(sw.ElapsedMilliseconds);
#endif

                    // Закрываем соединение с БД
                    conn.Close();

                }

            }).Start();

        }
        
        /*
         * Функция   инициализирует   соединение  с  базой
         * данных  MySQL  используя  данные  аутенфикации,
         * расположенные в конфигурационном файле сервера.
         */
        private static MySqlConnection StartMysqlConnection()
        {

            /*
             * Объявляем  переменную, которая  будет хранить
             * дескриптор соединения с базой данных системы.
             */
            MySqlConnection db = null;

            dynamic databaseConfig = JsonConvert.DeserializeObject(File.ReadAllText("./config/database.json"));
            
            try
            {

                /*
                 * Подключаемся к базе данных на удалённом
                 * MySQL  сервере  и  получаем  дескриптор
                 * подключения к ней.
                 */
                db = new MySqlConnection(
                    $@"
                        server={databaseConfig.hostname};
                        uid={databaseConfig.username};
                        pwd={databaseConfig.password};
                        database={databaseConfig.basename};
                        Charset={databaseConfig.mainchst};
                        SslMode=Preferred;
                        Pooling=False;
                    "
                );

                // Открываем соединение с БД
                db.Open();

            }
            catch (MySqlException)
            {
                
                /* Deal with it */

            }

            /*
             * Возвращаем дескриптор
             * подключения к БД.
             */
            return db;

        }
        
    }

}
