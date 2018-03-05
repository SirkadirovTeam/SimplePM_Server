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

namespace SimplePM_Server.SimplePM_Tester
{
    
    internal partial class SimplePM_Tester
    {

        public ProgramTestingResult Syntax()
        {

            /*
             * Генерируем результат тестирования
             * и возвращаем его  как объект типа
             * ProgramTestingResult.
             */

            return new ProgramTestingResult(1)
            {

                TestingResults =
                {
                    [0] = new TestResult()
                }

            };

        }

    }

}