﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Web;
using System.Runtime.InteropServices;

namespace SimplePM_Server
{
    class SimplePM_Tester
    {
        private MySqlConnection connection;
        private string exeFileUrl;
        private ulong problemId, submissionId,userId;
        private float problemDifficulty;

        public SimplePM_Tester(MySqlConnection connection, string exeFileUrl, ulong problemId, ulong submissionId, float problemDifficulty, ulong userId)
        {
            this.connection = connection;
            this.exeFileUrl = exeFileUrl;
            this.problemId = problemId;
            this.submissionId = submissionId;
            this.problemDifficulty = problemDifficulty;
            this.userId = userId;
        }
        
        public void DebugTest()
        {

        }

        public void ReleaseTest()
        {
            //Запрос на выборку из БД
            string querySelect = "SELECT * FROM `spm_problems_tests` WHERE `problemId` = '" + problemId.ToString() + "' ORDER BY `id` ASC;";

            //Дескрипторы временных таблиц выборки из БД
            MySqlCommand cmdSelect = new MySqlCommand(querySelect, connection);
            MySqlDataReader dataReader = cmdSelect.ExecuteReader();

            //Переменная результата выполнения всех тестов
            string _problemTestingResult = "";
            int _problemPassedTests = 0;

            Dictionary<int, Dictionary<string, string>> testsInfo = new Dictionary<int, Dictionary<string, string>>();
            
            int i = 1;
            while (dataReader.Read())
            {
                Dictionary<string, string> tmpDict = new Dictionary<string, string>();

                //Test id
                tmpDict.Add(
                    "testId",
                    HttpUtility.HtmlDecode(dataReader["id"].ToString())
                );
                //Input
                tmpDict.Add(
                    "input",
                    HttpUtility.HtmlDecode(dataReader["input"].ToString())
                );
                //Output
                tmpDict.Add(
                    "output",
                    HttpUtility.HtmlDecode(dataReader["output"].ToString())
                );
                //Time limit
                tmpDict.Add(
                    "timeLimit",
                    HttpUtility.HtmlDecode(dataReader["timeLimit"].ToString())
                );
                //Memory limit
                tmpDict.Add(
                    "memoryLimit",
                    HttpUtility.HtmlDecode(dataReader["memoryLimit"].ToString())
                );

                //Add to library
                testsInfo.Add(i, tmpDict);

                i++;
            }

            //Завершаем чтение потока
            dataReader.Close();

            //Создаём новую конфигурацию запуска процесса
            ProcessStartInfo startInfo = new ProcessStartInfo(exeFileUrl);

            //Перенаправляем потоки
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            //Запрещаем показ приложения на экран компа
            startInfo.ErrorDialog = false;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

            //Объявление необходимых переменных для тестирования
            //пользовательской программы
            ulong testId;
            string input, output;
            int timeLimit;
            long memoryLimit;

            for (i=1; i<=testsInfo.Count; i++)
            {
                //Присвоение переменных данных теста для быстрого доступа
                testId = ulong.Parse(testsInfo[i]["testId"]);
                input = testsInfo[i]["input"].ToString();
                output = testsInfo[i]["output"].ToString();
                timeLimit = int.Parse(testsInfo[i]["timeLimit"]);
                memoryLimit = long.Parse(testsInfo[i]["memoryLimit"]);

                //Объявляем дескриптор процесса
                Process problemProc = new Process();

                //Указываем интересующую нас конфигурацию тестирования
                problemProc.StartInfo = startInfo;

                //Запускаем процесс
                problemProc.Start();

                //Инъекция входного потока
                problemProc.StandardInput.WriteLine(input);
                problemProc.StandardInput.Flush();
                problemProc.StandardInput.Close();

                //Проверяем процесс на использованную память

                //Ждём завершения, максимум X миллимекунд
                problemProc.WaitForExit(timeLimit);

                if (!problemProc.HasExited)
                {
                    //Процесс не завершил свою работу
                    //Исключение: времени не хватило!
                    problemProc.Kill();
                    Thread.Sleep(10);
                    _problemTestingResult += 'T';
                }
                else
                {
                    //Проверка на "вшивость"
                    if (problemProc.StandardError.ReadToEnd().Length > 0)
                    {
                        //Ошибка при тесте!
                        _problemTestingResult += 'E';
                    }
                    /*else if (problemProc.PeakVirtualMemorySize64 > memoryLimit)
                    {
                        //Процесс израсходовал слишком много физической памяти!
                        _problemTestingResult += 'M';
                    }*/
                    else
                    {
                        //Ошибок при тесте не выявлено, но вы держитесь!
                        string pOut = problemProc.StandardOutput.ReadToEnd();
                        //Добавляем результат
                        //Console.WriteLine("'" + pOut + "'");
                        if (output == pOut)
                        {
                            _problemTestingResult += '+';
                            _problemPassedTests++;
                        }
                        else
                            _problemTestingResult += '-';
                    }

                }

            }

            //Объявляем переменную численного результата тестирования
            float _bResult = 0;

            try
            {
                //Тестов нет, но вы держитесь!
                if (_problemTestingResult.Length <= 0)
                {
                    _problemTestingResult = "+";
                    _problemPassedTests = 1;
                }

                //Вычисляем полученный балл за решение задачи
                //на основе количества пройденных тестов
                _bResult = (_problemPassedTests / testsInfo.Count) * problemDifficulty;
            }
            catch (Exception) { _bResult = problemDifficulty; _problemTestingResult = "+"; }
            
            //ОТПРАВКА РЕЗУЛЬТАТОВ ТЕСТИРОВАНИЯ НА СЕРВЕР БД
            //В ТАБЛИЦУ РЕЗУЛЬТАТОВ (`spm_submissions`)
            string queryUpdate = "UPDATE `spm_submissions` SET `status` = 'ready'," +
                                                              "`result` = '" + _problemTestingResult + "', " +
                                                              "`b` = '" + _bResult.ToString() + "' " +
                                 "WHERE `submissionId` = '" + submissionId.ToString() + "' LIMIT 1;";
            new MySqlCommand(queryUpdate, connection).ExecuteNonQuery();
            //Обновляем количество баллов и рейтинг пользователя
            //для этого вызываем пользовательские процедуры mysql,
            //созданные как раз для этих нужд
            new MySqlCommand("CALL updateBCount(" + userId + ")", connection).ExecuteNonQuery();
            new MySqlCommand("CALL updateRating(" + userId + ")", connection).ExecuteNonQuery();
        }
    }
}
